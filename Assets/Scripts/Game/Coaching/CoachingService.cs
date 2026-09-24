using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CinematicPoker.Engine.AI;
using CinematicPoker.Engine.Poker;

namespace CinematicPoker.Game.Coaching
{
    /// <summary>A single decision point re-analysed for the review screen.</summary>
    public sealed class DecisionAnalysis
    {
        public IReadOnlyList<Card> HoleCards;
        public IReadOnlyList<Card> Board;
        public long Pot;
        public long BetFaced;
        public double Equity;
        public double PotOdds;
        public int OpponentsInHand;

        /// <summary>"What Would They Have Done?" — action mix per NPC personality.</summary>
        public readonly Dictionary<string, ActionDistribution> PersonalityComparisons =
            new Dictionary<string, ActionDistribution>();
    }

    public struct ActionDistribution
    {
        public double Fold, Call, Raise;
        public override string ToString() => $"Fold {Fold:P0} / Call {Call:P0} / Raise {Raise:P0}";
    }

    /// <summary>
    /// "Improve My Game" analysis. Runs entirely on-device and off the main
    /// thread. In "Just Play" style this service is simply never invoked —
    /// coaching must be optional and non-intrusive.
    /// </summary>
    public static class CoachingService
    {
        /// <summary>
        /// Analyse a decision spot: equity vs pot odds, plus how different NPC
        /// personalities would have acted (teaching poker AND character).
        /// </summary>
        public static Task<DecisionAnalysis> AnalyseAsync(
            IReadOnlyList<Card> holeCards,
            IReadOnlyList<Card> board,
            long pot,
            long betFaced,
            int opponentsInHand,
            IReadOnlyDictionary<string, AIProfile> personalities,
            int iterations = 2000,
            int seed = 1)
        {
            return Task.Run(() =>
            {
                var rng = new Random(seed);
                double equity = EquityCalculator.Estimate(holeCards, board, Math.Max(1, opponentsInHand), iterations, rng);

                var analysis = new DecisionAnalysis
                {
                    HoleCards = holeCards,
                    Board = board,
                    Pot = pot,
                    BetFaced = betFaced,
                    Equity = equity,
                    PotOdds = betFaced <= 0 ? 0 : betFaced / (double)(pot + betFaced),
                    OpponentsInHand = opponentsInHand
                };

                if (personalities != null)
                {
                    foreach (var kv in personalities)
                        analysis.PersonalityComparisons[kv.Key] =
                            SampleDistribution(holeCards, board, pot, betFaced, opponentsInHand, kv.Value, rng);
                }

                return analysis;
            });
        }

        /// <summary>
        /// Run the real decision engine many times for one personality to get
        /// its fold/call/raise mix in this exact spot.
        /// </summary>
        private static ActionDistribution SampleDistribution(
            IReadOnlyList<Card> holeCards, IReadOnlyList<Card> board,
            long pot, long betFaced, int opponents, AIProfile profile, Random rng)
        {
            const int samples = 60;
            int fold = 0, call = 0, raise = 0;

            for (int i = 0; i < samples; i++)
            {
                var brain = new PokerDecisionEngine(rng.Next());
                var context = new DecisionContext
                {
                    HoleCards = holeCards,
                    Board = board,
                    Pot = pot,
                    ToCall = betFaced,
                    Stack = pot * 3 + betFaced, // representative stack
                    BigBlind = 10,
                    CurrentBet = betFaced,
                    OpponentsInHand = opponents,
                    EquityIterations = 100,
                    Legal = new LegalActions
                    {
                        CanFold = true,
                        CanCheck = betFaced == 0,
                        CanCall = betFaced > 0,
                        CallAmount = betFaced,
                        CanBet = betFaced == 0,
                        CanRaise = betFaced > 0,
                        MinBetOrRaiseTo = betFaced * 2 + 10,
                        MaxBetOrRaiseTo = pot * 3 + betFaced,
                        CanAllIn = true
                    }
                };

                PlayerAction action = brain.Decide(context, profile, new TiltState());
                switch (action.Type)
                {
                    case ActionType.Fold: fold++; break;
                    case ActionType.Check:
                    case ActionType.Call: call++; break;
                    default: raise++; break;
                }
            }

            return new ActionDistribution
            {
                Fold = fold / (double)samples,
                Call = call / (double)samples,
                Raise = raise / (double)samples
            };
        }
    }
}
