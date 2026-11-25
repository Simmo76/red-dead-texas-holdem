// Backbuffer pixel budget (unit-tested in tools/pixel-budget.test.mjs).

const BUDGET_FULL = 3.4e6;
const BUDGET_COARSE = 2.2e6;
const BUDGET_LITE = 1.8e6;
const BUDGET_ULTRA_LITE = 1.2e6;

const DPR_DESKTOP = 2;
const DPR_COARSE = 1.75;
const DPR_LITE = 1.5;

/**
 * @param {{ dpr?: number, width: number, height: number, gfxLite?: boolean, gfxUltraLite?: boolean, coarsePointer?: boolean }} opts
 */
export function computeCappedPixelRatio(opts) {
  const w = Math.max(1, opts.width);
  const h = Math.max(1, opts.height);
  const rawDpr = opts.dpr ?? 1;
  const gfxLite = !!opts.gfxLite;
  const gfxUltraLite = !!opts.gfxUltraLite;
  const coarse = !!opts.coarsePointer;

  const dprCap = gfxLite ? DPR_LITE : coarse ? DPR_COARSE : DPR_DESKTOP;
  const dpr = Math.min(rawDpr, dprCap);

  const budget = gfxUltraLite ? BUDGET_ULTRA_LITE
    : gfxLite ? BUDGET_LITE
    : coarse ? BUDGET_COARSE
    : BUDGET_FULL;

  return Math.max(1, Math.min(dpr, Math.sqrt(budget / (w * h))));
}