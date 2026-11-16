// Re-frame the Dead Man's Hand duo (Jeremy.glb / JimmyDMH.glb) onto Jacob's
// bone AXES — rotation only, same approach as tools/retarget_cowgirl.mjs.
//
// bake_deadmans_npc.py already renamed their Mixamo bones to Jacob's UE4
// names and aligned their rest SKELETON SHAPE (chain directions) to his, but
// the Blender FBX import leaves every bone on its own local frames, so
// Jacob's rotation curves would shear the rigs. Here each shared joint gets
// Jacob's world rest ROTATION with its own native world rest POSITION:
//   - new world W' = [R_jacob | t_native]; local = inv(parentNew) * W';
//   - inverse bind matrices are patched with C = inv(W') * W_native so the
//     skin is unchanged at rest;
//   - Jacob's body clips are copied with their position tracks dropped for
//     every bone except the pelvis (and root), so each character keeps his
//     own proportions under Jacob's joint angles.
//
// poker-scene.js applies the same pelvis-only position filter to the shared
// mighty-cat mood clips when they drive these rigs at runtime (the same
// stripClipPositions path the Cowboy Girl uses).
//
// Usage (from repo root; needs @gltf-transform/core + gl-matrix):
//   node tools/retarget_deadmans.mjs

import { NodeIO } from '@gltf-transform/core';
import { prune } from '@gltf-transform/functions';
import { mat4 } from 'gl-matrix';
import { fileURLToPath } from 'url';
import path from 'path';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const MODELS = path.join(ROOT, 'Web/CinematicPoker.Web/wwwroot/models/characters/western');
const JACOB = path.join(MODELS, 'Jacob.glb');

// Keep position tracks only for these nodes when copying Jacob's clips.
const KEEP_POSITION = new Set(['pelvis', 'root']);

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

async function retarget(targetPath) {
  const jacobDoc = await io.read(JACOB);
  const doc = await io.read(targetPath);

  const jacobNodes = nodeMap(jacobDoc);
  const targetNodes = nodeMap(doc);

  const oldWorld = new Map();
  for (const n of doc.getRoot().listNodes()) oldWorld.set(n, mat4.clone(n.getWorldMatrix()));
  const jacobWorld = new Map();
  for (const [name, n] of jacobNodes) jacobWorld.set(name, mat4.clone(n.getWorldMatrix()));

  const rootNode = targetNodes.get('root');
  if (!rootNode) throw new Error(`no 'root' node in ${targetPath}`);

  const reframed = new Map(); // node -> C = inv(W_new) * W_native
  let shared = 0;
  const walk = (node, parentNewWorld) => {
    const name = node.getName();
    const jw = jacobWorld.get(name);
    const old = oldWorld.get(node);
    const local = mat4.create();
    let world;
    if (jw && name !== 'root') {
      // Jacob's axes, this character's joint position.
      world = rotationOf(jw);
      world[12] = old[12]; world[13] = old[13]; world[14] = old[14];
      mat4.multiply(local, mat4.invert(mat4.create(), parentNewWorld), world);
      const C = mat4.create();
      mat4.multiply(C, mat4.invert(mat4.create(), world), old);
      reframed.set(node, C);
      shared++;
    } else {
      // Unshared bone (face rig, finger tips, weapon sockets): keep its
      // native world rest under the re-framed parents.
      world = mat4.clone(old);
      mat4.multiply(local, mat4.invert(mat4.create(), parentNewWorld), world);
    }
    node.setMatrix(local);
    for (const child of node.listChildren()) walk(child, world);
  };
  walk(rootNode, mat4.create());
  console.log(`re-framed ${shared} shared joints (rotation only)`);

  // Patch inverse bind matrices for the re-framed joints.
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
  // the pelvis/root so each character's proportions survive his animations.
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
  await io.write(targetPath, doc);
  console.log('wrote', targetPath);
}

for (const name of ['Jeremy', 'JimmyDMH']) {
  console.log(`=== re-framing ${name} onto Jacob's biped ===`);
  await retarget(path.join(MODELS, `${name}.glb`));
}
