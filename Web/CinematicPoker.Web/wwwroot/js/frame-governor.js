// Pure frame-rate governor logic (unit-tested in tools/frame-governor.test.mjs).
// poker-scene.js wires this to live state and the render loop.

export const ACTIVE_FPS = 30;
export const IDLE_FPS = 15;
export const IDLE_AFTER_MS = 2000;

/**
 * @param {{ introActive?: boolean, zoomActive?: boolean, fxCount?: number, lastActivity?: number }} scene
 * @param {number} now  performance.now() at decision time
 */
export function sceneBusy(scene, now = 0) {
  const last = scene.lastActivity ?? 0;
  return !!(scene.introActive)
    || !!(scene.zoomActive)
    || (scene.fxCount ?? 0) > 0
    || now - last < IDLE_AFTER_MS;
}

/** @param {boolean} presenting  coach overlay owns the canvas */
export function frameBudgetSeconds(presenting, busy) {
  const fps = presenting || busy ? ACTIVE_FPS : IDLE_FPS;
  return 1 / fps;
}

/**
 * Whether this rAF tick should run the render body.
 * @param {number} frameAccum  seconds accumulated since last rendered frame
 * @param {number} budget      target frame period in seconds
 * @param {boolean} renderNow  wake flag (input / state change while idling)
 */
export function shouldRenderFrame(frameAccum, budget, renderNow) {
  return renderNow || frameAccum >= budget;
}

/**
 * Carry overshoot after a rendered frame; clamp so one long stall cannot
 * queue a burst of back-to-back frames.
 */
export function advanceFrameAccum(frameAccum, budget) {
  return Math.min(Math.max(frameAccum - budget, 0), budget);
}