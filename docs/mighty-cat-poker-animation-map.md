# Mighty Cat → Red Dead Texas Hold'em (web)

Clips are retargeted from [The Mighty Cat Poker/Blackjack pack](https://anims.themightycat.com/?asset=poker-blackjack) onto Jacob's seated pose using **parent-local rotation deltas** (same Epic bone names as Jacob's upper body). Finger chains stay on Jacob's sit pose so hand meshes do not crumple. Rebuild: `blender --background --python tools/bake_mighty_cat_seated.py` (set `MIGHTY_CAT_SRC` to extracted pack FBX folder).

## Game events → GLB clip

| Game event | `pokerScene` API | Clip in `mighty-cat.glb` | Source FBX |
|---|---|---|---|
| Seated idle (loop) | mixer `IdleLoop` | `IdleLoop` | `AS_Reactions_Player01_Neutral_02` |
| Check | `action(seat, "check")` | `Check` | `AS_Poker_Player_02` |
| Post blind / open bet | `action(seat, "bet")` | `Bet` | `AS_Poker_Player_01` |
| Call | `action(seat, "call")` | `Call` | `AS_Poker_Player_01` (shorter window) |
| Raise | `action(seat, "raise")` | `Raise` | `AS_Poker_Player_01` |
| All-in | `action(seat, "allin")` | `AllIn` | `AS_Poker_Player_01` |
| Fold | `action(seat, "fold")` | `Fold` | `AS_Poker_Player_03` |
| Win (small pot) | `emotion(seat, "win", low)` | `WinSmall` | `AS_Reactions_Player01_Win_03` |
| Win (big pot) | `emotion(seat, "win", high)` | `WinBig` / `WinBigAlt` | Player01/02 Win |
| Lose | `emotion(seat, "lose", …)` | `LoseSmall` / `LoseBig` / `LoseBigAlt` | Player01/02/03 Lose |
| Waiting on others | `emotion(seat, "wait", …)` | `Wait` / `WaitHot` | WaitingOthers |
| Idle fidget | `tickFidget` → neutral | `Neutral` / `NeutralHot` | Neutral takes |
| Player pot win | `celebrate()` | `WinBig` (+ fireworks) | same as win |
| Bust | `update` when `out` | `LoseBig` or Jacob `Crouch` fallback | reactions |

Voice line sync still uses Jacob `Talk` from `Jacob.glb` (UAL retarget).
