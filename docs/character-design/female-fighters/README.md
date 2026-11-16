# Female octagon fighters — Meshy workflow

The Arcade Fighters pack has no female characters, so the female fighters are
generated. This folder holds the inputs for that pipeline.

## Files

| File | What it is |
|---|---|
| `fighter1-style-ref-front.png`, `fighter1-style-ref-quarter.png` | Headless renders of `Fighter1.glb` (the bald fighter in the purple gi), used as style anchors |
| `fighter2-style-ref-front.png`, `fighter2-style-ref-quarter.png` | Headless renders of `Fighter2.glb` (the brawler in the tank top and cap) |
| `female-fighter1-gi-concept.jpg` | AI concept: female gi martial artist matching Fighter1's style — Meshy Image-to-3D input |
| `female-fighter2-street-concept.jpg` | AI concept: female street brawler matching Fighter2's style — Meshy Image-to-3D input |

The style renders were produced with `tools/render-character/` (serves
`wwwroot` locally, loads a GLB with the vendored three.js, screenshots it
with headless Chrome via Playwright).

## Meshy steps (web app)

1. **Image to 3D**: upload one concept image. Pick the quality tier you are
   happy spending credits on; the concepts are front-facing A-pose on a plain
   background, which is the input Meshy handles best.
2. Inspect the mesh preview from all sides before accepting — rear detail is
   invented by the model, so regenerate if the back of the head/clothes look
   wrong.
3. **Rig**: run Meshy's auto-rigging on the accepted mesh (humanoid preset).
   No need to generate animations in Meshy — the game retargets its own poker
   animation catalogue (sit idles, bet, check, fold, win/lose) onto the rig.
4. **Export as GLB** (with skeleton) and drop the file into the repo or attach
   it to the agent chat.

## Integration (repo side, once a rigged GLB exists)

- Adapt the retarget pipeline (`tools/bake_street_scene.py`) to the Meshy
  skeleton's bone names and rest pose — the Arcade Fighters bone map
  (`RENAME`) will not match a Meshy rig.
- Strip bone *positions* from the retargeted clips so the female proportions
  survive the male-authored animations — same trick `stripClipPositions`
  already does for CowboyGirl in `poker-scene.js`.
- Add the new GLB(s) to `FIGHTER_URLS` in `poker-scene.js` and record the
  Meshy generation in `ASSET_LICENSES.md`.
