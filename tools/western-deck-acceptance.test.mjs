// Validate Western poker deck assets and rank+suit filename mapping.
// Run: node tools/western-deck-acceptance.test.mjs
import { existsSync } from 'node:fs';
import { join } from 'node:path';

const root = new URL('..', import.meta.url).pathname;
const cardsDir = join(root, 'Web/CinematicPoker.Web/wwwroot/img/cards');

const ranks = ['2', '3', '4', '5', '6', '7', '8', '9', '10', 'J', 'Q', 'K', 'A'];
const suits = ['C', 'D', 'H', 'S'];

function assert(cond, msg) {
  if (!cond) throw new Error(msg);
}

assert(existsSync(join(cardsDir, 'back.png')), 'missing card back');
assert(existsSync(join(cardsDir, 'panel.png')), 'missing panel background');

let count = 0;
for (const rank of ranks) {
  for (const suit of suits) {
    const file = join(cardsDir, `${rank}${suit}.png`);
    assert(existsSync(file), `missing ${rank}${suit}.png`);
    count++;
  }
}
assert(count === 52, 'expected 52 face textures');

console.log(`western-deck-acceptance: OK (${count} faces, back, panel)`);