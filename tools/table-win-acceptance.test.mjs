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
const cards = join(root, 'Web/CinematicPoker.Web/wwwroot/img/cards/ui');

const plaqueAssets = [
  'table-win-panel.png',
  'table-win-sunburst.png',
  'table-win-star.png',
  'table-win-star-sm.png',
  'table-win-eyebrow.png',
  'table-win-btn-gold.png',
  'table-win-btn-green.png',
  'table-win-confetti-red.png',
  'table-win-confetti-green.png',
  'table-win-confetti-gold.png',
];

for (const name of plaqueAssets) {
  const path = join(ui, name);
  assert(existsSync(path), `missing table-win asset: ${name}`);
  assert(statSync(path).size > 200, `${name} looks empty`);
}

// Dead man's hand fan — western card pack faces used by the overlay.
const handCards = [
  'spades_A.png',
  'clubs_A.png',
  'hearts_Q.png',
  'spades_8.png',
  'clubs_8.png',
];
for (const name of handCards) {
  assert(existsSync(join(cards, name)), `missing western card: ${name}`);
}

const home = readFileSync(join(root, 'Web/CinematicPoker.Web/Pages/Home.razor'), 'utf8');
const css = readFileSync(join(root, 'Web/CinematicPoker.Web/wwwroot/css/app.css'), 'utf8');
const shot = readFileSync(join(root, 'Web/CinematicPoker.Web/wwwroot/js/poker-screenshot.js'), 'utf8');

assert(home.includes('table-win-panel.png'), 'Home.razor: panel PNG wired');
assert(home.includes('table-win-sunburst.png'), 'Home.razor: sunburst PNG wired');
assert(home.includes('table-win-star-sm.png'), 'Home.razor: star PNG wired');
assert(home.includes('table-win-confetti-red.png'), 'Home.razor: confetti sprites listed');
assert(home.includes('win-btn-gold'), 'Home.razor: gold Export Certificate plaque class');
assert(home.includes('win-btn-green'), 'Home.razor: green Sit New Table plaque class');
assert(home.includes('WINNER TAKES ALL'), 'Home.razor: eyebrow copy');
assert(home.includes('img/cards/ui/spades_A.png'), 'Home.razor: western A♠ in fan');
assert(home.includes('img/cards/ui/clubs_8.png'), 'Home.razor: western 8♣ in fan');
assert(home.includes('winpreview=1'), 'Home.razor: ?winpreview=1 test hook');
assert(home.includes('ShowWinPreview'), 'Home.razor: ShowWinPreview helper');
assert(home.includes('ConfettiSprite'), 'Home.razor: ConfettiSprite helper');
assert(home.includes('_winPreviewHands'), 'Home.razor: winpreview sample hand count');
assert(
  /if \(_winPreview\)[\s\S]*?ShowWinPreview\(\);[\s\S]*?return;/.test(home),
  'Home.razor: winpreview skips the live deal loop',
);

assert(css.includes('.overlay-box.table-win'), 'css: table-win plaque');
assert(css.includes('table-win-btn-gold.png'), 'css: gold button plaque');
assert(css.includes('table-win-btn-green.png'), 'css: green button plaque');
assert(css.includes('.win-panel-art'), 'css: panel art layer');
assert(css.includes('.win-sunburst'), 'css: sunburst layer');
assert(css.includes('.win-star'), 'css: star ornaments');
assert(css.includes('.confetti-sprite'), 'css: confetti sprite pieces');
assert(!css.includes('.overlay-box.table-win > .win-rays'), 'css: legacy CSS ray burst removed');

assert(
  shot.includes(".win-celebration .overlay-box.table-win"),
  'poker-screenshot: Export Certificate prefers table-win plaque',
);

console.log(
  'table-win-acceptance: OK (' +
    plaqueAssets.length +
    ' PNGs, ' +
    handCards.length +
    ' cards, Razor/CSS/screenshot wiring)',
);
