using System.Collections.Generic;
using System.Linq;
using CinematicPoker.Engine.Poker;

namespace CinematicPoker.Engine.Coaching
{
    /// <summary>
    /// Splits a pending call into main-pot and side-pot chips using the same
    /// layering rules as the table engine. Used when a shorter stack is all-in.
    /// </summary>
    public static class SidePotCallPrice
    {
        public sealed class Split
        {
            public long MainPotAmount;
            public long SidePotAmount;
            public long CallToMainPot;
            public long CallToSidePot;
            public int MainPotEligible;
            public double? MainBreakEven;
            public double? SideBreakEven;
        }

        /// <summary>
        /// Returns null when seat state is too thin to layer pots reliably.
        /// </summary>
        public static Split Analyse(HeroDecisionSnapshot spot)
        {
            if (spot == null || spot.IncrementalCall <= 0) return null;
            if (spot.Seats == null || spot.Seats.Count == 0) return null;

            var folded = new Dictionary<int, bool>();
            var before = new Dictionary<int, long>();
            var after = new Dictionary<int, long>();

            foreach (SeatPublicState seat in spot.Seats)
            {
                folded[seat.Seat] = seat.Folded;
                before[seat.Seat] = seat.TotalCommitted;
                after[seat.Seat] = seat.TotalCommitted;
            }

            long heroBefore = spot.HeroCommittedTotal;
            if (before.ContainsKey(spot.HeroSeat))
                heroBefore = before[spot.HeroSeat];
            before[spot.HeroSeat] = heroBefore;
            after[spot.HeroSeat] = heroBefore + spot.IncrementalCall;

            List<Pot> potsAfter = SidePotCalculator.Build(after, folded);
            if (potsAfter.Count == 0) return null;

            List<long> heroBeforeLayers = HeroLayerChips(before, folded, spot.HeroSeat);
            List<long> heroAfterLayers = HeroLayerChips(after, folded, spot.HeroSeat);
            long toMain = 0;
            long toSide = 0;
            int layers = heroAfterLayers.Count > heroBeforeLayers.Count ? heroAfterLayers.Count : heroBeforeLayers.Count;
            for (int i = 0; i < layers; i++)
            {
                long prev = i < heroBeforeLayers.Count ? heroBeforeLayers[i] : 0;
                long next = i < heroAfterLayers.Count ? heroAfterLayers[i] : 0;
                long delta = next - prev;
                if (delta <= 0) continue;
                if (i == 0) toMain += delta;
                else toSide += delta;
            }

            long mainPot = potsAfter[0].Amount;
            long sidePot = 0;
            for (int i = 1; i < potsAfter.Count; i++)
            {
                if (potsAfter[i].EligibleSeats.Contains(spot.HeroSeat))
                    sidePot += potsAfter[i].Amount;
            }

            var split = new Split
            {
                MainPotAmount = mainPot,
                SidePotAmount = sidePot,
                CallToMainPot = toMain,
                CallToSidePot = toSide,
                MainPotEligible = potsAfter[0].EligibleSeats.Count
            };

            if (split.CallToMainPot > 0)
                split.MainBreakEven = PotPrice.BreakEvenEquity(mainPot - split.CallToMainPot, split.CallToMainPot);
            if (split.CallToSidePot > 0 && sidePot > 0)
                split.SideBreakEven = PotPrice.BreakEvenEquity(sidePot - split.CallToSidePot, split.CallToSidePot);

            return split;
        }

        /// <summary>Hero chips assigned to each pot layer under the engine rules.</summary>
        private static List<long> HeroLayerChips(IReadOnlyDictionary<int, long> contributions, IReadOnlyDictionary<int, bool> folded, int heroSeat)
        {
            var heroPerLayer = new List<long>();
            var remaining = contributions
                .Where(kv => kv.Value > 0)
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            while (true)
            {
                var contenders = remaining
                    .Where(kv => kv.Value > 0 && !folded[kv.Key])
                    .Select(kv => kv.Key)
                    .ToList();
                if (contenders.Count == 0) break;

                long cap = contenders.Min(seat => remaining[seat]);
                long heroTake = remaining.TryGetValue(heroSeat, out long left) ? System.Math.Min(left, cap) : 0;
                heroPerLayer.Add(heroTake);

                foreach (int seat in remaining.Keys.ToList())
                {
                    long take = System.Math.Min(remaining[seat], cap);
                    remaining[seat] -= take;
                }
            }

            return heroPerLayer;
        }
    }
}