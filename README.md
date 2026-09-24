# Cinematic Poker

A **single-player, offline-first, cinematic Texas Hold'em game** for iOS, iPadOS and Android, built with **Unity 6, C# and URP**.

> Play poker with believable characters, anywhere, in extraordinary places.

The player competes exclusively against NPCs — no real-money wagering, no multiplayer, no account, no server. Poker supplies the strategy; NPCs supply the personality; environments supply the fantasy; offline play supplies the convenience.

## Current status — Phase 1 complete, Phase 2/3 scaffolded

| Phase | Scope | Status |
|---|---|---|
| 1 | Pure poker engine, AI framework, tests, 10,000-hand simulation | **Done, all tests passing** |
| 2 | Primitive prototype scene (table plane, capsule NPCs, touch UI) | Scripts scaffolded; scene authoring next |
| 3 | Mates' Kitchen vertical slice | Architecture in place (profiles, dialogue, tells, coaching, save) |

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

## Opening in Unity

1. Open the project with **Unity 6000.0.32f1** (or newer 6000.x). Packages (URP, Cinemachine, Addressables, Animation Rigging, Input System, Test Framework) restore from `Packages/manifest.json`.
2. Run **Cinematic Poker → Generate Placeholder Environment Definitions** to create the eleven catalogue assets (Mates' Kitchen … Post-war WA 2041).
3. Phase 2 next step: author the `PokerPrototype` scene — a table plane, six seat anchors, capsule NPCs — and wire `PokerSessionBootstrap`, `PokerTableController`, `PokerTableView`, `ActionPanel`, `PokerHUD`.

## Content & licensing rules

- No assets, characters, dialogue, music, logos or UI from third-party IP (RDR2, Star Wars, Playboy, James Bond, etc.). Genre inspiration only.
- Licensed/CC0 sources only (Unity Asset Store, Poly Haven, Mixamo for prototyping); maintain an asset licence register before importing final art.
- Monetisation direction: sell environment packs, never chips. No real-money wagering, no cash-out.

## Roadmap

Phase 2 primitive prototype → Phase 3 **Mates' Kitchen vertical slice** (the product validation milestone: five believable NPCs at one cheap table must stay entertaining for 15–30 minutes) → Western 1860s → Kalgoorlie Shed → premium packs (Chicago 1926, Vegas, Disco 1978, Velvet Mansion, Tokyo, Desert Camp, Spaceport, Post-war WA).
