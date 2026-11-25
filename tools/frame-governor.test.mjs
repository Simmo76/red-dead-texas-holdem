// Unit tests for frame-governor.js (mobile GPU plan Tasks 1–2).
// Run: node tools/frame-governor.test.mjs
import {
  ACTIVE_FPS,
  IDLE_FPS,
  IDLE_AFTER_MS,
  sceneBusy,
  frameBudgetSeconds,
  shouldRenderFrame,
  advanceFrameAccum,
} from '../Web/CinematicPoker.Web/wwwroot/js/frame-governor.js';

function assert(cond, msg) {
  if (!cond) throw new Error(msg);
}

const t0 = 100_000;

assert(ACTIVE_FPS === 30 && IDLE_FPS === 15, 'FPS constants');
assert(IDLE_AFTER_MS === 2000, 'idle window');

assert(sceneBusy({ lastActivity: t0 }, t0 + 500), 'recent activity is busy');
assert(!sceneBusy({ lastActivity: t0 }, t0 + IDLE_AFTER_MS + 1), 'quiet table is idle');
assert(sceneBusy({ introActive: true, lastActivity: 0 }, t0 + 99_999), 'intro is busy');
assert(sceneBusy({ zoomActive: true, lastActivity: 0 }, t0), 'zoom is busy');
assert(sceneBusy({ fxCount: 1, lastActivity: 0 }, t0), 'particles are busy');

const activeBudget = frameBudgetSeconds(false, true);
const idleBudget = frameBudgetSeconds(false, false);
assert(Math.abs(activeBudget - 1 / ACTIVE_FPS) < 1e-9, 'active budget');
assert(Math.abs(idleBudget - 1 / IDLE_FPS) < 1e-9, 'idle budget');
assert(frameBudgetSeconds(true, false) === activeBudget, 'coach presenting uses active cap');

assert(!shouldRenderFrame(activeBudget * 0.5, activeBudget, false), 'accum below budget skips');
assert(shouldRenderFrame(activeBudget, activeBudget, false), 'accum at budget renders');
assert(shouldRenderFrame(0, idleBudget, true), 'renderNow wakes idle table immediately');

let acc = activeBudget * 1.4;
acc = advanceFrameAccum(acc, activeBudget);
assert(Math.abs(acc - activeBudget * 0.4) < 1e-9, 'modest overshoot carried');

acc = activeBudget * 2.5;
acc = advanceFrameAccum(acc, activeBudget);
assert(acc === activeBudget, 'large overshoot clamped to one frame period');

const stalled = advanceFrameAccum(activeBudget * 50, activeBudget);
assert(stalled === activeBudget, 'long stall carry clamped to one frame');

console.log('frame-governor: OK');