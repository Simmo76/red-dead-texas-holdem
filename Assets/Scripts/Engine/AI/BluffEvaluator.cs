using System;

namespace CinematicPoker.Engine.AI
{
    /// <summary>
    /// Decides whether a weak hand becomes a bluff. Probabilistic and
    /// personality-driven — never a fixed rule players could exploit perfectly.
    /// </summary>
    public static class BluffEvaluator
    {
        public static bool ShouldBluff(DecisionContext context, AIProfile profile, TiltState tilt, Random rng)
        {
            if (context.OpponentsInHand > 2)
                return rng.NextDouble() < profile.BluffFrequency * 0.2; // multiway bluffs are rare

            double chance = profile.BluffFrequency;

            // Better position → more bluffs (scaled by how positionally aware the NPC is).
            if (context.PlayersBehind == 0)
                chance *= 1.0 + profile.PositionAwareness * 0.75;

            // Heads-up is the classic bluffing spot.
            if (context.OpponentsInHand == 1)
                chance *= 1.4;

            // Exploit opponents known to fold too much.
            if (context.OpponentModel != null)
                chance *= 1.0 + (context.OpponentModel.FoldToRaise - 0.5) * profile.OpponentAwareness;

            // Emotional state: tilt and liquid courage produce wild bluffs;
            // fear suppresses them.
            chance *= 1.0 + (tilt.Tilt * 0.6 + tilt.Intoxication * 0.5 - tilt.Fear * 0.7) * profile.EmotionalBias;

            return rng.NextDouble() < Math.Max(0, Math.Min(0.75, chance));
        }
    }
}
