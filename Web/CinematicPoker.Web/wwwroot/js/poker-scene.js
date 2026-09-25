// First-person saloon poker scene (Three.js). Props/textures are CC0; the
// table characters are web-optimised conversions of the repo owner's licensed
// "Arcade Fighters" Unity pack — see ASSET_LICENSES.md. No third-party game
// content is copied from other titles.
import * as THREE from 'three';
import { GLTFLoader } from './vendor/GLTFLoader.js';
import { clone as cloneSkinned } from './vendor/SkeletonUtils.js';

const SEATS = 6;               // seat 0 = the player (camera)
const TABLE_TOP = 0.78;        // table surface height (m)
const TABLE_RADIUS = 1.12;
const SEAT_RADIUS = 1.74;
const EYE_HEIGHT = 1.42;

// Arcade Fighters (user-licensed, converted to GLB) per NPC seat (1..5).
// Two base fighters; extra seats use alternate outfit textures so all five
// opponents read as distinct people.
const NPC_MODELS = [
  { model: 'Fighter1', tex: null },                 // Davo — purple-gi monk
  { model: 'Fighter2', tex: null },                 // Mick — red-cap brawler
  { model: 'Fighter1', tex: 'fighter1_var.jpg' },   // Shazza — crimson gi
  { model: 'Fighter2', tex: 'fighter2_var.jpg' },   // Bluey — blue brawler
  { model: 'Fighter1', tex: 'fighter1_green.jpg' }  // Kev — green gi
];

const state = {
  ready: false,
  renderer: null, scene: null, camera: null, clock: null,
  textures: new Map(),
  cardGeo: null,
  boardCards: [],
  holeCards: [],
  seats: [],            // per seat: { group, label, cards:[], chips, dealerBtn, char }
  potChips: null,
  lastJson: ''
};

function texture(path) {
  let tex = state.textures.get(path);
  if (!tex) {
    tex = new THREE.TextureLoader().load(path);
    tex.colorSpace = THREE.SRGBColorSpace;
    tex.anisotropy = 4;
    state.textures.set(path, tex);
  }
  return tex;
}

// Warm all card textures up front so reveals never flash black mid-hand.
function preloadCardTextures() {
  const ranks = ['2', '3', '4', '5', '6', '7', '8', '9', '10', 'J', 'Q', 'K', 'A'];
  for (const suit of ['clubs', 'diamonds', 'hearts', 'spades'])
    for (const rank of ranks) texture(`img/cards/${suit}_${rank}.png`);
  texture('img/cards/back.png');
}

function seatAngle(i) {
  // Seat 0 (camera) sits at +Z looking at the table centre; others fan around.
  return Math.PI / 2 + (i * Math.PI * 2) / SEATS;
}

function seatPos(i, radius, y = 0) {
  const a = seatAngle(i);
  return new THREE.Vector3(Math.cos(a) * radius, y, Math.sin(a) * radius);
}

// ------------------------------------------------------------------ cards

function makeCard(path, w = 0.18, unlit = false) {
  const h = w * 190 / 140;
  const geo = new THREE.PlaneGeometry(w, h);
  const mat = unlit
    ? new THREE.MeshBasicMaterial({ map: texture(path), side: THREE.DoubleSide })
    : new THREE.MeshStandardMaterial({
        map: texture(path), side: THREE.DoubleSide, roughness: 0.8, metalness: 0
      });
  const mesh = new THREE.Mesh(geo, mat);
  mesh.castShadow = false;
  return mesh;
}

// Board / table cards lean back toward the seated player so they read clearly.
function placeTableCard(mesh, x, z, lean = 0.95, yaw = 0) {
  const h = mesh.geometry.parameters.height;
  mesh.position.set(x, TABLE_TOP + Math.sin(lean) * h * 0.5 + 0.002, z);
  mesh.rotation.order = 'YXZ'; // yaw first, then lean, so flat cards stay flat
  mesh.rotation.set(-(Math.PI / 2 - lean), yaw, 0);
}

// ------------------------------------------------------------------ labels

function makeLabel() {
  const canvas = document.createElement('canvas');
  canvas.width = 512; canvas.height = 192;
  const tex = new THREE.CanvasTexture(canvas);
  tex.colorSpace = THREE.SRGBColorSpace;
  const sprite = new THREE.Sprite(new THREE.SpriteMaterial({
    map: tex, transparent: true, depthTest: false
  }));
  sprite.scale.set(0.46, 0.1725, 1);
  sprite.renderOrder = 10;
  return { sprite, canvas, tex, text: '' };
}

function drawLabel(label, seat) {
  const key = JSON.stringify([seat.name, seat.stack, seat.status, seat.actor, seat.out, seat.folded, seat.dealer, seat.sb, seat.bb]);
  if (key === label.text) return;
  label.text = key;

  const ctx = label.canvas.getContext('2d');
  const W = 512, H = 192;
  ctx.clearRect(0, 0, W, H);

  ctx.fillStyle = seat.actor ? 'rgba(46,32,14,0.88)' : 'rgba(12,10,8,0.72)';
  ctx.beginPath();
  ctx.roundRect(8, 8, W - 16, H - 16, 26);
  ctx.fill();
  if (seat.actor) {
    ctx.strokeStyle = '#e8b23c'; ctx.lineWidth = 8; ctx.stroke();
  }

  ctx.textAlign = 'center';
  ctx.fillStyle = seat.out || seat.folded ? '#8a7f70' : '#f0e2c8';
  ctx.font = 'bold 58px Georgia, serif';
  let name = seat.name;
  if (seat.dealer) name += '  ◈';
  else if (seat.sb) name += '  sb';
  else if (seat.bb) name += '  bb';
  ctx.fillText(name, W / 2, 78);

  ctx.font = 'bold 50px Georgia, serif';
  if (seat.out) { ctx.fillStyle = '#b0483c'; ctx.fillText('OUT', W / 2, 148); }
  else {
    ctx.fillStyle = '#e0b95e';
    let line = '$' + seat.stack;
    if (seat.status) line += '   ·   ' + seat.status;
    ctx.fillText(line, W / 2, 148);
  }
  label.tex.needsUpdate = true;
}

// ------------------------------------------------------------------ chips

const CHIP_COLORS = [0xb0483c, 0x3b6ea5, 0x3f7d4e, 0x2b2b2b, 0xc9a13d];

function makeChipStacks(amount, spread = 0.05) {
  const group = new THREE.Group();
  if (amount <= 0) return group;
  const chips = Math.min(24, 1 + Math.floor(amount / 60));
  const perStack = 8;
  for (let i = 0; i < chips; i++) {
    const stack = Math.floor(i / perStack);
    const level = i % perStack;
    const geo = new THREE.CylinderGeometry(0.045, 0.045, 0.012, 20);
    const mat = new THREE.MeshStandardMaterial({
      color: CHIP_COLORS[(i + stack) % CHIP_COLORS.length], roughness: 0.55
    });
    const chip = new THREE.Mesh(geo, mat);
    chip.castShadow = true;
    chip.position.set(stack * spread * 1.9 - spread, 0.007 + level * 0.0125, (stack % 2) * spread * 0.8);
    chip.rotation.y = Math.random() * Math.PI;
    group.add(chip);
  }
  return group;
}

// ------------------------------------------------------------------ characters

function loadGlb(loader, url) {
  return new Promise((resolve) => loader.load(url, resolve, undefined, (err) => {
    console.warn('poker-scene: failed to load', url, err);
    resolve(null);
  }));
}

function makeCharacter(gltf, texPath) {
  if (!gltf) return null;
  // Two seats can share one fighter model, so clone the skinned rig per seat.
  const root = cloneSkinned(gltf.scene);

  // Normalise to a realistic human height using the standing bind pose.
  const box = new THREE.Box3().setFromObject(root);
  const height = Math.max(0.01, box.max.y - box.min.y);
  root.scale.setScalar(1.72 / height);

  // Alternate outfit texture (palette swap) so shared meshes look distinct.
  let overrideTex = null;
  if (texPath) {
    overrideTex = new THREE.TextureLoader().load(`models/characters/${texPath}`);
    overrideTex.colorSpace = THREE.SRGBColorSpace;
    overrideTex.flipY = false; // glTF UV convention
  }

  root.traverse((obj) => {
    if (!obj.isMesh) return;
    obj.castShadow = true;
    obj.receiveShadow = true;
    obj.frustumCulled = false; // skinned mesh bounds lag the animated pose
    if (overrideTex) {
      obj.material = obj.material.clone();
      obj.material.map = overrideTex;
    }
  });

  const mixer = new THREE.AnimationMixer(root);
  const clips = gltf.animations || [];
  const find = (name) => THREE.AnimationClip.findByName(clips, name);

  // 'Sit' is a single-frame poker pose baked during conversion; hold it.
  const sitClip = find('Sit');
  let sitAction = null;
  if (sitClip) {
    sitAction = mixer.clipAction(sitClip);
    sitAction.play();
    sitAction.paused = true;
  }

  const character = { root, mixer, sitAction, find, dead: false, reacting: false };

  // Reactions play the opening beat of a fight move, then settle back down.
  // The source takes are long combo loops, so we cut away on a timer rather
  // than waiting for the clip to finish.
  character.playOnce = (name, seconds = 1.5) => {
    if (character.dead || character.reacting) return;
    const clip = find(name);
    if (!clip || !sitAction) return;
    character.reacting = true;
    const action = mixer.clipAction(clip);
    action.reset();
    action.setLoop(THREE.LoopOnce, 1);
    sitAction.paused = false;
    sitAction.crossFadeTo(action, 0.25, false);
    action.play();
    setTimeout(() => {
      if (character.dead) return;
      sitAction.reset();
      action.crossFadeTo(sitAction, 0.35, false);
      sitAction.play();
      setTimeout(() => { sitAction.paused = true; character.reacting = false; }, 450);
    }, seconds * 1000);
  };

  character.die = () => {
    character.dead = true;
    const clip = find('Crouch');
    if (!clip || !sitAction) { return; }
    const action = mixer.clipAction(clip);
    action.reset();
    action.setLoop(THREE.LoopOnce, 1);
    action.clampWhenFinished = true;
    sitAction.paused = false;
    sitAction.crossFadeTo(action, 0.4, false);
    action.play();
    // Hold the slumped-down part of the crouch instead of cycling back up.
    setTimeout(() => { action.paused = true; }, 1200);
  };

  return character;
}

// ------------------------------------------------------------------ scene build

async function buildScene(canvas) {
  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  renderer.setSize(canvas.clientWidth, canvas.clientHeight, false);
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.0;
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;

  const scene = new THREE.Scene();
  scene.background = new THREE.Color(0x120b07);
  scene.fog = new THREE.Fog(0x120b07, 7, 14);

  const camera = new THREE.PerspectiveCamera(56, canvas.clientWidth / canvas.clientHeight, 0.05, 30);
  const camPos = seatPos(0, SEAT_RADIUS + 0.12, EYE_HEIGHT);
  camera.position.copy(camPos);
  camera.rotation.order = 'YXZ';
  camera.lookAt(0, TABLE_TOP - 0.06, 0);

  // One-finger (or mouse-drag) look-around, pivoting from the player's seat.
  const view = {
    yaw: camera.rotation.y, pitch: camera.rotation.x,
    targetYaw: camera.rotation.y, targetPitch: camera.rotation.x,
    baseYaw: camera.rotation.y, basePitch: camera.rotation.x
  };
  state.view = view;
  setupLookControls(canvas, view);

  // ---- lighting: warm lantern over the table, dim saloon ambience
  scene.add(new THREE.AmbientLight(0x8a6a45, 0.7));
  const hemi = new THREE.HemisphereLight(0x6f5a3e, 0x191009, 0.55);
  scene.add(hemi);

  const lantern = new THREE.PointLight(0xffc477, 22, 9, 1.9);
  lantern.position.set(0, 2.15, 0);
  scene.add(lantern);

  // Shadow-casting key light from the lamp: grounds characters and props.
  const keyLight = new THREE.SpotLight(0xffc98a, 30, 11, Math.PI / 2.4, 0.55, 1.6);
  keyLight.position.set(0, 2.7, 0);
  keyLight.target.position.set(0, 0, 0);
  keyLight.castShadow = true;
  keyLight.shadow.mapSize.set(1024, 1024);
  keyLight.shadow.bias = -0.0004;
  keyLight.shadow.camera.near = 0.5;
  keyLight.shadow.camera.far = 11;
  scene.add(keyLight, keyLight.target);

  const fill = new THREE.PointLight(0xff9d4d, 7, 8, 2);
  fill.position.set(2.6, 1.7, 2.4);
  scene.add(fill);

  // Back-wall glow so the room reads behind the far players.
  const backFill = new THREE.PointLight(0xffb066, 9, 9, 2);
  backFill.position.set(-1.8, 1.9, -3.1);
  scene.add(backFill);

  // Visible hanging lamp.
  const lampGroup = new THREE.Group();
  const cord = new THREE.Mesh(
    new THREE.CylinderGeometry(0.012, 0.012, 0.85, 8),
    new THREE.MeshStandardMaterial({ color: 0x1a130c }));
  cord.position.y = 2.75;
  const shade = new THREE.Mesh(
    new THREE.ConeGeometry(0.28, 0.2, 24, 1, true),
    new THREE.MeshStandardMaterial({ color: 0x27408b, roughness: 0.6, side: THREE.DoubleSide }));
  shade.position.y = 2.32;
  const bulb = new THREE.Mesh(
    new THREE.SphereGeometry(0.05, 12, 12),
    new THREE.MeshBasicMaterial({ color: 0xffd9a0 }));
  bulb.position.y = 2.22;
  lampGroup.add(cord, shade, bulb);
  scene.add(lampGroup);

  // ---- room (Poly Haven CC0 plank textures)
  const floorTex = texture('img/floor_planks.jpg');
  floorTex.wrapS = floorTex.wrapT = THREE.RepeatWrapping;
  floorTex.repeat.set(4, 4);
  const floor = new THREE.Mesh(
    new THREE.PlaneGeometry(10, 10),
    new THREE.MeshStandardMaterial({ map: floorTex, roughness: 0.9 }));
  floor.rotation.x = -Math.PI / 2;
  floor.receiveShadow = true;
  scene.add(floor);

  const wallTex = texture('img/wall_planks.jpg');
  wallTex.wrapS = wallTex.wrapT = THREE.RepeatWrapping;
  wallTex.repeat.set(3, 1.2);
  const wallMat = new THREE.MeshStandardMaterial({ map: wallTex, roughness: 0.95, color: 0xb59a78 });
  for (let i = 0; i < 4; i++) {
    const wall = new THREE.Mesh(new THREE.PlaneGeometry(10, 3.2), wallMat);
    const a = (i * Math.PI) / 2;
    wall.position.set(Math.sin(a) * 4.5, 1.6, Math.cos(a) * 4.5);
    wall.rotation.y = a + Math.PI;
    scene.add(wall);
  }
  const ceiling = new THREE.Mesh(
    new THREE.PlaneGeometry(10, 10),
    new THREE.MeshStandardMaterial({ map: wallTex.clone(), color: 0x4a3a28, roughness: 1 }));
  ceiling.material.map.repeat.set(4, 4);
  ceiling.rotation.x = Math.PI / 2;
  ceiling.position.y = 3.2;
  scene.add(ceiling);

  // ---- poker table
  const feltTex = texture('img/felt.jpg');
  feltTex.wrapS = feltTex.wrapT = THREE.RepeatWrapping;
  feltTex.repeat.set(2, 2);
  const felt = new THREE.Mesh(
    new THREE.CylinderGeometry(TABLE_RADIUS, TABLE_RADIUS, 0.05, 48),
    new THREE.MeshStandardMaterial({ map: feltTex, color: 0x6e241f, roughness: 0.97 }));
  felt.position.y = TABLE_TOP - 0.025;
  felt.castShadow = true;
  felt.receiveShadow = true;
  scene.add(felt);

  const woodTex = texture('img/wood.jpg');
  const rail = new THREE.Mesh(
    new THREE.TorusGeometry(TABLE_RADIUS + 0.035, 0.055, 14, 48),
    new THREE.MeshStandardMaterial({ map: woodTex, color: 0x8a5a33, roughness: 0.7 }));
  rail.rotation.x = Math.PI / 2;
  rail.position.y = TABLE_TOP - 0.01;
  rail.castShadow = true;
  scene.add(rail);

  const pedestal = new THREE.Mesh(
    new THREE.CylinderGeometry(0.16, 0.34, TABLE_TOP - 0.05, 20),
    new THREE.MeshStandardMaterial({ map: woodTex.clone(), color: 0x6e4526, roughness: 0.8 }));
  pedestal.position.y = (TABLE_TOP - 0.05) / 2;
  scene.add(pedestal);

  const base = new THREE.Mesh(
    new THREE.CylinderGeometry(0.55, 0.6, 0.05, 24),
    new THREE.MeshStandardMaterial({ color: 0x4a2f1a, roughness: 0.85 }));
  base.position.y = 0.025;
  scene.add(base);

  // ---- props & characters (Kenney CC0 GLBs)
  const loader = new GLTFLoader();
  const addProp = async (url, x, z, ry = 0, s = 1) => {
    const gltf = await loadGlb(loader, url);
    if (!gltf) return null;
    const obj = gltf.scene;
    obj.scale.setScalar(s);
    obj.position.set(x, 0, z);
    obj.rotation.y = ry;
    // Ground the prop: some packs use a centred origin, not a base origin.
    const bb = new THREE.Box3().setFromObject(obj);
    obj.position.y -= bb.min.y;
    obj.traverse((m) => { if (m.isMesh) { m.castShadow = true; m.receiveShadow = true; } });
    scene.add(obj);
    return obj;
  };

  const props = [
    addProp('models/props/survival/barrel.glb', -2.7, -3.5, 0.4, 1.5),
    addProp('models/props/survival/barrel.glb', -2.05, -3.7, 1.9, 1.5),
    addProp('models/props/survival/box-large.glb', 2.5, -3.6, 0.2, 1.6),
    addProp('models/props/survival/box-large.glb', 3.1, -3.1, 0.9, 1.6),
    addProp('models/props/survival/bottle-large.glb', -2.7, -3.5, 0, 1.5).then(async (b) => {
      if (b) b.position.y += 0.85; // on the barrel
    }),
    addProp('models/props/survival/barrel.glb', 3.7, 0.6, 2.6, 1.5),
    addProp('models/props/food/mug.glb', 3.7, 0.6, 0, 2.2).then(async (m) => {
      if (m) m.position.y += 0.85;
    })
  ];

  // ---- seats (chairs + characters + cards + labels)
  const chairGltf = await loadGlb(loader, 'models/props/furniture/chair.glb');
  state.seats = [];
  const charPromises = [];

  // The two fighter GLBs are shared across the five NPC seats.
  const modelCache = new Map();
  const loadCharacterModel = (name) => {
    if (!modelCache.has(name)) {
      modelCache.set(name, loadGlb(loader, `models/characters/${name}.glb`));
    }
    return modelCache.get(name);
  };

  for (let i = 0; i < SEATS; i++) {
    const seat = { group: new THREE.Group(), cards: [], chips: null, char: null, label: null };
    state.seats.push(seat);
    scene.add(seat.group);
    if (i === 0) continue; // the player: no chair/character/label needed

    const pos = seatPos(i, SEAT_RADIUS);
    const facing = Math.atan2(-pos.x, -pos.z); // yaw toward table centre

    if (chairGltf) {
      const chair = chairGltf.scene.clone(true);
      chair.traverse((m) => { if (m.isMesh) { m.castShadow = true; m.receiveShadow = true; } });
      chair.scale.setScalar(1.15);
      chair.position.copy(pos);
      chair.rotation.y = facing;
      scene.add(chair);
    }

    const npc = NPC_MODELS[i - 1];
    charPromises.push(loadCharacterModel(npc.model).then((gltf) => {
      const character = makeCharacter(gltf, npc.tex);
      if (!character) return;
      character.root.position.copy(seatPos(i, SEAT_RADIUS - 0.34));
      scene.add(character.root);

      // Apply the frozen 'Sit' pose before measuring any bones.
      character.mixer.update(0);
      character.root.updateMatrixWorld(true);

      // GLTFLoader sanitises 'Fighter1 L Foot' -> 'Fighter1_L_Foot'; match by
      // suffix so both fighters resolve the same skeleton landmarks.
      const bone = (suffix) => {
        let found = null;
        character.root.traverse((o) => {
          if (!found && o.isBone && o.name.endsWith(suffix)) found = o;
        });
        return found;
      };

      // Rigs differ in which axis they face, so measure the model's own
      // forward (up x left-to-right-foot) and correct toward the table.
      const lFoot = bone('L_Foot'), rFoot = bone('R_Foot');
      let modelYaw = 0;
      if (lFoot && rFoot) {
        const l = lFoot.getWorldPosition(new THREE.Vector3());
        const r = rFoot.getWorldPosition(new THREE.Vector3());
        const fwd = new THREE.Vector3(0, 1, 0).cross(r.sub(l));
        if (fwd.lengthSq() > 1e-6) modelYaw = Math.atan2(fwd.x, fwd.z);
      }
      character.root.rotation.y = facing - modelYaw;
      character.root.updateMatrixWorld(true);

      // Snap the seated pose onto the chair: hips over the seat and feet on
      // the floor, regardless of the pose's baked root offsets.
      const v = new THREE.Vector3();
      const hips = bone('Pelvis') || bone('Hips');
      if (hips) {
        hips.getWorldPosition(v);
        const target = seatPos(i, SEAT_RADIUS - 0.02);
        character.root.position.x += target.x - v.x;
        character.root.position.z += target.z - v.z;
      }
      let minFoot = Infinity;
      for (const b of [lFoot, rFoot]) {
        if (b) { b.getWorldPosition(v); minFoot = Math.min(minFoot, v.y); }
      }
      if (isFinite(minFoot)) character.root.position.y -= (minFoot - 0.09);
      seat.char = character;
    }));

    const label = makeLabel();
    const labelPos = seatPos(i, SEAT_RADIUS + 0.05, 1.52);
    label.sprite.position.copy(labelPos);
    scene.add(label.sprite);
    seat.label = label;
  }

  await Promise.all([...props, ...charPromises]);

  // ---- hole cards "in hand", attached to the camera
  const holeGroup = new THREE.Group();
  camera.add(holeGroup);
  scene.add(camera);
  state.holeGroup = holeGroup;

  state.renderer = renderer;
  state.scene = scene;
  state.camera = camera;
  state.clock = new THREE.Clock();

  preloadCardTextures();

  renderer.setAnimationLoop(() => {
    const dt = state.clock.getDelta();
    for (const seat of state.seats) if (seat.char) seat.char.mixer.update(dt);

    // Smoothly ease the camera toward where the finger dragged it.
    const ease = Math.min(1, dt * 14);
    view.yaw += (view.targetYaw - view.yaw) * ease;
    view.pitch += (view.targetPitch - view.pitch) * ease;
    camera.rotation.set(view.pitch, view.yaw, 0);

    resizeIfNeeded(canvas, renderer, camera);
    renderer.render(scene, camera);
  });
}

// ------------------------------------------------------------------ look controls

const PITCH_MIN = -1.05;  // looking down at your cards
const PITCH_MAX = 0.4;    // looking up at the lamp/ceiling

function setupLookControls(canvas, view) {
  canvas.style.touchAction = 'none'; // stop iOS Safari from scrolling/zooming the page
  let activePointer = -1;
  let lastX = 0, lastY = 0, moved = 0, lastTapAt = 0;
  const SPEED = 0.0042;

  canvas.addEventListener('pointerdown', (e) => {
    if (activePointer !== -1) return; // one finger only; ignore extra touches
    activePointer = e.pointerId;
    lastX = e.clientX; lastY = e.clientY; moved = 0;
    canvas.setPointerCapture(e.pointerId);
  });

  canvas.addEventListener('pointermove', (e) => {
    if (e.pointerId !== activePointer) return;
    const dx = e.clientX - lastX, dy = e.clientY - lastY;
    lastX = e.clientX; lastY = e.clientY;
    moved += Math.abs(dx) + Math.abs(dy);
    view.targetYaw -= dx * SPEED;   // swipe right = look right
    view.targetPitch = THREE.MathUtils.clamp(
      view.targetPitch - dy * SPEED, PITCH_MIN, PITCH_MAX); // swipe up = look up
  });

  const release = (e) => {
    if (e.pointerId !== activePointer) return;
    activePointer = -1;
    // Double-tap (without dragging) snaps the view back to the table.
    if (moved < 8) {
      const now = performance.now();
      if (now - lastTapAt < 350) {
        view.targetYaw = view.baseYaw;
        view.targetPitch = view.basePitch;
        lastTapAt = 0;
      } else {
        lastTapAt = now;
      }
    }
  };
  canvas.addEventListener('pointerup', release);
  canvas.addEventListener('pointercancel', release);
}

let lastW = 0, lastH = 0;
function resizeIfNeeded(canvas, renderer, camera) {
  const w = canvas.clientWidth, h = canvas.clientHeight;
  if (w === lastW && h === lastH) return;
  lastW = w; lastH = h;
  if (w === 0 || h === 0) return;
  renderer.setSize(w, h, false);
  camera.aspect = w / h;
  // Tall portrait phones need a wider view to keep the table in frame.
  camera.fov = camera.aspect < 0.8 ? 70 : 56;
  camera.updateProjectionMatrix();
}

// ------------------------------------------------------------------ state updates

function clearGroupChildren(list, parent) {
  for (const m of list) parent.remove(m);
  list.length = 0;
}

function updateBoard(paths) {
  clearGroupChildren(state.boardCards, state.scene);
  const n = paths.length;
  const spacing = 0.21;
  for (let i = 0; i < n; i++) {
    const card = makeCard(paths[i], 0.19);
    // Board sits between centre and the player, leaning toward the camera.
    placeTableCard(card, (i - (n - 1) / 2) * spacing, 0.34, 1.02);
    state.boardCards.push(card);
    state.scene.add(card);
  }
}

function updateHole(paths) {
  clearGroupChildren(state.holeCards, state.holeGroup);
  for (let i = 0; i < paths.length; i++) {
    const card = makeCard(paths[i], 0.15, true);
    card.material.depthTest = false;
    card.renderOrder = 20;
    const dir = i === 0 ? -1 : 1;
    card.position.set(0.05 + dir * 0.05, -0.26, -0.52);
    card.rotation.set(-0.3, 0, dir * 0.12);
    state.holeCards.push(card);
    state.holeGroup.add(card);
  }
}

function updateSeatCards(seat, i, data) {
  clearGroupChildren(seat.cards, state.scene);
  if (i === 0 || !data.active) return;

  const basePos = seatPos(i, TABLE_RADIUS - 0.33);
  const yaw = seatAngle(i) - Math.PI / 2; // cards face along seat direction
  const paths = data.reveal && data.reveal.length ? data.reveal : null;

  for (let c = 0; c < 2; c++) {
    const path = paths ? paths[c] : 'img/cards/back.png';
    const card = makeCard(path, paths ? 0.17 : 0.13, !!paths);
    const offset = (c === 0 ? -0.075 : 0.075);
    const ox = Math.cos(yaw) * offset;
    const oz = -Math.sin(yaw) * offset;
    // Revealed cards lean up toward the camera; face-down cards lie nearly flat.
    if (paths) {
      placeTableCard(card, basePos.x + ox, basePos.z + oz, 1.0, 0);
    } else {
      placeTableCard(card, basePos.x + ox, basePos.z + oz, 0.12, yaw);
    }
    seat.cards.push(card);
    state.scene.add(card);
  }
}

function updateSeatChips(seat, i, bet) {
  if (seat.chips) { state.scene.remove(seat.chips); seat.chips = null; }
  if (!bet || bet <= 0) return;
  const pos = seatPos(i, TABLE_RADIUS - 0.62);
  seat.chips = makeChipStacks(bet);
  seat.chips.position.set(pos.x, TABLE_TOP, pos.z);
  state.scene.add(seat.chips);
}

function updatePot(pot) {
  if (state.potChips) { state.scene.remove(state.potChips); state.potChips = null; }
  if (!pot || pot <= 0) return;
  state.potChips = makeChipStacks(pot, 0.07);
  state.potChips.position.set(-0.06, TABLE_TOP, -0.18);
  state.potChips.rotation.y = 0.5;
  state.scene.add(state.potChips);
}

// ------------------------------------------------------------------ public API

window.pokerScene = {
  get loaded() { return state.ready; },

  async init(canvasId) {
    if (state.ready) return true;
    const canvas = document.getElementById(canvasId);
    if (!canvas) return false;
    await buildScene(canvas);
    state.ready = true;
    if (state.pendingUpdate) { this.update(state.pendingUpdate); state.pendingUpdate = null; }
    return true;
  },

  update(snapshot) {
    if (!state.ready) { state.pendingUpdate = snapshot; return; }
    const json = JSON.stringify(snapshot);
    if (json === state.lastJson) return;

    const prev = state.lastJson ? JSON.parse(state.lastJson) : null;
    state.lastJson = json;

    if (!prev || JSON.stringify(prev.board) !== JSON.stringify(snapshot.board)) {
      updateBoard(snapshot.board || []);
    }
    if (!prev || JSON.stringify(prev.hole) !== JSON.stringify(snapshot.hole)) {
      updateHole(snapshot.hole || []);
    }
    if (!prev || prev.pot !== snapshot.pot) updatePot(snapshot.pot);

    const seats = snapshot.seats || [];
    for (const data of seats) {
      const seat = state.seats[data.seat];
      if (!seat) continue;
      const prevData = prev ? (prev.seats || []).find(s => s.seat === data.seat) : null;
      if (!prevData || JSON.stringify(prevData.reveal) !== JSON.stringify(data.reveal)
          || prevData.active !== data.active) {
        updateSeatCards(seat, data.seat, data);
      }
      if (!prevData || prevData.bet !== data.bet) updateSeatChips(seat, data.seat, data.bet);
      if (seat.label) drawLabel(seat.label, data);
      if (seat.char && data.out && !seat.char.dead) seat.char.die();
    }
  },

  react(seatIndex, positive) {
    if (!state.ready) return;
    const seat = state.seats[seatIndex];
    // Winners throw a quick victory strike; losers slump into a crouch.
    if (seat && seat.char) seat.char.playOnce(positive ? 'Attack' : 'Crouch', positive ? 1.5 : 1.2);
  }
};
