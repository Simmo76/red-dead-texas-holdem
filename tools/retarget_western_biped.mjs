// Re-frame the Sheriff / Bandit GLB skeletons onto Jacob's bone axes.
//
// All three characters share the David Grette UE4 biped: every joint rests
// at the *same world position*, but the two bakes assigned different bone
// frames (Blender import orientation), so Jacob's animation curves sheared
// these rigs and the hand-authored Sit in bake_western_npc.py left them
// frozen scarecrows with their arms out.
//
// Instead of rebinding meshes (which exploded neck/hair verts) this tool
// rewrites only the *frames*:
//   - every joint shared with Jacob gets Jacob's local rest TRS, so its
//     world position is unchanged but its local space becomes Jacob's;
//   - each inverse bind matrix is patched with the constant frame change
//     C = inv(W_jacob) * W_native, so the skin still deforms identically
//     (W'(t) * IB' == W(t) * IB at rest, vertices never move);
//   - cloth-only bones (Jacket_*, Raincoat_*, holster straps) keep their
//     world rest transform under the re-framed parents;
//   - the junk authored clips are dropped and Jacob's real body clips
//     (Sit, SitCards, Bet, Check, Fold, FoldShake, Talk, Attack, Crouch)
//     are copied in verbatim — valid now that local spaces match.
//
// After this, the shared mighty-cat mood clips (which target Jacob's bone
// names) drive Sheriff and Bandit directly at runtime, same as any Jacob
// outfit.
//
// Usage (from repo root; needs @gltf-transform/core + gl-matrix):
//   node tools/retarget_western_biped.mjs
//
// Verify with: three.js loads the GLB and plays 'Sit' or any mighty-cat
// idle without a bind-pose fallback or sheared limbs.

import { NodeIO } from '@gltf-transform/core';
import { prune } from '@gltf-transform/functions';
import { mat4 } from 'gl-matrix';
import { fileURLToPath } from 'url';
import path from 'path';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const MODELS = path.join(ROOT, 'Web/CinematicPoker.Web/wwwroot/models/characters/western');

const io = new NodeIO();

function nodeMap(doc) {
  const map = new Map();
  for (const n of doc.getRoot().listNodes()) {
    const name = n.getName();
    if (name && !map.has(name)) map.set(name, n);
  }
  return map;
}

function worldMatrix(node) {
  // getWorldMatrix walks the parent chain internally.
  return mat4.clone(node.getWorldMatrix());
}

async function retarget(jacobPath, targetPath) {
  const jacobDoc = await io.read(jacobPath);
  const doc = await io.read(targetPath);

  const jacobNodes = nodeMap(jacobDoc);
  const targetNodes = nodeMap(doc);

  // Old world matrices for every target node, before any edits.
  const oldWorld = new Map();
  for (const n of doc.getRoot().listNodes()) oldWorld.set(n, worldMatrix(n));

  // Jacob world rest per bone name.
  const jacobWorld = new Map();
  for (const [name, n] of jacobNodes) jacobWorld.set(name, worldMatrix(n));

  // Walk the skeleton top-down from the armature root so each child sees
  // its parent's *new* world matrix.
  const rootNode = targetNodes.get('root');
  if (!rootNode) throw new Error(`no 'root' node in ${targetPath}`);

  const newWorld = new Map(); // node -> mat4
  const reframed = new Map(); // node -> C = inv(W_jacob) * W_native
  const walk = (node, parentNewWorld) => {
    const name = node.getName();
    const jw = jacobWorld.get(name);
    const local = mat4.create();
    let world;
    if (jw && name !== 'root') {
      // Shared joint: adopt Jacob's frame. World position is identical by
      // construction (verified: all 69 animated joints coincide), so the
      // new local is simply inv(parentNewWorld) * W_jacob.
      world = mat4.clone(jw);
      mat4.multiply(local, mat4.invert(mat4.create(), parentNewWorld), world);
      const C = mat4.create();
      mat4.multiply(C, mat4.invert(mat4.create(), jw), oldWorld.get(node));
      reframed.set(node, C);
    } else {
      // Cloth-only bone (or the root itself): keep its world rest pose.
      world = mat4.clone(oldWorld.get(node));
      mat4.multiply(local, mat4.invert(mat4.create(), parentNewWorld), world);
    }
    node.setMatrix(local);
    newWorld.set(node, world);
    for (const child of node.listChildren()) walk(child, world);
  };
  const rootParentWorld = mat4.create(); // scene root parents are identity
  walk(rootNode, rootParentWorld);

  // Patch every skin's inverse bind matrices: IB' = C * IB for re-framed
  // joints, untouched for cloth bones (their world rest didn't move).
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

  // Drop the broken authored clips (and the stray *_shape_action track).
  for (const anim of doc.getRoot().listAnimations()) anim.dispose();

  // Copy Jacob's body clips verbatim; local spaces now match, so the raw
  // curve data is correct as-is. Channels target nodes by name.
  const buffer = doc.getRoot().listBuffers()[0];
  for (const srcAnim of jacobDoc.getRoot().listAnimations()) {
    const anim = doc.createAnimation(srcAnim.getName());
    const accCache = new Map(); // source accessor -> copied accessor
    const copyAccessor = (srcAcc) => {
      if (accCache.has(srcAcc)) return accCache.get(srcAcc);
      const acc = doc.createAccessor()
        .setType(srcAcc.getType())
        .setArray(srcAcc.getArray().slice())
        .setBuffer(buffer);
      accCache.set(srcAcc, acc);
      return acc;
    };
    for (const srcChannel of srcAnim.listChannels()) {
      const srcTarget = srcChannel.getTargetNode();
      const node = srcTarget && targetNodes.get(srcTarget.getName());
      if (!node) continue;
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
    console.log(`  + clip ${srcAnim.getName()} (${anim.listChannels().length} channels)`);
  }

  await doc.transform(prune());
  await io.write(targetPath, doc);
  console.log('wrote', targetPath);
}

for (const name of ['Sheriff', 'Bandit']) {
  console.log(`=== re-framing ${name} onto Jacob's biped ===`);
  await retarget(path.join(MODELS, 'Jacob.glb'), path.join(MODELS, `${name}.glb`));
}
