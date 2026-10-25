// Separate post-hand coaching scene. It reuses the saloon backdrop and a
// Jacob cowboy (the closest rigged Western player in this checkout) with a
// looping table-observe idle. It is its own scene graph and camera. The
// poker scene's render loop is the only requestAnimationFrame driver.
//
// CoachSceneDefinition
//   model:    models/characters/western/Jacob.glb   (SK_Jacob)
//   look:     shirt, no jacket, no hat, full hair, clean head
//   idle:     IdleTableObserveLeftRight01 (mighty-cat.glb, retargeted)
//   bar:      models/env/backdrop.glb
// The mock-up's grey-haired waistcoat, tie and standing gesture are not in
// the asset pack. Jacob stays on his authored seated idle — that clip is a
// real loop, not a frozen bind pose. Camera distance is chosen for this rig.

import * as THREE from 'three';
import { clone as cloneSkinned } from './vendor/SkeletonUtils.js';

export const COACH_SCENE_DEFINITION = {
  id: 'maximilian',
  displayName: 'Maximilian',
  model: 'models/characters/western/Jacob.glb?v=3',
  modelId: 'SK_Jacob',
  look: {
    pants: 3, jacket: 0, shirt: 2, shirtOpt: 0, scarf: 0, hat: 0,
    hair: 'full', gun: false, holster: false, belt: true, head: 'clean',
  },
  idleClip: 'IdleTableObserveLeftRight01',
  idleFallbacks: ['IdleSit01', 'IdleSitThinking01', 'IdleHoldCards01', 'Sit'],
  saloon: 'models/env/backdrop.glb?v=2',
  chair: 'models/props/furniture/chair.glb?v=6',
  reason: 'No waistcoat-and-tie model is in the checkout. Jacob is the Western player the table already rigs, shown without hat or jacket so the face and shirt read.',
};

const WESTERN_SCALE = 1.098;
const TABLE_TOP = 0.78;

function westernMeshNames(look) {
  const names = new Set(['Boots', 'Hands_Optimized', 'Hair_cap']);
  names.add(`Pants__${look.pants}`);
  if (look.jacket) names.add(`Jacket__${look.jacket}`);
  if (look.shirt) names.add(`Shirt__${look.shirt}`);
  if (look.shirtOpt) names.add(`ShirtOpt__${look.shirtOpt}`);
  if (look.scarf) names.add(`Scarf__${look.scarf}`);
  if (look.hat) names.add(`Hat__${look.hat}`);
  names.add(look.hair === 'full' ? 'Hair_full' : 'Hair_cut');
  if (look.gun) names.add('Gun_belt');
  if (look.holster) names.add('Holster');
  if (look.belt) names.add('Pants_belt');
  names.add(`Head__${look.head || 'clean'}`);
  return names;
}

function findBone(root, names) {
  let found = null;
  root.traverse((o) => {
    if (!found && o.isBone && names.includes(o.name)) found = o;
  });
  return found;
}

export function createCoachController(deps) {
  const loadGlb = deps.loadGlb;
  const getRenderer = deps.getRenderer;
  const getAssets = deps.assets;
  const onHide = deps.onHide || (() => {});

  let presenting = false;
  let ready = false;
  let token = 0;
  let scene = null;
  let camera = null;
  let root = null;
  let mixer = null;
  let action = null;
  let clipName = '';
  let reduced = false;
  let planted = new THREE.Vector3();
  let cardGroup = null;
  let owned = [];
  let buildPromise = null;
  let failedReason = '';

  const texLoader = new THREE.TextureLoader();
  const textures = new Map();
  const cardGeos = new Map();

  function renderer() {
    return getRenderer ? getRenderer() : null;
  }

  function contextOk() {
    const r = renderer();
    if (!r) return false;
    const gl = r.getContext && r.getContext();
    return !!gl && !gl.isContextLost();
  }

  function texture(url) {
    let tex = textures.get(url);
    if (!tex) {
      tex = texLoader.load(url);
      tex.colorSpace = THREE.SRGBColorSpace;
      textures.set(url, tex);
    }
    return tex;
  }

  function own(obj) {
    owned.push(obj);
    return obj;
  }

  async function ensure(mine) {
    if (ready && scene) return true;
    if (!buildPromise) buildPromise = build().catch((err) => {
      console.warn('coach-scene: build failed', err);
      failedReason = 'missing-model';
      buildPromise = null;
      return false;
    });
    const ok = await buildPromise;
    if (mine !== token) return false;
    return ok;
  }

  async function build() {
    const r = renderer();
    if (!r || !contextOk()) {
      failedReason = 'no-webgl';
      return false;
    }
    const assets = (getAssets && getAssets()) || {};
    const jacob = assets.jacob || await loadGlb(COACH_SCENE_DEFINITION.model);
    const clips = assets.clips || await loadGlb('models/characters/western/mighty-cat.glb?v=9');
    const chairGltf = assets.chair || await loadGlb(COACH_SCENE_DEFINITION.chair);
    if (token === 0) {
      /* hide() bumped the token; abandon before attaching */
    }
    if (!jacob) {
      failedReason = 'missing-model';
      return false;
    }

    scene = new THREE.Scene();
    scene.background = new THREE.Color(0x140e0a);
    scene.fog = new THREE.Fog(0x140e0a, 7.5, 26);

    // Frame the seated coach in the left third, bar behind him, felt in front.
    // The advice panel owns the right side of the page.
    // Look toward the bar, right of the coach, so he sits in the left third
    // and the advice panel can cover the right without hiding his face.
    camera = new THREE.PerspectiveCamera(40, 1, 0.05, 80);
    camera.position.set(0.08, 1.52, 1.88);
    camera.lookAt(0.22, 1.16, -0.3);

    scene.add(own(new THREE.AmbientLight(0xffe6c4, 0.55)));
    scene.add(own(new THREE.HemisphereLight(0xffd7a8, 0x3a2416, 0.38)));
    const key = own(new THREE.SpotLight(0xffc98a, 28, 9, 0.7, 0.45, 1.2));
    key.position.set(1.4, 2.5, 1.6);
    key.target.position.set(-0.4, 1.15, -0.6);
    scene.add(key, key.target);
    const fill = own(new THREE.PointLight(0xffb060, 6, 8, 1.6));
    fill.position.set(-1.6, 1.8, 1.2);
    scene.add(fill);

    const saloon = await addSaloon(assets.saloon);
    if (!saloon) {
      failedReason = 'missing-saloon';
      scene = null;
      return false;
    }

    addTable();
    const character = addCharacter(jacob, clips);
    if (!character) {
      failedReason = 'missing-model';
      scene = null;
      return false;
    }
    if (chairGltf) addChair(chairGltf, character);
    addChips();
    cardGroup = new THREE.Group();
    scene.add(cardGroup);

    ready = true;
    failedReason = '';
    return true;
  }

  async function addSaloon(existing) {
    if (existing) {
      const clone = existing.clone(true);
      scene.add(clone);
      return clone;
    }
    const gltf = await loadGlb(COACH_SCENE_DEFINITION.saloon);
    if (!gltf) return null;
    const env = new THREE.Group();
    const room = gltf.scene;
    room.rotation.y = Math.PI;
    room.position.z = 2.4;
    env.add(room);
    const barLamp = own(new THREE.PointLight(0xffb060, 8, 12, 1.6));
    barLamp.position.set(0, 3.1, -4.2);
    env.add(barLamp);
    scene.add(env);
    return env;
  }

  function addTable() {
    const feltTex = texture('img/felt.jpg');
    feltTex.wrapS = feltTex.wrapT = THREE.RepeatWrapping;
    feltTex.repeat.set(2, 2);
    const felt = own(new THREE.Mesh(
      own(new THREE.CylinderGeometry(1.18, 1.18, 0.06, 48)),
      own(new THREE.MeshStandardMaterial({ map: feltTex, color: 0x2f6e3c, roughness: 0.96 }))
    ));
    felt.position.y = TABLE_TOP - 0.03;
    felt.receiveShadow = true;
    scene.add(felt);

    const wood = texture('img/wood.jpg');
    const rail = own(new THREE.Mesh(
      own(new THREE.TorusGeometry(1.22, 0.055, 12, 48)),
      own(new THREE.MeshStandardMaterial({ map: wood, color: 0x8a5a33, roughness: 0.72 }))
    ));
    rail.rotation.x = Math.PI / 2;
    rail.position.y = TABLE_TOP - 0.01;
    scene.add(rail);

    const apron = own(new THREE.Mesh(
      own(new THREE.CylinderGeometry(1.05, 1.12, 0.62, 32)),
      own(new THREE.MeshStandardMaterial({ map: wood, color: 0x5c3a22, roughness: 0.8 }))
    ));
    apron.position.y = 0.34;
    scene.add(apron);
  }

  function addCharacter(jacob, clipsGltf) {
    root = cloneSkinned(jacob.scene);
    root.scale.setScalar(WESTERN_SCALE);
    const names = westernMeshNames(COACH_SCENE_DEFINITION.look);
    const inLook = (obj) => {
      for (let p = obj; p; p = p.parent) {
        const n = (p.name || '').replace(/\.\d+$/, '');
        if (names.has(n)) return true;
      }
      return false;
    };
    const drop = [];
    root.traverse((obj) => {
      if (!obj.isMesh) return;
      if (!inLook(obj)) drop.push(obj);
    });
    for (const obj of drop) if (obj.parent) obj.parent.remove(obj);
    root.traverse((obj) => {
      if (!obj.isMesh || !obj.material) return;
      obj.frustumCulled = false;
      obj.castShadow = true;
      const mats = Array.isArray(obj.material) ? obj.material : [obj.material];
      const cloned = mats.map((m) => (m ? own(m.clone()) : m));
      obj.material = cloned.length === 1 ? cloned[0] : cloned;
    });

    const mood = (clipsGltf && clipsGltf.animations) || [];
    const body = jacob.animations || [];
    const wanted = [COACH_SCENE_DEFINITION.idleClip, ...COACH_SCENE_DEFINITION.idleFallbacks];
    let clip = null;
    for (const name of wanted) {
      clip = THREE.AnimationClip.findByName(mood, name) || THREE.AnimationClip.findByName(body, name);
      if (clip) { clipName = name; break; }
    }
    if (!clip) {
      failedReason = 'missing-idle';
      root = null;
      return null;
    }

    mixer = new THREE.AnimationMixer(root);
    action = mixer.clipAction(clip);
    action.setLoop(THREE.LoopRepeat, Infinity);
    action.clampWhenFinished = false;
    action.play();
    mixer.update(0.05);

    root.position.set(-0.58, 0, -0.36);
    root.updateMatrixWorld(true);
    const lFoot = findBone(root, ['L_ankle', 'foot_l']);
    const rFoot = findBone(root, ['R_ankle', 'foot_r']);
    if (lFoot && rFoot) {
      const l = lFoot.getWorldPosition(new THREE.Vector3());
      const rgt = rFoot.getWorldPosition(new THREE.Vector3());
      const fwd = new THREE.Vector3(0, 1, 0).cross(rgt.sub(l));
      if (fwd.lengthSq() > 1e-6) {
        const modelYaw = Math.atan2(fwd.x, fwd.z);
        root.rotation.y = 0 - modelYaw;
      }
    } else {
      root.rotation.y = Math.PI;
    }
    root.updateMatrixWorld(true);

    const hands = ['hand_l', 'LeftHand', 'hand_r', 'RightHand', 'middle_01_l', 'middle_01_r']
      .map((n) => findBone(root, [n]))
      .filter(Boolean);
    if (hands.length) {
      let minY = Infinity;
      const v = new THREE.Vector3();
      for (const b of hands) {
        b.getWorldPosition(v);
        minY = Math.min(minY, v.y);
      }
      root.position.y -= minY - (TABLE_TOP + 0.05);
    } else {
      root.position.y = 0.15;
    }
    planted.copy(root.position);
    scene.add(root);
    return root;
  }

  function addChair(chairGltf, character) {
    const chair = chairGltf.scene.clone(true);
    const hips = findBone(character, ['hips', 'pelvis']);
    const anchor = new THREE.Vector3(-0.58, TABLE_TOP, -0.36);
    if (hips) hips.getWorldPosition(anchor);
    chair.scale.setScalar(1);
    chair.rotation.y = character.rotation.y;
    chair.position.set(anchor.x, anchor.y - 0.05 - 0.479, anchor.z);
    scene.add(chair);
  }

  function addChips() {
    const colors = [0xc23b3b, 0x2f6b45, 0x24558a, 0xe6d7b0, 0x1c1c1c];
    const spots = [[-0.72, 0.42], [0.55, 0.15], [-0.15, 0.62], [0.35, -0.35]];
    spots.forEach(([x, z], i) => {
      const stack = new THREE.Group();
      const n = 4 + (i % 3);
      for (let c = 0; c < n; c++) {
        const chip = own(new THREE.Mesh(
          own(new THREE.CylinderGeometry(0.045, 0.045, 0.012, 16)),
          own(new THREE.MeshStandardMaterial({ color: colors[(i + c) % colors.length], roughness: 0.55 }))
        ));
        chip.position.y = c * 0.013;
        stack.add(chip);
      }
      stack.position.set(x, TABLE_TOP, z);
      scene.add(stack);
    });
  }

  function cardGeometry(w, h) {
    const key = w + 'x' + h;
    let geo = cardGeos.get(key);
    if (!geo) {
      geo = new THREE.PlaneGeometry(w, h);
      cardGeos.set(key, geo);
    }
    return geo;
  }

  function clearCards() {
    if (!cardGroup) return;
    while (cardGroup.children.length) {
      const child = cardGroup.children.pop();
      if (child.material && child.material.dispose) child.material.dispose();
    }
  }

  function layCards(payload) {
    clearCards();
    if (!payload || !cardGroup) return;
    const highlights = new Set(payload.highlights || []);
    const board = payload.board || [];
    board.forEach((url, i) => {
      const x = (i - (board.length - 1) / 2) * 0.2;
      placeCard(url, x, 0.02, highlights.has(url));
    });
    const hero = payload.hero || [];
    hero.forEach((url, i) => {
      placeCard(url, -0.12 + i * 0.16, 0.48, highlights.has(url), 0.2);
    });
    const revealed = payload.revealed || [];
    revealed.forEach((pair, row) => {
      (pair || []).forEach((url, i) => {
        placeCard(url, -0.55 + row * 0.34 + i * 0.12, -0.42, highlights.has(url), 0.15);
      });
    });
  }

  function placeCard(url, x, z, hot, scale = 0.18) {
    const mat = new THREE.MeshBasicMaterial({ map: texture(url) });
    const card = new THREE.Mesh(cardGeometry(scale, scale * 1.36), mat);
    card.rotation.x = -Math.PI / 2;
    card.position.set(x, TABLE_TOP + 0.01, z);
    card.userData.url = url;
    if (hot) {
      const rim = new THREE.Mesh(
        cardGeometry(scale + 0.02, scale * 1.36 + 0.02),
        new THREE.MeshBasicMaterial({ color: 0xe2c36a })
      );
      rim.rotation.x = -Math.PI / 2;
      rim.position.set(x, TABLE_TOP + 0.008, z);
      cardGroup.add(rim);
    }
    cardGroup.add(card);
  }

  function playIdle() {
    if (!action) return;
    action.paused = false;
    action.play();
    if (reduced) {
      mixer.update(0.04);
      action.paused = true;
    }
  }

  const api = {
    get presenting() { return presenting; },
    get ready() { return ready && !!scene; },
    scene() { return scene; },
    camera() { return camera; },
    definition: COACH_SCENE_DEFINITION,

    async show(payload) {
      const mine = ++token;
      presenting = true;
      reduced = !!(payload && payload.reducedMotion);
      if (!contextOk()) {
        failedReason = 'no-webgl';
        ready = false;
        return { ok: false, reason: failedReason, definition: COACH_SCENE_DEFINITION };
      }
      const ok = await ensure(mine);
      if (mine !== token || !presenting) return { ok: false, reason: 'stale', definition: COACH_SCENE_DEFINITION };
      if (!ok || !scene) return { ok: false, reason: failedReason || 'missing-model', definition: COACH_SCENE_DEFINITION };
      layCards(payload || {});
      playIdle();
      return {
        ok: true,
        reason: '',
        model: COACH_SCENE_DEFINITION.model,
        clip: clipName,
        definition: COACH_SCENE_DEFINITION,
      };
    },

    setHighlights(urls) {
      if (!presenting) return;
      const payload = api._lastPayload;
      if (!payload) return;
      payload.highlights = urls || [];
      layCards(payload);
    },

    hide() {
      token++;
      presenting = false;
      if (action) action.paused = true;
      try { onHide(); } catch (e) { /* camera restore is best-effort */ }
    },

    tick(dt) {
      if (!mixer || !root || reduced) return;
      const step = Math.min(Math.max(dt || 0, 0), 0.05);
      if (document.hidden || step <= 0) return;
      mixer.update(step);
      root.position.copy(planted);
    },

    debug() {
      let saloonMeshes = 0;
      let characterMeshes = 0;
      if (scene) {
        scene.traverse((o) => {
          if (!o.isMesh) return;
          if (root && (o === root || root.getObjectById && isUnder(root, o))) characterMeshes++;
        });
        scene.traverse((o) => { if (o.isMesh) saloonMeshes++; });
      }
      return {
        presenting,
        ready,
        model: COACH_SCENE_DEFINITION.model,
        modelId: COACH_SCENE_DEFINITION.modelId,
        clip: clipName,
        mixerTime: action ? action.time : 0,
        playing: !!(action && action.isRunning() && !action.paused),
        loop: action ? action.loop : -1,
        characterMeshes,
        sceneMeshes: saloonMeshes,
        instances: root ? 1 : 0,
        reason: failedReason,
        look: 'jacob-shirt-no-jacket-no-hat',
      };
    },
  };

  // Remember the last payload so highlight toggles can redraw cards.
  const show = api.show;
  api.show = async (payload) => {
    api._lastPayload = payload ? {
      board: payload.board || [],
      hero: payload.hero || [],
      revealed: payload.revealed || [],
      highlights: payload.highlights || [],
      reducedMotion: payload.reducedMotion,
    } : null;
    return show(payload);
  };

  return { api };
}

function isUnder(root, obj) {
  for (let p = obj; p; p = p.parent) if (p === root) return true;
  return false;
}
