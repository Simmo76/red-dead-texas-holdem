// Intro splash layered art (bg + title + tap).
// Run: node tools/intro-layers-acceptance.test.mjs
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

const root = new URL('..', import.meta.url).pathname;
const intro = join(root, 'Web/CinematicPoker.Web/wwwroot/img/intro');
const home = readFileSync(join(root, 'Web/CinematicPoker.Web/Pages/Home.razor'), 'utf8');

function assert(cond, msg) {
  if (!cond) throw new Error(msg);
}

for (const scene of ['saloon', 'street']) {
  assert(existsSync(join(intro, `${scene}-bg.jpg`)), `missing intro/${scene}-bg.jpg`);
}
for (const piece of ['hero-plate.png', 'hero-plate@2x.png']) {
  assert(existsSync(join(intro, piece)), `missing intro/${piece}`);
}

assert(home.includes('intro-hero-plate'), 'Home: intro hero title plate');
assert(home.includes('intro-splash-bg'), 'Home: intro scenic background');

console.log('intro-layers-acceptance: OK');