using System;
using System.Collections.Generic;
using System.Linq;
using CinematicPoker.Engine.AI;
using CinematicPoker.Engine.Players;
using CinematicPoker.Engine.Poker;

namespace CinematicPoker.Engine.Simulation
{
    public sealed class SimulationResult
    {
        public int HandsPlayed;
        public int SessionsPlayed;
        public long DecisionsMade;
        public int ShowdownCount;
        public int FoldWinCount;
        public int EliminationCount;
        public readonly Dictionary<HandCategory, int> WinningCategories = new Dictionary<HandCategory, int>();
        public int SidePotHands;

        public override string ToString() =>
            $"Hands: {HandsPlayed}, Sessions: {SessionsPlayed}, Decisions: {DecisionsMade}, " +
            $"Showdowns: {ShowdownCount}, FoldWins: {FoldWinCount}, SidePotHands: {SidePotHands}, " +
            $"Eliminations: {EliminationCount}";
    }

    public sealed class SimulationInvariantException : Exception
    {
        public SimulationInvariantException(string message) : base(message) { }
    }

    /// <summary>
    /// Plays a large number of automated AI-vs-AI hands and asserts the engine's
    /// core invariants on every single hand:
    ///  - no duplicate cards
    ///  - no negative stacks
    ///  - no deadlocks (bounded decisions per hand)
    ///  - only eligible seats are ever asked to act
    ///  - total money is conserved (before == after, every hand and session)
    /// Used by the 10,000-hand EditMode test and the console soak runner.
    /// </summary>
    public static class SimulationRunner
    {
        private const int MaxDecisionsPerHand = 500;

        public static SimulationResult Run(int targetHands, int seed, int equityIterations = 60, Action<string> log = null)
        {
            var result = new SimulationResult();
            var masterRng = new Random(seed);

            while (result.HandsPlayed < targetHands)
            {
                RunSession(targetHands, masterRng, result, equityIterations, log);
                result.SessionsPlayed++;
            }

            return result;
        }

        private static void RunSession(int targetHands, Random rng, SimulationResult result, int equityIterations, Action<string> log)
        {
            int playerCount = rng.Next(2, 7); // 2-6
            var rules = new TableRules(smallBlind: 5, bigBlind: 10, startingStack: rng.Next(30, 150) * 10);

            var profiles = new Func<AIProfile>[]
            {
                AIProfile.CallingStation, AIProfile.Rock, AIProfile.Maniac,
                AIProfile.Professional, AIProfile.Regular
            };

            var players = new List<PokerPlayer>();
            for (int i = 0; i < playerCount; i++)
            {
                players.Add(new AIPlayer(
                    id: $"npc{i}", name: $"NPC {i}", seat: i, stack: rules.StartingStack,
                    profile: profiles[rng.Next(profiles.Length)](), seed: rng.Next()));
            }

            var game = new PokerGame(rules, players, seed: rng.Next());

            // Per-hand event tracking for invariant checks.
            var cardsThisHand = new HashSet<int>();
            bool sawSidePot = false;
            int potsAwardedThisHand = 0;

            game.EventEmitted += evt =>
            {
                switch (evt)
                {
                    case CardDealt cd:
                        if (!cardsThisHand.Add(cd.Card.Index))
                            throw new SimulationInvariantException($"Duplicate hole card {cd.Card} in hand {evt.HandNumber}.");
                        break;
                    case FlopDealt f:
                        foreach (Card c in new[] { f.Card1, f.Card2, f.Card3 })
                            if (!cardsThisHand.Add(c.Index))
                                throw new SimulationInvariantException($"Duplicate board card {c} in hand {evt.HandNumber}.");
                        break;
                    case TurnDealt t:
                        if (!cardsThisHand.Add(t.Card.Index))
                            throw new SimulationInvariantException($"Duplicate turn card {t.Card} in hand {evt.HandNumber}.");
                        break;
                    case RiverDealt r:
                        if (!cardsThisHand.Add(r.Card.Index))
                            throw new SimulationInvariantException($"Duplicate river card {r.Card} in hand {evt.HandNumber}.");
                        break;
                    case ShowdownStarted _:
                        result.ShowdownCount++;
                        break;
                    case PotAwarded pa:
                        potsAwardedThisHand++;
                        if (pa.PotIndex > 0) sawSidePot = true;
                        if (pa.WinningHand.HasValue)
                        {
                            HandCategory cat = pa.WinningHand.Value.Category;
                            result.WinningCategories.TryGetValue(cat, out int n);
                            result.WinningCategories[cat] = n + 1;
                        }
                        break;
                    case HandCompleted hc:
                        if (hc.WonByFolds) result.FoldWinCount++;
                        break;
                    case PlayerEliminated _:
                        result.EliminationCount++;
                        break;
                }
            };

            long sessionChips = game.TotalChipsInPlay;
            int handsThisSession = 0;

            while (!game.IsSessionOver && result.HandsPlayed < targetHands && handsThisSession < 500)
            {
                cardsThisHand.Clear();
                sawSidePot = false;
                potsAwardedThisHand = 0;

                long before = game.TotalChipsInPlay;
                if (before != sessionChips)
                    throw new SimulationInvariantException(
                        $"Money leak between hands: expected {sessionChips}, found {before}.");

                game.StartHand();

                int decisions = 0;
                while (game.Phase == GamePhase.HandInProgress)
                {
                    if (++decisions > MaxDecisionsPerHand)
                        throw new SimulationInvariantException(
                            $"Deadlock: more than {MaxDecisionsPerHand} decisions in hand {game.HandNumber}.");

                    int seat = game.CurrentSeat;
                    if (seat < 0)
                        throw new SimulationInvariantException("Hand in progress but no seat to act.");

                    SeatState seatState = game.CurrentRound.GetSeat(seat);
                    if (!seatState.CanStillAct)
                        throw new SimulationInvariantException(
                            $"Turn order violation: seat {seat} (folded={seatState.Folded}, allin={seatState.AllIn}) asked to act.");

                    var npc = (AIPlayer)game.GetPlayer(seat);
                    DecisionContext context = DecisionContext.ForCurrentActor(game, equityIterations);
                    PlayerAction action = npc.DecideAction(context);
                    game.SubmitAction(action);
                    result.DecisionsMade++;
                }

                long after = game.TotalChipsInPlay;
                if (after != before)
                    throw new SimulationInvariantException(
                        $"Money not conserved in hand {game.HandNumber}: before {before}, after {after}.");

                foreach (PokerPlayer p in game.Players)
                    if (p.Stack < 0)
                        throw new SimulationInvariantException($"Negative stack for seat {p.Seat}: {p.Stack}.");

                if (potsAwardedThisHand == 0)
                    throw new SimulationInvariantException($"Hand {game.HandNumber} completed without awarding a pot.");

                if (sawSidePot) result.SidePotHands++;

                result.HandsPlayed++;
                handsThisSession++;

                if (log != null && result.HandsPlayed % 1000 == 0)
                    log($"{result.HandsPlayed} hands... ({result})");
            }
        }
    }
}
