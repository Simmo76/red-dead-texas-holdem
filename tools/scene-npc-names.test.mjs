#!/usr/bin/env node
// Each seat holds one western and one fighter nickname for the session; the
// active scene picks which row is shown (mirrors Home.razor dual maps).
// Run: node tools/scene-npc-names.test.mjs

function assert(cond, msg) {
  if (!cond) throw new Error(msg);
}

const saloon = ['Sam Six-Shot', 'Billie Quickdraw', 'Jesse Deadeye', 'Charlie Cold-Iron', 'Frankie Fast-Hand'];
const street = ['Alex Iron-Fist', 'Jordan Dust-Knuckle', 'Taylor Low-Blow', 'Morgan Cage-Breaker', 'Reese Blood-Odds'];

function nameFor(scene, seat) {
  const map = scene === 'street' ? street : saloon;
  return map[seat - 1];
}

assert(nameFor('saloon', 1) !== nameFor('street', 1), 'scene swap changes nickname at seat 1');
assert(nameFor('saloon', 3) === 'Jesse Deadeye', 'saloon seat 3');
assert(nameFor('street', 3) === 'Taylor Low-Blow', 'street seat 3');

console.log('scene npc names: dual map ok');