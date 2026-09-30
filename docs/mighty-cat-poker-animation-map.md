# Mighty Cat pack vs Jacob (web)

Pack: [Unity Asset Store](https://assetstore.unity.com/packages/3d/animations/poker-blackjack-and-card-games-animation-pack-311361) · [animation viewer](https://anims.themightycat.com/?asset=poker-blackjack)

## What runs now

Jacob plays **only clips authored on his own skeleton** in `Jacob.glb`:

| Event | Clip |
|---|---|
| Idle | `Sit` |
| Check / bet / call / raise / all-in | `Check` / `Bet` |
| Fold | `Fold` / `FoldShake` (player) |
| Win / lose fallback | `Attack` / `Fold` |

`mighty-cat.glb` is **not loaded**. Driving Jacob with the pack produced wrong poses (arms behind the back, splayed fingers, T-pose-like IK).

## Why the viewer takes do not play on Jacob

The pack is Epic/MetaHuman. Jacob’s bind **is already a seated cowboy pose** with different arm bone axes. There is no shared T-pose.

Tried and rejected:

1. World-space rotation deltas — left arm flipped 180°.
2. Parent-local deltas from sit — same twist, broken fingers.
3. Sit + hand-offset IK — looked like a frozen lap sit (wrong clip window + clamp).
4. Absolute character-space IK onto the pack’s table-hand positions — zombie/T-pose arms, not the viewer performances.

Those viewer clips (Blackjack/Poker Player 01–03, Player01 Win 01–03, Player01/02 Lose 01–02) will look like the viewer only if they run on the **pack’s own mannequin** (or after a Humanoid/muscle-space retarget in a DCC). They cannot be quaternion-copied onto Jacob.

## If we play the pack later

Sit the official Mighty Cat preview character at the table (same skeleton as the FBX), or retarget in Unity Humanoid / Mixamo onto a T-pose Jacob, then re-export. Do not bake local rotations from FBX onto `Jacob.glb` again.
