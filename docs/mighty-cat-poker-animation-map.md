# Mighty Cat pack vs Jacob (web)

Pack: [Unity Asset Store](https://assetstore.unity.com/packages/3d/animations/poker-blackjack-and-card-games-animation-pack-311361) · [animation viewer](https://anims.themightycat.com/?asset=poker-blackjack)

## What runs now

Jacob keeps his own `Check` / `Bet` / `Fold` / `FoldShake` clips. Every seat idles on Cocomotion `AS_Idle_Sit_Thinking_01` (`IdleSitThinking01`). Win and lose stay Mighty Cat takes. All mood clips are Humanoid-retargeted onto Jacob (`tools/bake_idle_sit_thinking.py`, `tools/bake_mighty_cat_humanoid.py` → `mighty-cat.glb`).

| Event | Clip |
|---|---|
| Idle | `IdleSitThinking01` (`AS_Idle_Sit_Thinking_01`) for every seat |
| Check / bet / call / raise / all-in | Jacob `Check` / `Bet` |
| Fold | Jacob `Fold` / `FoldShake` (player) |
| Win | random `Win01–03` (Player01 Win 01–03) |
| Lose | random `Lose01–04` (Player01 Lose 01–02, Player02 Lose 01–02) |

## How the retarget works

The pack is Epic/MetaHuman (A-pose rest, extra spine bones). Jacob's bind is already a seated cowboy pose with different arm rolls. Local-quaternion copy and world-space rotation deltas both twist the left arm.

The baker does **not** apply a new rest pose (that exploded the mesh). It:

1. Holds Jacob's authored sit on hips and legs so he stays in the chair.
2. Swings spine, neck, clavicles, arms, hands and fingers so each bone's child-aim matches the source bone's child-aim in a shared character-facing frame (clavicle-left × world-up).
3. Maps Jacob `spine_03` → pack `spine_05` and Jacob `neck_01` → pack `neck_02`.

That is Mixamo/Humanoid aim retarget: directions travel, Jacob's bone rolls (and skinning) stay his.

A Unity Editor menu (`Cinematic Poker → Bake Mighty Cat onto Jacob (Humanoid)`) is also in the project for a muscle-space bake when the Editor is available; the shipped web GLB is the Blender aim-copy.

## Tried and rejected

1. World-space rotation deltas — left arm flipped 180°.
2. Parent-local deltas from sit — same twist, broken fingers.
3. Sit + hand-offset IK — frozen lap sit or zombie arms.
4. Apply A-pose as rest, then copy takes — standing reach or spaghetti mesh.
