# Dead Man's Hand — mobile toolbar redesign

Implementation specification • iPhone landscape / web / Three.js

## Goal and scope

Replace the existing top gameplay toolbar, not the poker engine, camera implementation, menus or 3D assets. Preserve Rules, Tune, Hands, View (overhead camera), and Settings actions. **Tune** invokes the existing next-song action (`NextSong`). Use the existing web/Three.js UI and iOS wrapper.

## Layout

Semantic `<header role="toolbar">` with a flexible brand area and a right-aligned control group.

| Mode | Trigger | Brand | Controls |
|------|---------|-------|----------|
| Wide | ≥700 CSS px width | `DEAD MAN'S HAND` | Horizontal icon + label; Settings icon-only |
| Compact | &lt;560 CSS px width | `DMH` | Icon left of caption in shared chrome; Settings icon-only |
| Short landscape | low height, wider width | Full title (scaled) | Same row layout as mockup |
| Overflow | &lt;420 CSS px | (compact) | Tune + Settings move into **More** menu; all actions remain reachable |

Measure safe-area width; never shrink touch targets below **44×44 CSS px**.

## Visual tokens

- Background: `#181410` (mostly opaque over scene)
- Icon: `#D9B875`
- Text: `#F4E8D2`
- Border: `#795733`
- Active accent: `#FFC66B`
- Icons: 24 CSS px; captions: 12–13 px system sans-serif; gaps: 8 px; radius: 8–12 px
- Brand: existing Rye serif

## Assets

`wwwroot/icons/toolbar/*.svg` (primary, `currentColor`) with matching transparent PNG fallbacks.

## Semantics

- **View**: `aria-pressed` reflects overhead camera; active border + accent when on
- **Settings** / **More**: icon-only with accessible names
- Native `<button>` elements; decorative SVGs `aria-hidden` and `focusable="false"`
- Toolbar uses `pointer-events: auto`; does not steal canvas gestures outside controls

## Integration checklist

1. Locate toolbar markup, handlers, camera state, CSS, viewport (`viewport-fit=cover` — already set)
2. Add SVG assets; reusable `ToolbarButton` component (no new npm dependencies)
3. Wire existing callbacks; no duplicated game state
4. Responsive CSS + `env(safe-area-inset-*)`; overflow menu instead of clipping
5. Run engine tests + `tools/toolbar-acceptance.test.mjs`; deliver screenshots

## Acceptance tests

- Each original action fires once per tap; View stays in sync after modals and scene changes
- No overlap with notch, hand window, or action bar; test landscape both orientations, compact + large phone widths, desktop
- Widths: 320, 390, 568, 667, 844, 1024 CSS px — no horizontal scroll; overflow items reachable
- Targets ≥44×44 CSS px; contrast ≥4.5:1 text, ≥3:1 icons on toolbar background
- No new network deps; offline assets load; no regression to dealing, betting, camera, modals, scene input