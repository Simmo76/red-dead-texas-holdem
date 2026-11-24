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
  for (const piece of ['bg.jpg', 'title.png', 'title@2x.png', 'tap.png', 'tap@2x.png']) {
    assert(existsSync(join(intro, `${scene}-${piece}`)), `missing intro/${scene}-${piece}`);
  }
}

assert(home.includes('intro-title-plate'), 'Home: layered intro title');
assert(home.includes('intro-tap-art'), 'Home: art-backed tap');
assert(home.includes('intro-splash-bg'), 'Home: intro background plate');

console.log('intro-layers-acceptance: OK');