# Cinematic Poker — Project Task History

A hand-off summary of everything done on this project so far, intended as context for an AI assistant (e.g. Claude).

- **Git repository:** https://github.com/Simmo76/red-dead-texas-holdem
- **Live web build (GitHub Pages):** https://simmo76.github.io/red-dead-texas-holdem/
- **Project:** *Cinematic Poker* — a single-player, offline-first, cinematic Texas Hold'em game for iOS/iPadOS/Android built with Unity 6, C# and URP, plus a shareable Blazor WebAssembly / Three.js web version running the exact same pure C# poker engine. The player (James) competes against five NPCs (Simon, Steve, Tom, Nick, Pete); no real-money wagering, no multiplayer, no server.

## Phase overview

| Phase | Scope | Status |
|---|---|---|
| 1 | Pure poker engine, AI framework, NUnit tests, 10,000-hand invariant simulation | Done, all tests passing |
| 2 | Playable prototype: runtime-generated table, touch HUD, Android build pipeline | Done |
| — | Shareable web version (same engine, auto-deploys to GitHub Pages) | Done, continuously iterated |
| 3 | "Mates' Kitchen" vertical slice | Architecture in place |

## Completed tasks (merged pull requests, oldest first)

| PR | Task |
|---|---|
| [#1](https://github.com/Simmo76/red-dead-texas-holdem/pull/1) | Cinematic Poker foundation: poker engine + 10k-hand simulation (Phase 1), playable prototype with Android pipeline (Phase 2), and shareable web version |
| [#2](https://github.com/Simmo76/red-dead-texas-holdem/pull/2) | Folded players grey out and turn semi-transparent for the rest of the hand |
| [#3](https://github.com/Simmo76/red-dead-texas-holdem/pull/3) | Remove bottom HUD shadow and fix clipped action buttons on mobile |
| [#4](https://github.com/Simmo76/red-dead-texas-holdem/pull/4) | Pace showdown reveals so the last card lands before the win is announced |
| [#5](https://github.com/Simmo76/red-dead-texas-holdem/pull/5) | Make folded players more opaque while keeping the grey look |
| [#6](https://github.com/Simmo76/red-dead-texas-holdem/pull/6) | Skip opening camera intro on game start |
| [#7](https://github.com/Simmo76/red-dead-texas-holdem/pull/7) | Fix Jimmy shirt shading and chair alignment at the table |
| [#8](https://github.com/Simmo76/red-dead-texas-holdem/pull/8) | Fix shirt black patches and chair seat alignment (follow-up to #7) |
| [#9](https://github.com/Simmo76/red-dead-texas-holdem/pull/9) | Fix folded player ghost bodies hidden behind table depth |
| [#10](https://github.com/Simmo76/red-dead-texas-holdem/pull/10) | Fix invisible holes in folded ghost player rendering |
| [#11](https://github.com/Simmo76/red-dead-texas-holdem/pull/11) | Seat a random Meshy character at each NPC chair |
| [#12](https://github.com/Simmo76/red-dead-texas-holdem/pull/12) | Add ordered hand/win markdown logs for math analysis |
| [#13](https://github.com/Simmo76/red-dead-texas-holdem/pull/13) | Add CSV export of player stats via email or text |
| [#15](https://github.com/Simmo76/red-dead-texas-holdem/pull/15) | Play seated card-game reactions from NPC mood |
| [#16](https://github.com/Simmo76/red-dead-texas-holdem/pull/16) | Fit the arcade backdrop around the table and ground it at street level |
| [#17](https://github.com/Simmo76/red-dead-texas-holdem/pull/17) | Brighten the arcade street so the backdrop reads at night |
| [#18](https://github.com/Simmo76/red-dead-texas-holdem/pull/18) | Add dealer, small blind and big blind button chips to the 3D table |
| [#19](https://github.com/Simmo76/red-dead-texas-holdem/pull/19) | Shrink the game log backdrop to fit its text |
| [#20](https://github.com/Simmo76/red-dead-texas-holdem/pull/20) | Export a real .csv file and add coach-style session analysis |
| [#21](https://github.com/Simmo76/red-dead-texas-holdem/pull/21) | Tapping the community-card HUD snaps the camera back to the seat view |
| [#22](https://github.com/Simmo76/red-dead-texas-holdem/pull/22) | Map Mighty Cat idle, win, and lose clips onto Jacob |
| [#23](https://github.com/Simmo76/red-dead-texas-holdem/pull/23) | Retarget Mighty Cat poker animations onto Jacob |
| [#28](https://github.com/Simmo76/red-dead-texas-holdem/pull/28) | Remove dealer voice pack audio clips and callouts |
| [#29](https://github.com/Simmo76/red-dead-texas-holdem/pull/29) | Add fast-forward button to skip NPC actions after folding |
| [#30](https://github.com/Simmo76/red-dead-texas-holdem/pull/30) | Remove the on-screen action history/log window |
| [#31](https://github.com/Simmo76/red-dead-texas-holdem/pull/31) | Announce winning hand when the player wins a showdown |
| [#32](https://github.com/Simmo76/red-dead-texas-holdem/pull/32) | Make the actor stencil outline twice as thick |
| [#33](https://github.com/Simmo76/red-dead-texas-holdem/pull/33) | Grey transparent folded players with hands on head |
| [#34](https://github.com/Simmo76/red-dead-texas-holdem/pull/34) | Enlarge action buttons and split Fold to the left |
| [#35](https://github.com/Simmo76/red-dead-texas-holdem/pull/35) | Hold folded players on the arms-folded sit idle |
| [#37](https://github.com/Simmo76/red-dead-texas-holdem/pull/37) | Replace Kokoro table voices with outlaw win/lose/banter packs |
| [#38](https://github.com/Simmo76/red-dead-texas-holdem/pull/38) | Replace saloon music with rowdy ragtime bar ambience |
| [#39](https://github.com/Simmo76/red-dead-texas-holdem/pull/39) | Rename the six-handed table to James, Simon, Steve, Tom, Nick and Pete |
| [#40](https://github.com/Simmo76/red-dead-texas-holdem/pull/40) | Lock the table to landscape and freeze the over-the-shoulder seat camera (with 10° orbit tweaks) |
| [#44](https://github.com/Simmo76/red-dead-texas-holdem/pull/44) | Zoom to player's cards with live chip-stack preview while sizing a bet |
| [#45](https://github.com/Simmo76/red-dead-texas-holdem/pull/45) | Hold the win/lose result until the player taps NEXT HAND |
| [#46](https://github.com/Simmo76/red-dead-texas-holdem/pull/46) | Enlarge remaining-stack HUD and seat money labels |
| [#47](https://github.com/Simmo76/red-dead-texas-holdem/pull/47) | Show win amount and pause on opponent hole cards |
| [#48](https://github.com/Simmo76/red-dead-texas-holdem/pull/48) | Space out NPC table talk so sayings do not repeat back-to-back |
| [#49](https://github.com/Simmo76/red-dead-texas-holdem/pull/49) | Keep bottom buttons after NEXT HAND when the game is over |
| [#50](https://github.com/Simmo76/red-dead-texas-holdem/pull/50) | Ask first-time players for a name and remember it |
| [#51](https://github.com/Simmo76/red-dead-texas-holdem/pull/51) | Persist the player name on this device across visits |
| [#52](https://github.com/Simmo76/red-dead-texas-holdem/pull/52) | Live-services & monetisation plan plus offline telemetry and player identity foundation |
| [#53](https://github.com/Simmo76/red-dead-texas-holdem/pull/53) | Add "What would AI do?" advisor window with Maximilian's card-counting advice on the player's turn |

Closed without merging: [#24](https://github.com/Simmo76/red-dead-texas-holdem/pull/24) (sit-thinking idle for every player — a reworked version landed on main) and [#25](https://github.com/Simmo76/red-dead-texas-holdem/pull/25) (random seated Cocomotion idles).

## Open pull requests (not yet merged)

| PR | Task |
|---|---|
| [#14](https://github.com/Simmo76/red-dead-texas-holdem/pull/14) | Fix CSV export email, text, and download failures |
| [#26](https://github.com/Simmo76/red-dead-texas-holdem/pull/26) | Go-to-market plan: RDR2-itch niche, no-real-money monetisation, $1,000 launch |
| [#27](https://github.com/Simmo76/red-dead-texas-holdem/pull/27) | Strategy v3 PDF (validation-first plan superseding marketing-plan v2) |
| [#36](https://github.com/Simmo76/red-dead-texas-holdem/pull/36) | End the session when the player busts |
| [#41](https://github.com/Simmo76/red-dead-texas-holdem/pull/41) | Show the deciding high card when showdown hands match |
| [#42](https://github.com/Simmo76/red-dead-texas-holdem/pull/42) | Show GAME OVER and Restart when James is busted |
| [#43](https://github.com/Simmo76/red-dead-texas-holdem/pull/43) | Use the Hand-Painted Forest Lite scene as the BEACH backdrop |

## Work committed directly to main (no PR), grouped by theme

**Foundation (24 Sept)**
- Unity 6 project skeleton (URP, Cinemachine, Addressables, Input System).
- Pure C# Texas Hold'em engine: cards, hand evaluator, betting, side pots, events, NPC AI (Monte Carlo equity, 12-parameter personality profiles, tilt model, opponent modelling).
- NUnit EditMode test suite and 10,000-hand invariant simulation (money conservation, no duplicate cards, legal turn order).
- Unity presentation layer: NPC characters, environments, UI, coaching, save system, mobile performance tiers.
- Phase 2 prototype: runtime-generated playable table with capsule NPCs and touch HUD; Android/Windows build scripts and CI APK workflow.
- Shareable web version: Blazor WASM app running the same engine, auto-deployed to GitHub Pages; iPhone/PWA support; password gate added and later removed (link-only sharing).
- CC0 asset integration (Kenney cards/chips/audio/characters/furniture, Poly Haven textures) with a licence register.

**3D web table and characters (24–30 Sept)**
- Rebuilt the web visuals as a first-person 3D saloon scene in Three.js with an over-the-shoulder seated camera, drag look-around, pinch zoom and two-finger pan.
- Character iterations: Quaternius western characters → licensed Arcade Fighters → owner-licensed Jimmy Lite soldier at all six seats → random Meshy characters per NPC chair, with scale, seating, chair-alignment and face fixes.
- Seated idle/reaction animation work: retargeted CC0 sitting idles, breathing/head-drift motion, action gestures for check/fold, Mighty Cat animation retargeting onto Jacob (several fix passes), NPC head-spin fixes, lose/bust slump recovery.
- Cards: held in hand then laid flat at real size, tap/press to zoom, z-fighting fixes, unlit rendering for readability.
- Backdrops and audio: cyberpunk arcade street, beach (owner photo panorama), desert, shed, space station and dance club backdrops with a cycle button and per-backdrop music; saloon music; Kokoro-82M TTS voice lines (40 regenerated for natural prosody, later replaced by outlaw voice packs via PR #37); iOS-audible chatter via MP3 + WebAudio.
- HUD: RDR2-style top-right community-card strip, player hole cards under the board, click-to-expand sizing passes, turn indicator with gold inverted-hull outline on the actor.
- Polish: fireworks/sparklers on player wins, chip-arc animations into the pot, player-win camera close-up, AI difficulty settings (easy/normal/hard), 1-second minimum NPC turn pacing, opening intro movie (title card, desert and club sweeps) later skipped by PR #6.

## Current state and roadmap

All engine tests pass (run `dotnet test` in `DotNet/`). The web version auto-deploys from `main`. Next milestone is the Phase 3 "Mates' Kitchen" vertical slice (five believable NPCs at one table staying entertaining for 15–30 minutes), then themed environment packs (Western 1860s, Kalgoorlie Shed, Chicago 1926, Vegas, and others). Monetisation direction: sell environment packs, never chips — see `docs/live-services-and-monetisation-plan.md`.
