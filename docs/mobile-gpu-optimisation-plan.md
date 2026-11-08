# Mobile GPU optimisation plan — frame caps, idle throttle, resolution

A self-contained implementation plan for reducing phone heat and battery
drain in the web version of Cinematic Poker. Written to be handed to an
agent with no prior context.

## Problem

On phones the game makes the device hot and drains the battery quickly.
Root cause (verified by code inspection): the three.js scene in
`Web/CinematicPoker.Web/wwwroot/js/poker-scene.js` renders **continuously at
the display's native refresh rate** — 60 fps on most iPhones, 120 fps on
some ProMotion devices — with no frame cap, no idle throttling, and no
battery mode. Every frame updates six skinned characters (animation mixers,
fidgets, arm poses, name-tag anchors), eases the camera, and renders with
antialiasing, PCF soft shadows and ACES tone mapping at up to 2×
devicePixelRatio. Poker is mostly a static scene: between actions almost
nothing changes, yet the GPU works flat-out.

There is an existing quality-tier system (`GFX_LITE` / `GFX_ULTRA_LITE`,
lines ~20–60) but it is **crash-driven** (it reacts to iOS killing the page
from memory pressure), not heat-driven. A phone that runs hot without
crashing stays at full quality forever.

## Where everything lives

All changes are in **one file**:
`Web/CinematicPoker.Web/wwwroot/js/poker-scene.js` (~4,000 lines).

| Thing | Location (at time of writing) |
|---|---|
| Render loop | `renderer.setAnimationLoop(() => { ... })`, ~line 2160 |
| Frame delta | `state.clock.getDelta()` (THREE.Clock), first line of the loop |
| Coach-mode branch | early `return` inside the loop when `coachControl.api.presenting` |
| Pixel ratio logic | `cappedPixelRatio(canvas)`, ~line 162 (DPR cap 2.0 tier 0 / 1.5 lite, plus a pixel budget: 3.4e6 tier 0, 1.8e6 lite, 1.2e6 ultra-lite) |
| Resize re-applies DPR | two call sites around lines 2794 and 2836 (`renderer.setPixelRatio(cappedPixelRatio(canvas))`) |
| Busy-state signals | `state.intro.active` (opening cinematic), `state.zoom.active` (card/bet zoom flights), `state.fx` (celebration particles array, empty when none), `state.lastJson` (last game-state snapshot pushed from Blazor — changes whenever the game state changes) |
| Pointer input | pointer/touch handlers around lines 2390–2520 |
| Character gestures | `playOnce` voice/fold/win gestures, ~line 3866 |

Testing the result does not need a phone: `cd Web/CinematicPoker.Web &&
dotnet run` serves the game locally (needs .NET 8 SDK), or `dotnet publish
-c Release -o /tmp/pub` and serve `/tmp/pub/wwwroot` with any static server.
The scene is landscape-only — use a wide browser window.

## Task 1 — cap the active frame rate at 30 fps

Inside the existing `setAnimationLoop` callback, accumulate elapsed time and
skip rendering until at least `1/30 s` has accumulated; then run the whole
existing body with the **accumulated** delta so animation speed is unchanged.

Implementation notes:

- Keep calling `state.clock.getDelta()` on every rAF tick and add it to an
  accumulator; when below the frame budget, `return` early. When at/over
  budget, run the loop body with the accumulated value as `rawDt`, then
  reset the accumulator to the remainder (carry the overshoot so the real
  rate stays close to 30 and does not beat against the display rate).
- The coach branch already clamps its dt (`Math.min(rawDt, 0.05)`); larger
  accumulated deltas are therefore safe there, but keep the clamp.
- The opening cinematic (`state.intro`) is **wall-clock driven** by design
  (comment in the loop explains audio sync) — do not change that.
- Do not skip the per-tick bookkeeping that must observe every rAF (there
  is none today — the whole body is render work — but preserve that
  property if the file has changed).
- Make the cap a named constant (e.g. `ACTIVE_FPS = 30`), and use a lower
  value for the lite tier if desired (`GFX_LITE` is in scope at the top of
  the file).

## Task 2 — drop to ~15 fps when the table is idle

Most session time is spent waiting (player thinking, between hands). Add an
idle detector and halve the frame budget again when idle (e.g.
`IDLE_FPS = 15`). Character idle fidgets still read as alive at 15 fps.

Busy means **any** of:

- `state.intro.active` or `state.zoom.active` (cinematic / zoom flight);
- `state.fx.length > 0` (celebration particles live);
- a pointer/touch interaction in the last ~2 s (set a `lastActivity`
  timestamp in the existing pointer handlers — drag, pinch, tap);
- the game state changed in the last ~2 s (the Blazor layer pushes
  snapshots; `state.lastJson` changes on every update — compare and stamp
  `lastActivity` where it is assigned, or stamp in the snapshot-apply
  function);
- a one-shot character gesture is playing (the `playOnce` path), if cheap
  to detect — the 2 s activity window after the snapshot change that
  triggered it covers most of this anyway.

Everything else (camera easing toward a target it has essentially reached,
idle mixers) is idle. The camera-easing lerps converge exponentially, so at
15 fps they still settle correctly (`ease = Math.min(1, dt * 8)` is already
frame-rate independent in formulation).

Transitions must be instant in the busy direction: a tap or snapshot change
while idle should render on the **next** rAF tick (reset the accumulator
budget check, don't wait out a 15 fps frame window), so input never feels
laggy.

## Task 3 — lower the render resolution on phones

`cappedPixelRatio()` currently allows DPR 2.0 and a 3.4-megapixel
backbuffer at tier 0. On a phone-sized screen at arm's length, 1.5× is
visually near-identical and shades ~44 % fewer pixels.

- Detect phones coarsely (e.g. `matchMedia('(pointer: coarse)')` or a
  user-agent/touch check — the code base favours capability checks) and on
  phones cap tier-0 DPR at **1.5** and the pixel budget at ~**2.2e6**.
  Desktop keeps 2.0 / 3.4e6.
- Optional refinement if cheap: restore full DPR while `state.zoom.active`
  (card close-ups benefit from crispness) and drop back on zoom exit.
  **Apply pixel-ratio changes only on zoom enter/exit transitions**, never
  per frame — `setPixelRatio` reallocates the backbuffer, which is
  expensive and was implicated in iOS Safari memory crashes (see the long
  comment above the renderer construction, ~line 1531). If this feels
  risky, skip the refinement; the flat 1.5 cap is the win.
- The two resize call sites (~2794, 2836) call `cappedPixelRatio` again, so
  a capability-based cap inside that one function covers all paths.

## Acceptance criteria

1. With dev tools open on a desktop browser, an fps counter (temporary
   `console.log` once per second, or the Performance panel) shows ≤ 30 fps
   during a hand and ≤ 15 fps when idle ≥ 2 s; a click/tap or a new game
   event returns it to 30 immediately (no perceptible input lag).
2. Card-deal, chip, zoom and celebration animations play at normal speed
   (no slow motion, no fast forward) — i.e. deltas are accumulated, not
   dropped.
3. The opening intro cinematic stays in sync with its audio.
4. Coach mode (`presenting`) still renders correctly.
5. On a narrow/phone-sized viewport with `devicePixelRatio ≥ 2`, the canvas
   backbuffer is ≤ 1.5× (check `renderer.getPixelRatio()` in the console);
   desktop is unchanged.
6. A full hand plays cleanly start to finish (fold, call, showdown,
   winner celebration) with no console errors.
7. `GFX_LITE` tiers still boot and play (force with the localStorage key
   `cinematic-poker-gfx-tier` = `1` or `2`, then reload).

## Out of scope (deliberately)

- A user-facing Battery Saver toggle in settings (candidate follow-up).
- Shadow-map rate reduction or shadow quality changes.
- Any Unity-side changes. The Capacitor iOS wrapper
  (`Mobile/ios-wrapper/`) ships this same JS, so it inherits the wins
  automatically — no wrapper changes needed.
