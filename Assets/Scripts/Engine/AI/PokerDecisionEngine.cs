using System;
using CinematicPoker.Engine.Poker;

namespace CinematicPoker.Engine.AI
{
    /// <summary>
    /// Deterministic/probabilistic NPC poker brain. Estimates equity by Monte
    /// Carlo simulation, compares against pot odds, and filters the result
    /// through the NPC's personality (AIProfile) and emotional state (TiltState).
    ///
    /// Design rules:
    /// - Never returns an illegal action (everything is clamped to LegalActions).
    /// - Deliberately imperfect: DecisionNoise, PotOddsAccuracy and emotional
    ///   bias produce believable human mistakes.
    /// - No LLM involvement: dialogue may be generative later, poker never is.
    /// </summary>
    public sealed class PokerDecisionEngine
    {
        private readonly Random _rng;

        public PokerDecisionEngine(Random rng)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        public PokerDecisionEngine(int seed) : this(new Random(seed)) { }

        public PlayerAction Decide(DecisionContext context, AIProfile profile, TiltState tilt)
        {
            tilt ??= new TiltState();

            // Skilled players buy sharper equity estimates.
            int iterations = Math.Max(50, (int)(context.EquityIterations * (0.4 + profile.Skill * 0.8)));
            double equity = EquityCalculator.Estimate(
                context.HoleCards, context.Board, Math.Max(1, context.OpponentsInHand), iterations, _rng);

            double perceived = PerceivedEquity(equity, context, profile, tilt);

            return context.ToCall <= 0
                ? DecideUnopened(context, profile, tilt, perceived)
                : DecideFacingBet(context, profile, tilt, perceived);
        }

        /// <summary>Equity as this character believes it to be right now.</summary>
        private double PerceivedEquity(double equity, DecisionContext context, AIProfile profile, TiltState tilt)
        {
            double perceived = equity;

            // Random misjudgement, worse for noisy characters.
            perceived += Gaussian() * profile.DecisionNoise * 0.15;

            // Emotional distortion: tilt/confidence inflate, fear deflates.
            double emotional = (tilt.Tilt * 0.10 + (tilt.Confidence - 0.5) * 0.06 - tilt.Fear * 0.08
                                + tilt.Intoxication * 0.08) * profile.EmotionalBias;
            perceived += emotional;

            return Math.Max(0.0, Math.Min(1.0, perceived));
        }

        private PlayerAction DecideUnopened(DecisionContext context, AIProfile profile, TiltState tilt, double equity)
        {
            LegalActions legal = context.Legal;
            bool canOpen = legal.CanBet || legal.CanRaise; // preflop big-blind option is a raise

            // Threshold to bet for value: tight players need more, aggressive need less.
            double betThreshold = 0.5 + profile.Tightness * 0.15 - profile.Aggression * 0.12
                                  - tilt.Tilt * profile.EmotionalBias * 0.08;

            if (canOpen && equity > betThreshold)
                return ClampAggressive(context, SizeBet(context, profile, equity));

            if (canOpen && equity < 0.35 && BluffEvaluator.ShouldBluff(context, profile, tilt, _rng))
                return ClampAggressive(context, SizeBet(context, profile, 0.65)); // bluffs sized like value

            if (legal.CanCheck)
                return PlayerAction.Check();

            // Shouldn't happen (ToCall == 0 implies check is legal), but stay safe.
            return legal.CanCall ? PlayerAction.Call() : PlayerAction.Fold();
        }

        private PlayerAction DecideFacingBet(DecisionContext context, AIProfile profile, TiltState tilt, double equity)
        {
            LegalActions legal = context.Legal;

            // Perceived pot odds: sloppy players miscalculate.
            double potOdds = context.PotOdds;
            potOdds += Gaussian() * (1.0 - profile.PotOddsAccuracy) * 0.06;
            potOdds = Math.Max(0.01, Math.Min(0.95, potOdds));

            // Tightness demands extra margin; risk tolerance and tilt reduce it.
            double requiredEdge = profile.Tightness * 0.08
                                  - profile.RiskTolerance * 0.04
                                  - tilt.Tilt * profile.EmotionalBias * 0.06;

            double margin = equity - (potOdds + requiredEdge);

            // Facing a big bet relative to stack scares fearful characters.
            if (context.ToCall > context.Stack / 2)
                margin -= tilt.Fear * profile.EmotionalBias * 0.08;

            if (margin < 0)
            {
                // Occasionally bluff-raise instead of folding.
                if (legal.CanRaise && equity < 0.35 && BluffEvaluator.ShouldBluff(context, profile, tilt, _rng))
                    return ClampAggressive(context, RaiseTarget(context, profile));

                // Loose players sometimes call anyway ("I had to see it").
                double stubbornCall = (1.0 - profile.Tightness) * 0.15 + tilt.Tilt * 0.1;
                if (legal.CanCall && margin > -0.12 && _rng.NextDouble() < stubbornCall)
                    return PlayerAction.Call();

                return legal.CanCheck ? PlayerAction.Check() : PlayerAction.Fold();
            }

            // Strong edge: raise with probability scaled by aggression.
            double raiseChance = profile.Aggression * (0.35 + margin * 2.0) + tilt.Tilt * profile.EmotionalBias * 0.15;
            if (legal.CanRaise && margin > 0.08 && _rng.NextDouble() < raiseChance)
                return ClampAggressive(context, RaiseTarget(context, profile));

            return legal.CanCall ? PlayerAction.Call() : PlayerAction.Check();
        }

        /// <summary>Choose a bet size (total for this street) from pot fraction and personality.</summary>
        private long SizeBet(DecisionContext context, AIProfile profile, double strength)
        {
            double fraction = 0.4 + profile.Aggression * 0.5 + (strength - 0.5) * 0.5 + Gaussian() * 0.1;
            fraction = Math.Max(0.3, Math.Min(1.2, fraction));
            long amount = (long)(context.Pot * fraction);
            return Math.Max(context.BigBlind, amount);
        }

        private long RaiseTarget(DecisionContext context, AIProfile profile)
        {
            double factor = 2.2 + profile.Aggression * 1.3 + Gaussian() * 0.3;
            return (long)(context.CurrentBet * Math.Max(2.0, factor));
        }

        /// <summary>
        /// Convert a desired aggressive amount into a legal Bet/Raise/AllIn/Call.
        /// </summary>
        private PlayerAction ClampAggressive(DecisionContext context, long desiredTo)
        {
            LegalActions legal = context.Legal;

            if (!legal.CanBet && !legal.CanRaise)
                return legal.CanCall ? PlayerAction.Call() : (legal.CanCheck ? PlayerAction.Check() : PlayerAction.Fold());

            long to = Math.Max(legal.MinBetOrRaiseTo, Math.Min(desiredTo, legal.MaxBetOrRaiseTo));
            if (to >= legal.MaxBetOrRaiseTo)
                return PlayerAction.AllIn();

            return legal.CanBet ? PlayerAction.Bet(to) : PlayerAction.Raise(to);
        }

        private double Gaussian()
        {
            // Box-Muller
            double u1 = 1.0 - _rng.NextDouble();
            double u2 = _rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        }
    }
}
