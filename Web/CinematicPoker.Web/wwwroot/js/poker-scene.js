// Over-the-shoulder saloon poker scene (Three.js). Props/textures are CC0;
// the table characters are web-optimised conversions of the repo owner's
// licensed CrowArt "Hunter" Unity asset — see ASSET_LICENSES.md. No
// third-party game content is copied from other titles.
import * as THREE from 'three';
import { GLTFLoader } from './vendor/GLTFLoader.js';
import { clone as cloneSkinned } from './vendor/SkeletonUtils.js';

const SEATS = 6;               // seat 0 = the player (camera)
const TABLE_TOP = 0.78;        // table surface height (m)
const TABLE_RADIUS = 1.12;
const SEAT_RADIUS = 1.74;
const EYE_HEIGHT = 1.42;

// All six players are variations of the owner-licensed CrowArt "Hunter"
// western character (converted to GLB): different hats plus shirt palette
// swaps so everyone at the table reads as a distinct person.
//   hat: 'top' | 'fedora' | null (bare-headed)
const NPC_MODELS = [
  { model: 'Hunter', hat: 'fedora', tex: null },                     // Davo
  { model: 'Hunter', hat: null, tex: 'hunter_shirt_red.jpg' },       // Mick
  { model: 'Hunter', hat: 'top', tex: null },                        // Shazza
  { model: 'Hunter', hat: 'fedora', tex: 'hunter_shirt_blue.jpg' },  // Bluey
  { model: 'Hunter', hat: null, tex: 'hunter_shirt_green.jpg' }      // Kev
];

// The player's own body, seen from the over-the-shoulder camera. He uses the
// card-holding sit loop, and his hole cards ride in his raised left hand.
const PLAYER_MODEL = { model: 'Hunter', hat: 'top', tex: 'hunter_shirt_dark.jpg', cards: true };

// The Hunter GLB is authored at ~0.40 units tall; scale to life size.
const CHAR_SCALE = 4.3;

const _idleQuat = new THREE.Quaternion();
const _idleEuler = new THREE.Euler();

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

// Table cards are twice real size (63mm -> 126mm wide) so they stay readable
// from the seat; tap-to-zoom still gives a full close-up.
const CARD_W = 0.128;

// Table cards lie flat on the felt in world space, like real dealt cards.
function placeTableCard(mesh, x, z, lean = 0, yaw = 0) {
  const h = mesh.geometry.parameters.height;
  mesh.position.set(x, TABLE_TOP + Math.sin(lean) * h * 0.5 + 0.004, z);
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

// ------------------------------------------------------------------ dealer arrow

// A golden arrow hanging over the head of whoever must act, pointing down.
function makeDealerArrow() {
  const group = new THREE.Group();
  const mat = new THREE.MeshStandardMaterial({
    color: 0xe8b23c, emissive: 0x8a5f12, roughness: 0.35, metalness: 0.4
  });
  const tip = new THREE.Mesh(new THREE.ConeGeometry(0.055, 0.12, 12), mat);
  tip.rotation.x = Math.PI; // point straight down
  const shaft = new THREE.Mesh(new THREE.CylinderGeometry(0.018, 0.018, 0.12, 10), mat);
  shaft.position.y = 0.115;
  group.add(tip, shaft);
  group.visible = false;
  return group;
}

function updateDealerArrow(dealerSeat) {
  const arrow = state.dealerArrow;
  if (!arrow) return;
  if (dealerSeat < 0) { arrow.visible = false; return; }
  const pos = seatPos(dealerSeat, SEAT_RADIUS - 0.1);
  // Hang it above the seated character's head (labels float higher still).
  arrow.position.set(pos.x, 1.62, pos.z);
  arrow.visible = true;
}

// ------------------------------------------------------------------ characters

function loadGlb(loader, url) {
  return new Promise((resolve) => loader.load(url, resolve, undefined, (err) => {
    console.warn('poker-scene: failed to load', url, err);
    resolve(null);
  }));
}

function makeCharacter(gltf, spec) {
  if (!gltf) return null;
  // All seats share one Hunter model, so clone the skinned rig per seat.
  const root = cloneSkinned(gltf.scene);
  root.scale.setScalar(CHAR_SCALE);

  // Shirt palette swap so shared meshes look distinct (torso only: the head,
  // hats and trousers keep their original textures).
  let overrideTex = null;
  if (spec.tex) {
    overrideTex = new THREE.TextureLoader().load(`models/characters/${spec.tex}`);
    overrideTex.colorSpace = THREE.SRGBColorSpace;
    overrideTex.flipY = false; // glTF UV convention
  }

  root.traverse((obj) => {
    if (!obj.isMesh) return;
    // Each seat wears at most one of the two hat meshes in the GLB.
    if (obj.name === 'top_hat') obj.visible = spec.hat === 'top';
    if (obj.name === 'fedora_hat') obj.visible = spec.hat === 'fedora';
    obj.castShadow = true;
    obj.receiveShadow = true;
    obj.frustumCulled = false; // skinned mesh bounds lag the animated pose
    if (overrideTex && obj.name === 'hunting_mesh') {
      obj.material = obj.material.clone();
      obj.material.map = overrideTex;
    }
  });

  const mixer = new THREE.AnimationMixer(root);
  const clips = gltf.animations || [];
  const find = (name) => THREE.AnimationClip.findByName(clips, name);

  // 'Sit' is a looping seated idle retargeted from the Quaternius UAL (CC0).
  // The player's own body uses 'SitCards' (left hand raised holding cards).
  // Random start offsets keep the players from breathing in unison.
  const sitClip = (spec.cards && find('SitCards')) || find('Sit');
  let sitAction = null;
  if (sitClip) {
    sitAction = mixer.clipAction(sitClip);
    sitAction.play();
    sitAction.time = Math.random() * sitClip.duration;
  }

  const character = { root, mixer, sitAction, find, dead: false, reacting: false };

  // The sit loop already breathes; just layer a slow head drift on top so
  // players occasionally glance around the table. Applied after the mixer
  // writes each frame, so offsets never accumulate.
  const idlePhase = Math.random() * Math.PI * 2;
  const idleBones = [];
  root.traverse((o) => {
    if (o.isBone && o.name === 'head') idleBones.push(o);
  });
  character.idle = (t) => {
    for (const b of idleBones) {
      const drift = Math.sin(t * 0.16 + idlePhase * 2.3) * 0.09;
      _idleQuat.setFromEuler(_idleEuler.set(0, drift, 0));
      b.quaternion.multiply(_idleQuat);
    }
  };

  // Reactions play the opening beat of a fight move, then settle back down.
  // The source takes are long combo loops, so we cut away on a timer rather
  // than waiting for the clip to finish.
  character.playOnce = (name, seconds = 1.5, timeScale = 1) => {
    if (character.dead || character.reacting) return;
    const clip = find(name);
    if (!clip || !sitAction) return;
    character.reacting = true;
    const action = mixer.clipAction(clip);
    action.reset();
    action.timeScale = timeScale;
    action.setLoop(THREE.LoopOnce, 1);
    action.clampWhenFinished = true; // hold the last pose until we fade back
    sitAction.crossFadeTo(action, 0.25, false);
    action.play();
    setTimeout(() => {
      if (character.dead) return;
      sitAction.reset();
      action.crossFadeTo(sitAction, 0.35, false);
      sitAction.play();
      setTimeout(() => { character.reacting = false; }, 450);
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
  scene.background = new THREE.Color(0x0d0906);
  scene.fog = new THREE.Fog(0x0d0906, 6, 12);

  const camera = new THREE.PerspectiveCamera(56, canvas.clientWidth / canvas.clientHeight, 0.05, 30);
  camera.rotation.order = 'YXZ';

  // One-finger (or mouse-drag) look-around, pivoting from the camera home.
  const view = { yaw: 0, pitch: 0, targetYaw: 0, targetPitch: 0, baseYaw: 0, basePitch: 0 };
  state.view = view;
  state.zoom = { active: false, savedYaw: 0, savedPitch: 0, pos: new THREE.Vector3() };
  state.camHome = new THREE.Vector3();
  state.baseFov = camera.fov;
  state.camera = camera;

  // Cinematic over-the-shoulder framing: the camera hangs behind and beside
  // the player's own seated character, looking past him at the table. Narrow
  // portrait screens pull the camera up and back so the table still fits.
  const applyCameraHome = () => {
    const portrait = camera.aspect < 0.8;
    state.camHome.set(
      portrait ? -0.3 : -0.42,
      EYE_HEIGHT + (portrait ? 0.42 : 0.3),
      SEAT_RADIUS + (portrait ? 0.94 : 0.76));
    if (state.zoom.active) return; // don't yank a zoomed-in view around
    camera.position.copy(state.camHome);
    camera.lookAt(portrait ? 0 : 0.05, TABLE_TOP - 0.05, -0.25);
    view.yaw = view.targetYaw = view.baseYaw = camera.rotation.y;
    view.pitch = view.targetPitch = view.basePitch = camera.rotation.x;
  };
  state.applyCameraHome = applyCameraHome;
  applyCameraHome();
  setupLookControls(canvas, view);

  // ---- lighting: warm lantern pool over the table, dim moody saloon around
  // it, so the felt and faces glow while the room falls off into shadow.
  scene.add(new THREE.AmbientLight(0x7a5c3c, 0.5));
  const hemi = new THREE.HemisphereLight(0x5e4b33, 0x120b07, 0.42);
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

  state.dealerArrow = makeDealerArrow();
  scene.add(state.dealerArrow);

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
  // Classic green baize, the traditional card-table felt.
  const felt = new THREE.Mesh(
    new THREE.CylinderGeometry(TABLE_RADIUS, TABLE_RADIUS, 0.05, 48),
    new THREE.MeshStandardMaterial({ map: feltTex, color: 0x2f6e3c, roughness: 0.97 }));
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

  // Position a seated character at seat i: face the table, hips over the
  // chair, feet on the floor, regardless of the pose's baked root offsets.
  const placeSeatedCharacter = (character, i, facing) => {
    character.root.position.copy(seatPos(i, SEAT_RADIUS - 0.34));
    scene.add(character.root);

    // Apply the frozen 'Sit' pose before measuring any bones.
    character.mixer.update(0);
    character.root.updateMatrixWorld(true);

    const bone = (name) => {
      let found = null;
      character.root.traverse((o) => {
        if (!found && o.isBone && o.name === name) found = o;
      });
      return found;
    };

    // Rigs differ in which axis they face, so measure the model's own
    // forward (up x left-to-right-foot) and correct toward the table.
    const lFoot = bone('L_ankle'), rFoot = bone('R_ankle');
    let modelYaw = 0;
    if (lFoot && rFoot) {
      const l = lFoot.getWorldPosition(new THREE.Vector3());
      const r = rFoot.getWorldPosition(new THREE.Vector3());
      const fwd = new THREE.Vector3(0, 1, 0).cross(r.sub(l));
      if (fwd.lengthSq() > 1e-6) modelYaw = Math.atan2(fwd.x, fwd.z);
    }
    character.root.rotation.y = facing - modelYaw;
    character.root.updateMatrixWorld(true);

    const v = new THREE.Vector3();
    const hips = bone('hips');
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
  };

  for (let i = 0; i < SEATS; i++) {
    const seat = { group: new THREE.Group(), cards: [], chips: null, stackChips: null, char: null, label: null };
    state.seats.push(seat);
    scene.add(seat.group);

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

    // Seat 0 is the player's own body, seen from behind by the camera.
    const spec = i === 0 ? PLAYER_MODEL : NPC_MODELS[i - 1];
    charPromises.push(loadCharacterModel(spec.model).then((gltf) => {
      const character = makeCharacter(gltf, spec);
      if (!character) return;
      placeSeatedCharacter(character, i, facing);
      seat.char = character;
    }));

    if (i === 0) continue; // no floating name label over the player himself

    const label = makeLabel();
    const labelPos = seatPos(i, SEAT_RADIUS + 0.05, 1.52);
    label.sprite.position.copy(labelPos);
    scene.add(label.sprite);
    seat.label = label;
  }

  await Promise.all([...props, ...charPromises]);

  // The player's hole cards ride in his character's raised left hand.
  state.playerHand = null;
  if (state.seats[0].char) {
    state.seats[0].char.root.traverse((o) => {
      if (!state.playerHand && o.isBone && o.name === 'L_wrist') state.playerHand = o;
    });
  }

  state.renderer = renderer;
  state.scene = scene;
  state.camera = camera;
  state.clock = new THREE.Clock();

  preloadCardTextures();

  renderer.setAnimationLoop(() => {
    const dt = state.clock.getDelta();
    const t = state.clock.elapsedTime;
    for (const seat of state.seats) if (seat.char) {
      seat.char.mixer.update(dt);
      seat.char.idle(t);
    }

    // Smoothly ease the camera toward where the finger dragged it.
    const ease = Math.min(1, dt * 14);
    view.yaw += (view.targetYaw - view.yaw) * ease;
    view.pitch += (view.targetPitch - view.pitch) * ease;
    camera.rotation.set(view.pitch, view.yaw, 0);

    // Fly the camera between the seat and the top-down card view.
    camera.position.lerp(state.zoom.active ? state.zoom.pos : state.camHome, Math.min(1, dt * 6));

    // The dealer arrow bobs and slowly spins over the dealer's head.
    if (state.dealerArrow && state.dealerArrow.visible) {
      state.dealerArrow.position.y = 1.62 + Math.sin(t * 2.2) * 0.035;
      state.dealerArrow.rotation.y = t * 1.2;
    }

    positionHandCards(camera);
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
    if (state.zoom && state.zoom.active) return; // no look-around while zoomed
    view.targetYaw -= dx * SPEED;   // swipe right = look right
    view.targetPitch = THREE.MathUtils.clamp(
      view.targetPitch - dy * SPEED, PITCH_MIN, PITCH_MAX); // swipe up = look up
  });

  const release = (e) => {
    if (e.pointerId !== activePointer) return;
    activePointer = -1;
    if (moved >= 8) return; // it was a drag, not a tap

    // While zoomed on cards, any tap returns the camera to the seat.
    if (state.zoom.active) {
      state.zoom.active = false;
      view.targetYaw = state.zoom.savedYaw;
      view.targetPitch = state.zoom.savedPitch;
      return;
    }

    // Tap on a table card: fly to a top-down view over that card group.
    const target = pickCardZoomTarget(canvas, e);
    if (target) {
      state.zoom.active = true;
      state.zoom.savedYaw = view.targetYaw;
      state.zoom.savedPitch = view.targetPitch;
      state.zoom.pos.copy(target);
      view.targetYaw = 0;          // cards lie upright toward -z for the player
      view.targetPitch = -Math.PI / 2 + 0.02; // straight down
      return;
    }

    // Double-tap (without dragging) snaps the view back to the table.
    const now = performance.now();
    if (now - lastTapAt < 350) {
      view.targetYaw = view.baseYaw;
      view.targetPitch = view.basePitch;
      lastTapAt = 0;
    } else {
      lastTapAt = now;
    }
  };
  canvas.addEventListener('pointerup', release);
  canvas.addEventListener('pointercancel', release);
}

// Raycast a tap against the community/seat cards. Returns the camera position
// for a top-down view centred over the tapped card group, or null on miss.
const _raycaster = new THREE.Raycaster();
const _ndc = new THREE.Vector2();
function pickCardZoomTarget(canvas, e) {
  if (!state.camera) return null;
  const rect = canvas.getBoundingClientRect();
  _ndc.set(
    ((e.clientX - rect.left) / rect.width) * 2 - 1,
    -((e.clientY - rect.top) / rect.height) * 2 + 1);
  _raycaster.setFromCamera(_ndc, state.camera);
  const targets = [...state.boardCards];
  for (const seat of state.seats) targets.push(...seat.cards);
  const hits = _raycaster.intersectObjects(targets, false);
  if (!hits.length) return null;

  // Hover over the whole group the tapped card belongs to (all five community
  // cards, or an opponent's pair) so every card in it is readable at once.
  const obj = hits[0].object;
  let group = state.boardCards.includes(obj) ? state.boardCards : null;
  if (!group) {
    for (const seat of state.seats) {
      if (seat.cards.includes(obj)) { group = seat.cards; break; }
    }
  }
  if (!group || !group.length) return null;
  const centre = new THREE.Vector3();
  for (const c of group) centre.add(c.position);
  centre.divideScalar(group.length);
  // Height chosen so the group fills the view: wider groups sit higher.
  const height = group === state.boardCards ? 0.8 : 0.52;
  return new THREE.Vector3(centre.x, TABLE_TOP + height, centre.z);
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
  state.baseFov = camera.aspect < 0.8 ? 70 : 56;
  camera.fov = state.baseFov;
  camera.updateProjectionMatrix();
  if (state.applyCameraHome) state.applyCameraHome();
}

// ------------------------------------------------------------------ state updates

function clearGroupChildren(list, parent) {
  for (const m of list) parent.remove(m);
  list.length = 0;
}

function updateBoard(paths) {
  clearGroupChildren(state.boardCards, state.scene);
  const n = paths.length;
  const spacing = CARD_W + 0.014;
  for (let i = 0; i < n; i++) {
    const card = makeCard(paths[i], CARD_W);
    // Flat on the felt, upright when read from the player's seat.
    placeTableCard(card, (i - (n - 1) / 2) * spacing, 0.34, 0, 0);
    state.boardCards.push(card);
    state.scene.add(card);
  }
}

function updateHole(paths) {
  clearGroupChildren(state.holeCards, state.scene);
  for (let i = 0; i < paths.length; i++) {
    const card = makeCard(paths[i], 0.105, true);
    card.visible = false; // shown once positioned in the hand
    state.holeCards.push(card);
    state.scene.add(card);
  }
}

// Fan the hole cards in the player character's raised left hand, tilted back
// toward the camera so they read over his shoulder, like a held poker hand.
const _handPos = new THREE.Vector3();
const _camDir = new THREE.Vector3();
function positionHandCards(camera) {
  if (!state.holeCards.length) return;
  const bone = state.playerHand;
  if (!bone) return;
  bone.getWorldPosition(_handPos);
  // Sit the fan just above the palm, nudged toward the camera so the
  // character's fingers don't poke through the card faces.
  _camDir.copy(camera.position).sub(_handPos).normalize();
  _handPos.addScaledVector(_camDir, 0.06);
  _handPos.y += 0.05;
  for (let i = 0; i < state.holeCards.length; i++) {
    const card = state.holeCards[i];
    const dir = i === 0 ? -1 : 1;
    card.visible = !state.zoom.active;
    card.position.copy(_handPos);
    card.lookAt(camera.position);
    card.rotateZ(dir * 0.16);      // fan the pair like a held hand
    card.translateX(dir * 0.026);
    card.translateY(0.03);
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
    const card = makeCard(path, CARD_W);
    const offset = (c === 0 ? -1 : 1) * (CARD_W / 2 + 0.008);
    const ox = Math.cos(yaw) * offset;
    const oz = -Math.sin(yaw) * offset;
    // All table cards lie flat: revealed ones upright for the player's seat,
    // face-down backs aligned with their owner's seat.
    placeTableCard(card, basePos.x + ox, basePos.z + oz, 0, paths ? 0 : yaw);
    seat.cards.push(card);
    state.scene.add(card);
  }
}

// Each player's remaining stack sits on the rail in front of them, so the
// table reads like a live cash game rather than a bare felt.
function updateStackChips(seat, i, data) {
  if (seat.stackChips) { state.scene.remove(seat.stackChips); seat.stackChips = null; }
  if (data.out || !data.stack || data.stack <= 0) return;
  const pos = seatPos(i, TABLE_RADIUS - 0.16);
  seat.stackChips = makeChipStacks(data.stack, 0.045);
  seat.stackChips.position.set(pos.x, TABLE_TOP, pos.z);
  seat.stackChips.rotation.y = seatAngle(i) + Math.PI / 2; // spread along the rail
  state.scene.add(seat.stackChips);
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
    // The arrow tracks whoever currently has to act (including the player).
    const actor = seats.find(s => s.actor);
    updateDealerArrow(actor ? actor.seat : -1);
    for (const data of seats) {
      const seat = state.seats[data.seat];
      if (!seat) continue;
      const prevData = prev ? (prev.seats || []).find(s => s.seat === data.seat) : null;
      if (!prevData || JSON.stringify(prevData.reveal) !== JSON.stringify(data.reveal)
          || prevData.active !== data.active) {
        updateSeatCards(seat, data.seat, data);
      }
      if (!prevData || prevData.bet !== data.bet) updateSeatChips(seat, data.seat, data.bet);
      if (!prevData || prevData.stack !== data.stack || prevData.out !== data.out) {
        updateStackChips(seat, data.seat, data);
      }
      if (seat.label) drawLabel(seat.label, data);
      if (seat.char && data.out && !seat.char.dead) seat.char.die();
    }

    // Busted players stop joining the table chatter.
    if (window.pokerAudio && window.pokerAudio.setAlive) {
      window.pokerAudio.setAlive(seats.filter(s => s.seat !== 0 && !s.out).map(s => s.seat));
    }
  },

  // A short talking gesture, played when a character's voice line fires.
  talk(seatIndex) {
    if (!state.ready) return;
    const seat = state.seats[seatIndex];
    if (seat && seat.char && !seat.char.dead) seat.char.playOnce('Talk', 2.8);
  },

  // Betting-action gestures: a knuckle tap over the felt for a check, a chip
  // toss for a call/bet/raise, and a fold — opponents flick their cards away,
  // while the player's own character shakes his head with crossed arms.
  action(seatIndex, kind) {
    if (!state.ready) return;
    const seat = state.seats[seatIndex];
    if (!seat || !seat.char || seat.char.dead) return;
    if (kind === 'check') {
      seat.char.playOnce('Check', 1.9);
    } else if (kind === 'bet') {
      // A quick flick of the wrist, tossing chips toward the pot.
      seat.char.playOnce('Bet', 1.0, 0.7);
    } else if (kind === 'fold') {
      if (seatIndex === 0) {
        seat.char.playOnce('FoldShake', 1.9);
        return;
      }
      seat.char.playOnce('Fold', 1.0, 0.55);
      // Sometimes they grumble about it, too.
      if (window.pokerAudio && Math.random() < 0.25) {
        window.pokerAudio.voice(seatIndex, 'lose');
      }
    }
  },

  react(seatIndex, positive) {
    if (!state.ready) return;
    const seat = state.seats[seatIndex];
    // Winners throw a quick victory strike; losers slump into a crouch.
    if (seat && seat.char) seat.char.playOnce(positive ? 'Attack' : 'Crouch', positive ? 1.5 : 1.2);
    // Sometimes they say something about it, too.
    if (window.pokerAudio && Math.random() < (positive ? 0.8 : 0.35)) {
      window.pokerAudio.voice(seatIndex, positive ? 'win' : 'lose');
    }
  }
};
