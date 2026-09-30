# Mighty Cat → Red Dead Texas Hold'em (web)

Pack reference: [animation viewer](https://anims.themightycat.com/?asset=poker-blackjack). Rebuild torso moods: `blender --background --python tools/bake_mighty_cat_seated.py`.

## What runs in the browser today

| Game event | Clip source | Notes |
|---|---|---|
| Seated idle | **Jacob** `Sit` | Static authored sit — arms correct on this rig |
| Check / bet / call / raise / all-in / fold | **Jacob** `Check`, `Bet`, `Fold`, `FoldShake` (player) | UAL clips retargeted for Jacob in `Jacob.glb` |
| Talk (voice line) | **Jacob** `Talk` | |
| Win / lose / wait / neutral moods | **Mighty Cat** torso retarget in `mighty-cat.glb` | Spine, neck, head only — **arms stay on Sit** |
| Mood fallback if GLB missing | **Jacob** `Attack` / `Fold` / `Talk` | |

## Pack FBX mapping (for future full-body retarget)

| Source FBX | Intended use | Web status |
|---|---|---|
| `AS_Poker_Player_02` | Check | Needs IK retarget — use Jacob `Check` |
| `AS_Poker_Player_01` | Bet / call / raise | Use Jacob `Bet` |
| `AS_Poker_Player_03` | Fold | Use Jacob `Fold` |
| `AS_Reactions_Player*_Win/Lose/Neutral/WaitingOthers` | Moods | Torso-only in `mighty-cat.glb` |

Full-body Mighty Cat poker clips share Epic/MetaHuman rest poses that do **not** match Jacob's seated bind; applying arm rotation deltas twists limbs. Do not rebake arms onto Jacob until a proper IK/seated retarget pass exists.
