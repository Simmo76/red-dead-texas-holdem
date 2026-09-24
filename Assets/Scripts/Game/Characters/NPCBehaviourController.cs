using System.Collections.Generic;
using CinematicPoker.Engine.Players;
using CinematicPoker.Engine.Poker;
using CinematicPoker.Game.Presentation;
using CinematicPoker.Game.Profiles;
using UnityEngine;
using Random = System.Random;

namespace CinematicPoker.Game.Characters
{
    /// <summary>
    /// The character half of an NPC: emotion display, gaze, reactions, tells,
    /// dialogue and memory. Strictly separated from the poker brain
    /// (PokerDecisionEngine inside AIPlayer): this class REACTS to engine
    /// events and NPC state but never authoritatively modifies cards, bets,
    /// pots or legal game state.
    /// </summary>
    public sealed class NPCBehaviourController : MonoBehaviour
    {
        [SerializeField] private PokerTableController table;
        [SerializeField] private NPCProfileAsset profile;
        [SerializeField] private int seat;

        private IPokerCharacter _character;
        private AIPlayer _brain;
        private INpcDialogueProvider _dialogue;
        private Random _rng;

        /// <summary>Notable hands remembered for later dialogue callbacks.</summary>
        private readonly List<string> _memorableMoments = new List<string>();

        public NPCProfileAsset Profile => profile;
        public int Seat => seat;

        public void Bind(PokerTableController tableController, NPCProfileAsset npcProfile, AIPlayer brain, IPokerCharacter rig, int seatIndex, int seed)
        {
            table = tableController;
            profile = npcProfile;
            _brain = brain;
            _character = rig;
            seat = seatIndex;
            _rng = new Random(seed);
            _dialogue = new AuthoredDialogueProvider(npcProfile != null ? npcProfile.dialogueProfile : null);

            table.EngineEvent += OnEngineEvent;
        }

        private void OnDestroy()
        {
            if (table != null)
                table.EngineEvent -= OnEngineEvent;
        }

        private void OnEngineEvent(PokerEvent evt)
        {
            switch (evt)
            {
                case BetPlaced bet when bet.Seat != seat:
                    WatchSeat(bet.Seat);
                    break;

                case PlayerRaised raise when raise.Seat != seat:
                    WatchSeat(raise.Seat);
                    MaybeSpeak(DialogueTrigger.PlayerRaises);
                    break;

                case PlayerAllIn allIn:
                    WatchSeat(allIn.Seat);
                    if (allIn.Seat != seat)
                    {
                        React(PokerReaction.Surprised, 0.7f);
                        MaybeSpeak(DialogueTrigger.PlayerAllIn);
                    }
                    break;

                case PlayerFolded folded when folded.Seat == seat:
                    _character?.PlayGesture(PokerGesture.FoldCards);
                    break;

                case PotAwarded award:
                    OnPotAwarded(award);
                    break;

                case HandCompleted completed:
                    OnHandCompleted(completed);
                    break;
            }
        }

        private void OnPotAwarded(PotAwarded award)
        {
            bool iWon = ContainsSeat(award.WinnerSeats, seat);
            if (iWon)
            {
                bool big = award.Amount > 50 * (table.Game?.Rules.BigBlind ?? 10);
                React(big ? PokerReaction.CelebrateLarge : PokerReaction.CelebrateSmall, big ? 1f : 0.5f);
                _character?.PlayGesture(PokerGesture.CollectChips);
                if (big) _memorableMoments.Add($"won a big pot ({award.Amount}) in hand {award.HandNumber}");
            }
            else if (award.Amount > 30 * (table.Game?.Rules.BigBlind ?? 10))
            {
                React(PokerReaction.Disappointed, 0.6f);
                MaybeSpeak(DialogueTrigger.BigPot);
            }
        }

        private void OnHandCompleted(HandCompleted completed)
        {
            if (_brain == null) return;

            if (completed.StackChanges.TryGetValue(seat, out long change))
            {
                // Emotional state update lives in the engine-side TiltState;
                // presentation just mirrors it.
                int biggestWinner = -1;
                long best = 0;
                foreach (var kv in completed.StackChanges)
                    if (kv.Value > best) { best = kv.Value; biggestWinner = kv.Key; }

                _brain.OnHandCompleted(change, table.Game.Rules.BigBlind, biggestWinner);

                if (_brain.Tilt.Tilt > 0.6f)
                    MaybeSpeak(DialogueTrigger.NpcTilted);
            }
        }

        private void WatchSeat(int actingSeat)
        {
            // Context-aware gaze: watch whoever is acting.
            if (_character == null || table == null) return;
            // Seat-to-transform resolution is provided by the table view; for
            // now we simply signal intent through the interface.
            _character.LookAt(null, 0.8f);
        }

        private void React(PokerReaction reaction, float intensity) =>
            _character?.PlayReaction(reaction, intensity);

        private void MaybeSpeak(DialogueTrigger trigger)
        {
            if (_dialogue == null || _rng == null) return;
            string line = _dialogue.GetLine(trigger, null, (float)(_brain?.Tilt.Tilt ?? 0), _rng);
            if (!string.IsNullOrEmpty(line))
                DialogueBus.Publish(profile != null ? profile.displayName : name, line, _character?.VoiceAnchor);
        }

        private static bool ContainsSeat(IReadOnlyList<int> seats, int seat)
        {
            for (int i = 0; i < seats.Count; i++)
                if (seats[i] == seat) return true;
            return false;
        }
    }

    /// <summary>Minimal dialogue pub/sub so UI (subtitles) and audio can present lines.</summary>
    public static class DialogueBus
    {
        public delegate void DialogueSpoken(string speakerName, string line, Transform voiceAnchor);
        public static event DialogueSpoken Spoken;
        public static void Publish(string speaker, string line, Transform anchor) =>
            Spoken?.Invoke(speaker, line, anchor);
    }
}
