// Over-the-shoulder poker scene (Three.js) set on a neon arcade street, with
// five extra switchable backdrops (beach, desert, shed, space station, dance
// club) built procedurally from primitives, each with its own mood lighting.
// Props/textures are CC0; the street backdrop is a web-optimised conversion of
// the repo owner's licensed Unity asset (Leartes "Stylized Cyberpunk Arcade").
// Every seat, including the player, is a random outfit of David Grette's
// Jacob cowboy. Check/bet/fold stay on Jacob's own clips. Active seats idle
// on Cocomotion AS_Idle_Sit_Thinking_01; folded seats hold
// AS_Idle_Sit_ArmsFolded_01. Win / lose stay Mighty Cat takes. All mood
// clips are Humanoid-retargeted onto Jacob
// (see docs/mighty-cat-poker-animation-map.md).
// No third-party game content is copied from other titles.
import * as THREE from 'three';
import { GLTFLoader } from './vendor/GLTFLoader.js';
import { clone as cloneSkinned } from './vendor/SkeletonUtils.js';
import { MeshoptDecoder } from './vendor/meshopt_decoder.module.js';

const SEATS = 6;               // seat 0 = the player (camera)
const TABLE_TOP = 0.78;        // table surface height (m)
const TABLE_RADIUS = 1.12;
const SEAT_RADIUS = 1.74;
const EYE_HEIGHT = 1.70;       // camera shoulder height over the bigger build
// Wrist bone sits a few centimetres above the palm; this keeps the palms
// on the baize instead of burying the mesh in the felt.
const HAND_ON_TABLE = 0.04;
// Plant the hands just inside the rail so they rest on the felt, not in the air.
const HAND_RADIUS = TABLE_RADIUS - 0.08;

// Jacob is exported in metres. This scale matches his seated height (boots to
// the top of the hat) to the build the table and camera were framed for.
const WESTERN_SCALE = 1.098;

// Outfit and head presets from the Cowboy 1 pack (SK_Jacob). Each game deals
// six distinct looks, one per seat, including the player. `shirt` / `shirtOpt`
// are the two shirt meshes (only one is worn). Zero means that piece is off.
const WESTERN_LOOKS = [
  { pants: 1, jacket: 1, shirt: 0, shirtOpt: 1, scarf: 1, hat: 1, hair: 'cut', gun: true, holster: true, belt: true, head: 'clean' },
  { pants: 2, jacket: 2, shirt: 0, shirtOpt: 2, scarf: 2, hat: 2, hair: 'cut', gun: true, holster: true, belt: true, head: 'dirt' },
  { pants: 3, jacket: 3, shirt: 0, shirtOpt: 3, scarf: 3, hat: 0, hair: 'full', gun: true, holster: true, belt: true, head: 'blood2' },
  { pants: 2, jacket: 0, shirt: 1, shirtOpt: 0, scarf: 3, hat: 2, hair: 'cut', gun: false, holster: false, belt: true, head: 'dirtblood' },
  { pants: 1, jacket: 1, shirt: 0, shirtOpt: 2, scarf: 2, hat: 0, hair: 'full', gun: false, holster: false, belt: false, head: 'blood' },
  { pants: 2, jacket: 0, shirt: 3, shirtOpt: 0, scarf: 3, hat: 2, hair: 'cut', gun: true, holster: true, belt: true, head: 'clean' },
  { pants: 3, jacket: 0, shirt: 2, shirtOpt: 0, scarf: 0, hat: 0, hair: 'full', gun: false, holster: false, belt: true, head: 'dirt' }
];

// Per-seat accent colours. Each seat's hat is tinted with its colour and the
// HUD paints that player's name in the same colour, so a name always points
// at a hat around the table. Keep in sync with SeatColors in Pages/Home.razor.
const SEAT_COLORS = [0xe0b84a, 0xd87a6a, 0x7aa5d8, 0x8cc47e, 0xc08ad8, 0x5ec8b8];

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

function shuffle(list) {
  const a = list.slice();
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    const tmp = a[i];
    a[i] = a[j];
    a[j] = tmp;
  }
  return a;
}

// The player's own body, seen from the over-the-shoulder camera. Same cowboy
// pool as the other seats. His hole cards stay on the felt in front of him;
// the sit pose already rests his hands on his lap.

// Opening cinematic: seconds per backdrop sweep (desert, then club). The
// matching audio parts are sequenced by pokerAudio.playIntro on the same
// timings.
const INTRO_PART = 8;
const _introLook = new THREE.Vector3(0, 0.95, 0);

function endIntro() {
  if (state.intro) state.intro.active = false;
  if (state.setEnvironment) state.setEnvironment(0); // settle into ARCADE
}

const state = {
  ready: false,
  renderer: null, scene: null, camera: null, clock: null,
  textures: new Map(),
  cardGeo: null,
  boardCards: [],
  holeCards: [],
  seats: [],            // per seat: { group, label, cards:[], chips, dealerBtn, char }
  potChips: null,
  cardHold: null,       // community card being press-and-held (expands 2x)
  fx: [],               // live celebration particle systems
  envGroups: [],        // switchable backdrop groups, index matches ENVS
  envIndex: 0,
  eyeHeight: EYE_HEIGHT,
  envNames: [],
  setEnvironment: null,
  lastJson: '',
  lastActorSeat: -2
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

// Seat reveal cards are twice real size (63mm -> 128mm wide); the community
// cards in the middle are bigger again (x1.6) so the board reads from the
// seat, and tap-to-zoom still gives a full close-up.
const CARD_W = 0.128;
const BOARD_W = 0.205;

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
  // Modest bump over the old 0.46×0.1725 so stacks stay readable without
  // exploding on seats closest to the camera.
  sprite.scale.set(0.58, 0.2175, 1);
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

// ------------------------------------------------------------------ button chips

// Physical dealer/blind buttons, the standard casino set: a white DEALER
// puck, a blue SMALL BLIND and a yellow BIG BLIND. Each sits flat on the
// felt beside its player's cards, slides across the table when the deal
// passes on, and the blind pucks leave once the flop is out.
const BUTTON_RADIUS = 0.08;
const BUTTON_HEIGHT = 0.02;

function buttonFaceTexture(lines, bg, fg) {
  const c = document.createElement('canvas');
  c.width = c.height = 256;
  const g = c.getContext('2d');
  g.fillStyle = bg;
  g.fillRect(0, 0, 256, 256); // the circular cap shows only the inscribed disc
  g.fillStyle = fg;
  g.textAlign = 'center';
  g.textBaseline = 'middle';
  g.font = 'bold 58px Arial, sans-serif';
  if (lines.length === 1) {
    g.fillText(lines[0], 128, 132);
  } else {
    g.fillText(lines[0], 128, 99);
    g.fillText(lines[1], 128, 163);
  }
  const tex = new THREE.CanvasTexture(c);
  tex.colorSpace = THREE.SRGBColorSpace;
  tex.anisotropy = 8;
  return tex;
}

function makeButtonChip(lines, faceBg, textColor, sideColor) {
  const group = new THREE.Group();
  const body = new THREE.Mesh(
    new THREE.CylinderGeometry(BUTTON_RADIUS, BUTTON_RADIUS, BUTTON_HEIGHT, 32),
    new THREE.MeshStandardMaterial({ color: sideColor, roughness: 0.35 }));
  body.position.y = BUTTON_HEIGHT / 2;
  body.castShadow = true;
  // Unlit face (like the community cards) so the text stays vivid and
  // readable under every backdrop's mood lighting.
  const face = new THREE.Mesh(
    new THREE.CircleGeometry(BUTTON_RADIUS * 0.985, 32),
    new THREE.MeshBasicMaterial({ map: buttonFaceTexture(lines, faceBg, textColor) }));
  face.rotation.x = -Math.PI / 2; // lie flat, text upright from the player's seat
  face.position.y = BUTTON_HEIGHT + 0.0008;
  group.add(body, face);
  group.visible = false;
  return group;
}

function makeButtonChips() {
  return {
    dealer: { mesh: makeButtonChip(['DEALER'], '#f5f2ea', '#17151a', 0xe6e2d8), target: new THREE.Vector3() },
    sb: { mesh: makeButtonChip(['SMALL', 'BLIND'], '#2e3192', '#f2f0ff', 0x272a7d), target: new THREE.Vector3() },
    bb: { mesh: makeButtonChip(['BIG', 'BLIND'], '#f3c73f', '#241a05', 0xd8ac2a), target: new THREE.Vector3() },
  };
}

// Spot beside a seat's cards: same ring the hole cards sit on, pushed along
// the table tangent so the puck never covers cards, bets or the stack.
function buttonChipTarget(seatIndex, tangentOff, out) {
  const a = seatAngle(seatIndex);
  const radial = TABLE_RADIUS - 0.34;
  out.set(
    Math.cos(a) * radial - Math.sin(a) * tangentOff,
    TABLE_TOP,
    Math.sin(a) * radial + Math.cos(a) * tangentOff);
  return out;
}

function setButtonChip(btn, seatIndex, tangentOff) {
  if (seatIndex < 0) { btn.mesh.visible = false; return; }
  buttonChipTarget(seatIndex, tangentOff, btn.target);
  if (!btn.mesh.visible) {
    // (Re)appearing — no cross-table slide, just land at the new seat.
    btn.mesh.position.copy(btn.target);
    btn.mesh.visible = true;
  }
}

// The dealer puck goes one side of the cards, the blinds the other, so a
// heads-up seat holding both DEALER and SMALL BLIND shows both cleanly.
function updateButtonChips(dealerSeat, sbSeat, bbSeat) {
  const b = state.buttons;
  if (!b) return;
  setButtonChip(b.dealer, dealerSeat, 0.24);
  setButtonChip(b.sb, sbSeat, -0.24);
  setButtonChip(b.bb, bbSeat, -0.24);
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

// Shared gold hull material for the current actor's outline. The hull is
// inflated in screen space: the offset grows with the vertex's view depth so
// the outline keeps a constant pixel width whether the actor sits next to
// the camera or across the table (a fixed world-space offset all but
// vanished on the far seats).
const OUTLINE_PX = 2.5; // outline thickness in CSS pixels
const _outlineUniforms = {
  uOutlinePx: { value: OUTLINE_PX },
  uOutlineViewH: { value: 720 }, // canvas CSS height, refreshed on resize
};
let _outlineMat = null;
function outlineMaterial() {
  if (_outlineMat) return _outlineMat;
  _outlineMat = new THREE.MeshBasicMaterial({
    color: 0xffc14d, side: THREE.BackSide, toneMapped: false,
  });
  _outlineMat.onBeforeCompile = (shader) => {
    shader.uniforms.uOutlinePx = _outlineUniforms.uOutlinePx;
    shader.uniforms.uOutlineViewH = _outlineUniforms.uOutlineViewH;
    // Inflate after skinning so the hull follows the posed surface normals
    // (bind-space offset before skinning intersects the body and reads as
    // dark z-fighting patches on the shirt). World units per screen pixel at
    // view depth z is 2z / (P[1][1] * viewportHeight); dividing by the
    // model's uniform scale converts that back to object space, where
    // `transformed` lives.
    shader.vertexShader = 'uniform float uOutlinePx;\n'
      + 'uniform float uOutlineViewH;\n'
      + shader.vertexShader.replace(
        '#include <skinning_vertex>',
        `#include <skinning_vertex>
\t{
\t\tfloat outlineViewZ = max( -( modelViewMatrix * vec4( transformed, 1.0 ) ).z, 0.0 );
\t\tfloat outlineWorldPerPx = 2.0 * outlineViewZ / ( projectionMatrix[1][1] * uOutlineViewH );
\t\tfloat outlineModelScale = length( modelViewMatrix[0].xyz );
\t\ttransformed += normalize( normal ) * ( uOutlinePx * outlineWorldPerPx / outlineModelScale );
\t}`);
  };
  return _outlineMat;
}

// Outline whoever currently has to act (same signal as the turn arrow).
function updateActorOutline(seatIndex) {
  for (let i = 0; i < state.seats.length; i++) {
    const c = state.seats[i].char;
    if (c && c.setOutline && c.outlineOn !== (i === seatIndex)) {
      c.setOutline(i === seatIndex);
    }
  }
}

let ARROW_Y = 1.92; // updated after seats plant so it stays above their heads

function updateDealerArrow(dealerSeat) {
  const arrow = state.dealerArrow;
  if (!arrow) return;
  if (dealerSeat < 0) { arrow.visible = false; return; }
  const pos = seatPos(dealerSeat, SEAT_RADIUS - 0.1);
  // Hang it above the seated character's head (labels float higher still).
  arrow.position.set(pos.x, ARROW_Y, pos.z);
  arrow.visible = true;
}

// ------------------------------------------------------------------ characters

function loadGlb(loader, url) {
  return new Promise((resolve) => loader.load(url, resolve, undefined, (err) => {
    console.warn('poker-scene: failed to load', url, err);
    resolve(null);
  }));
}

// Per-character clone of a mesh material with three shader hooks:
// - shirt: a hue-rotate + desaturate pass on the albedo (Rodrigues rotation
//   about the grey axis), so one shared shirt texture can yield genuinely
//   different colours per seat (a plain colour multiply can only darken).
// - hatTint: a luminance colourize on the albedo (hat meshes only) — the
//   leather texture's shading survives but the hat takes the seat's accent
//   colour, matching the colour the HUD paints that player's name in.
// - uGrey: a fold grey-out on the final colour — while raised, the fragment
//   collapses to a pale luminance, and with the material's opacity lowered the
//   folded player reads as a muted grey ghost until the next hand.
function characterMaterial(material, shirt, meshName, hatTint) {
  const hue = shirt ? (shirt.hue || 0) * Math.PI / 180 : 0;
  const sat = shirt && shirt.sat !== undefined ? shirt.sat : 1;
  const tint = hatTint ? new THREE.Color(hatTint) : null;
  const m = material.clone();
  // Hair cards and lashes are thin shells and need both sides. The head meshes
  // do too: the iris and cornea are separate shells whose normals face inward
  // on this export, and a front-only pass draws the white sclera instead of
  // the eye. Solid cloth stays front-faced.
  const alphaCard = !!(m.transparent || m.alphaTest > 0);
  const headMesh = /^Head__|^Jacob_head/i.test(meshName || '');
  m.side = (alphaCard || headMesh) ? THREE.DoubleSide : THREE.FrontSide;
  m.shadowSide = THREE.FrontSide;
  m.userData.baseOpacity = m.opacity;
  m.userData.keepTransparent = !!(m.transparent || m.alphaTest > 0);
  if (m.emissive) m.emissive.setRGB(0, 0, 0);
  if (m.emissiveIntensity !== undefined) m.emissiveIntensity = 0;
  // Vertex colours are unity white on Jimmy — drop them so lighting uses
  // the albedo map only (avoids rare sRGB vertex-tint shading artifacts).
  m.vertexColors = false;
  m.onBeforeCompile = (shader) => {
    shader.uniforms.uHue = { value: hue };
    shader.uniforms.uSat = { value: sat };
    shader.uniforms.uTint = { value: tint || new THREE.Color(1, 1, 1) };
    shader.uniforms.uTintAmt = { value: tint ? 1.0 : 0 };
    shader.uniforms.uGrey = { value: m.userData.foldGrey || 0 };
    shader.fragmentShader = 'uniform float uHue;\nuniform float uSat;\nuniform vec3 uTint;\nuniform float uTintAmt;\nuniform float uGrey;\n' +
      shader.fragmentShader.replace('#include <map_fragment>', `#include <map_fragment>
      {
        const vec3 kGrey = vec3(0.57735026919);
        float ca = cos(uHue), sa = sin(uHue);
        vec3 c = diffuseColor.rgb;
        c = c * ca + cross(kGrey, c) * sa + kGrey * dot(kGrey, c) * (1.0 - ca);
        c = mix(vec3(dot(c, vec3(0.299, 0.587, 0.114))), c, uSat);
        c = mix(c, uTint * (dot(c, vec3(0.299, 0.587, 0.114)) * 2.2 + 0.1), uTintAmt);
        diffuseColor.rgb = clamp(c, 0.0, 1.0);
      }`).replace('#include <dithering_fragment>', `#include <dithering_fragment>
      gl_FragColor.rgb = mix(gl_FragColor.rgb,
        vec3(dot(gl_FragColor.rgb, vec3(0.299, 0.587, 0.114)) * 0.6 + 0.3), uGrey);`);
    m.userData.foldShader = shader;
  };
  m.customProgramCacheKey = () => `char-${hue.toFixed(3)}-${sat}-${hatTint || 0}`;
  return m;
}

function makeCharacter(gltf, spec, extraClips) {
  if (!gltf) return null;
  // All seats share one model, so clone the skinned rig per seat.
  const root = cloneSkinned(gltf.scene);
  root.scale.setScalar(spec.scale || WESTERN_SCALE);

  // The GLB carries every outfit and head. Keep only this seat's look so the
  // other coats aren't drawn on top of him.
  if (spec.look) {
    const names = westernMeshNames(spec.look);
    // A multi-material piece (the head) loads as a group named Head__clean
    // with a child mesh per material slot, named after the mesh asset.
    // Those children are part of the look; only drop a mesh when neither it
    // nor any ancestor is one of this seat's pieces.
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

  // Every mesh gets a per-seat material clone so this character can grey out
  // independently when he folds.
  // Like the head, a hat can load as a group with child meshes per material
  // slot, so hat-ness is decided by the mesh or any ancestor's name.
  const isHatMesh = (obj) => {
    for (let p = obj; p; p = p.parent)
      if (/^Hat__/.test((p.name || '').replace(/\.\d+$/, ''))) return true;
    return false;
  };
  const bodyMeshes = [];
  const bodyMats = [];
  root.traverse((obj) => {
    if (!obj.isMesh) return;
    obj.castShadow = true;
    obj.receiveShadow = true;
    obj.frustumCulled = false; // skinned mesh bounds lag the animated pose
    const shirt = spec.shirt && obj.name === 'jimmy_body_top' ? spec.shirt : null;
    const hatTint = spec.hatColor && isHatMesh(obj) ? spec.hatColor : null;
    const srcMats = Array.isArray(obj.material) ? obj.material : [obj.material];
    const made = srcMats.map((mat) => characterMaterial(mat, shirt, obj.name, hatTint));
    obj.material = made.length === 1 ? made[0] : made;
    bodyMeshes.push(obj);
    bodyMats.push(...made);
  });

  const mixer = new THREE.AnimationMixer(root);
  const jacobClips = gltf.animations || [];
  const moodClips = extraClips || [];
  const findBody = (name) => THREE.AnimationClip.findByName(jacobClips, name);
  const findMood = (name) => THREE.AnimationClip.findByName(moodClips, name);
  const pick = (names) => names[Math.floor(Math.random() * names.length)];

  // Active seats loop sit-thinking; a fold swaps the home idle to arms-folded
  // for the rest of the hand, then thinking returns on the next deal.
  const THINKING_IDLE = 'IdleSitThinking01';
  const FOLDED_IDLE = 'IdleSitArmsFolded01';
  const WIN_CLIPS = ['Win01', 'Win02', 'Win03'];
  const LOSE_CLIPS = ['Lose01', 'Lose02', 'Lose03', 'Lose04'];

  const idleName = THINKING_IDLE;
  const sitClip = findMood(idleName) || findBody('Sit');
  const isPlayer = !!spec.cards;
  let sitAction = null;
  const makeSitAction = (clip) => {
    const action = mixer.clipAction(clip);
    action.setLoop(THREE.LoopRepeat, Infinity);
    action.enabled = true;
    return action;
  };
  if (sitClip) {
    sitAction = makeSitAction(sitClip);
    sitAction.play();
    sitAction.time = Math.random() * sitClip.duration;
  }

  const character = {
    root, mixer, sitAction, findBody, findMood,
    dead: false, reacting: false, isPlayer, armPose: null, armW: 1,
    outlineOn: false, folded: false, foldW: 0, idleName,
  };

  const adoptSitAction = (next, name) => {
    sitAction = next;
    character.sitAction = next;
    character.idleName = name;
  };

  // Three.js drop to the standing bind if every action is disabled or at
  // weight 0. Home-idle swaps must land on a playing seated clip at full
  // weight — never fade the old sit to 0 and hope a scheduled stop is safe.
  const runSit = (action) => {
    if (!action) return;
    action.enabled = true;
    action.paused = false;
    action.reset();
    action.setEffectiveWeight(1);
    action.setEffectiveTimeScale(1);
    action.play();
  };

  const switchHomeIdle = (name) => {
    const clip = findMood(name) || findBody('Sit');
    if (!clip) return;
    if (sitAction && sitAction.getClip() === clip
        && sitAction.isRunning() && sitAction.getEffectiveWeight() > 0.5) {
      return;
    }
    const next = makeSitAction(clip);
    if (sitAction && sitAction !== next) sitAction.stop();
    adoptSitAction(next, name);
    runSit(next);
    next.time = Math.random() * clip.duration;
  };

  // Inverted-hull outline (the classic stencil-outline look): a back-face
  // copy of every mesh, inflated along the normals, shown only while this
  // character is the one to act. Clones share the source skeleton, so the
  // hull follows the animation for free.
  const outlineParts = [];
  const outlineSources = [];
  root.traverse((o) => { if (o.isMesh) outlineSources.push(o); });
  for (const src of outlineSources) {
    const hull = src.clone();
    hull.material = outlineMaterial();
    hull.castShadow = false;
    hull.receiveShadow = false;
    hull.frustumCulled = false;
    hull.visible = false;
    hull.renderOrder = -1; // draw the hull first, body over it
    src.parent.add(hull);
    outlineParts.push({ hull, src });
  }
  character.setOutline = (on) => {
    character.outlineOn = on;
    for (const p of outlineParts) p.hull.visible = on && p.src.visible;
  };

  // Folded players grey out for the rest of the hand: pale grey (uGrey
  // collapses the fragment to lifted luminance) with a clear alpha fade, with
  // their shadow dropped so the ghost look reads. The flag simply mirrors the
  // engine's per-hand folded state, so everyone returns to full colour when
  // the next hand starts.
  //
  // Plain alpha on a multi-part character shows its own insides (the far
  // side of the head, the eyes from behind) — the x-ray look. Fix: order-
  // independent transparency via depth peeling, keeping only the front peel.
  // While ghosted, depth-only copies of every mesh run in the opaque queue
  // and record the nearest body surface per pixel; the semi-transparent
  // colour pass then depth-tests (LessEqual) against that peel with
  // depthWrite off, so exactly one layer — the outer shell — blends over the
  // scene and interior surfaces are rejected.
  //
  // The prepass must keep depthTest ENABLED: WebGL skips depth *writes*
  // whenever the test is disabled, so a depthTest:false prepass writes
  // nothing at all and the body degrades to unsorted alpha (the x-ray).
  // Normal LessEqual testing also keeps the peel honest against the table —
  // parts genuinely hidden behind the rail stay hidden, same as when the
  // character renders opaque.
  //
  // The prepass stays in the *opaque* queue (transparent: false); skinning is
  // automatic because the clones are SkinnedMesh sharing the source skeleton,
  // so the peel follows the animated pose exactly.
  //
  // FrontSide everywhere to match the colour pass, which forces FrontSide
  // while ghosted — a double-sided peel would write back-face depth the
  // front-only colour pass can never match, punching holes in the body.
  function ghostDepthMaterial(srcMat) {
    const m = new THREE.MeshBasicMaterial({
      colorWrite: false,
      transparent: false,
      depthWrite: true,
      depthTest: true,
      side: THREE.FrontSide,
    });
    // Alpha-carded shells (hair, lashes) must cut their texture silhouette
    // into the peel, or their card quads stamp solid rectangles of depth
    // that erase the face behind them.
    if (srcMat.alphaTest > 0 || srcMat.transparent) {
      m.map = srcMat.map || null;
      m.alphaTest = Math.max(srcMat.alphaTest || 0, 0.35);
    }
    return m;
  }
  const ghostParts = [];
  for (const src of bodyMeshes) {
    const depthMesh = src.clone();
    depthMesh.material = Array.isArray(src.material)
      ? src.material.map(ghostDepthMaterial)
      : ghostDepthMaterial(src.material);
    depthMesh.castShadow = false;
    depthMesh.receiveShadow = false;
    depthMesh.frustumCulled = false;
    depthMesh.visible = false;
    // Opaque pass: after the table (0), before the transparent ghost body (2).
    depthMesh.renderOrder = 1;
    src.parent.add(depthMesh);
    ghostParts.push({ src, depthMesh });
  }

  character.setFolded = (on) => {
    if (character.folded === on) return;
    character.folded = on;
    const name = on ? FOLDED_IDLE : THINKING_IDLE;
    const clip = findMood(name) || findBody('Sit');
    if (!clip || !sitAction || sitAction.getClip() === clip) return;
    // A Fold / FoldShake already in flight should land on the new hold, not
    // fade back to thinking. Retarget the restore clip but leave the gesture
    // playing — stopping the current sit here is what left everyone standing.
    if (character.reacting) {
      const next = makeSitAction(clip);
      next.time = Math.random() * clip.duration;
      adoptSitAction(next, name);
      return;
    }
    switchHomeIdle(name);
  };
  character.updateFold = (dt) => {
    const target = character.folded ? 1 : 0;
    if (character.foldW === target) return;
    character.foldW += (target - character.foldW) * Math.min(1, dt * 3.5);
    if (Math.abs(character.foldW - target) < 0.01) character.foldW = target;
    const w = character.foldW;
    const ghost = w > 0.001;
    for (const m of bodyMats) {
      const wantTransparent = ghost || m.userData.keepTransparent;
      if (m.transparent !== wantTransparent) { m.transparent = wantTransparent; m.needsUpdate = true; }
      m.depthWrite = !ghost; // the prepass owns depth while ghosted
      // Double-sided shells (head/eyes/hair) fight the depth prepass under alpha
      // and punch holes through the body; force a single front pass while ghosted.
      if (m.userData.origSide === undefined) m.userData.origSide = m.side;
      m.side = ghost ? THREE.FrontSide : m.userData.origSide;
      m.forceSinglePass = ghost;
      const base = m.userData.baseOpacity ?? 1;
      m.opacity = base * (1 - w * 0.45);
      m.userData.foldGrey = w;
      if (m.userData.foldShader) m.userData.foldShader.uniforms.uGrey.value = w;
    }
    for (const p of ghostParts) {
      p.depthMesh.visible = ghost;
      p.src.renderOrder = ghost ? 2 : 0;
      p.src.castShadow = w < 0.5;
    }
  };

  // Idle is AS_Idle_Sit_Thinking_01 until a fold; then AS_Idle_Sit_ArmsFolded_01.

  // Unused for Jacob: the sit pose is the hold. Kept so a gesture can still
  // fade a pinned pose back in if one is ever stored on the character.
  character.applyArmPose = (dt) => {
    if (!character.armPose) return;
    const target = (character.dead || character.reacting) ? 0 : 1;
    character.armW += (target - character.armW) * Math.min(1, dt * 4);
    if (character.armW < 0.001) return;
    for (const p of character.armPose) p.bone.quaternion.slerp(p.quat, character.armW);
  };

  // Reactions play the opening beat of a fight move, then settle back down.
  // The source takes are long combo loops, so we cut away on a timer rather
  // than waiting for the clip to finish. Every gesture / death / revive bumps
  // a generation counter so the pending restore timers of an older gesture
  // become no-ops instead of fighting the newer animation state.
  let gestureGen = 0;

  // Clean recovery position: drop everything and rejoin the sit loop
  // (or the frozen first frame of it, for the player's still body).
  const hardSit = () => {
    mixer.stopAllAction();
    runSit(sitAction);
  };

  // Returns true only when the gesture actually started, so callers that must
  // not miss their reaction (e.g. the player's bust slump) can retry later.
  character.playOnce = (name, seconds = 1.5, timeScale = 1, mood = false) => {
    if (character.dead || character.reacting) return false;
    const clip = mood ? findMood(name) : findBody(name);
    if (!clip || !sitAction) return false;
    const gen = ++gestureGen;
    character.reacting = true;
    const action = mixer.clipAction(clip);
    action.reset();
    action.timeScale = timeScale;
    action.setLoop(THREE.LoopOnce, 1);
    action.clampWhenFinished = true; // hold the last pose until we fade back
    sitAction.crossFadeTo(action, 0.25, false);
    action.play();
    const clipSeconds = clip.duration / Math.max(timeScale, 0.01);
    const holdMs = Math.min(seconds * 1000, clipSeconds * 1000);
    setTimeout(() => {
      if (character.dead || gen !== gestureGen) return;
      action.stop();
      runSit(sitAction);
      setTimeout(() => {
        if (character.dead || gen !== gestureGen) return;
        character.reacting = false;
        // Always snap back onto a seated loop. A faded-out sit left at
        // weight 0 drops Jacob into the standing bind (arms out).
        const wantName = character.folded ? FOLDED_IDLE : THINKING_IDLE;
        const want = findMood(wantName) || findBody('Sit');
        if (want && (!sitAction || sitAction.getClip() !== want)) {
          const nextSit = makeSitAction(want);
          nextSit.time = Math.random() * want.duration;
          adoptSitAction(nextSit, wantName);
        }
        hardSit();
        const next = character._queued;
        character._queued = null;
        if (next && !character.dead) {
          character.playOnce(next.name, next.seconds, next.timeScale, next.mood);
        }
      }, 450);
    }, holdMs);
    return true;
  };

  // Safety net after a bust: if the slump got stuck (e.g. a restart landed
  // mid-gesture), cancel it and snap cleanly back onto the sit loop.
  character.revive = () => {
    character.dead = false;
    character.reacting = false;
    character._queued = null;
    gestureGen++; // cancel any pending gesture restores
    hardSit();
  };

  // Seated emotion takes retargeted from the Mighty Cat card-game pack.
  character.variant = 0;
  character.heat = 0.25;
  character.idleName = idleName;
  character._queued = null;
  character.playEmotion = (mood, intensity = 0.5) => {
    if (character.dead) return false;
    if (mood !== 'win' && mood !== 'lose') return false;
    const name = mood === 'win' ? pick(WIN_CLIPS) : pick(LOSE_CLIPS);
    const seconds = 2.6;
    if (character.reacting) {
      character._queued = { name, seconds, timeScale: 1, mood: true };
      return false;
    }
    if (character.playOnce(name, seconds, 1, true)) return true;
    return character.playOnce(mood === 'win' ? 'Attack' : 'Fold', mood === 'win' ? 2.2 : 1.0);
  };
  character.tickFidget = () => {};

  // Professional-poker steady hands: the seated idles keep drumming Jacob's
  // fingers, which reads as nervous fiddling from the over-the-shoulder
  // camera. For the player only, capture the finger pose from the first
  // frame of the thinking idle and re-assert it after every mixer update, so
  // the hands rest dead still while the rest of the body keeps breathing.
  // The pin eases out during gestures/reactions (and the bust slump) so
  // bets, folds and win/lose takes still play with their full animation.
  if (isPlayer && sitAction) {
    const FINGER_BONE = /^(thumb|index|middle|ring|pinky)_\d+_[lr]$/;
    const fingerPose = [];
    const prevTime = sitAction.time;
    sitAction.time = 0;
    mixer.update(0);
    root.traverse((o) => {
      if (o.isBone && FINGER_BONE.test(o.name)) {
        fingerPose.push({ bone: o, quat: o.quaternion.clone() });
      }
    });
    sitAction.time = prevTime;
    mixer.update(0);
    if (fingerPose.length) {
      let pinW = 1;
      let lastT = null;
      character.tickFidget = (t) => {
        const dt = lastT === null ? 0 : Math.max(0, t - lastT);
        lastT = t;
        const target = (character.dead || character.reacting) ? 0 : 1;
        pinW += (target - pinW) * Math.min(1, dt * 6);
        if (pinW < 0.01) return;
        for (const p of fingerPose) p.bone.quaternion.slerp(p.quat, pinW);
      };
    }
  }
  character.ensureSit = () => {
    if (character.dead || character.reacting || !sitAction) return;
    if (!sitAction.isRunning() || sitAction.getEffectiveWeight() < 0.2) hardSit();
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
  scene.background = new THREE.Color(0x07070f);
  scene.fog = new THREE.Fog(0x07070f, 14, 70);

  const camera = new THREE.PerspectiveCamera(52, canvas.clientWidth / canvas.clientHeight, 0.05, 140);

  // Orbit around the table-top centre. Home is the over-the-shoulder seat
  // plus a 10° clockwise yaw (peek at James's hole cards) and 10° more
  // pitch (looking down at the felt). Drag/pinch may only wander ±10°.
  const view = {
    homeYaw: 0, homePitch: 0, dist: 2.8,
    offYaw: 0, offPitch: 0,
    targetOffYaw: 0, targetOffPitch: 0,
    distScale: 1, targetDistScale: 1
  };
  state.view = view;
  state.pivot = new THREE.Vector3(0, TABLE_TOP, 0);
  state.zoom = { active: false, kind: 'board', pos: new THREE.Vector3(), look: new THREE.Vector3() };
  state.intro = { active: false, start: 0, phase: 0 };
  state.camHome = new THREE.Vector3();
  state.camLook = new THREE.Vector3(0, TABLE_TOP, 0);
  state.baseFov = camera.fov;
  state.camera = camera;

  const applyCameraHome = (resetLook = false) => {
    const look = state.pivot;
    state.camLook.copy(look);
    // Seat-side starting point (behind the player, slightly to his right).
    const base = new THREE.Vector3(
      -0.62,
      (state.eyeHeight || EYE_HEIGHT) + 0.08,
      SEAT_RADIUS + 0.50);
    const d = base.sub(look);
    view.dist = d.length();
    const baseYaw = Math.atan2(d.x, d.z);
    const basePitch = Math.asin(THREE.MathUtils.clamp(d.y / view.dist, -1, 1));
    // Clockwise from above is negative yaw in this rig (yaw 0 = +Z / player).
    view.homeYaw = baseYaw + HOME_YAW_OFFSET;
    view.homePitch = basePitch + HOME_PITCH_OFFSET;
    if (resetLook) {
      view.offYaw = view.targetOffYaw = 0;
      view.offPitch = view.targetOffPitch = 0;
      view.distScale = view.targetDistScale = 1;
    }
    orbitFromView(view, state.camHome);
    if (state.zoom.active) return;
    camera.position.copy(state.camHome);
    camera.lookAt(look);
  };
  state.applyCameraHome = applyCameraHome;
  applyCameraHome(true);
  setupLookControls(canvas, view);
  requestLandscapeLock();

  // ---- lighting: cool neon night around the street, with the familiar warm
  // lantern pool kept over the felt so the game still reads like a card den.
  const ambient = new THREE.AmbientLight(0x5a6690, 1.2);
  scene.add(ambient);
  const hemi = new THREE.HemisphereLight(0x46538a, 0x1c1a2e, 0.9);
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
  keyLight.shadow.normalBias = 0.025;
  keyLight.shadow.camera.near = 0.5;
  keyLight.shadow.camera.far = 11;
  scene.add(keyLight, keyLight.target);

  // Soft neon spill from the arcade street: a hint of cyan one side and
  // magenta the other, kept subtle so faces stay skin-toned.
  const fill = new THREE.PointLight(0x9adfe8, 3.5, 9, 2);
  fill.position.set(3.2, 2.0, 2.6);
  scene.add(fill);

  const backFill = new THREE.PointLight(0xe08ad8, 4.5, 11, 2);
  backFill.position.set(-2.4, 2.2, -3.6);
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

  state.buttons = makeButtonChips();
  scene.add(state.buttons.dealer.mesh, state.buttons.sb.mesh, state.buttons.bb.mesh);

  // ---- street backdrop: the repo owner's licensed Leartes "Stylized
  // Cyberpunk Arcade" environment, converted to one merged meshopt GLB.
  // The set is a dressed street, not a plaza: its origin sits inside a light
  // mast and the ground meshes were stripped in the web conversion. So the
  // whole street is scaled/offset to wrap its clearest pocket (between the
  // crate cluster and the storefront row) around the table, with the set's
  // street level (y=-1.85 in the GLB's world frame) raised to y=0 so the
  // table legs and the players' boots sit on the arcade floor.
  const ARCADE_SCALE = 1.0;
  const ARCADE_POCKET = { x: -7.5, y: -1.85, z: -3.0 }; // table spot, GLB world frame
  const loader = new GLTFLoader();
  loader.setMeshoptDecoder(MeshoptDecoder);
  const backdropPromise = loadGlb(loader, 'models/env/backdrop.glb').then((gltf) => {
    if (!gltf) return;
    const env = new THREE.Group();
    const street = gltf.scene;
    street.rotation.y = Math.PI; // arcade frontage wraps around the player's view
    street.scale.setScalar(ARCADE_SCALE);
    street.position.set(
      -ARCADE_POCKET.x * ARCADE_SCALE,
      -ARCADE_POCKET.y * ARCADE_SCALE,
      -ARCADE_POCKET.z * ARCADE_SCALE);
    street.traverse((m) => {
      if (m.isMesh) {
        m.castShadow = false;
        m.receiveShadow = false;
        // Punch up the neon signage so it glows through the night fog.
        if (m.material && m.material.emissiveIntensity) {
          m.material.emissiveIntensity *= 3.0;
        }
      }
    });
    env.add(street);
    // Street lamps: cool pools over the storefront row and the crate cluster
    // so the backdrop reads instead of dissolving into the night. They live in
    // the env group, so they switch off with the arcade backdrop.
    const lampSpecs = [
      [0x8fb8ff, 50, 24, [4.5, 3.6, -7.5]],   // storefront frontage, north-east
      [0x9fc4ff, 45, 22, [-6.0, 3.4, -8.0]],  // crate cluster, north-west
      [0xffb493, 38, 22, [11.0, 3.2, -1.5]],  // warm spill down the east street
      [0x8fa8e8, 32, 24, [-13.0, 3.8, -12.0]], // west cluster rim light
      [0x7d8fc8, 70, 36, [0.0, 9.0, -13.0]]    // broad wash: mid-street ground + facades
    ];
    for (const [color, intensity, dist, pos] of lampSpecs) {
      const lamp = new THREE.PointLight(color, intensity, dist, 1.8);
      lamp.position.set(pos[0], pos[1], pos[2]);
      env.add(lamp);
    }
    // The converted set has no ground surface (only thin neon grid lines), so
    // lay a night asphalt slab at street level for the table to stand on.
    const asphalt = new THREE.Mesh(
      new THREE.CircleGeometry(70, 48),
      new THREE.MeshStandardMaterial({ color: 0x181d30, roughness: 0.96, metalness: 0.05 }));
    asphalt.rotation.x = -Math.PI / 2;
    asphalt.position.y = -0.01; // just below the shadow catcher
    env.add(asphalt);
    scene.add(env);
    state.envGroups[0] = env;
    env.visible = state.envIndex === 0;
  });

  // Faint cool moonlight so the street silhouettes read against the night.
  // (Doubles as the sun/celestial key for the other switchable backdrops.)
  const moon = new THREE.DirectionalLight(0x7285c8, 1.8);
  moon.position.set(-14, 26, 10);
  scene.add(moon);

  // ---- switchable backdrops: the arcade GLB plus five procedural sets that
  // are built lazily on first visit. Each entry retunes the mood lighting
  // (ambient / hemisphere / celestial / neon fills) while the warm lantern
  // pool over the felt stays constant so the game always reads the same.
  // bg doubles as the fog colour so distant geometry melts into the sky.
  const ENVS = [
    { name: 'ARCADE', bg: 0x07070f, fog: [14, 70], amb: [0x5a6690, 1.2], hemi: [0x46538a, 0x1c1a2e, 0.9], dir: [0x7285c8, 1.8, [-14, 26, 10]], fill: [0x9adfe8, 4.5], back: [0xe08ad8, 5.5], build: null },
    { name: 'BEACH', bg: 0x2f9ed3, fog: [30, 130], amb: [0xbfd8e8, 0.65], hemi: [0xcfe8ff, 0x8a7a5a, 0.55], dir: [0xfff2d8, 1.7, [18, 30, 12]], fill: [0xbfe8ff, 1.2], back: [0xffe8c8, 1.2], build: buildBeach },
    { name: 'DESERT', bg: 0xe8b878, fog: [25, 110], amb: [0xd8b890, 0.85], hemi: [0xf0d0a8, 0x9a6a3a, 0.7], dir: [0xffd8a0, 2.4, [-20, 18, 8]], fill: [0xffc890, 1.2], back: [0xff9860, 1.8], build: buildDesert },
    { name: 'SHED', bg: 0x0d0906, fog: [8, 26], amb: [0x584838, 0.35], hemi: [0x4a3828, 0x140c06, 0.35], dir: [0xc8a878, 0.15, [-6, 12, 6]], fill: [0xffb868, 1.2], back: [0x684828, 1.0], build: buildShed },
    { name: 'STATION', bg: 0x02030a, fog: [20, 80], amb: [0x88a0c0, 0.7], hemi: [0xa8c8e8, 0x182028, 0.6], dir: [0xcfe0ff, 1.2, [8, 24, -14]], fill: [0x78c8ff, 2.5], back: [0x4868d8, 2.5], build: buildStation },
    { name: 'CLUB', bg: 0x070310, fog: [10, 40], amb: [0x382848, 0.4], hemi: [0x582878, 0x080410, 0.35], dir: [0x8858c8, 0.25, [-14, 26, 10]], fill: [0x00e8ff, 6], back: [0xff28c8, 7], build: buildClub },
  ];
  state.envNames = ENVS.map(e => e.name);
  state.setEnvironment = function (i) {
    const def = ENVS[i];
    state.envIndex = i;
    if (!state.envGroups[i] && def.build) {
      state.envGroups[i] = def.build();
      scene.add(state.envGroups[i]);
    }
    for (let k = 0; k < ENVS.length; k++) {
      if (state.envGroups[k]) state.envGroups[k].visible = (k === i);
    }
    scene.background.setHex(def.bg);
    scene.fog.color.setHex(def.bg);
    scene.fog.near = def.fog[0];
    scene.fog.far = def.fog[1];
    ambient.color.setHex(def.amb[0]); ambient.intensity = def.amb[1];
    hemi.color.setHex(def.hemi[0]); hemi.groundColor.setHex(def.hemi[1]); hemi.intensity = def.hemi[2];
    moon.color.setHex(def.dir[0]); moon.intensity = def.dir[1];
    moon.position.set(def.dir[2][0], def.dir[2][1], def.dir[2][2]);
    fill.color.setHex(def.fill[0]); fill.intensity = def.fill[1];
    backFill.color.setHex(def.back[0]); backFill.intensity = def.back[1];
  };

  // The backdrop doesn't receive shadows (perf), so a shadow catcher under
  // the table keeps the players grounded on the street.
  const catcher = new THREE.Mesh(
    new THREE.CircleGeometry(3.6, 40),
    new THREE.ShadowMaterial({ opacity: 0.45 }));
  catcher.rotation.x = -Math.PI / 2;
  catcher.position.y = 0.002;
  catcher.receiveShadow = true;
  scene.add(catcher);

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

  // ---- seats (characters + cards + labels). Chairs are left out for now.
  // (The street backdrop supplies its own clutter — crates, bottles, arcade
  // cabinets — so the old saloon barrels/boxes are gone.)
  state.seats = [];
  const charPromises = [];

  // One rig, every outfit. A fresh draw every time the table is built.
  const westernModel = loadGlb(loader, 'models/characters/western/Jacob.glb?v=3');
  const mightyCatModel = loadGlb(loader, 'models/characters/western/mighty-cat.glb?v=7');
  // Every seat wears a hat so the seat's accent colour always has somewhere
  // to live; hatless presets borrow a hat (and the matching short hair so the
  // full hairstyle doesn't clip through the brim).
  const lineup = shuffle(WESTERN_LOOKS).slice(0, SEATS)
    .map((look, i) => look.hat ? look : { ...look, hat: 1 + (i % 2), hair: 'cut' });

  // Position a seated character at seat i: face the table and plant the
  // idle's hands on the felt. The thinking clip holds the palms at lap
  // height, so a foot-plant left everyone hovering above the rail.
  const placeSeatedCharacter = (character, i, facing) => {
    character.root.position.copy(seatPos(i, SEAT_RADIUS - 0.34));
    scene.add(character.root);

    // Apply the current idle pose before measuring any bones.
    character.mixer.update(0);
    character.root.updateMatrixWorld(true);

    const bone = (names) => {
      const want = Array.isArray(names) ? names : [names];
      let found = null;
      character.root.traverse((o) => {
        if (!found && o.isBone && want.includes(o.name)) found = o;
      });
      return found;
    };

    // Rigs differ in which axis they face, so measure the model's own
    // forward (up x left-to-right-foot) and correct toward the table.
    const lFoot = bone(['L_ankle', 'foot_l']), rFoot = bone(['R_ankle', 'foot_r']);
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
    const hips = bone(['hips', 'pelvis']);
    if (hips) {
      hips.getWorldPosition(v);
      const target = seatPos(i, SEAT_RADIUS - 0.02);
      character.root.position.x += target.x - v.x;
      character.root.position.z += target.z - v.z;
      character.root.updateMatrixWorld(true);
    }

    const handL = bone(['hand_l', 'LeftHand']);
    const handR = bone(['hand_r', 'RightHand']);
    const midL = bone(['middle_01_l']);
    const midR = bone(['middle_01_r']);
    const hands = [handL, handR].filter(Boolean);
    const palms = [handL, handR, midL, midR].filter(Boolean);
    let minPalm = Infinity;
    const mid = new THREE.Vector3();
    for (const b of palms) {
      b.getWorldPosition(v);
      minPalm = Math.min(minPalm, v.y);
    }
    for (const b of hands) {
      b.getWorldPosition(v);
      mid.add(v);
    }
    if (hands.length) mid.divideScalar(hands.length);
    if (isFinite(minPalm)) {
      character.root.position.y -= (minPalm - (TABLE_TOP + HAND_ON_TABLE));
    }
    const radial = Math.hypot(mid.x, mid.z);
    if (hands.length && radial > 1e-4) {
      const scale = HAND_RADIUS / radial;
      character.root.position.x += mid.x * (scale - 1);
      character.root.position.z += mid.z * (scale - 1);
    }
    character.root.updateMatrixWorld(true);
    character.modelYaw = modelYaw;
    character.seatFacing = facing;
  };

  for (let i = 0; i < SEATS; i++) {
    const seat = { group: new THREE.Group(), cards: [], chips: null, stackChips: null, char: null, label: null };
    state.seats.push(seat);
    scene.add(seat.group);

    const pos = seatPos(i, SEAT_RADIUS);
    const facing = Math.atan2(-pos.x, -pos.z); // yaw toward table centre

    // Every seat, including the player, is one of this game's looks, with
    // the hat tinted in the seat's accent colour (the HUD paints this
    // player's name in the same colour).
    const spec = {
      look: lineup[i], cards: i === 0, scale: WESTERN_SCALE,
      hatColor: SEAT_COLORS[i % SEAT_COLORS.length]
    };
    charPromises.push(Promise.all([westernModel, mightyCatModel]).then(([gltf, mc]) => {
      const character = makeCharacter(gltf, spec, mc && mc.animations);
      if (!character) return;
      character.variant = i;
      placeSeatedCharacter(character, i, facing);
      seat.char = character;
    }));

    if (i === 0) continue; // no floating name label over the player himself

    // NPC name/stack lives in the screen-space HUD (Home.razor actor-card),
    // not as a world sprite that clips through the over-the-shoulder camera.
  }

  await Promise.all([backdropPromise, ...charPromises]);

  // Labels, turn arrow, and the over-the-shoulder camera were framed for the
  // old foot-plant height. Re-seat them on the planted heads so they don't
  // float above the now-lower sit.
  const headY = [];
  const hv = new THREE.Vector3();
  for (let i = 0; i < state.seats.length; i++) {
    const seat = state.seats[i];
    const ch = seat.char;
    if (!ch) continue;
    let head = null;
    ch.root.traverse((o) => {
      if (!head && o.isBone && (o.name === 'head' || o.name === 'Head')) head = o;
    });
    if (!head) continue;
    head.getWorldPosition(hv);
    headY.push(hv.y);
    if (i === 0) state.eyeHeight = hv.y;
  }
  if (headY.length) {
    ARROW_Y = Math.max(...headY) + 0.28;
    applyCameraHome();
  }

  // Pin both of the player's arms in a fixed hold pose — hands resting on
  // the table edge with the hole cards parked between them.
  setupPlayerArms();

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
      if (seat.char.tickFidget) seat.char.tickFidget(t);
      if (seat.char.ensureSit) seat.char.ensureSit();
      seat.char.applyArmPose(dt);
      seat.char.updateFold(dt);
    }

    // Ease drag/pinch offsets toward their targets, then orbit the table
    // centre (or fly to a tapped card group while zoomed).
    const ease = Math.min(1, dt * 8);
    view.offYaw += (view.targetOffYaw - view.offYaw) * ease;
    view.offPitch += (view.targetOffPitch - view.offPitch) * ease;
    view.distScale += (view.targetDistScale - view.distScale) * ease;
    if (state.intro.active) {
      // Opening cinematic: two slow aerial sweeps (desert, then a hard cut
      // to the club) before the camera settles into the normal seat view.
      // Wall-clock, not frame-time: the audio parts run on wall-clock too,
      // so picture and sound stay locked even when frames stutter.
      const it = (performance.now() - state.intro.start) / 1000;
      if (it >= INTRO_PART * 2) {
        endIntro();
      } else {
        if (it >= INTRO_PART && state.intro.phase === 0) {
          state.intro.phase = 1;
          state.setEnvironment(5); // CLUB
        }
        const phase = state.intro.phase;
        const k = (it - phase * INTRO_PART) / INTRO_PART; // 0..1 in segment
        const s = k * k * (3 - 2 * k);                   // smoothstep drift
        const yaw = phase === 0 ? -2.3 + 1.25 * s : 0.7 + 1.25 * s;
        const dist = 6.2 - 2.0 * s;
        const h = 3.1 - 1.3 * s;
        camera.position.set(Math.sin(yaw) * dist, h, Math.cos(yaw) * dist);
        _lookMat.lookAt(camera.position, _introLook, _upVec);
        camera.quaternion.setFromRotationMatrix(_lookMat);
      }
    } else if (state.zoom.active) {
      _desiredPos.copy(state.zoom.pos);
      _lookTarget.copy(state.zoom.look);
    } else {
      const yaw = view.homeYaw + view.offYaw;
      const pitch = view.homePitch + view.offPitch;
      const dist = view.dist * view.distScale;
      _desiredPos.set(
        state.pivot.x + Math.sin(yaw) * Math.cos(pitch) * dist,
        state.pivot.y + Math.sin(pitch) * dist,
        state.pivot.z + Math.cos(yaw) * Math.cos(pitch) * dist);
      _lookTarget.copy(state.pivot);
    }
    if (!state.intro.active) {
      camera.position.lerp(_desiredPos, Math.min(1, dt * 6));
      _lookMat.lookAt(camera.position, _lookTarget, _upVec);
      _desiredQuat.setFromRotationMatrix(_lookMat);
      camera.quaternion.slerp(_desiredQuat, Math.min(1, dt * 8));
    }

    // The dealer/blind pucks glide across the felt when the deal passes on.
    if (state.buttons) {
      for (const key of ['dealer', 'sb', 'bb']) {
        const btn = state.buttons[key];
        if (btn.mesh.visible) btn.mesh.position.lerp(btn.target, Math.min(1, dt * 5));
      }
    }

    // The turn arrow bobs and slowly spins over the actor's head.
    if (state.dealerArrow && state.dealerArrow.visible) {
      state.dealerArrow.position.y = ARROW_Y + Math.sin(t * 2.2) * 0.035;
      state.dealerArrow.rotation.y = t * 1.2;
    }

    // Press-and-hold magnifier: the held community card eases to 2x and
    // lifts slightly so it reads cleanly over its neighbours.
    for (const card of state.boardCards) {
      const target = card === state.cardHold ? 2 : 1;
      if (Math.abs(card.scale.x - target) > 0.001) {
        const s = card.scale.x + (target - card.scale.x) * Math.min(1, dt * 14);
        card.scale.setScalar(s);
        card.position.y = TABLE_TOP + 0.004 + (s - 1) * 0.02;
      }
    }

    updateFx(dt);
    positionHandCards();
    resizeIfNeeded(canvas, renderer, camera);
    renderer.render(scene, camera);
  });
}

// ------------------------------------------------------------------ look controls (clamped orbit around the table)

const _desiredPos = new THREE.Vector3();
const _lookTarget = new THREE.Vector3();
const _lookMat = new THREE.Matrix4();
const _desiredQuat = new THREE.Quaternion();
const _upVec = new THREE.Vector3(0, 1, 0);

const DEG = Math.PI / 180;
// Home is 10° clockwise (negative yaw: toward the player's right, so his
// hole cards read on the left of his body) and 10° more overhead.
const HOME_YAW_OFFSET = -10 * DEG;
const HOME_PITCH_OFFSET = 10 * DEG;
const LOOK_RANGE = 10 * DEG;
const PINCH_MIN = 0.88;
const PINCH_MAX = 1.12;
const DRAG_SPEED = 0.003;

function orbitFromView(view, out) {
  const yaw = view.homeYaw + (view.offYaw || 0);
  const pitch = view.homePitch + (view.offPitch || 0);
  const dist = view.dist * (view.distScale || 1);
  const pivot = state.pivot;
  out.set(
    pivot.x + Math.sin(yaw) * Math.cos(pitch) * dist,
    pivot.y + Math.sin(pitch) * dist,
    pivot.z + Math.cos(yaw) * Math.cos(pitch) * dist);
  return out;
}

function clampLook(view) {
  view.targetOffYaw = THREE.MathUtils.clamp(view.targetOffYaw, -LOOK_RANGE, LOOK_RANGE);
  view.targetOffPitch = THREE.MathUtils.clamp(view.targetOffPitch, -LOOK_RANGE, LOOK_RANGE);
  view.targetDistScale = THREE.MathUtils.clamp(view.targetDistScale, PINCH_MIN, PINCH_MAX);
}

function requestLandscapeLock() {
  const orient = screen.orientation;
  if (orient && typeof orient.lock === 'function') {
    orient.lock('landscape').catch(() => { /* browsers may require a user gesture */ });
  }
}

window.addEventListener('orientationchange', requestLandscapeLock);
window.addEventListener('click', requestLandscapeLock);
document.addEventListener('touchmove', (e) => {
  if (e.touches.length > 1) e.preventDefault();
}, { passive: false });

// Drag orbits ±10° from home; pinch/wheel dollies a little. Card tap zoom
// and press-and-hold magnify still work. No free mouse-look, no pan.
function setupLookControls(canvas, view) {
  canvas.style.touchAction = 'none';
  const touches = new Map();
  let activePointer = -1;
  let lastX = 0, lastY = 0, moved = 0, lastTapAt = 0;
  let pinchSpan0 = 0, pinchScale0 = 1;
  let holdTimer = 0, holdWasActive = false, downAt = 0;

  const cancelHold = () => {
    if (holdTimer) { clearTimeout(holdTimer); holdTimer = 0; }
    state.cardHold = null;
  };

  const pinchSpan = () => {
    const [a, b] = [...touches.values()];
    return Math.hypot(a.x - b.x, a.y - b.y);
  };

  canvas.addEventListener('wheel', (e) => {
    e.preventDefault();
    if (!state.view || state.zoom.active) return;
    view.targetDistScale *= (1 + Math.sign(e.deltaY) * 0.04);
    clampLook(view);
  }, { passive: false });
  canvas.addEventListener('gesturestart', (e) => e.preventDefault());
  canvas.addEventListener('gesturechange', (e) => e.preventDefault());
  canvas.addEventListener('gestureend', (e) => e.preventDefault());

  canvas.addEventListener('pointerdown', (e) => {
    if (e.pointerType !== 'mouse') {
      touches.set(e.pointerId, { x: e.clientX, y: e.clientY });
      if (touches.size === 2) {
        pinchSpan0 = pinchSpan();
        pinchScale0 = view.targetDistScale;
        moved = 100;
        cancelHold();
      }
    }
    if (activePointer !== -1) return;
    activePointer = e.pointerId;
    lastX = e.clientX; lastY = e.clientY; moved = 0;
    holdWasActive = false;
    downAt = e.timeStamp;
    canvas.setPointerCapture(e.pointerId);
    requestLandscapeLock();

    const held = pickBoardCard(canvas, e);
    if (held) {
      const id = e.pointerId;
      holdTimer = setTimeout(() => {
        holdTimer = 0;
        if (activePointer !== id || touches.size >= 2) return;
        state.cardHold = held;
        holdWasActive = true;
      }, 220);
    }
  });

  canvas.addEventListener('pointermove', (e) => {
    const touch = touches.get(e.pointerId);
    if (touch) { touch.x = e.clientX; touch.y = e.clientY; }
    if (touch && touches.size >= 2) {
      const span = pinchSpan();
      if (span > 24 && pinchSpan0 > 24 && !state.zoom.active) {
        view.targetDistScale = pinchScale0 * pinchSpan0 / span;
        clampLook(view);
      }
      moved = 100;
      return;
    }
    const isDrag = e.pointerId === activePointer;
    if (isDrag) {
      moved += Math.abs(e.clientX - lastX) + Math.abs(e.clientY - lastY);
    }
    if (isDrag && state.cardHold) {
      const r = cardScreenRect(state.cardHold, canvas);
      if (e.clientX < r.minX - 8 || e.clientX > r.maxX + 8
          || e.clientY < r.minY - 8 || e.clientY > r.maxY + 8) {
        state.cardHold = null;
      }
      lastX = e.clientX; lastY = e.clientY;
      return;
    }
    if (isDrag && holdWasActive) {
      lastX = e.clientX; lastY = e.clientY;
      return;
    }
    if (isDrag && holdTimer && moved >= 10) cancelHold();
    if (state.zoom && state.zoom.active) {
      if (isDrag) { lastX = e.clientX; lastY = e.clientY; }
      return;
    }
    if (isDrag && !state.zoom.active) {
      const dx = e.clientX - lastX, dy = e.clientY - lastY;
      view.targetOffYaw -= dx * DRAG_SPEED;
      view.targetOffPitch += dy * DRAG_SPEED;
      clampLook(view);
    }
    if (isDrag) { lastX = e.clientX; lastY = e.clientY; }
  });

  const release = (e) => {
    touches.delete(e.pointerId);
    if (e.pointerId !== activePointer) return;
    activePointer = -1;
    cancelHold();
    if (touches.size) {
      const [id] = touches.keys();
      const p = touches.get(id);
      activePointer = id;
      lastX = p.x; lastY = p.y;
      holdWasActive = false;
      return;
    }
    if (holdWasActive) {
      holdWasActive = false;
      if (e.timeStamp - downAt >= 220) return;
    }
    if (moved >= 8) return;

    if (state.zoom.active) {
      // The bet-sizing close-up is owned by the slider panel (CONFIRM/BACK),
      // so stray taps on the felt don't knock the camera out of it.
      if (state.zoom.kind !== 'bet') state.zoom.active = false;
      return;
    }

    const target = pickCardZoomTarget(canvas, e);
    if (target) {
      state.zoom.active = true;
      state.zoom.kind = target.kind;
      state.zoom.pos.copy(target.pos);
      state.zoom.look.copy(target.look);
      return;
    }

    const now = performance.now();
    if (now - lastTapAt < 350) {
      view.targetOffYaw = 0;
      view.targetOffPitch = 0;
      view.targetDistScale = 1;
      lastTapAt = 0;
    } else {
      lastTapAt = now;
    }
  };
  canvas.addEventListener('pointerup', release);
  canvas.addEventListener('pointercancel', release);
}

// Raycast a tap against the community/seat cards and the player's held pair.
// Returns { pos, look, kind } for the zoom flight, or null on miss.
const _raycaster = new THREE.Raycaster();
const _ndc = new THREE.Vector2();
function pickCardZoomTarget(canvas, e) {
  if (!state.camera) return null;
  const rect = canvas.getBoundingClientRect();
  _ndc.set(
    ((e.clientX - rect.left) / rect.width) * 2 - 1,
    -((e.clientY - rect.top) / rect.height) * 2 + 1);
  _raycaster.setFromCamera(_ndc, state.camera);
  const targets = [...state.boardCards, ...state.holeCards];
  for (const seat of state.seats) targets.push(...seat.cards);
  const hits = _raycaster.intersectObjects(targets, false);
  if (!hits.length) return null;
  const obj = hits[0].object;

  // The player's own pair: fly to just in front of the fan resting on the
  // table (the exact camera spot comes from positionHandCards).
  if (state.holeCards.includes(obj)) {
    return { pos: state.camera.position.clone(), look: obj.position.clone(), kind: 'hand' };
  }

  // Hover over the whole group the tapped card belongs to (all five community
  // cards, or an opponent's pair) so every card in it is readable at once.
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
  // Height chosen so the group fills the view: wider groups sit higher. The
  // camera hangs a touch behind the group so the top-down view stays stable
  // and the cards read upright from the player's side of the table.
  const height = group === state.boardCards ? 1.28 : 0.52;
  return {
    pos: new THREE.Vector3(centre.x, TABLE_TOP + height, centre.z + height * 0.09),
    look: new THREE.Vector3(centre.x, TABLE_TOP, centre.z),
    kind: group === state.boardCards ? 'board' : 'table'
  };
}

// ------------------------------------------------------------------ celebration fx

const FX_COLORS = [0xffd27a, 0xff6a5e, 0x7ae0ff, 0xfff6d8, 0xc7ff7a, 0xff9df2];

let _fxTex = null;
function fxTexture() {
  if (_fxTex) return _fxTex;
  const c = document.createElement('canvas');
  c.width = c.height = 32;
  const g = c.getContext('2d');
  const grad = g.createRadialGradient(16, 16, 0, 16, 16, 16);
  grad.addColorStop(0, 'rgba(255,255,255,1)');
  grad.addColorStop(0.4, 'rgba(255,255,255,0.55)');
  grad.addColorStop(1, 'rgba(255,255,255,0)');
  g.fillStyle = grad;
  g.fillRect(0, 0, 32, 32);
  _fxTex = new THREE.CanvasTexture(c);
  return _fxTex;
}

function fxMaterial(color, size) {
  return new THREE.PointsMaterial({
    color, size, map: fxTexture(), transparent: true, opacity: 1,
    blending: THREE.AdditiveBlending, depthWrite: false,
  });
}

// One firework explosion: a shell of sparks flying out from a point,
// pulled down by gravity and fading over its life.
function spawnBurst(pos, color, count = 130, speed = 2.4, life = 1.6, size = 0.05) {
  const positions = new Float32Array(count * 3);
  const vels = [];
  for (let i = 0; i < count; i++) {
    positions.set([pos.x, pos.y, pos.z], i * 3);
    vels.push(new THREE.Vector3().randomDirection()
      .multiplyScalar(speed * (0.35 + Math.random() * 0.65)));
  }
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  const pts = new THREE.Points(geo, fxMaterial(color, size));
  pts.frustumCulled = false;
  state.scene.add(pts);
  state.fx.push({ kind: 'burst', mesh: pts, vels, life, age: 0 });
}

// A sparkler: a fountain that keeps respawning short-lived sparks at its
// base while the emitter is alive.
function spawnSparkler(pos, color, duration = 3.6) {
  const count = 180;
  const positions = new Float32Array(count * 3);
  const parts = [];
  for (let i = 0; i < count; i++) {
    positions.set([pos.x, pos.y, pos.z], i * 3);
    parts.push({
      vel: new THREE.Vector3(),
      life: 0.001,
      age: Math.random() * 0.5, // stagger the first wave
    });
  }
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  const pts = new THREE.Points(geo, fxMaterial(color, 0.034));
  pts.frustumCulled = false;
  state.scene.add(pts);
  state.fx.push({ kind: 'sparkler', mesh: pts, parts, origin: pos.clone(), duration, age: 0 });
}

// The full player-win show, staged at the player's own seat (seat 0, the
// +Z side): staggered fireworks over his head and his edge of the felt,
// plus a pair of sparklers crackling on the rail either side of his cards.
function launchCelebration() {
  if (!state.ready) return;
  const rockets = 7;
  for (let i = 0; i < rockets; i++) {
    setTimeout(() => {
      if (!state.scene) return;
      // Alternate: low over the player's edge of the felt and higher over
      // his head. Both stay on his side of the table and inside the frame
      // of the seated camera, so the win clearly erupts where he sits.
      const high = i % 2 === 1;
      const pos = new THREE.Vector3(
        (Math.random() - 0.5) * 1.1,
        high ? 1.55 + Math.random() * 0.3 : 1.25 + Math.random() * 0.25,
        high ? 1.05 + Math.random() * 0.35 : 0.65 + Math.random() * 0.35);
      spawnBurst(pos, FX_COLORS[Math.floor(Math.random() * FX_COLORS.length)]);
    }, i * 380 + Math.random() * 120);
  }
  // Sparklers flank the player's hole-card fan on the rail in front of him.
  spawnSparkler(new THREE.Vector3(-0.42, TABLE_TOP + 0.03, 1.0), 0xffe9b0);
  spawnSparkler(new THREE.Vector3(0.42, TABLE_TOP + 0.03, 1.0), 0xffe9b0);
}

// A few chips arcing from a seat's rail into the pot whenever that player
// puts chips in the middle.
const POT_POS = new THREE.Vector3(-0.06, TABLE_TOP, -0.18);

function spawnChipToss(seatIndex, count = 3) {
  const from = seatPos(seatIndex, TABLE_RADIUS - 0.18);
  for (let i = 0; i < count; i++) {
    const chip = new THREE.Mesh(
      new THREE.CylinderGeometry(0.045, 0.045, 0.012, 16),
      new THREE.MeshStandardMaterial({
        color: CHIP_COLORS[Math.floor(Math.random() * CHIP_COLORS.length)],
        roughness: 0.55,
      }));
    chip.castShadow = true;
    const start = new THREE.Vector3(from.x, TABLE_TOP + 0.03, from.z);
    const end = POT_POS.clone().add(new THREE.Vector3(
      (Math.random() - 0.5) * 0.18, 0.02, (Math.random() - 0.5) * 0.18));
    chip.position.copy(start);
    chip.visible = false; // until its stagger delay elapses
    state.scene.add(chip);
    state.fx.push({
      kind: 'chip', mesh: chip, start, end,
      life: 0.55, age: -(i * 0.1 + Math.random() * 0.05),
      arc: 0.2 + Math.random() * 0.1, spin: (Math.random() - 0.5) * 12,
    });
  }
}

const FX_GRAVITY = 2.6;

function updateFx(dt) {
  // Clamp the step: one huge frame (slow device, tab switch) must age the
  // show a little, not fast-forward it past its whole lifetime.
  dt = Math.min(dt, 0.05);
  for (let i = state.fx.length - 1; i >= 0; i--) {
    const fx = state.fx[i];
    fx.age += dt;
    const attr = fx.mesh.geometry.getAttribute('position');
    if (fx.kind === 'burst') {
      for (let p = 0; p < fx.vels.length; p++) {
        const v = fx.vels[p];
        v.y -= FX_GRAVITY * dt;
        attr.setXYZ(p, attr.getX(p) + v.x * dt, attr.getY(p) + v.y * dt, attr.getZ(p) + v.z * dt);
      }
      fx.mesh.material.opacity = Math.max(0, 1 - fx.age / fx.life);
      attr.needsUpdate = true;
      if (fx.age < fx.life) continue;
    } else if (fx.kind === 'chip') {
      // Parabolic arc from the rail into the pot, spinning end over end,
      // then a short rest on the felt before despawning.
      const t = THREE.MathUtils.clamp(fx.age / fx.life, 0, 1);
      fx.mesh.visible = fx.age >= 0;
      fx.mesh.position.lerpVectors(fx.start, fx.end, t);
      fx.mesh.position.y += Math.sin(t * Math.PI) * fx.arc;
      if (t < 1) fx.mesh.rotation.x += fx.spin * dt;
      if (fx.age < fx.life + 0.35) continue;
    } else {
      const emitting = fx.age < fx.duration;
      let alive = false;
      for (let p = 0; p < fx.parts.length; p++) {
        const part = fx.parts[p];
        part.age += dt;
        if (part.age >= part.life) {
          if (!emitting) continue;
          // Respawn at the emitter: a narrow upward cone of sparks.
          part.age = 0;
          part.life = 0.35 + Math.random() * 0.4;
          part.vel.set((Math.random() - 0.5) * 0.7,
            0.9 + Math.random() * 0.9,
            (Math.random() - 0.5) * 0.7);
          attr.setXYZ(p, fx.origin.x, fx.origin.y, fx.origin.z);
          alive = true;
          continue;
        }
        alive = true;
        part.vel.y -= FX_GRAVITY * dt;
        attr.setXYZ(p, attr.getX(p) + part.vel.x * dt,
          attr.getY(p) + part.vel.y * dt, attr.getZ(p) + part.vel.z * dt);
      }
      attr.needsUpdate = true;
      if (alive) continue;
    }
    state.scene.remove(fx.mesh);
    fx.mesh.geometry.dispose();
    fx.mesh.material.dispose();
    state.fx.splice(i, 1);
  }
}

// Raycast a pointer event against the community cards only.
function pickBoardCard(canvas, e) {
  if (!state.camera || !state.boardCards.length) return null;
  const rect = canvas.getBoundingClientRect();
  _ndc.set(
    ((e.clientX - rect.left) / rect.width) * 2 - 1,
    -((e.clientY - rect.top) / rect.height) * 2 + 1);
  _raycaster.setFromCamera(_ndc, state.camera);
  const hits = _raycaster.intersectObjects(state.boardCards, false);
  return hits.length ? hits[0].object : null;
}

// The card's current screen-space bounding rectangle (client px), used to end
// a press-and-hold when the finger drags off the card.
const _cardCorner = new THREE.Vector3();
function cardScreenRect(card, canvas) {
  const rect = canvas.getBoundingClientRect();
  const gw = card.geometry.parameters.width / 2;
  const gh = card.geometry.parameters.height / 2;
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
  for (const [sx, sy] of [[-1, -1], [1, -1], [1, 1], [-1, 1]]) {
    _cardCorner.set(sx * gw, sy * gh, 0);
    card.localToWorld(_cardCorner).project(state.camera);
    const px = rect.left + (_cardCorner.x + 1) / 2 * rect.width;
    const py = rect.top + (-_cardCorner.y + 1) / 2 * rect.height;
    minX = Math.min(minX, px); maxX = Math.max(maxX, px);
    minY = Math.min(minY, py); maxY = Math.max(maxY, py);
  }
  return { minX, minY, maxX, maxY };
}

let lastW = 0, lastH = 0;
function resizeIfNeeded(canvas, renderer, camera) {
  const w = canvas.clientWidth, h = canvas.clientHeight;
  if (w === lastW && h === lastH) return;
  lastW = w; lastH = h;
  if (w === 0 || h === 0) return;
  _outlineUniforms.uOutlineViewH.value = h;
  renderer.setSize(w, h, false);
  camera.aspect = w / h;
  state.baseFov = 52;
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
  state.cardHold = null; // the held mesh is being replaced
  clearGroupChildren(state.boardCards, state.scene);
  const n = paths.length;
  const spacing = BOARD_W + 0.014;
  for (let i = 0; i < n; i++) {
    // Unlit: the board must stay readable under every backdrop's lighting.
    const card = makeCard(paths[i], BOARD_W, true);
    // Flat on the felt, upright when read from the player's seat.
    placeTableCard(card, (i - (n - 1) / 2) * spacing, 0.34, 0, 0);
    state.boardCards.push(card);
    state.scene.add(card);
  }
}

function updateHole(paths) {
  clearGroupChildren(state.holeCards, state.scene);
  for (let i = 0; i < paths.length; i++) {
    const card = makeCard(paths[i], 0.145, true);
    state.holeCards.push(card);
    state.scene.add(card);
  }
  layoutHoleCards();
}

// ---- the player's cards: a fan on the felt, in front of his seat ----

// Spot on the felt in front of the player where his card fan rests — well
// inside the rail so the pair reads clearly past his body from the camera.
const HOLE_ANCHOR = new THREE.Vector3(0, TABLE_TOP + 0.02, TABLE_RADIUS - 0.24);

// Where the player's committed chips land (same ring updateSeatChips uses for
// seat 0), so the sizing preview grows exactly where the real bet will sit.
const BET_SPOT = new THREE.Vector3(0, TABLE_TOP, TABLE_RADIUS - 0.62);

// Leave the seated pose alone. A shoulder/elbow solve written for a different
// rig shears Jacob's face and sleeves, so the hole cards stay at the anchor
// on the felt instead of being pinned between the hands.
function setupPlayerArms() {
  state.holeAnchor = HOLE_ANCHOR.clone();
  layoutHoleCards();
}

// The pair rests tilted back toward the player's eyes so the faces read from
// the seat. A slight fan plus a few millimetres of separation between the
// two planes keeps the overlapping faces from z-fighting.
const _eyePoint = new THREE.Vector3();
function layoutHoleCards() {
  if (!state.holeAnchor) return;
  state.holeFlat = false;
  _eyePoint.set(0, (state.eyeHeight || EYE_HEIGHT) + 0.25, SEAT_RADIUS + 0.9);
  for (let i = 0; i < state.holeCards.length; i++) {
    const card = state.holeCards[i];
    const dir = i === 0 ? -1 : 1;
    card.scale.setScalar(1);
    card.position.copy(state.holeAnchor);
    card.lookAt(_eyePoint);
    card.rotateZ(dir * 0.13);         // small fan, like a pair held together
    card.translateX(dir * 0.045);
    card.translateY(0.055);           // bottom edge rests at the anchor
    card.translateZ(0.006 * (i + 1)); // separated planes: no z-fighting
  }
}

// Handles the hand-zoom camera target, plus how the pair behaves during the
// other zooms: laid flat under the community row while the board close-up is
// active (so you can read your hand against the board), hidden during an
// opponent-pair close-up.
const _handZoomDir = new THREE.Vector3();
const _betFocus = new THREE.Vector3();
function positionHandCards() {
  if (!state.holeCards.length || !state.holeAnchor) return;
  const handZoom = state.zoom.active && state.zoom.kind === 'hand';
  const boardZoom = state.zoom.active && state.zoom.kind === 'board';
  const betZoom = state.zoom.active && state.zoom.kind === 'bet';
  if (handZoom) {
    // Hover above and just in front of the pair (backing straight toward the
    // seat would put the camera inside the player's leaned-forward head).
    _handZoomDir.copy(state.camHome).sub(state.holeAnchor).normalize();
    state.zoom.pos.copy(state.holeAnchor).addScaledVector(_handZoomDir, 0.4)
      .setY(state.holeAnchor.y + 0.18);
    state.zoom.look.copy(state.holeAnchor).setY(state.holeAnchor.y + 0.07);
  }
  if (betZoom) {
    // Sizing a bet: frame the player's pair AND the spot where the preview
    // stack grows, hovering a little higher than the hand close-up so both
    // stay in shot while the slider moves.
    _betFocus.copy(state.holeAnchor).lerp(BET_SPOT, 0.6);
    _handZoomDir.copy(state.camHome).sub(state.holeAnchor).normalize();
    state.zoom.pos.copy(_betFocus).addScaledVector(_handZoomDir, 0.52)
      .setY(state.holeAnchor.y + 0.34);
    state.zoom.look.copy(_betFocus).setY(TABLE_TOP + 0.03);
  }
  if (boardZoom && !state.holeFlat) {
    state.holeFlat = true;
    const spacing = BOARD_W + 0.014;
    for (let i = 0; i < state.holeCards.length; i++) {
      const card = state.holeCards[i];
      card.scale.setScalar(BOARD_W / 0.145); // match the community card size
      placeTableCard(card, (i - (state.holeCards.length - 1) / 2) * spacing, 0.64, 0, 0);
    }
  } else if (!boardZoom && state.holeFlat) {
    layoutHoleCards(); // back into the character's hands (also resets scale)
  }
  for (const card of state.holeCards) {
    card.visible = handZoom || boardZoom || betZoom || !state.zoom.active;
  }
}

function updateSeatCards(seat, i, data) {
  clearGroupChildren(seat.cards, state.scene);
  const paths = data.reveal && data.reveal.length ? data.reveal : null;
  // Keep revealed showdown cards on the felt even after the engine has
  // already marked that seat eliminated / inactive.
  if (i === 0 || (!data.active && !paths)) return;

  if (paths) {
    // Revealed hands sit in from the rail (so they are not buried in the
    // cowboy's torso from the over-the-shoulder camera) and tip toward
    // the lens the same way James's pair does.
    const basePos = seatPos(i, TABLE_RADIUS - 0.62);
    const eye = state.camera
      ? state.camera.position
      : new THREE.Vector3(0, (state.eyeHeight || EYE_HEIGHT) + 0.25, SEAT_RADIUS + 0.9);
    for (let c = 0; c < Math.min(2, paths.length); c++) {
      const card = makeCard(paths[c], BOARD_W * 0.82, true);
      const dir = c === 0 ? -1 : 1;
      card.position.set(basePos.x, TABLE_TOP + 0.03, basePos.z);
      card.lookAt(eye.x, eye.y, eye.z);
      card.rotateZ(dir * 0.1);
      card.translateX(dir * 0.075);
      card.translateZ(0.006 * (c + 1));
      seat.cards.push(card);
      state.scene.add(card);
    }
    return;
  }

  const basePos = seatPos(i, TABLE_RADIUS - 0.33);
  const yaw = seatAngle(i) - Math.PI / 2; // cards face along seat direction

  for (let c = 0; c < 2; c++) {
    const card = makeCard('img/cards/back.png', CARD_W);
    const offset = (c === 0 ? -1 : 1) * (CARD_W / 2 + 0.008);
    const ox = Math.cos(yaw) * offset;
    const oz = -Math.sin(yaw) * offset;
    placeTableCard(card, basePos.x + ox, basePos.z + oz, 0, yaw);
    seat.cards.push(card);
    state.scene.add(card);
  }
}

function seatCardZoom(seatIndex) {
  const seat = state.seats[seatIndex];
  if (!seat || !seat.cards.length) return null;
  const centre = new THREE.Vector3();
  for (const c of seat.cards) centre.add(c.position);
  centre.divideScalar(seat.cards.length);
  const height = 0.58;
  return {
    pos: new THREE.Vector3(centre.x, TABLE_TOP + height, centre.z + height * 0.12),
    look: new THREE.Vector3(centre.x, TABLE_TOP + 0.02, centre.z),
    kind: 'table'
  };
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

// -------------------------------------------------------- procedural backdrops
// Five simple low-poly sets built from primitives (nothing loaded, nothing
// copied) that swap in behind the constant table + lantern pool. Each is
// only constructed the first time the player cycles to it.

function envMat(color, opts) {
  return new THREE.MeshStandardMaterial(Object.assign({ color, roughness: 1 }, opts));
}

function envGround(color, r) {
  const m = new THREE.Mesh(new THREE.CircleGeometry(r || 60, 48), envMat(color));
  m.rotation.x = -Math.PI / 2;
  m.position.y = -0.01;
  m.receiveShadow = true;
  return m;
}

// Unlit marker (sun, glow strips): ignores fog so it stays vivid at distance.
function envGlow(geo, color) {
  return new THREE.Mesh(geo, new THREE.MeshBasicMaterial({ color, fog: false }));
}

function buildBeach() {
  const g = new THREE.Group();
  // A small sand island under the table, surrounded by water out to the
  // panorama wall. Sand kept below full white under the noon sun.
  g.add(envGround(0xd9c8a4, 8.2));
  const sea = new THREE.Mesh(
    new THREE.RingGeometry(8, 41, 48),
    envMat(0x3fa3c8, { roughness: 0.35, metalness: 0.1 }));
  sea.rotation.x = -Math.PI / 2;
  sea.position.y = -0.06;
  g.add(sea);
  // The repo owner's beach picture wrapped around the inside of a tall
  // cylinder as the sky/horizon; mirrored repeat hides the wrap seams.
  const panoTex = texture('img/env/beach-pano.jpg');
  panoTex.wrapS = THREE.MirroredRepeatWrapping;
  panoTex.repeat.set(6, 1);
  const pano = new THREE.Mesh(
    new THREE.CylinderGeometry(40, 40, 30, 48, 1, true),
    new THREE.MeshBasicMaterial({ map: panoTex, side: THREE.BackSide, fog: false }));
  // Centre the photo's sea horizon at camera eye height so it reads level;
  // the below-ground half is hidden by the sand/sea planes.
  pano.position.y = 2.0;
  g.add(pano);
  return g;
}

function buildDesert() {
  const g = new THREE.Group();
  g.add(envGround(0xd9a45b));
  // half-buried dune mounds on the horizon
  for (let d = 0; d < 7; d++) {
    const a = d * (Math.PI * 2 / 7) + 1.1;
    const dune = new THREE.Mesh(new THREE.SphereGeometry(1, 12, 8), envMat(d % 2 ? 0xd9a45b : 0xcf9950));
    dune.scale.set(9 + d, 1.4 + (d % 3) * 0.5, 5.5);
    dune.position.set(Math.cos(a) * (20 + (d % 3) * 6), -0.35, Math.sin(a) * (20 + (d % 3) * 6));
    dune.rotation.y = a;
    g.add(dune);
  }
  const cactusMat = envMat(0x4a7a3a);
  for (let c = 0; c < 4; c++) {
    const a = c * (Math.PI * 2 / 4) + 0.7;
    const r = 6.5 + (c % 2) * 2.5;
    const cactus = new THREE.Group();
    const body = new THREE.Mesh(new THREE.CylinderGeometry(0.16, 0.2, 1.7, 8), cactusMat);
    body.position.y = 0.85;
    cactus.add(body);
    const arm = new THREE.Mesh(new THREE.CylinderGeometry(0.09, 0.11, 0.7, 8), cactusMat);
    arm.position.set(0.3, 1.1, 0);
    arm.rotation.z = -0.5;
    cactus.add(arm);
    cactus.position.set(Math.cos(a) * r, 0, Math.sin(a) * r);
    g.add(cactus);
  }
  for (let k = 0; k < 5; k++) {
    const a = k * (Math.PI * 2 / 5) + 2.0;
    const rock = new THREE.Mesh(new THREE.DodecahedronGeometry(0.4 + (k % 3) * 0.3), envMat(0x8a705a, { flatShading: true }));
    rock.position.set(Math.cos(a) * (9 + k), 0.2, Math.sin(a) * (9 + k));
    g.add(rock);
  }
  // low evening sun sitting on the horizon
  g.add((() => { const s = envGlow(new THREE.SphereGeometry(2.6, 16, 12), 0xffd890); s.position.set(-42, 10, 14); return s; })());
  return g;
}

function buildShed() {
  const g = new THREE.Group();
  // one boxy timber room; BackSide so we see it from within
  const room = new THREE.Mesh(
    new THREE.BoxGeometry(13, 4.2, 13),
    envMat(0x54402a, { side: THREE.BackSide }));
  room.position.y = 2.08;
  g.add(room);
  const floor = new THREE.Mesh(new THREE.CircleGeometry(9.5, 4), envMat(0x3d2c1a));
  floor.rotation.x = -Math.PI / 2;
  floor.rotation.z = Math.PI / 4;
  floor.position.y = -0.005;
  floor.receiveShadow = true;
  g.add(floor);
  const crateMat = envMat(0x6a4a2a);
  const crates = [[4.6, 0.45, 3.4, 0.9], [5.0, 0.35, -2.8, 0.7], [-4.4, 0.4, -3.9, 0.8], [-5.1, 0.3, 2.6, 0.6]];
  for (const [x, y, z, s] of crates) {
    const crate = new THREE.Mesh(new THREE.BoxGeometry(s, s, s), crateMat);
    crate.position.set(x, y, z);
    crate.rotation.y = x + z;
    g.add(crate);
  }
  // bare hanging bulb by the lantern light source
  const bulb = envGlow(new THREE.SphereGeometry(0.06, 10, 8), 0xffd9a0);
  bulb.position.set(0, 2.35, 0);
  g.add(bulb);
  const cord = new THREE.Mesh(new THREE.CylinderGeometry(0.008, 0.008, 1.8, 4), envMat(0x181008));
  cord.position.set(0, 3.28, 0);
  g.add(cord);
  return g;
}

function buildStation() {
  const g = new THREE.Group();
  const floor = new THREE.Mesh(
    new THREE.CircleGeometry(8, 32),
    envMat(0x303844, { metalness: 0.7, roughness: 0.35 }));
  floor.rotation.x = -Math.PI / 2;
  floor.position.y = -0.005;
  floor.receiveShadow = true;
  g.add(floor);
  const wall = new THREE.Mesh(
    new THREE.CylinderGeometry(8, 8, 4.4, 32, 1, true),
    envMat(0x4a5666, { metalness: 0.25, roughness: 0.55, side: THREE.BackSide }));
  wall.position.y = 2.2;
  g.add(wall);
  // glowing observation window strip looking out into space, kept low enough
  // to read behind the far players from the home camera's downward pitch
  const band = new THREE.Mesh(
    new THREE.CylinderGeometry(7.92, 7.92, 0.9, 32, 1, true),
    new THREE.MeshBasicMaterial({ color: 0x9fd8ff, fog: false, side: THREE.BackSide }));
  band.position.y = 1.5;
  g.add(band);
  // open ceiling: a dome of stars overhead
  const starGeo = new THREE.BufferGeometry();
  const pts = [];
  for (let s = 0; s < 700; s++) {
    const r = 30 + Math.random() * 50;
    const th = Math.random() * Math.PI * 2;
    const ph = Math.random() * Math.PI * 0.45; // upper dome only
    pts.push(r * Math.sin(ph) * Math.cos(th), r * Math.cos(ph) + 3, r * Math.sin(ph) * Math.sin(th));
  }
  starGeo.setAttribute('position', new THREE.Float32BufferAttribute(pts, 3));
  g.add(new THREE.Points(starGeo, new THREE.PointsMaterial({ color: 0xffffff, size: 0.3, fog: false })));
  return g;
}

function buildClub() {
  const g = new THREE.Group();
  const floor = new THREE.Mesh(
    new THREE.CircleGeometry(7, 32),
    envMat(0x16121e, { metalness: 0.55, roughness: 0.25 }));
  floor.rotation.x = -Math.PI / 2;
  floor.position.y = -0.005;
  floor.receiveShadow = true;
  g.add(floor);
  const wall = new THREE.Mesh(
    new THREE.CylinderGeometry(7, 7, 4.4, 24, 1, true),
    envMat(0x241a30, { side: THREE.BackSide }));
  wall.position.y = 2.2;
  g.add(wall);
  const ceiling = new THREE.Mesh(new THREE.CircleGeometry(7, 32), envMat(0x0e0a14, { side: THREE.DoubleSide }));
  ceiling.rotation.x = Math.PI / 2;
  ceiling.position.y = 4.4;
  g.add(ceiling);
  // neon glow rings around the walls
  const rings = [[0xff30c0, 1.1], [0x30e0ff, 2.3], [0x8040ff, 3.3]];
  for (const [color, y] of rings) {
    const ring = new THREE.Mesh(
      new THREE.CylinderGeometry(6.94, 6.94, 0.1, 24, 1, true),
      new THREE.MeshBasicMaterial({ color, fog: false, side: THREE.BackSide }));
    ring.position.y = y;
    g.add(ring);
  }
  const ball = new THREE.Mesh(
    new THREE.SphereGeometry(0.45, 20, 14),
    envMat(0xc8c8d8, { metalness: 1, roughness: 0.08, flatShading: true }));
  ball.position.y = 3.5;
  g.add(ball);
  const rod = new THREE.Mesh(new THREE.CylinderGeometry(0.015, 0.015, 0.45, 6), envMat(0x181018));
  rod.position.y = 4.18;
  g.add(rod);
  return g;
}

// ------------------------------------------------------------------ public API

window.pokerScene = {
  get loaded() { return state.ready; },

  idleDebug() {
    return (state.seats || []).map((seat, i) => {
      const c = seat && seat.char;
      if (!c) return { i };
      const sit = c.sitAction;
      const foldedClip = c.findMood && c.findMood('IdleSitArmsFolded01');
      return {
        i,
        folded: !!c.folded,
        idleName: c.idleName,
        sitClip: sit && sit.getClip() && sit.getClip().name,
        sitW: sit ? Number(sit.getEffectiveWeight().toFixed(2)) : 0,
        hasFoldedClip: !!foldedClip,
        foldedDur: foldedClip ? Number(foldedClip.duration.toFixed(2)) : 0,
        reacting: !!c.reacting,
      };
    });
  },

  // Opening cinematic disabled: stay on the normal seat view. Returns 0 so
  // callers know not to wait or play intro audio.
  playIntro() {
    endIntro();
    return 0;
  },

  skipIntro() {
    endIntro();
  },

  // Snap back to the 10°/10° table-centre home (cancels card close-up too).
  resetCamera() {
    if (!state.ready) return;
    this.endBetPreview();
    state.zoom.active = false;
    if (state.applyCameraHome) state.applyCameraHome(true);
  },

  // Fly the seated camera onto an opponent's revealed hole cards so a
  // win (including a fold-win) is readable without hunting the rail.
  focusReveal(seatIndex) {
    if (!state.ready) return;
    const target = seatCardZoom(seatIndex);
    if (!target) return;
    this.endBetPreview();
    state.zoom.active = true;
    state.zoom.kind = target.kind;
    state.zoom.pos.copy(target.pos);
    state.zoom.look.copy(target.look);
  },

  lockLandscape() {
    requestLandscapeLock();
  },

  // Live bet/raise sizing: zoom in over the player's cards and keep a chip
  // stack at his bet spot in sync with the slider. Called once when the
  // slider opens and again on every slider change; the stack uses the same
  // chip scale as real committed bets, so what you see is what lands.
  betPreview(amount) {
    if (!state.ready) return;
    const amt = Math.max(0, Math.floor(amount || 0));
    state.zoom.active = true;
    state.zoom.kind = 'bet';
    // Hide the already-committed chips while sizing: the preview stack shows
    // the full "raise to" total, so keeping both would double-count.
    const seat0 = state.seats && state.seats[0];
    if (seat0 && seat0.chips) seat0.chips.visible = false;
    if (!state.betPreview) state.betPreview = { group: null, amount: -1 };
    if (state.betPreview.amount === amt) return;
    state.betPreview.amount = amt;
    if (state.betPreview.group) state.scene.remove(state.betPreview.group);
    const stack = makeChipStacks(amt);
    stack.position.copy(BET_SPOT);
    state.betPreview.group = stack;
    state.scene.add(stack);
  },

  // Dismiss the sizing preview: remove the stack, restore any committed
  // chips, and let the camera ease back to the seat view.
  endBetPreview() {
    if (!state.ready) return;
    if (state.betPreview && state.betPreview.group) {
      state.scene.remove(state.betPreview.group);
    }
    state.betPreview = null;
    const seat0 = state.seats && state.seats[0];
    if (seat0 && seat0.chips) seat0.chips.visible = true;
    if (state.zoom.kind === 'bet') state.zoom.active = false;
  },

  // Cycle to the next backdrop set; returns the new backdrop name for the
  // topbar button label.
  cycleBackdrop() {
    if (!state.ready || !state.setEnvironment) return 'ARCADE';
    const next = (state.envIndex + 1) % state.envNames.length;
    state.setEnvironment(next);
    return state.envNames[next];
  },

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
    const actorSeat = actor ? actor.seat : -1;
    if (actorSeat !== state.lastActorSeat) {
      state.lastActorSeat = actorSeat;
      for (const data of seats) {
        if (data.seat === actorSeat || data.folded || !data.active) continue;
        const s = state.seats[data.seat];
        if (s && s.char && !s.char.dead && s.char.playEmotion) {
          s.char.playEmotion('wait', typeof data.heat === 'number' ? data.heat : 0.35);
        }
      }
    }
    updateDealerArrow(actorSeat);
    updateActorOutline(actorSeat);

    // Dealer/blind pucks: the DEALER button rides with the deal all hand;
    // the blind pucks only mark who posted until the flop hits the felt.
    const preflop = !(snapshot.board && snapshot.board.length);
    const findSeat = (flag) => {
      const s = seats.find(d => d[flag] && !d.out);
      return s ? s.seat : -1;
    };
    updateButtonChips(
      findSeat('dealer'),
      preflop ? findSeat('sb') : -1,
      preflop ? findSeat('bb') : -1);
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
      // Folded players (the human included) sit out the rest of the hand as
      // grey ghosts with arms folded; the flag clears when the next hand
      // deals, so colour and the thinking idle come back on their own.
      if (seat.char) seat.char.setFolded(!!data.folded && !data.out);
      if (seat.char && typeof data.heat === 'number') seat.char.heat = data.heat;
      if (seat.char && data.out) {
        // A bust plays a lose reaction once, then settles back to sitting.
        // playOnce is skipped while another gesture is mid-flight, so only
        // latch once the reaction really started and retry until then.
        const slumped = seat.char.playEmotion('lose', 0.7) || seat.char.playOnce('Crouch', 1.6);
        if (!seat.char.bustReacted && slumped) seat.char.bustReacted = true;
      }
      if (seat.char && !data.out && seat.char.bustReacted) {
        // Fresh session after a bust: make sure they're sitting upright.
        seat.char.bustReacted = false;
        seat.char.revive();
      }
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

  // Mighty Cat seated poker takes (Player_01 bet, Player_02 check, Player_03 fold).
  action(seatIndex, kind) {
    if (!state.ready) return;
    const seat = state.seats[seatIndex];
    if (!seat || !seat.char || seat.char.dead) return;
    // Snapshot can mark folded before this gesture fires; still play Fold /
    // FoldShake so the arms-folded hold is the settle, not the only beat.
    if (seat.char.folded && kind !== 'fold') return;
    const play = (name, seconds, timeScale = 1) => seat.char.playOnce(name, seconds, timeScale);
    if (kind === 'check') {
      play('Check', 1.7);
    } else     if (kind === 'call') {
      play('Bet', 1.0, 0.75);
    } else if (kind === 'raise') {
      play('Bet', 1.0, 0.85);
      spawnChipToss(seatIndex);
    } else if (kind === 'allin') {
      play('Bet', 1.0, 1.0);
      spawnChipToss(seatIndex);
    } else if (kind === 'bet') {
      play('Bet', 1.0, 0.7);
      spawnChipToss(seatIndex);
    } else if (kind === 'fold') {
      if (seatIndex === 0) {
        play('FoldShake', 1.9);
      } else {
        play('Fold', 1.0, 0.55);
      }
      if (window.pokerAudio) window.pokerAudio.voice(seatIndex, 'fold');
    }
  },

  // Fireworks and sparklers when the player takes a pot. Stay on the
  // seated table camera so opponent hole cards remain readable.
  celebrate() {
    if (!state.ready) return;
    launchCelebration();
    const char = state.seats[0] && state.seats[0].char;
    if (char) {
      // The gesture may be blocked by one still playing (e.g. the chip
      // toss that won the pot) — retry briefly until it starts.
      let tries = 0;
      const strike = () => {
        const started = char.playEmotion('win', 0.9) || char.playOnce('Attack', 2.2);
        if (!started && ++tries < 6) setTimeout(strike, 350);
      };
      strike();
    }
  },

  // mood is win | lose | wait | neutral. intensity 0..1 picks the size of
  // the take (a small pot shrugs, a stack-changing one throws the arms).
  // speak=false plays the body take without a table voice line, so a pot
  // full of reactions does not chorus the same sayings.
  emotion(seatIndex, mood, intensity = 0.5, speak = true) {
    if (!state.ready) return;
    const seat = state.seats[seatIndex];
    if (seat && seat.char && !seat.char.dead && seat.char.playEmotion) {
      seat.char.playEmotion(mood, intensity);
    }
    if (speak && window.pokerAudio && (mood === 'win' || mood === 'lose')) {
      window.pokerAudio.voice(seatIndex, mood);
    }
  },

  react(seatIndex, positive) {
    window.pokerScene.emotion(seatIndex, positive ? 'win' : 'lose', positive ? 0.7 : 0.6);
  }
};
