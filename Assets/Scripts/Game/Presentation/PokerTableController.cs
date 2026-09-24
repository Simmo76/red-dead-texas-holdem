using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using CinematicPoker.Engine.AI;
using CinematicPoker.Engine.Players;
using CinematicPoker.Engine.Poker;
using CinematicPoker.Game.Profiles;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CinematicPoker.Game.Presentation
{
    /// <summary>
    /// The bridge between the pure C# engine and the Unity scene.
    ///
    /// Responsibilities:
    /// - owns the PokerGame session (exactly one human + 1-5 NPCs)
    /// - re-broadcasts engine events to presentation systems on the main thread
    /// - runs NPC decisions (Monte Carlo equity) on a worker thread, never
    ///   blocking the render loop
    /// - paces the game according to Quick/Cinematic PlayModeSettings
    ///
    /// It never decides poker legality itself: animation finishing must never
    /// determine whether a poker action was legally completed.
    /// </summary>
    public sealed class PokerTableController : MonoBehaviour
    {
        [SerializeField] private PlayModeSettings playModeSettings;
        [SerializeField] private PlayMode playMode = PlayMode.Cinematic;

        [Header("Device tier equity iterations (low/mid/high)")]
        [SerializeField] private int lowTierIterations = 200;
        [SerializeField] private int midTierIterations = 500;
        [SerializeField] private int highTierIterations = 1500;

        public PokerGame Game { get; private set; }
        public PlayMode Mode { get => playMode; set => playMode = value; }

        /// <summary>Inject settings from code (used by the runtime-generated prototype scene).</summary>
        public void Configure(PlayModeSettings settings) => playModeSettings = settings;

        private void Awake()
        {
            if (playModeSettings == null)
                playModeSettings = ScriptableObject.CreateInstance<PlayModeSettings>();
        }

        /// <summary>Presentation systems (views, NPCs, audio, cameras, UI) subscribe here.</summary>
        public event Action<PokerEvent> EngineEvent;

        /// <summary>Raised when the human must act, with their legal actions.</summary>
        public event Action<LegalActions> HumanTurnStarted;

        public event Action HandFinished;
        public event Action SessionFinished;

        private readonly Queue<PokerEvent> _pendingEvents = new Queue<PokerEvent>();
        private bool _awaitingHuman;
        private Coroutine _loop;

        public void StartSession(TableRules rules, HumanPlayer human, IReadOnlyList<AIPlayer> npcs, int? seed = null)
        {
            var players = new List<PokerPlayer> { human };
            players.AddRange(npcs);

            StopSession();
            _pendingEvents.Clear();
            _awaitingHuman = false;

            Game = new PokerGame(rules, players, seed);
            Game.EventEmitted += evt => _pendingEvents.Enqueue(evt);

            _loop = StartCoroutine(GameLoop());
        }

        /// <summary>Restore a session created by SaveSystem (stacks already applied).</summary>
        public void ResumeSession(PokerGame game)
        {
            StopSession();
            _pendingEvents.Clear();
            _awaitingHuman = false;

            Game = game;
            Game.EventEmitted += evt => _pendingEvents.Enqueue(evt);
            _loop = StartCoroutine(GameLoop());
        }

        public void StopSession()
        {
            if (_loop != null) StopCoroutine(_loop);
            _loop = null;
        }

        /// <summary>Called by the ActionPanel when the human taps an action.</summary>
        public void SubmitHumanAction(PlayerAction action)
        {
            if (!_awaitingHuman)
            {
                Debug.LogWarning("Human action submitted out of turn; ignoring.");
                return;
            }

            try
            {
                Game.SubmitAction(action);
                _awaitingHuman = false;
            }
            catch (IllegalActionException ex)
            {
                // The UI should prevent this; if it happens, keep waiting.
                Debug.LogWarning($"Illegal human action rejected: {ex.Message}");
            }
        }

        private IEnumerator GameLoop()
        {
            while (true)
            {
                if (Game.IsSessionOver)
                {
                    FlushEvents();
                    SessionFinished?.Invoke();
                    yield break;
                }

                Game.StartHand();
                FlushEvents();

                while (Game.Phase == GamePhase.HandInProgress)
                {
                    PokerPlayer actor = Game.CurrentPlayer;

                    if (actor.IsHuman)
                    {
                        _awaitingHuman = true;
                        HumanTurnStarted?.Invoke(Game.GetLegalActions());
                        while (_awaitingHuman && Game.Phase == GamePhase.HandInProgress)
                            yield return null;
                    }
                    else
                    {
                        yield return NpcTurn((AIPlayer)actor);
                    }

                    FlushEvents();
                }

                HandFinished?.Invoke();
                yield return new WaitForSeconds(playModeSettings.NextHandDelay(playMode));
            }
        }

        private IEnumerator NpcTurn(AIPlayer npc)
        {
            // Personality-flavoured think time (pacing only, never legality).
            Vector2 range = playModeSettings.ThinkTime(playMode);
            float thinkTime = Random.Range(range.x, range.y);

            // Monte Carlo equity runs off the main thread.
            DecisionContext context = DecisionContext.ForCurrentActor(Game, EquityIterationsForDevice());
            Task<PlayerAction> decision = Task.Run(() => npc.DecideAction(context));

            float elapsed = 0f;
            while (!decision.IsCompleted || elapsed < thinkTime)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (decision.IsFaulted)
            {
                // Fail safe: never deadlock the table because one brain crashed.
                Debug.LogError($"NPC decision failed: {decision.Exception}");
                LegalActions legal = Game.GetLegalActions();
                Game.SubmitAction(legal.CanCheck ? PlayerAction.Check() : PlayerAction.Fold());
            }
            else
            {
                Game.SubmitAction(decision.Result);
            }
        }

        private int EquityIterationsForDevice()
        {
            // Simple heuristic tiering; refined later by Mobile/PerformanceManager.
            int memory = SystemInfo.systemMemorySize;
            if (memory >= 6000) return highTierIterations;
            if (memory >= 3500) return midTierIterations;
            return lowTierIterations;
        }

        private void FlushEvents()
        {
            while (_pendingEvents.Count > 0)
                EngineEvent?.Invoke(_pendingEvents.Dequeue());
        }
    }
}
