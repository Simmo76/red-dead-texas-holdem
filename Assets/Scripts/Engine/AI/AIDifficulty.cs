using System;

namespace CinematicPoker.Engine.AI
{
    /// <summary>
    /// Scales authored NPC personalities for table-wide AI difficulty. Only the
    /// "how well do they play" parameters move — aggression and tightness stay
    /// untouched so each seat still feels distinct.
    /// </summary>
    public static class AIDifficulty
    {
        public static readonly string[] Names = { "EASY", "NORMAL", "HARD" };

        public const int Easy = 0;
        public const int Normal = 1;
        public const int Hard = 2;

        /// <summary>Returns a copy of <paramref name="source"/> scaled for <paramref name="difficultyIndex"/>.</summary>
        public static AIProfile Apply(AIProfile source, int difficultyIndex)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            AIProfile p = source.Clone();

            if (difficultyIndex == Easy)
            {
                // Sloppier reads, more mistakes, tilts easily.
                p.Skill *= 0.45;
                p.PotOddsAccuracy *= 0.45;
                p.HandReadingAbility *= 0.45;
                p.PositionAwareness *= 0.5;
                p.OpponentAwareness *= 0.5;
                p.BluffFrequency *= 0.7;
                p.DecisionNoise = Math.Min(1, p.DecisionNoise + 0.15);
                p.TiltSensitivity = Math.Min(1, p.TiltSensitivity + 0.2);
            }
            else if (difficultyIndex == Hard)
            {
                // Sharper reads, fewer mistakes, cool heads.
                p.Skill += (1 - p.Skill) * 0.55;
                p.PotOddsAccuracy += (1 - p.PotOddsAccuracy) * 0.55;
                p.HandReadingAbility += (1 - p.HandReadingAbility) * 0.55;
                p.PositionAwareness += (1 - p.PositionAwareness) * 0.5;
                p.OpponentAwareness += (1 - p.OpponentAwareness) * 0.5;
                p.DecisionNoise *= 0.35;
                p.TiltSensitivity *= 0.5;
            }

            return p;
        }

        public static int ClampIndex(int saved) => Math.Max(0, Math.Min(saved, Names.Length - 1));
    }
}