# Mighty Cat → Red Dead Texas Hold'em (web)

Pack: [Unity Asset Store](https://assetstore.unity.com/packages/3d/animations/poker-blackjack-and-card-games-animation-pack-311361) · [animation viewer](https://anims.themightycat.com/?asset=poker-blackjack)

Rebuild: `blender --background --python tools/bake_mighty_cat_seated.py` (`MIGHTY_CAT_SRC` = extracted FBX folder).

Retarget is **character-space IK**, not bone-quaternion copy. Jacob and the pack do not share arm bind axes; copying local rotations twisted the left arm behind the back. Hands move as offsets from Jacob’s sit, in a chest/clavicle facing frame, then two-bone IK aims the arms. Fingers get damped rest-relative curls. Legs stay on Jacob `Sit`.

## Runtime mapping

Each seat picks **one idle at random** and loops it. Wins and losses pick **one take at random**.

| Game event | Clip in `mighty-cat.glb` | Viewer / FBX |
|---|---|---|
| Idle (random) | `IdleBlackjack01` | Blackjack Player 01 / `AS_Blackjack_Player_01` |
| Idle (random) | `IdleBlackjack02` | Blackjack Player 02 / `AS_Blackjack_Player_02` |
| Idle (random) | `IdleBlackjack03` | Blackjack Player 03 / `AS_Blackjack_Player_03` |
| Idle (random) | `IdlePoker01` | Poker Player 01 / `AS_Poker_Player_01` |
| Idle (random) | `IdlePoker02` | Poker Player 02 / `AS_Poker_Player_02` |
| Idle (random) | `IdlePoker03` | Poker Player 03 / `AS_Poker_Player_03` |
| Winner (random) | `Win01` | Reactions Player01 Win 01 |
| Winner (random) | `Win02` | Reactions Player01 Win 02 |
| Winner (random) | `Win03` | Reactions Player01 Win 03 |
| Loser (random) | `Lose01` | Reactions Player01 Lose 01 |
| Loser (random) | `Lose02` | Reactions Player01 Lose 02 |
| Loser (random) | `Lose03` | Reactions Player02 Lose 01 |
| Loser (random) | `Lose04` | Reactions Player02 Lose 02 |
| Check / bet / fold | Jacob `Check` / `Bet` / `Fold` | UAL takes already on `Jacob.glb` |

Check/bet/fold stay on Jacob until a dedicated IK bake of those gestures exists.
