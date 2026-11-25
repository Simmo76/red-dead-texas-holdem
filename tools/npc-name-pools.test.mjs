#!/usr/bin/env node
// Mirrors Home.razor restore rules for split saloon / octagon NPC name pools.
// Run: node tools/npc-name-pools.test.mjs

const SALOON = [
  'Sam Six-Shot', 'Billie Quickdraw', 'Jesse Deadeye', 'Charlie Cold-Iron',
  'Frankie Fast-Hand', 'Riley Black-Hat', 'Casey Trigger',
];
const OCTAGON = [
  'Alex Iron-Fist', 'Jordan Dust-Knuckle', 'Taylor Low-Blow', 'Morgan Cage-Breaker',
  'Reese Blood-Odds', 'Cameron Blindside', 'Quinn Dead-Ringer',
];

function inPool(name, pool) {
  return pool.some((n) => n.toLowerCase() === name.toLowerCase());
}

function shouldRemap(name, savedScene) {
  if (!name) return true;
  const inSaloon = inPool(name, SALOON);
  const inOctagon = inPool(name, OCTAGON);
  if (!inSaloon && !inOctagon) return true;
  return savedScene === 'saloon' && !inSaloon && inOctagon;
}

function assert(cond, msg) {
  if (!cond) throw new Error(msg);
}

assert(!shouldRemap('Sam Six-Shot', 'saloon'), 'keep western on saloon restore');
assert(!shouldRemap('Jordan Dust-Knuckle', 'street'), 'keep fighter on octagon restore');
assert(shouldRemap('Jordan Dust-Knuckle', 'saloon'), 'remap fighter on saloon restore');
assert(!shouldRemap('Sam Six-Shot', 'street'), 'keep western after mid-session switch to octagon');
assert(shouldRemap('Nickey 2-guns', 'saloon'), 'remap retired name');
assert(shouldRemap('', 'street'), 'remap empty');

console.log('npc name pools: restore rules ok');