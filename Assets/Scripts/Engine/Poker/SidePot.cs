using System;
using System.Collections.Generic;
using System.Linq;

namespace CinematicPoker.Engine.Poker
{
    /// <summary>
    /// Builds main and side pots from per-seat total contributions.
    /// Pure function of (contributions, folded flags) so it is trivially testable.
    /// </summary>
    public static class SidePotCalculator
    {
        /// <summary>
        /// Layer contributions into a main pot and side pots.
        /// Folded seats' chips are included in pot amounts but folded seats
        /// are never eligible to win.
        /// </summary>
        /// <param name="contributions">Total chips committed this hand, per seat.</param>
        /// <param name="folded">Whether each seat has folded.</param>
        public static List<Pot> Build(IReadOnlyDictionary<int, long> contributions, IReadOnlyDictionary<int, bool> folded)
        {
            var pots = new List<Pot>();
            var remaining = contributions
                .Where(kv => kv.Value > 0)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            while (true)
            {
                // Contenders with chips still unassigned define the next layer cap.
                var contenders = remaining
                    .Where(kv => kv.Value > 0 && !folded[kv.Key])
                    .Select(kv => kv.Key)
                    .ToList();

                if (contenders.Count == 0)
                {
                    // Only folded chips remain (e.g. a raiser folded to a shove);
                    // fold them into the last pot rather than creating a dead pot.
                    long leftover = remaining.Values.Sum();
                    if (leftover > 0 && pots.Count > 0)
                        pots[pots.Count - 1].Amount += leftover;
                    break;
                }

                long cap = contenders.Min(seat => remaining[seat]);
                var pot = new Pot();
                foreach (int seat in remaining.Keys.ToList())
                {
                    long take = Math.Min(remaining[seat], cap);
                    if (take > 0)
                    {
                        pot.Amount += take;
                        remaining[seat] -= take;
                    }
                }
                pot.EligibleSeats.AddRange(contenders);
                pots.Add(pot);
            }

            return pots;
        }
    }
}
