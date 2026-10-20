using System.Collections.Generic;
using CinematicPoker.Engine.AI;
using CinematicPoker.Engine.Players;
using CinematicPoker.Engine.Poker;
using CinematicPoker.Game.Environments;
using CinematicPoker.Game.Save;
using UnityEngine;

namespace CinematicPoker.Game.Presentation
{
    /// <summary>
    /// Builds a session from an environment definition: one human plus up to
    /// five NPCs from the environment's pool, then hands off to the
    /// PokerTableController. This is the "Open app → Continue → back at the
    /// table" entry point for the primitive prototype scene.
    /// </summary>
    public sealed class PokerSessionBootstrap : MonoBehaviour
    {
        [SerializeField] private PokerTableController controller;
        [SerializeField] private PokerEnvironmentManager environmentManager;
        [SerializeField] private string environmentId = "MatesKitchen";
        [SerializeField] private int npcCount = 5;
        [SerializeField] private bool startOnAwake = true;
        private SaveData _save;

        private void Start()
        {
            _save = SaveSystem.Load();
            if (!startOnAwake) return;

            if (!TryResumeSession())
                StartNewSession();
        }

        public void StartNewSession()
        {
            _save.session = null;
            SaveSystem.Save(_save);
            controller.Mode = (PlayMode)Mathf.Clamp(_save.playMode, 0, 1);
            controller.HandFinished -= SaveCurrentSession;
            controller.HandFinished += SaveCurrentSession;

            PokerEnvironmentDefinition env = environmentManager != null
                ? environmentManager.Find(environmentId)
                : null;

            TableRules rules = env != null && env.pokerDifficulty != null
                ? new TableRules(env.pokerDifficulty.smallBlind, env.pokerDifficulty.bigBlind, env.pokerDifficulty.startingStack)
                : TableRules.Default;

            var human = new HumanPlayer("human",
                string.IsNullOrWhiteSpace(_save.playerName) ? "James" : _save.playerName,
                seat: 0, stack: rules.StartingStack);

            var npcs = new List<AIPlayer>();
            int count = Mathf.Clamp(npcCount, 1, 5);
            // Table nicknames shuffled each session so any personality can wear any name.
            string[] fallbackNames = { "Nickey 2-guns", "Tommy Gunn", "Stevie Wanders", "Jimmy two-hands", "Simmo Says" };
            for (int i = fallbackNames.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (fallbackNames[i], fallbackNames[j]) = (fallbackNames[j], fallbackNames[i]);
            }
            for (int i = 0; i < count; i++)
            {
                var profileAsset = env != null && env.npcPool != null && i < env.npcPool.Length ? env.npcPool[i] : null;
                AIProfile profile = profileAsset != null && profileAsset.aiProfile != null
                    ? profileAsset.aiProfile.ToProfile()
                    : DefaultProfile(i);
                string npcName = profileAsset != null ? profileAsset.displayName : fallbackNames[i];

                npcs.Add(new AIPlayer($"npc{i}", npcName, seat: i + 1, stack: rules.StartingStack,
                    profile: profile, seed: Random.Range(int.MinValue, int.MaxValue)));
            }

            if (env != null)
                environmentManager.Activate(env);

            controller.StartSession(rules, human, npcs);
        }

        private bool TryResumeSession()
        {
            SessionSnapshot snapshot = _save?.session;
            if (snapshot == null || snapshot.seats == null || snapshot.seats.Count < 2)
                return false;

            if (!string.IsNullOrWhiteSpace(snapshot.environmentId))
                environmentId = snapshot.environmentId;
            PokerEnvironmentDefinition env = environmentManager != null
                ? environmentManager.Find(environmentId)
                : null;
            if (env != null) environmentManager.Activate(env);

            TableRules rules = env != null && env.pokerDifficulty != null
                ? new TableRules(env.pokerDifficulty.smallBlind, env.pokerDifficulty.bigBlind, env.pokerDifficulty.startingStack)
                : TableRules.Default;
            SeatSnapshot humanSave = snapshot.seats.Find(s => s.seat == 0);
            if (humanSave == null) return false;

            var human = new HumanPlayer("human",
                string.IsNullOrWhiteSpace(snapshot.playerName) ? "James" : snapshot.playerName,
                seat: 0, stack: Math.Max(0, humanSave.stack));
            human.Status = humanSave.eliminated ? PlayerStatus.Eliminated : PlayerStatus.Active;
            var npcs = new List<AIPlayer>();
            foreach (SeatSnapshot saved in snapshot.seats)
            {
                if (saved.seat == 0) continue;
                AIProfileAsset profileAsset = env != null && env.npcPool != null && saved.seat - 1 < env.npcPool.Length
                    ? env.npcPool[saved.seat - 1]
                    : null;
                AIProfile profile = profileAsset != null && profileAsset.aiProfile != null
                    ? profileAsset.aiProfile.ToProfile()
                    : DefaultProfile(saved.seat - 1);
                var npc = new AIPlayer(
                    string.IsNullOrEmpty(saved.npcId) ? $"npc{saved.seat - 1}" : saved.npcId,
                    string.IsNullOrEmpty(saved.displayName) ? $"Player {saved.seat}" : saved.displayName,
                    saved.seat, Math.Max(0, saved.stack), profile,
                    Random.Range(int.MinValue, int.MaxValue));
                npc.Status = saved.eliminated ? PlayerStatus.Eliminated : PlayerStatus.Active;
                npc.Tilt.Tilt = Mathf.Clamp01(saved.tilt);
                npc.Tilt.Confidence = Mathf.Clamp01(saved.confidence);
                npcs.Add(npc);
            }

            if (npcs.Count == 0) return false;
            controller.Mode = (PlayMode)Mathf.Clamp(snapshot.playMode, 0, 1);
            var players = new List<PokerPlayer> { human };
            players.AddRange(npcs);
            var game = new PokerGame(rules, players, Random.Range(int.MinValue, int.MaxValue));
            game.RestoreProgress(snapshot.handNumber, snapshot.dealerSeat);

            controller.HandFinished -= SaveCurrentSession;
            controller.HandFinished += SaveCurrentSession;
            controller.ResumeSession(game);
            return true;
        }

        private void SaveCurrentSession()
        {
            if (controller == null || controller.Game == null) return;
            SaveSession(controller.Game);
        }

        private void SaveSession(PokerGame game)
        {
            var snapshot = new SessionSnapshot
            {
                environmentId = environmentId,
                playerName = game.Human != null ? game.Human.Name : _save.playerName,
                handNumber = game.HandNumber,
                dealerSeat = game.DealerSeat,
                playMode = (int)controller.Mode,
                smallBlind = game.Rules.SmallBlind,
                bigBlind = game.Rules.BigBlind
            };
            foreach (PokerPlayer player in game.Players)
            {
                var seat = new SeatSnapshot
                {
                    seat = player.Seat,
                    npcId = player.IsHuman ? "" : player.Id,
                    displayName = player.Name,
                    stack = player.Stack,
                    eliminated = player.Status == PlayerStatus.Eliminated
                };
                if (player is AIPlayer ai)
                {
                    seat.tilt = ai.Tilt.Tilt;
                    seat.confidence = ai.Tilt.Confidence;
                }
                snapshot.seats.Add(seat);
            }

            _save.playerName = snapshot.playerName;
            _save.playMode = snapshot.playMode;
            _save.session = snapshot;
            SaveSystem.Save(_save);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveCurrentSession();
        }

        private void OnApplicationQuit() => SaveCurrentSession();

        private static AIProfile DefaultProfile(int index) => (index % 5) switch
        {
            0 => AIProfile.CallingStation(),
            1 => AIProfile.Rock(),
            2 => AIProfile.Maniac(),
            3 => AIProfile.Professional(),
            _ => AIProfile.Regular()
        };
    }
}
