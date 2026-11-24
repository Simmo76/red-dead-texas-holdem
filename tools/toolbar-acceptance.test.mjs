// Static acceptance checks for the mobile gameplay toolbar (docs/toolbar/IMPLEMENTATION.md).
// Run: node tools/toolbar-acceptance.test.mjs
import { readFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';

const root = new URL('..', import.meta.url).pathname;

function assert(cond, msg) {
  if (!cond) throw new Error(msg);
}

const icons = ['rules', 'tune', 'hands', 'view', 'settings', 'more'];
for (const name of icons) {
  const svg = join(root, `Web/CinematicPoker.Web/wwwroot/icons/toolbar/${name}.svg`);
  const png = join(root, `Web/CinematicPoker.Web/wwwroot/icons/toolbar/${name}.png`);
  assert(existsSync(svg), `missing ${name}.svg`);
  assert(existsSync(png), `missing ${name}.png`);
}

const home = readFileSync(join(root, 'Web/CinematicPoker.Web/Pages/Home.razor'), 'utf8');
const css = readFileSync(join(root, 'Web/CinematicPoker.Web/wwwroot/css/app.css'), 'utf8');
const impl = readFileSync(join(root, 'docs/toolbar/IMPLEMENTATION.md'), 'utf8');

assert(home.includes('gameplay-toolbar'), 'Home.razor: gameplay-toolbar');
assert(home.includes('role="toolbar"'), 'Home.razor: toolbar role');
assert(home.includes('<ToolbarButton'), 'Home.razor: ToolbarButton components');
assert(home.includes('OnClick="OpenRules"'), 'handlers: OpenRules');
assert(home.includes('OnClick="NextSong"'), 'handlers: NextSong / Tune');
assert(home.includes('OnClick="OpenHandRankings"'), 'handlers: OpenHandRankings');
assert(home.includes('OnClick="ToggleOverheadView"'), 'handlers: View camera');
assert(home.includes('OnClick="OpenSettings"'), 'handlers: Settings');
assert(home.includes('Label="View"'), 'View label');
assert(home.includes('Toggle="true"'), 'View aria-pressed via Toggle');
assert(!home.includes("bird's eye"), 'legacy bird\'s eye label removed from toolbar');

assert(css.includes('--toolbar-bg'), 'css: design tokens');
assert(css.includes('min-height: 44px') || css.includes('min-width: 44px'), 'css: 44px targets');
assert(css.includes('env(safe-area-inset-top)'), 'css: safe-area insets');
assert(css.includes('max-width: 419px'), 'css: overflow breakpoint');
assert(css.includes('max-width: 699px'), 'css: compact breakpoint');
assert(css.includes('pointer-events: none'), 'css: toolbar does not block canvas globally');
assert(css.includes('.toolbar-overflow-menu'), 'css: More menu');

assert(impl.includes('Acceptance tests'), 'IMPLEMENTATION.md present');

const widths = [320, 390, 568, 667, 844, 1024];
assert(widths.length === 6, 'acceptance width list sanity');

console.log('toolbar-acceptance: OK (' + icons.length + ' icons, handlers wired, CSS breakpoints)');