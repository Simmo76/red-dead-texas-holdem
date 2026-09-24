using System;

namespace CinematicPoker.Engine.AI
{
    /// <summary>
    /// Locally tracked tendencies of one opponent (usually the human player).
    /// Skilled NPCs (high OpponentAwareness) use this to adapt; it also feeds
    /// contextual dialogue ("You haven't played a hand in twenty minutes...").
    /// All data stays on device.
    /// </summary>
    public sealed class PlayerModel
    {
        public int HandsObserved;

        // Preflop
        public int VpipOpportunities;
        public int VpipCount;            // voluntarily put money in preflop
        public int PreflopRaiseCount;
        public int ThreeBetOpportunities;
        public int ThreeBetCount;

        // Facing aggression
        public int FacedRaiseCount;
        public int FoldedToRaiseCount;

        // Showdowns and bluffs
        public int ShowdownCount;
        public int ObservedBluffCount;   // showed down a busted bluff or was caught

        /// <summary>Exponentially weighted recent aggression (bets+raises per hand).</summary>
        public double RecentAggression;

        /// <summary>Hands since the player last voluntarily played (for dialogue).</summary>
        public int HandsSinceVoluntaryPlay;

        public double Vpip => Ratio(VpipCount, VpipOpportunities, 0.25);
        public double PreflopRaiseFrequency => Ratio(PreflopRaiseCount, VpipOpportunities, 0.1);
        public double ThreeBetFrequency => Ratio(ThreeBetCount, ThreeBetOpportunities, 0.05);
        public double FoldToRaise => Ratio(FoldedToRaiseCount, FacedRaiseCount, 0.5);
        public double ShowdownFrequency => Ratio(ShowdownCount, HandsObserved, 0.25);
        public double ObservedBluffFrequency => Ratio(ObservedBluffCount, ShowdownCount, 0.1);

        private static double Ratio(int count, int total, double prior)
        {
            // Small-sample smoothing so early reads aren't extreme.
            const int priorWeight = 8;
            return (count + prior * priorWeight) / (double)(total + priorWeight);
        }

        public void RecordHandStart()
        {
            HandsObserved++;
            VpipOpportunities++;
            HandsSinceVoluntaryPlay++;
            RecentAggression *= 0.9;
        }

        public void RecordVoluntaryPreflopPlay(bool raised)
        {
            VpipCount++;
            HandsSinceVoluntaryPlay = 0;
            if (raised) PreflopRaiseCount++;
        }

        public void RecordThreeBetOpportunity(bool threeBet)
        {
            ThreeBetOpportunities++;
            if (threeBet) ThreeBetCount++;
        }

        public void RecordFacedRaise(bool folded)
        {
            FacedRaiseCount++;
            if (folded) FoldedToRaiseCount++;
        }

        public void RecordAggressiveAction() => RecentAggression += 0.5;

        public void RecordShowdown(bool wasBluff)
        {
            ShowdownCount++;
            if (wasBluff) ObservedBluffCount++;
        }
    }
}
