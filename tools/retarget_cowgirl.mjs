// Re-frame the Cowboy Girl GLB onto Jacob's bone AXES — rotation only.
//
// Sheriff and Bandit share Jacob's exact joint positions, so
// retarget_western_biped.mjs adopts his whole local rest TRS. The girl
// shares all 69 bone NAMES but has female proportions (joints sit up to
// ~15 cm from Jacob's — shorter arms, narrower shoulders, smaller hands),
// so adopting his translations would stretch her onto a male frame.
//
// Instead each shared joint gets Jacob's world rest ROTATION with her own
// native world rest POSITION:
//   - new world W' = [R_jacob | t_girl]; local = inv(parentNew) * W';
//   - inverse bind matrices are patched with C = inv(W') * W_native so the
//     skin is unchanged at rest;
//   - Jacob's body clips are copied with their position tracks dropped for
//     every bone except the pelvis (and root). Rotations now read correctly
//     because local frames match Jacob's, and her own bone offsets keep her
//     female proportions under his joint angles.
//
// poker-scene.js applies the same pelvis-only position filter to the shared
// mighty-cat mood clips when they drive her at runtime.
//
// Her extra spring-bone chains (Skirt_*, Hair_*_x, Chest_*, Bag_*) are not
// in Jacob and are never animated, so the skirt would hang in its standing
// drape while she sits. SKIRT_DRAPE re-aims each skirt segment at a seated
// direction (world space targets, +Z facing, +Y up at rest).
//
// Usage (from repo root; needs @gltf-transform/core + gl-matrix):
//   node tools/retarget_cowgirl.mjs

import { NodeIO } from '@gltf-transform/core';
import { prune } from '@gltf-transform/functions';
import { mat4, vec3, quat } from 'gl-matrix';
import { fileURLToPath } from 'url';
import path from 'path';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const MODELS = path.join(ROOT, 'Web/CinematicPoker.Web/wwwroot/models/characters/western');
const JACOB = path.join(MODELS, 'Jacob.glb');
const GIRL = path.join(MODELS, 'CowboyGirl.glb');

// Keep position tracks only for these nodes when copying Jacob's clips.
const KEEP_POSITION = new Set(['pelvis', 'root']);

// World-space directions each skirt segment should point while seated
// (character faces +Z, +Y up). Chains run hip -> hem, 4 segments each.
// Front chains lie along the seated thighs then drop at the knee; side
// chains flare over the chair edge; back chains keep their standing drape
// (they hang over the seat behind her).
const SKIRT_DRAPE = {
  // front left / right (x = +/-0.066)
  Skirt_01: [0.05, -0.30, 0.95],
  Skirt_02: [0.05, -0.95, 0.30],
  Skirt_03: [0.02, -1.0, 0.05],
  Skirt_04: [0.0, -1.0, 0.0],
  // sides (x = +/-0.195)
  Skirt_05: [0.55, -0.70, 0.45],
  Skirt_06: [0.25, -0.95, 0.15],
  Skirt_07: [0.1, -1.0, 0.0],
  Skirt_08: [0.0, -1.0, 0.0],
  // back: leave as exported
};

const io = new NodeIO();

function nodeMap(doc) {
  const map = new Map();
  for (const n of doc.getRoot().listNodes()) {
    const name = n.getName();
    if (name && !map.has(name)) map.set(name, n);
  }
  return map;
}

function rotationOf(m) {
  // Normalized rotation part of a TRS matrix (no shear in these rigs).
  const r = mat4.clone(m);
  for (const c of [0, 1, 2]) {
    const len = Math.hypot(r[c * 4], r[c * 4 + 1], r[c * 4 + 2]) || 1;
    for (const i of [0, 1, 2]) r[c * 4 + i] /= len;
  }
  r[12] = 0; r[13] = 0; r[14] = 0;
  return r;
}

async function run() {
  const jacobDoc = await io.read(JACOB);
  const doc = await io.read(GIRL);

  const jacobNodes = nodeMap(jacobDoc);
  const targetNodes = nodeMap(doc);

  const oldWorld = new Map();
  for (const n of doc.getRoot().listNodes()) oldWorld.set(n, mat4.clone(n.getWorldMatrix()));
  const jacobWorld = new Map();
  for (const [name, n] of jacobNodes) jacobWorld.set(name, mat4.clone(n.getWorldMatrix()));

  const rootNode = targetNodes.get('root');
  if (!rootNode) throw new Error(`no 'root' node in ${GIRL}`);

  const reframed = new Map(); // node -> C = inv(W_new) * W_native
  let shared = 0;
  const walk = (node, parentNewWorld) => {
    const name = node.getName();
    const jw = jacobWorld.get(name);
    const old = oldWorld.get(node);
    const local = mat4.create();
    let world;
    if (jw && name !== 'root') {
      // Jacob's axes, her joint position.
      world = rotationOf(jw);
      world[12] = old[12]; world[13] = old[13]; world[14] = old[14];
      mat4.multiply(local, mat4.invert(mat4.create(), parentNewWorld), world);
      const C = mat4.create();
      mat4.multiply(C, mat4.invert(mat4.create(), world), old);
      reframed.set(node, C);
      shared++;
    } else {
      // Spring/cloth bone or the root: keep its native world rest.
      world = mat4.clone(old);
      mat4.multiply(local, mat4.invert(mat4.create(), parentNewWorld), world);
    }
    node.setMatrix(local);
    for (const child of node.listChildren()) walk(child, world);
  };
  walk(rootNode, mat4.create());
  console.log(`re-framed ${shared} shared joints (rotation only)`);

  // Seated skirt drape: re-aim each segment's +Y (the bone axis points from
  // the segment toward the hip in this export, i.e. opposite the hang), so
  // aim -Y at the target direction. Walk top-down with updated parents.
  const drape = (node, parentWorld) => {
    const base = (node.getName() || '').replace(/_[lr]$/, '');
    const target = SKIRT_DRAPE[base];
    const local = mat4.fromRotationTranslationScale(
      mat4.create(), node.getRotation(), node.getTranslation(), node.getScale());
    let world = mat4.multiply(mat4.create(), parentWorld, local);
    if (target) {
      // Current world direction of the bone axis (+Y in node space).
      const dir = vec3.fromValues(world[4], world[5], world[6]);
      vec3.normalize(dir, dir);
      // The exported chains hang downward with +Y pointing down the chain.
      // If this export flips that, the sign check keeps the maths honest.
      const down = vec3.fromValues(0, -1, 0);
      const sign = vec3.dot(dir, down) >= 0 ? 1 : -1;
      const want = vec3.fromValues(target[0] * sign, target[1] * sign, target[2] * sign);
      vec3.normalize(want, want);
      const q = quat.rotationTo(quat.create(), dir, want);
      const rotW = mat4.fromQuat(mat4.create(), q);
      const newRot = mat4.multiply(mat4.create(), rotW, rotationOf(world));
      const newWorld = mat4.clone(newRot);
      newWorld[12] = world[12]; newWorld[13] = world[13]; newWorld[14] = world[14];
      const newLocal = mat4.multiply(
        mat4.create(), mat4.invert(mat4.create(), parentWorld), newWorld);
      node.setMatrix(newLocal);
      world = newWorld;
    }
    for (const child of node.listChildren()) drape(child, world);
  };
  // Re-walk from the scene roots so parents' matrices are current.
  for (const scene of doc.getRoot().listScenes()) {
    for (const n of scene.listChildren()) drape(n, mat4.create());
  }
  console.log('skirt draped for the seated pose');

  // Patch inverse bind matrices for the re-framed joints. Skirt bones are
  // NOT patched: their world pose moved on purpose (the mesh follows).
  for (const skin of doc.getRoot().listSkins()) {
    const acc = skin.getInverseBindMatrices();
    const arr = acc.getArray().slice();
    const joints = skin.listJoints();
    for (let j = 0; j < joints.length; j++) {
      const C = reframed.get(joints[j]);
      if (!C) continue;
      const ib = arr.subarray(j * 16, j * 16 + 16);
      const patched = mat4.create();
      mat4.multiply(patched, C, ib);
      arr.set(patched, j * 16);
    }
    acc.setArray(arr);
  }

  for (const anim of doc.getRoot().listAnimations()) anim.dispose();

  // Copy Jacob's body clips, dropping position tracks for everything but
  // the pelvis/root so her female proportions survive his animations.
  const buffer = doc.getRoot().listBuffers()[0];
  for (const srcAnim of jacobDoc.getRoot().listAnimations()) {
    const anim = doc.createAnimation(srcAnim.getName());
    const accCache = new Map();
    const copyAccessor = (srcAcc) => {
      if (accCache.has(srcAcc)) return accCache.get(srcAcc);
      const acc = doc.createAccessor()
        .setType(srcAcc.getType())
        .setArray(srcAcc.getArray().slice())
        .setBuffer(buffer);
      accCache.set(srcAcc, acc);
      return acc;
    };
    let dropped = 0;
    for (const srcChannel of srcAnim.listChannels()) {
      const srcTarget = srcChannel.getTargetNode();
      const node = srcTarget && targetNodes.get(srcTarget.getName());
      if (!node) continue;
      if (srcChannel.getTargetPath() === 'translation'
          && !KEEP_POSITION.has(srcTarget.getName())) {
        dropped++;
        continue;
      }
      const srcSampler = srcChannel.getSampler();
      const sampler = doc.createAnimationSampler()
        .setInput(copyAccessor(srcSampler.getInput()))
        .setOutput(copyAccessor(srcSampler.getOutput()))
        .setInterpolation(srcSampler.getInterpolation());
      const channel = doc.createAnimationChannel()
        .setTargetNode(node)
        .setTargetPath(srcChannel.getTargetPath())
        .setSampler(sampler);
      anim.addSampler(sampler).addChannel(channel);
    }
    console.log(`  + clip ${srcAnim.getName()} (${anim.listChannels().length} channels, ${dropped} position tracks dropped)`);
  }

  await doc.transform(prune());
  await io.write(GIRL, doc);
  console.log('wrote', GIRL);
}

await run();
