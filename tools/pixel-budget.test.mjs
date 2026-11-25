// Unit tests for pixel-budget.js (mobile GPU plan Task 3).
// Run: node tools/pixel-budget.test.mjs
import { computeCappedPixelRatio } from '../Web/CinematicPoker.Web/wwwroot/js/pixel-budget.js';

function assert(cond, msg) {
  if (!cond) throw new Error(msg);
}

function approx(a, b, eps = 0.02) {
  return Math.abs(a - b) <= eps;
}

// Desktop landscape: full DPR cap when budget allows
const desktop = computeCappedPixelRatio({
  dpr: 2,
  width: 844,
  height: 390,
  coarsePointer: false,
  gfxLite: false,
});
assert(desktop === 2, `desktop dpr ${desktop}`);

// Phone coarse pointer: capped at 1.75 before budget squeeze
const phone = computeCappedPixelRatio({
  dpr: 3,
  width: 390,
  height: 844,
  coarsePointer: true,
  gfxLite: false,
});
assert(phone <= 1.75 + 1e-6, `phone dpr ${phone} ≤ 1.75`);

// Lite tier never exceeds 1.5
const lite = computeCappedPixelRatio({
  dpr: 3,
  width: 390,
  height: 844,
  coarsePointer: false,
  gfxLite: true,
});
assert(lite <= 1.5 + 1e-6, `lite dpr ${lite} ≤ 1.5`);

// Ultra-lite is tier 2, which always includes lite caps (see poker-scene GFX_*)
const ultra = computeCappedPixelRatio({
  dpr: 2,
  width: 800,
  height: 600,
  gfxLite: true,
  gfxUltraLite: true,
});
const liteSame = computeCappedPixelRatio({
  dpr: 2,
  width: 800,
  height: 600,
  gfxLite: true,
});
assert(ultra <= liteSame, 'ultra-lite ≤ lite on same canvas');

// Huge canvas: budget forces ratio down, never below 1
const huge = computeCappedPixelRatio({ dpr: 2, width: 4000, height: 3000 });
assert(huge >= 1 && huge < 2, `huge canvas ratio ${huge}`);

// Matches legacy formula on a reference phone viewport (regression lock)
const ref = computeCappedPixelRatio({
  dpr: 2,
  width: 844,
  height: 390,
  coarsePointer: true,
});
assert(approx(ref, Math.min(1.75, Math.sqrt(2.2e6 / (844 * 390)))), `ref phone ${ref}`);

console.log('pixel-budget: OK');