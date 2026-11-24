# Preview integration build (`cursor/preview-integration-abf1`)

**This is the only branch** for recent UI + card work. All other `cursor/*-abf1`
feature branches are superseded by this line — fix issues here, then merge **once**
to `main` when the owner approves.

## Preview URL

After CI publishes GitHub Pages (a few minutes after push):

https://simmo76.github.io/red-dead-texas-holdem/preview/cursor-preview-integration-abf1/

Hard-refresh or open in a private window so `index.html` cache busts apply (`app.css?v=89`).

## Included (vs `main`)

- Ornate toolbar pill artwork (Rules, Tune, Hands, View, Settings)
- Art-backed Fold / Call / Raise / All-in actions
- Pot badge in top bar
- Rules / Hand Rankings full-screen art overlays (transparent outside frame)
- Settings: portrait transparent frame, dropdown pill art, session actions in sheet
- Intro: scenic background plates + owner hero title / tap plate
- Western poker deck (52 faces, back, HUD `panel.png`) for HUD and 3D table

**Not merged to `main` until the owner approves.**

## Regenerate assets

```bash
python3 tools/build_settings_panel_shell.py
python3 tools/build_intro_layers.py
python3 tools/build_intro_hero.py
```

## Smoke checks

```bash
node tools/toolbar-acceptance.test.mjs
node tools/intro-layers-acceptance.test.mjs
node tools/western-deck-acceptance.test.mjs
```