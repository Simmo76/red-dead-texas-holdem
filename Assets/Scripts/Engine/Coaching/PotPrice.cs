namespace CinematicPoker.Engine.Coaching
{
    /// <summary>
    /// Immediate pot-price arithmetic for a call into a simple pot.
    /// The pot already includes bets the hero is facing and excludes the
    /// pending call. Earlier chips the hero put in are sunk.
    /// These equations are not a general optimal-action solver.
    /// </summary>
    public static class PotPrice
    {
        public static double? BreakEvenEquity(long potBefore, long incrementalCall)
        {
            if (incrementalCall <= 0) return null;
            if (potBefore < 0) return null;
            return incrementalCall / (double)(potBefore + incrementalCall);
        }

        public static double? CallEv(double equityShare, long potBefore, long incrementalCall)
        {
            if (incrementalCall <= 0) return null;
            if (potBefore < 0) return null;
            return equityShare * (potBefore + incrementalCall) - incrementalCall;
        }
    }
}
