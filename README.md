# Cinematic Poker

A **single-player, offline-first, cinematic Texas Hold'em game** for iOS, iPadOS and Android, built with **Unity 6, C# and URP**.

> Play poker with believable characters, anywhere, in extraordinary places.

The player competes exclusively against NPCs — no real-money wagering, no multiplayer, no account, no server. Poker supplies the strategy; NPCs supply the personality; environments supply the fantasy; offline play supplies the convenience.

## Current status — Phase 1 complete, Phase 2/3 scaffolded

| Phase | Scope | Status |
|---|---|---|
| 1 | Pure poker engine, AI framework, tests, 10,000-hand simulation | **Done, all tests passing** |
| 2 | Primitive prototype: playable table, capsule NPCs, touch UI, Quick/Cinematic toggle, Android build | **Done (code-generated scene; see below)** |
| — | **Shareable web version** (Blazor WebAssembly running the same engine) | **Done — auto-deploys to GitHub Pages** |
| 3 | Mates' Kitchen vertical slice | Architecture in place (profiles, dialogue, tells, coaching, save) |

## Play it in the browser

A playable web version lives in `Web/CinematicPoker.Web` — a Blazor WebAssembly app that compiles **the exact same pure C# engine** the Unity game and the test suite use, so the poker rules and the five NPC personalities are identical. It runs entirely client-side (offline once loaded, nothing to install) and works on desktop and mobile browsers.

- **Live link (once merged to `main` and GitHub Pages is enabled):** `https://simmo76.github.io/red-dead-texas-holdem/`
- **Private table**: the page is gated by a table password (salted SHA-256 checked client-side, remembered per device via localStorage) and carries `noindex` so search engines skip it. Share the link *and* the password only with invited players. To change the password, generate a new hash with the command in the comment above `AccessHash` in `Pages/Home.razor` and replace the constant — never commit the plaintext password. This keeps strangers out; it is not strong security (the game holds nothing sensitive). For real per-person access control, put the site behind an auth proxy such as Cloudflare Access.
- Deployment is automatic: `.github/workflows/deploy-web.yml` runs the engine tests, publishes the app and deploys it to GitHub Pages on every push to `main`. One-time setup: repo **Settings → Pages → Source: GitHub Actions** (the workflow also attempts to enable this automatically), and the repo must be public (or on a plan with private Pages).

Run it locally:

```bash
cd Web/CinematicPoker.Web
dotnet run          # then open the printed http://localhost:xxxx URL
```

## Architecture

The poker rules are a **pure C# library** with no Unity dependency — no MonoBehaviours, GameObjects, scenes, cameras, animation or UI. The Unity layer is presentation only and observes the engine through events.

```
Assets/Scripts/
  Engine/                     CinematicPoker.Engine.asmdef (noEngineReferences: true)
    Poker/                    Card, Deck, HandEvaluator, PokerGame, PokerRound,
                              BettingRound, Pot, SidePot, TableRules, events
    Players/                  PokerPlayer, HumanPlayer, AIPlayer, PlayerState
    AI/                       PokerDecisionEngine, AIProfile, EquityCalculator,
                              BluffEvaluator, PlayerModel, TiltState, DecisionContext
    Simulation/               SimulationRunner (10,000-hand invariant soak)
  Game/                       CinematicPoker.Game.asmdef (Unity presentation layer)
    Presentation/             PokerTableController (engine↔Unity bridge),
                              PokerTableView, CardView, ChipView, DealerController,
                              PlayModeSettings (Quick vs Cinematic), PokerSessionBootstrap
    Characters/               IPokerCharacter, NPCBehaviourController,
                              CharacterAnimationController, CharacterLookController,
                              ReactionController (probabilistic tells)
    Environments/             PokerEnvironmentDefinition + style profiles,
                              PokerEnvironmentManager, EnvironmentEventDirector
    Cameras/                  PokerCameraController (seated presence + limited cuts)
    UI/                       PokerHUD, ActionPanel, BetSlider, HandReviewUI, SafeAreaFitter
    Coaching/                 HandHistoryRecorder, CoachingService ("Improve My Game")
    Audio/                    PokerAudioController (spatialised voices, table foley)
    Mobile/                   PerformanceManager (Battery Saver 30 FPS … Ultra)
    Save/                     SaveSystem (atomic offline JSON), SaveData
    Profiles/                 AIProfileAsset, NPCProfileAsset, DialogueProfile,
                              TellProfileAsset (ScriptableObjects)
  Editor/                     Placeholder environment catalogue generator
Assets/Tests/EditMode/        NUnit tests (run in Unity Test Runner AND dotnet)
DotNet/                       Standalone solution compiling the same engine sources
```

### Key design rules (enforced by the architecture)

- **Event-driven presentation.** The engine emits `CardDealt`, `BetPlaced`, `PlayerRaised`, `FlopDealt`, `ShowdownStarted`, `PotAwarded`, `HandCompleted`… Presentation subscribes; an animation finishing never determines whether a poker action was legally completed.
- **Poker brain ≠ character brain.** `AIPlayer` (decisions) is separate from `NPCBehaviourController` (emotion, gaze, dialogue, reactions, memory). The character layer may react to events but can never modify cards, bets, pots or legal state.
- **No LLMs in poker decisions.** NPC decisions are deterministic/probabilistic (Monte Carlo equity + personality parameters + human-like noise). The `INpcDialogueProvider` interface leaves room for LLM banter later — dialogue only, never game state.
- **Environments are data.** `PokerEnvironmentDefinition` (ScriptableObject) carries the NPC pool, difficulty, dialogue, tells, styles, cameras, audio and ambient events. Adding an environment never touches the engine, and shared gameplay code never branches on environment id. Designed for 50+ environments and the future Impossible Table mode.
- **Tells are probabilistic.** `TellProfileAsset` uses `tellProbability`, `falseTellProbability` and context sensitivity. `scratch nose == bluff` cannot exist.
- **Offline-first.** No account, no matchmaking, atomic on-device saves, all AI on device (Monte Carlo runs on a worker thread with device-tiered iteration counts).

## Engine features

- 2–6 players, exactly one human
- Blinds (with short-stack all-in posting), dealer rotation, heads-up rules (dealer = SB, acts first preflop, last postflop)
- Check / call / bet / raise / fold / all-in with full legality validation (`IllegalActionException`)
- Min-raise tracking, big-blind option, uncalled-bet return
- Split pots (odd chips to the earliest seat left of the button) and layered side pots
- All ten hand categories including the wheel and kicker-exact comparisons
- Fisher-Yates shuffle with deterministic seeding; `StackedDeck` for rigged test scenarios
- Eliminations and session lifecycle
- AI: 12-parameter `AIProfile` (skill, aggression, tightness, bluff frequency, tilt sensitivity…), Monte Carlo `EquityCalculator`, `TiltState` emotional model, `PlayerModel` opponent tracking (VPIP, PFR, 3-bet, fold-to-raise…), `BluffEvaluator`

## Running the tests (no Unity required)

The `DotNet/` solution compiles the exact same engine and test sources Unity uses:

```bash
cd DotNet
dotnet test                          # full suite incl. the 10,000-hand simulation
dotnet run --project CinematicPoker.Simulation -- 10000 42 60   # soak runner: hands, seed, equity iters
```

In Unity, the same tests appear in **Window → General → Test Runner (EditMode)**.

The simulation asserts on every hand: no exceptions, no deadlocks, no duplicate cards, no negative stacks, correct turn order, and

```
TOTAL MONEY BEFORE HAND == TOTAL MONEY AFTER HAND
```

## Playing the Phase 2 prototype

The prototype scene is **generated entirely at runtime** (`Assets/Scripts/Game/Prototype/`): a green table, five colour-coded capsule NPCs (Davo, Mick, Shazza, Bluey, Kev — maniac, regular, rock, calling station and pro), 3D community/hole cards, dealer button, turn indicator, an action log, and a touch HUD with Fold/Check/Call/Bet/Raise/All-In, a raise slider (½ Pot / Pot / Max / Confirm) and a Quick/Cinematic mode toggle. The .unity file on disk is deliberately empty, so nothing can break through serialized references.

1. Open the project with **Unity 6000.0.32f1** (or newer 6000.x). Packages restore from `Packages/manifest.json` on first open.
2. Press **Play in any empty scene** (e.g. File → New Scene → Empty) — the prototype bootstraps automatically. Or run **Cinematic Poker → Create Prototype Scene** once to create `Assets/Scenes/PokerPrototype.unity` and play that.
3. Optional: **Cinematic Poker → Generate Prototype AI Profiles** and **… → Generate Placeholder Environment Definitions** create the tweakable ScriptableObject assets.

## Building the Android APK

In the editor: install Android Build Support via Unity Hub, then **Cinematic Poker → Build → Android APK**. The APK lands in `Builds/Android/CinematicPoker.apk` (IL2CPP, ARM64+ARMv7, landscape, min SDK 23).

Headless/CI:

```bash
unity -batchmode -quit -projectPath . -buildTarget Android \
      -executeMethod CinematicPoker.Editor.BuildScript.BuildAndroid
```

GitHub Actions: `.github/workflows/android-apk.yml` runs the engine tests and then builds the APK with [GameCI](https://game.ci), uploading it as the `CinematicPoker-Android` artifact. It needs Unity licence secrets in the repo (`UNITY_LICENSE`, or `UNITY_EMAIL` + `UNITY_PASSWORD`) — see [game.ci/docs/github/activation](https://game.ci/docs/github/activation).

## Content & licensing rules

- No assets, characters, dialogue, music, logos or UI from third-party IP (RDR2, Star Wars, Playboy, James Bond, etc.). Genre inspiration only.
- Licensed/CC0 sources only (Unity Asset Store, Poly Haven, Mixamo for prototyping); maintain an asset licence register before importing final art.
- Monetisation direction: sell environment packs, never chips. No real-money wagering, no cash-out.

### Bundled CC0 assets

The prototype and web version use free CC0 game assets — every file is recorded in [`ASSET_LICENSES.md`](ASSET_LICENSES.md):

- **Kenney Boardgame Pack** (card faces, card backs, poker chips) and **Kenney Casino Audio** (shuffle, deal, chip sounds) — [kenney.nl](https://kenney.nl), CC0.
- **Kenney Mini Characters** — five rigged, animated 3D characters (idle/sit/emote-yes/emote-no/die clips embedded in the FBX) used as the NPCs, and **Kenney Furniture Kit** — chairs, ceiling lamp, rug, potted plant, radio for set dressing. CC0.
- **Poly Haven** fabric and wood textures (table felt and rail) — [polyhaven.com](https://polyhaven.com), CC0.

In Unity they live under `Assets/Resources/Poker/` and are loaded at runtime by `PrototypeAssets` (card quads get real faces, NPCs get face-down cards, the table gets felt/wood, and engine events trigger casino audio). The NPCs are Kenney mini-characters seated on furniture-kit chairs: `PrototypeCharacter` drives them with the Playables API directly (no AnimatorController asset needed) — a held frame of the `sit` clip as the seated pose, `emote-yes` when they win a pot, `emote-no` when they fold, and `die` when they bust out. Everything is null-safe: delete the folder and the prototype falls back to its primitive-and-text look (capsule NPCs included).

## Roadmap

Phase 2 primitive prototype → Phase 3 **Mates' Kitchen vertical slice** (the product validation milestone: five believable NPCs at one cheap table must stay entertaining for 15–30 minutes) → Western 1860s → Kalgoorlie Shed → premium packs (Chicago 1926, Vegas, Disco 1978, Velvet Mansion, Tokyo, Desert Camp, Spaceport, Post-war WA).
