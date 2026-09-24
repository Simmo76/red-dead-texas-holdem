using System.Collections.Generic;
using CinematicPoker.Engine.AI;
using CinematicPoker.Engine.Players;
using CinematicPoker.Engine.Poker;
using CinematicPoker.Game.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CinematicPoker.Game.Prototype
{
    /// <summary>
    /// Auto-starts the Phase 2 primitive prototype. The whole scene is
    /// generated at runtime from primitives, so the .unity file stays empty
    /// (no fragile serialized references) and pressing Play in any empty
    /// scene — or launching the APK — drops you straight at the table.
    /// </summary>
    public static class PrototypeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            Scene scene = SceneManager.GetActiveScene();
            bool isPrototype = scene.name == "PokerPrototype" || string.IsNullOrEmpty(scene.name);
            if (!isPrototype) return;
            if (Object.FindFirstObjectByType<PokerPrototypeScene>() != null) return;
            new GameObject("PokerPrototypeScene").AddComponent<PokerPrototypeScene>();
        }
    }

    /// <summary>
    /// Phase 2 primitive prototype: one human seat and five capsule NPCs
    /// around a table, connected to the pure C# PokerEngine through
    /// PokerTableController, with a mobile-friendly HUD (Fold/Check/Call/
    /// Bet/Raise/All-In), hole cards, community cards, pot, stacks, current
    /// actor and dealer/blind indicators. No final character art — that is
    /// Phase 3.
    /// </summary>
    public sealed class PokerPrototypeScene : MonoBehaviour
    {
        private const int NpcCount = 5;
        private const float TableHeight = 0.74f;
        private const float SeatRadius = 1.7f;
        private static readonly Vector3 TableCentre = new Vector3(0f, TableHeight, 0f);

        private static readonly (string name, System.Func<AIProfile> profile)[] Roster =
        {
            ("Davo", AIProfile.Maniac),
            ("Mick", AIProfile.Regular),
            ("Shazza", AIProfile.Rock),
            ("Bluey", AIProfile.CallingStation),
            ("Kev", AIProfile.Professional)
        };

        public PokerTableController Controller { get; private set; }

        private readonly TextMesh[] _seatLabels = new TextMesh[NpcCount + 1];
        private readonly GameObject[] _boardCards = new GameObject[5];
        private readonly TextMesh[] _boardTexts = new TextMesh[5];
        private readonly GameObject[] _holeCards = new GameObject[2];
        private readonly TextMesh[] _holeTexts = new TextMesh[2];
        private GameObject _actorIndicator;
        private GameObject _dealerButton;
        private Camera _camera;
        private PrototypeHUD _hud;
        private Material _cardMaterial;
        private AudioSource _audio;
        private readonly GameObject[][] _npcBacks = new GameObject[NpcCount + 1][];

        private void Start()
        {
            BuildWorld();
            BuildController();
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _hud = new GameObject("PrototypeHUD").AddComponent<PrototypeHUD>();
            _hud.Build(this, Controller);
            StartNewSession();
        }

        // ------------------------------------------------------------ world

        private void BuildWorld()
        {
            _camera = Camera.main;
            if (_camera == null)
                _camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            _camera.tag = "MainCamera";
            _camera.transform.position = new Vector3(0f, 2.05f, -3.1f);
            _camera.transform.LookAt(TableCentre + Vector3.up * 0.1f);
            _camera.fieldOfView = 48f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.09f, 0.09f, 0.12f);

            if (FindFirstObjectByType<Light>() == null)
            {
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(55f, -25f, 0f);
                light.intensity = 1.15f;
            }

            GameObject floor = CreatePrimitive(PrimitiveType.Plane, "Floor",
                new Vector3(0f, 0f, 0f), new Vector3(2f, 1f, 2f), new Color(0.16f, 0.13f, 0.11f));
            PrototypeAssets.ApplyTexture(floor, PrototypeAssets.Wood, new Color(0.45f, 0.38f, 0.32f), new Vector2(6f, 6f));

            GameObject table = CreatePrimitive(PrimitiveType.Cylinder, "Table",
                new Vector3(0f, TableHeight - 0.03f, 0f), new Vector3(2.7f, 0.03f, 2.7f), new Color(0.05f, 0.35f, 0.16f));
            PrototypeAssets.ApplyTexture(table, PrototypeAssets.Felt, new Color(0.1f, 0.52f, 0.26f), new Vector2(2.5f, 2.5f));

            GameObject tableBase = CreatePrimitive(PrimitiveType.Cylinder, "TableBase",
                new Vector3(0f, TableHeight * 0.5f, 0f), new Vector3(0.4f, TableHeight * 0.5f, 0.4f), new Color(0.2f, 0.14f, 0.09f));
            PrototypeAssets.ApplyTexture(tableBase, PrototypeAssets.Wood, new Color(0.5f, 0.38f, 0.28f), new Vector2(1f, 1f));

            _cardMaterial = MakeMaterial(new Color(0.95f, 0.94f, 0.9f));

            Color[] npcColors =
            {
                new Color(0.85f, 0.3f, 0.25f),  // Davo
                new Color(0.25f, 0.5f, 0.85f),  // Mick
                new Color(0.8f, 0.65f, 0.2f),   // Shazza
                new Color(0.35f, 0.7f, 0.4f),   // Bluey
                new Color(0.6f, 0.4f, 0.75f)    // Kev
            };

            // Seat 0 is the human (camera side); seats 1..5 are capsule NPCs.
            for (int seat = 0; seat <= NpcCount; seat++)
            {
                Vector3 pos = SeatPosition(seat, SeatRadius);
                if (seat > 0)
                {
                    GameObject body = CreatePrimitive(PrimitiveType.Capsule, $"NPC_{seat}",
                        pos + Vector3.up * 0.62f, new Vector3(0.42f, 0.55f, 0.42f), npcColors[seat - 1]);
                    body.transform.LookAt(new Vector3(TableCentre.x, body.transform.position.y, TableCentre.z));
                    _npcBacks[seat] = CreateNpcBackCards(seat);
                }

                _seatLabels[seat] = CreateLabel($"SeatLabel_{seat}",
                    pos + Vector3.up * (seat == 0 ? 0.35f : 1.5f), 0.055f);
            }

            // Community cards: five flat quads at the table centre.
            for (int i = 0; i < 5; i++)
            {
                Vector3 pos = TableCentre + new Vector3((i - 2) * 0.32f, 0.012f, 0.1f);
                _boardCards[i] = CreateCardQuad($"Board_{i}", pos);
                _boardTexts[i] = CreateLabel($"BoardText_{i}", pos + new Vector3(0f, 0.05f, -0.02f), 0.05f);
                _boardCards[i].SetActive(false);
            }

            // Human hole cards on the table edge in front of the camera.
            for (int i = 0; i < 2; i++)
            {
                Vector3 pos = TableCentre + new Vector3(-0.18f + i * 0.36f, 0.012f, -0.85f);
                _holeCards[i] = CreateCardQuad($"Hole_{i}", pos);
                _holeTexts[i] = CreateLabel($"HoleText_{i}", pos + new Vector3(0f, 0.06f, -0.03f), 0.07f);
                _holeCards[i].SetActive(false);
            }

            _actorIndicator = CreatePrimitive(PrimitiveType.Sphere, "ActorIndicator",
                Vector3.zero, Vector3.one * 0.14f, new Color(1f, 0.85f, 0.1f));
            _actorIndicator.SetActive(false);

            _dealerButton = CreatePrimitive(PrimitiveType.Cylinder, "DealerButton",
                Vector3.zero, new Vector3(0.12f, 0.012f, 0.12f), Color.white);
            _dealerButton.SetActive(false);
        }

        private static Vector3 SeatPosition(int seat, float radius)
        {
            // Human (seat 0) sits at the bottom (camera side); NPCs fan around.
            float angleDeg = -90f + seat * (360f / (NpcCount + 1));
            float rad = angleDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius);
        }

        private GameObject CreatePrimitive(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = MakeMaterial(color);
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider); // prototype: no physics needed
            return go;
        }

        private GameObject CreateCardQuad(string name, Vector3 pos)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // face up
            go.transform.localScale = new Vector3(0.26f, 0.36f, 1f);
            go.GetComponent<Renderer>().sharedMaterial = _cardMaterial;
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            return go;
        }

        /// <summary>Two face-down cards on the felt in front of an NPC seat.</summary>
        private GameObject[] CreateNpcBackCards(int seat)
        {
            Vector3 basePos = SeatPosition(seat, SeatRadius * 0.68f);
            basePos.y = TableHeight + 0.012f;
            Vector3 toCentre = (TableCentre - new Vector3(basePos.x, TableCentre.y, basePos.z)).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, toCentre);
            float yaw = Mathf.Atan2(toCentre.x, toCentre.z) * Mathf.Rad2Deg;

            Material back = PrototypeAssets.CardBackMaterial();
            var cards = new GameObject[2];
            for (int i = 0; i < 2; i++)
            {
                GameObject go = CreateCardQuad($"NpcBack_{seat}_{i}", basePos + right * (i == 0 ? -0.075f : 0.075f));
                go.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
                go.transform.localScale = new Vector3(0.15f, 0.21f, 1f);
                if (back != null) go.GetComponent<Renderer>().sharedMaterial = back;
                go.SetActive(false);
                cards[i] = go;
            }
            return cards;
        }

        private static Material MakeMaterial(Color color)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader) { color = color };
            return mat;
        }

        private TextMesh CreateLabel(string name, Vector3 pos, float characterSize)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var text = go.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.fontSize = 64;
            text.characterSize = characterSize;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = Color.white;
            // Billboard toward the fixed prototype camera.
            go.transform.rotation = Quaternion.LookRotation(pos - _camera.transform.position);
            return text;
        }

        // ------------------------------------------------------- controller

        private void BuildController()
        {
            Controller = new GameObject("PokerTableController").AddComponent<PokerTableController>();
            Controller.Configure(ScriptableObject.CreateInstance<PlayModeSettings>());
            Controller.EngineEvent += OnEngineEvent;
            Controller.SessionFinished += OnSessionFinished;
        }

        public void StartNewSession()
        {
            Controller.StopSession();

            var rules = TableRules.Default;
            var human = new HumanPlayer("human", "You", seat: 0, stack: rules.StartingStack);
            var npcs = new List<AIPlayer>();
            for (int i = 0; i < NpcCount; i++)
            {
                npcs.Add(new AIPlayer($"npc{i}", Roster[i].name, seat: i + 1, stack: rules.StartingStack,
                    profile: Roster[i].profile(), seed: Random.Range(int.MinValue, int.MaxValue)));
            }

            ResetTableVisuals();
            _hud.OnSessionStarted();
            Controller.StartSession(rules, human, npcs);
        }

        // ------------------------------------------------------------ events

        private void OnEngineEvent(PokerEvent evt)
        {
            PlaySoundFor(evt);

            switch (evt)
            {
                case HandStarted started:
                    ResetTableVisuals();
                    PlaceDealerButton(started.DealerSeat);
                    _hud.AddLog($"— Hand {started.HandNumber} —");
                    break;

                case FlopDealt flop:
                    ShowBoardCard(0, flop.Card1);
                    ShowBoardCard(1, flop.Card2);
                    ShowBoardCard(2, flop.Card3);
                    break;

                case TurnDealt turn:
                    ShowBoardCard(3, turn.Card);
                    break;

                case RiverDealt river:
                    ShowBoardCard(4, river.Card);
                    break;

                case CardDealt dealt when dealt.Seat == 0:
                    ShowHoleCard(dealt.Card);
                    break;

                case ShowdownStarted showdown:
                    foreach (var kv in showdown.RevealedHands)
                        if (kv.Key != 0)
                            _hud.AddLog($"{NameOf(kv.Key)} shows {Join(kv.Value)}");
                    break;

                default:
                    string line = Describe(evt);
                    if (line != null) _hud.AddLog(line);
                    break;
            }
        }

        /// <summary>Kenney casino audio (CC0); silent when the pack is absent.</summary>
        private void PlaySoundFor(PokerEvent evt)
        {
            string group = evt switch
            {
                HandStarted _ => "card-shuffle",
                CardDealt dealt when dealt.Seat == 0 => "card-place",
                FlopDealt _ => "card-slide",
                TurnDealt _ => "card-slide",
                RiverDealt _ => "card-slide",
                BetPlaced _ => "chips-stack",
                PlayerCalled _ => "chips-stack",
                PlayerRaised _ => "chips-handle",
                PlayerAllIn _ => "chips-collide",
                PlayerFolded _ => "card-shove",
                PotAwarded _ => "chips-handle",
                _ => null
            };
            if (group == null) return;
            AudioClip clip = PrototypeAssets.Sound(group);
            if (clip != null) _audio.PlayOneShot(clip);
        }

        private string Describe(PokerEvent evt) => evt switch
        {
            BlindPosted b => $"{NameOf(b.Seat)} posts {(b.IsBigBlind ? "BB" : "SB")} {b.Amount}",
            PlayerChecked c => $"{NameOf(c.Seat)} checks",
            PlayerCalled c => $"{NameOf(c.Seat)} calls {c.AddedAmount}",
            BetPlaced b => $"{NameOf(b.Seat)} bets {b.ToAmount}",
            PlayerRaised r => $"{NameOf(r.Seat)} raises to {r.ToAmount}",
            PlayerFolded f => $"{NameOf(f.Seat)} folds",
            PlayerAllIn a => $"{NameOf(a.Seat)} is ALL-IN ({a.ToAmount})",
            UncalledBetReturned u => $"{u.Amount} returned to {NameOf(u.Seat)}",
            PotAwarded p => $"{NameOf(p.WinnerSeats)} win{(p.WinnerSeats.Count == 1 ? "s" : "")} {p.Amount}" +
                            (p.WinningHand.HasValue ? $" with {p.WinningHand.Value.Category}" : ""),
            PlayerEliminated e => $"{NameOf(e.Seat)} is eliminated!",
            _ => null
        };

        private void OnSessionFinished()
        {
            string winner = "Nobody";
            foreach (var p in Controller.Game.Players)
                if (p.Stack > 0) winner = p.Name;
            _hud.ShowSessionOver($"Session over — {winner} wins the table!");
        }

        // ----------------------------------------------------------- visuals

        private void ResetTableVisuals()
        {
            foreach (GameObject card in _boardCards) card.SetActive(false);
            foreach (GameObject card in _holeCards) card.SetActive(false);
            _holeShown = 0;
        }

        private int _holeShown;

        private void ShowHoleCard(Card card)
        {
            if (_holeShown >= 2) return;
            ApplyCardVisual(_holeCards[_holeShown], _holeTexts[_holeShown], card);
            _holeShown++;
        }

        private void ShowBoardCard(int index, Card card)
        {
            ApplyCardVisual(_boardCards[index], _boardTexts[index], card);
        }

        /// <summary>Real card art when the CC0 pack is present, text fallback otherwise.</summary>
        private void ApplyCardVisual(GameObject quad, TextMesh label, Card card)
        {
            quad.SetActive(true);
            Material face = PrototypeAssets.CardFaceMaterial(card);
            if (face != null)
            {
                quad.GetComponent<Renderer>().sharedMaterial = face;
                label.text = "";
            }
            else
            {
                quad.GetComponent<Renderer>().sharedMaterial = _cardMaterial;
                label.text = card.ToString();
                label.color = IsRed(card) ? new Color(0.8f, 0.1f, 0.1f) : Color.black;
            }
        }

        private static bool IsRed(Card card) => card.Suit == Suit.Hearts || card.Suit == Suit.Diamonds;

        private void PlaceDealerButton(int dealerSeat)
        {
            _dealerButton.SetActive(true);
            Vector3 seatPos = SeatPosition(dealerSeat, SeatRadius * 0.62f);
            _dealerButton.transform.position = new Vector3(seatPos.x, TableHeight + 0.015f, seatPos.z);
        }

        private void LateUpdate()
        {
            PokerGame game = Controller != null ? Controller.Game : null;
            if (game == null) return;

            bool inHand = game.Phase == GamePhase.HandInProgress;
            PokerRound round = game.CurrentRound;

            foreach (var player in game.Players)
            {
                TextMesh label = _seatLabels[player.Seat];
                if (label == null) continue;

                SeatState seatState = inHand ? TryGetSeat(round, player.Seat) : null;
                long stack = seatState?.Stack ?? player.Stack;

                GameObject[] backs = _npcBacks[player.Seat];
                if (backs != null)
                {
                    bool showBacks = inHand && seatState != null && !seatState.Folded;
                    foreach (GameObject back in backs)
                        if (back.activeSelf != showBacks) back.SetActive(showBacks);
                }

                string status = "";
                if (player.Status == PlayerStatus.Eliminated) status = "\n<OUT>";
                else if (seatState != null && seatState.Folded) status = "\n<folded>";
                else if (seatState != null && seatState.AllIn) status = "\n<ALL-IN>";
                else if (seatState != null && seatState.CommittedThisStreet > 0) status = $"\nbet {seatState.CommittedThisStreet}";

                string blind = "";
                if (inHand && player.Seat == round.SmallBlindSeat) blind = " (SB)";
                else if (inHand && player.Seat == round.BigBlindSeat) blind = " (BB)";
                else if (inHand && player.Seat == round.DealerSeat) blind = " (D)";

                label.text = $"{player.Name}{blind}\n${stack}{status}";
            }

            int actorSeat = game.CurrentSeat;
            if (actorSeat >= 0)
            {
                _actorIndicator.SetActive(true);
                Vector3 pos = SeatPosition(actorSeat, SeatRadius);
                _actorIndicator.transform.position = pos + Vector3.up * (actorSeat == 0 ? 0.55f : 1.75f);
            }
            else
            {
                _actorIndicator.SetActive(false);
            }

            _hud.SetStatus(inHand
                ? $"Hand {game.HandNumber}  •  {round.Phase}  •  Pot ${round.PotTotal}"
                : $"Hand {game.HandNumber} complete");
        }

        private static SeatState TryGetSeat(PokerRound round, int seat)
        {
            IReadOnlyList<SeatState> seats = round.Seats;
            for (int i = 0; i < seats.Count; i++)
                if (seats[i].Seat == seat)
                    return seats[i];
            return null;
        }

        // ----------------------------------------------------------- helpers

        private string NameOf(int seat) => Controller.Game.GetPlayer(seat).Name;

        private string NameOf(IReadOnlyList<int> seats)
        {
            var names = new List<string>();
            foreach (int seat in seats) names.Add(NameOf(seat));
            return string.Join(" & ", names);
        }

        private static string Join(IReadOnlyList<Card> cards) => string.Join(" ", cards);
    }
}
