using System;

namespace CinematicPoker.Engine.AI
{
    /// <summary>
    /// Mutable emotional state for an NPC. Modifies decision thresholds but
    /// never bypasses legal poker rules. Also read by the presentation layer
    /// (dialogue, reactions, tells).
    /// </summary>
    public sealed class TiltState
    {
        public double Tilt;          // 0..1 — widens ranges, raises aggression
        public double Confidence = 0.5; // 0..1 — winning raises it
        public double Fear;          // 0..1 — big losses to all-ins raise it
        public double Frustration;   // 0..1 — card-dead sessions
        public double Fatigue;       // 0..1 — long sessions
        public double Intoxication;  // 0..1 — environment-specific (e.g. mansion)

        /// <summary>Revenge target seat, or -1. Set when one opponent keeps winning our chips.</summary>
        public int RevengeSeat = -1;

        public void OnHandResult(long stackChange, long bigBlind, AIProfile profile, int biggestWinnerSeat)
        {
            double magnitude = Math.Min(1.0, Math.Abs(stackChange) / (double)(bigBlind * 25));

            if (stackChange < 0)
            {
                Tilt = Clamp(Tilt + magnitude * profile.TiltSensitivity * 0.5);
                Confidence = Clamp(Confidence - magnitude * 0.2);
                Frustration = Clamp(Frustration + magnitude * 0.3);
                if (magnitude > 0.5)
                    RevengeSeat = biggestWinnerSeat;
            }
            else if (stackChange > 0)
            {
                Tilt = Clamp(Tilt - magnitude * 0.4);
                Confidence = Clamp(Confidence + magnitude * 0.25);
                Frustration = Clamp(Frustration - magnitude * 0.3);
                if (magnitude > 0.4)
                    RevengeSeat = -1;
            }

            // Emotions cool off slowly every hand.
            Tilt = Clamp(Tilt - 0.02);
            Frustration = Clamp(Frustration - 0.02);
            Fear = Clamp(Fear - 0.03);
            Fatigue = Clamp(Fatigue + 0.002);
        }

        private static double Clamp(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
