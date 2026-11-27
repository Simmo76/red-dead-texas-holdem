// Static acceptance checks for the Winner Takes All (table-win) overlay.
// Guards the painted plaque assets and Home.razor / CSS wiring.
// Run: node tools/table-win-acceptance.test.mjs
import { readFileSync, existsSync, statSync } from 'node:fs';
import { join } from 'node:path';

const root = new URL('..', import.meta.url).pathname;

function assert(cond, msg) {
  if (!cond) throw new Error(msg);
}

const ui = join(root, 'Web/CinematicPoker.Web/wwwroot/img/ui');

const plaqueAssets = [
  'table-win-panel.png',
  'table-win-panel-alt.png',
  'table-win-card-fan.png',
  'table-win-eyebrow.png',
  'table-win-nameplate.png',
  'table-win-title-flourish.png',
  'table-win-haul-label.png',
  'table-win-stat-hands.png',
  'table-win-stat-rivals.png',
  'table-win-stat-biggest.png',
  'table-win-stat-divider.png',
  'table-win-btn-gold.png',
  'table-win-btn-green.png',
  'table-win-btn-stats.png',
  'table-win-confetti-red.png',
  'table-win-confetti-green.png',
  'table-win-confetti-gold.png',
];

for (const name of plaqueAssets) {
  const path = join(ui, name);
  assert(existsSync(path), `missing table-win asset: ${name}`);
  assert(statSync(path).size > 200, `${name} looks empty`);
}

const home = readFileSync(join(root, 'Web/CinematicPoker.Web/Pages/Home.razor'), 'utf8');
const css = readFileSync(join(root, 'Web/CinematicPoker.Web/wwwroot/css/app.css'), 'utf8');
const shot = readFileSync(join(root, 'Web/CinematicPoker.Web/wwwroot/js/poker-screenshot.js'), 'utf8');

assert(home.includes('table-win-panel-alt.png'), 'Home.razor: kit panel PNG wired');
assert(home.includes('table-win-card-fan.png'), 'Home.razor: card fan PNG wired');
assert(home.includes('table-win-eyebrow.png'), 'Home.razor: eyebrow PNG wired');
assert(home.includes('table-win-nameplate.png'), 'Home.razor: nameplate PNG wired');
assert(home.includes('table-win-title-flourish.png'), 'Home.razor: title flourish PNG wired');
assert(home.includes('table-win-haul-label.png'), 'Home.razor: haul label PNG wired');
assert(home.includes('table-win-stat-hands.png'), 'Home.razor: hands stat PNG wired');
assert(home.includes('table-win-stat-rivals.png'), 'Home.razor: rivals stat PNG wired');
assert(home.includes('table-win-stat-biggest.png'), 'Home.razor: biggest pot PNG wired');
assert(home.includes('table-win-btn-gold.png'), 'Home.razor: gold button PNG wired');
assert(home.includes('table-win-btn-green.png'), 'Home.razor: green button PNG wired');
assert(home.includes('table-win-btn-stats.png'), 'Home.razor: stats button PNG wired');
assert(home.includes('win-btn-gold'), 'Home.razor: gold Export Certificate plaque class');
assert(home.includes('win-btn-green'), 'Home.razor: green Sit New Table plaque class');
assert(home.includes('win-nameplate-text'), 'Home.razor: player name on nameplate');
assert(home.includes('winpreview=1'), 'Home.razor: ?winpreview=1 test hook');
assert(home.includes('ShowWinPreview'), 'Home.razor: ShowWinPreview helper');
assert(home.includes('ConfettiSprite'), 'Home.razor: ConfettiSprite helper');
assert(home.includes('_winPreviewHands'), 'Home.razor: winpreview sample hand count');
assert(
  /if \(_winPreview\)[\s\S]*?ShowWinPreview\(\);[\s\S]*?return;/.test(home),
  'Home.razor: winpreview skips the live deal loop',
);

assert(css.includes('.overlay-box.table-win'), 'css: table-win plaque');
assert(css.includes('.win-panel-art'), 'css: panel art layer');
assert(css.includes('.win-panel-inner'), 'css: content inset');
assert(home.includes('win-panel-inner'), 'Home.razor: content inset');
assert(css.includes('.win-card-fan'), 'css: card fan');
assert(css.includes('.win-nameplate'), 'css: nameplate');
assert(css.includes('.win-title-flourish'), 'css: title flourish');
assert(css.includes('.win-stat-plaque'), 'css: stat plaques');
assert(css.includes('.win-btn'), 'css: painted buttons');
assert(css.includes('.confetti-sprite'), 'css: confetti sprite pieces');
assert(!css.includes('.overlay-box.table-win > .win-rays'), 'css: legacy CSS ray burst removed');
assert(!home.includes('TableWinSunburstImage'), 'Home.razor: sunburst layer removed (baked into kit panel)');

assert(
  shot.includes(".win-celebration .overlay-box.table-win"),
  'poker-screenshot: Export Certificate prefers table-win plaque',
);

console.log(
  'table-win-acceptance: OK (' +
    plaqueAssets.length +
    ' PNGs, Razor/CSS/screenshot wiring)',
);
