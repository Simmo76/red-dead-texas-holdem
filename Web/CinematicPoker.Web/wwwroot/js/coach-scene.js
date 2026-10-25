// Separate post-hand coaching scene. Saloon reviews use the Bandit in the
// bar. Octagon reviews use Fighter1 in the street-arena cage. Each is its
// own scene graph and camera. The poker scene's render loop is the only
// requestAnimationFrame.
//
// Saloon: Bandit.glb (SK_Cowboy_2), eye-patch bandage, hat off in this
// close framing, IdleTableObserveLeftRight01 from mighty-cat.glb.
// Octagon: Fighter1.glb, same idle baked on that rig, street-arena.glb.

import * as THREE from 'three';
import { clone as cloneSkinned } from './vendor/SkeletonUtils.js';

const IDLE_CLIP = 'IdleTableObserveLeftRight01';
const IDLE_FALLBACKS = ['IdleSit01', 'IdleSitThinking01', 'IdleHoldCards01', 'Sit'];
const CHAIR_URL = 'models/props/furniture/chair.glb?v=6';

export const SALOON_COACH_SCENE = {
  id: 'maximilian',
  displayName: 'Maximilian',
  model: 'models/characters/western/Bandit.glb?v=10',
  modelId: 'SK_Cowboy_2',
  // null keeps the baked outfit, including the Bandage eye patch.
  look: null,
  // The hat brim hides the patch in this close framing.
  hide: ['Hat__1'],
  scale: 1.098,
  idleClip: IDLE_CLIP,
  idleFallbacks: IDLE_FALLBACKS,
  roomKind: 'saloon',
  room: 'models/env/backdrop.glb?v=2',
  saloon: 'models/env/backdrop.glb?v=2',
  chair: CHAIR_URL,
  reason: 'Bandit is the Western character with the stitched eye-patch bandage (mesh Bandage).',
};

export const OCTAGON_COACH_SCENE = {
  id: 'octagon',
  displayName: 'Maximilian',
  model: 'models/characters/street/Fighter1.glb?v=3',
  modelId: 'Fighter1',
  look: null,
  hide: [],
  scale: 1,
  idleClip: IDLE_CLIP,
  idleFallbacks: IDLE_FALLBACKS,
  roomKind: 'octagon',
  room: 'models/env/street-arena.glb?v=5',
  saloon: 'models/env/street-arena.glb?v=5',
  chair: CHAIR_URL,
  reason: 'Octagon review seats Fighter1 in the street-arena cage. The observe idle is baked on that rig.',
};

export const COACH_SCENE_DEFINITION = SALOON_COACH_SCENE;

export function coachSceneFor(sceneId) {
  return sceneId === 'street' ? OCTAGON_COACH_SCENE : SALOON_COACH_SCENE;
}

const WESTERN_SCALE = 1.098;
const ARENA_SCALE = 2.2;
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
  let active = SALOON_COACH_SCENE;

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
    active = coachSceneFor(assets.scene);
    const octagon = active.roomKind === 'octagon';
    const jacob = (octagon ? assets.fighter : assets.bandit) || await loadGlb(active.model);
    const clips = octagon
      ? jacob
      : (assets.clips || await loadGlb('models/characters/western/mighty-cat.glb?v=9'));
    const chairGltf = assets.chair || await loadGlb(active.chair);
    if (token === 0) {
      /* hide() bumped the token; abandon before attaching */
    }
    if (!jacob) {
      failedReason = 'missing-model';
      return false;
    }

    scene = new THREE.Scene();
    scene.background = new THREE.Color(octagon ? 0x0a0610 : 0x140e0a);
    scene.fog = new THREE.Fog(octagon ? 0x0a0610 : 0x140e0a, octagon ? 8 : 7.5, octagon ? 32 : 26);

    // Frame the seated coach in the left half. The advice card covers the right.
    camera = new THREE.PerspectiveCamera(40, 1, 0.05, octagon ? 120 : 80);
    camera.position.set(0.08, 1.52, 1.88);
    camera.lookAt(0.22, 1.16, -0.3);

    if (octagon) {
      scene.add(own(new THREE.AmbientLight(0xc8b8d8, 0.42)));
      scene.add(own(new THREE.HemisphereLight(0xd0c0e8, 0x1a1020, 0.32)));
    } else {
      scene.add(own(new THREE.AmbientLight(0xffe6c4, 0.55)));
      scene.add(own(new THREE.HemisphereLight(0xffd7a8, 0x3a2416, 0.38)));
    }
    const key = own(new THREE.SpotLight(octagon ? 0xffe0c0 : 0xffc98a, octagon ? 34 : 28, 10, 0.65, 0.42, 1.1));
    key.position.set(0.8, 2.5, 2.0);
    key.target.position.set(-0.45, 1.2, -0.4);
    scene.add(key, key.target);
    const fill = own(new THREE.PointLight(octagon ? 0xe8d0ff : 0xffb060, octagon ? 5 : 6, 8, 1.6));
    fill.position.set(-1.6, 1.8, 1.2);
    scene.add(fill);

    const saloon = await addRoom(assets.room, active);
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

  function softenArenaBeams(room) {
    room.traverse((m) => {
      if (!m.isMesh || !m.material) return;
      const name = m.material.name || '';
      if (!/high light/i.test(name)) return;
      const map = m.material.map;
      if (map) map.colorSpace = THREE.SRGBColorSpace;
      m.material = own(new THREE.MeshBasicMaterial({
        name,
        map,
        transparent: true,
        opacity: 0.25,
        blending: THREE.AdditiveBlending,
        depthWrite: false,
        side: THREE.DoubleSide,
        fog: false,
      }));
    });
  }

  async function addRoom(existing, def) {
    if (existing) {
      const clone = existing.clone(true);
      scene.add(clone);
      return clone;
    }
    const gltf = await loadGlb(def.room);
    if (!gltf) return null;
    const env = new THREE.Group();
    const room = gltf.scene;
    if (def.roomKind === 'octagon') {
      room.rotation.y = 0;
      room.position.set(0, 0, 0);
      room.scale.setScalar(ARENA_SCALE);
      softenArenaBeams(room);
      const rig = [
        [0xffb000, -1.58, 7.31, -2.43],
        [0x20ff50, 1.41, 7.31, -2.43],
        [0x2090ff, -1.58, 7.31, 2.42],
        [0xa030ff, 0.04, 7.31, 2.42],
      ];
      for (const [color, x, y, z] of rig) {
        const spot = own(new THREE.SpotLight(color, 40, 16, 0.45, 0.65, 1.4));
        spot.position.set(x, y, z);
        spot.target.position.set(0, 0.9, 0);
        env.add(spot, spot.target);
      }
    } else {
      room.rotation.y = Math.PI;
      room.position.z = 2.4;
      const barLamp = own(new THREE.PointLight(0xffb060, 8, 12, 1.6));
      barLamp.position.set(0, 3.1, -4.2);
      env.add(barLamp);
    }
    env.add(room);
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
    root.scale.setScalar(active.scale || WESTERN_SCALE);
    // Jacob ships every outfit in one file, so a look drops the extras.
    // Bandit is already one outfit; filtering him would remove the eye patch.
    // The hat brim hides that patch in this close framing, so the saloon
    // coach leaves the hat off and keeps the bandage. The fighter has no hat.
    const hide = new Set(active.hide || []);
    const look = active.look;
    if (look) {
      const names = westernMeshNames(look);
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
    }
    const uncovered = [];
    root.traverse((obj) => {
      if (!obj.isMesh) return;
      const n = (obj.name || '').replace(/\.\d+$/, '');
      const parent = obj.parent && (obj.parent.name || '').replace(/\.\d+$/, '');
      if (hide.has(n) || hide.has(parent)) uncovered.push(obj);
    });
    for (const obj of uncovered) if (obj.parent) obj.parent.remove(obj);
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
    const wanted = [active.idleClip, ...active.idleFallbacks];
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
    get definition() { return active; },

    async show(payload) {
      const mine = ++token;
      presenting = true;
      reduced = !!(payload && payload.reducedMotion);
      if (!contextOk()) {
        failedReason = 'no-webgl';
        ready = false;
        return { ok: false, reason: failedReason, definition: active };
      }
      const ok = await ensure(mine);
      if (mine !== token || !presenting) return { ok: false, reason: 'stale', definition: active };
      if (!ok || !scene) return { ok: false, reason: failedReason || 'missing-model', definition: active };
      layCards(payload || {});
      playIdle();
      return {
        ok: true,
        reason: '',
        model: active.model,
        clip: clipName,
        definition: active,
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
        model: active.model,
        modelId: active.modelId,
        room: active.roomKind,
        clip: clipName,
        mixerTime: action ? action.time : 0,
        playing: !!(action && action.isRunning() && !action.paused),
        loop: action ? action.loop : -1,
        characterMeshes,
        sceneMeshes: saloonMeshes,
        instances: root ? 1 : 0,
        reason: failedReason,
        look: active.roomKind === 'octagon' ? 'fighter1-seated' : 'bandit-eye-patch',
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
