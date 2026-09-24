using System;
using System.Collections.Generic;

namespace CinematicPoker.Engine.Poker
{
    /// <summary>
    /// Evaluates the best five-card poker hand from five, six or seven cards.
    /// Pure C#, allocation-light, and safe to call from worker threads
    /// (e.g. Monte Carlo equity simulation).
    /// </summary>
    public static class HandEvaluator
    {
        /// <summary>Evaluate the best five-card hand from 5-7 cards.</summary>
        public static HandValue Evaluate(IReadOnlyList<Card> cards)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            if (cards.Count < 5 || cards.Count > 7)
                throw new ArgumentException($"Expected 5-7 cards, got {cards.Count}.");

            // rankCounts[r] = how many cards of rank r (index 2..14).
            Span<int> rankCounts = stackalloc int[15];
            Span<int> suitCounts = stackalloc int[4];
            // For each suit, a bitmask of ranks present (bit r set = rank r present).
            Span<int> suitRankMasks = stackalloc int[4];
            int rankMask = 0;

            for (int i = 0; i < cards.Count; i++)
            {
                Card c = cards[i];
                rankCounts[(int)c.Rank]++;
                suitCounts[(int)c.Suit]++;
                suitRankMasks[(int)c.Suit] |= 1 << (int)c.Rank;
                rankMask |= 1 << (int)c.Rank;
            }

            // Straight flush / royal flush.
            for (int s = 0; s < 4; s++)
            {
                if (suitCounts[s] >= 5)
                {
                    int high = StraightHigh(suitRankMasks[s]);
                    if (high > 0)
                    {
                        return high == (int)Rank.Ace
                            ? new HandValue(HandCategory.RoyalFlush, new[] { Rank.Ace })
                            : new HandValue(HandCategory.StraightFlush, new[] { (Rank)high });
                    }
                }
            }

            // Group ranks by multiplicity, highest rank first.
            var quads = new List<Rank>();
            var trips = new List<Rank>();
            var pairs = new List<Rank>();
            var singles = new List<Rank>();
            for (int r = 14; r >= 2; r--)
            {
                switch (rankCounts[r])
                {
                    case 4: quads.Add((Rank)r); break;
                    case 3: trips.Add((Rank)r); break;
                    case 2: pairs.Add((Rank)r); break;
                    case 1: singles.Add((Rank)r); break;
                }
            }

            if (quads.Count > 0)
            {
                Rank kicker = HighestExcluding(rankCounts, quads[0]);
                return new HandValue(HandCategory.FourOfAKind, new[] { quads[0], kicker });
            }

            if (trips.Count > 0 && (pairs.Count > 0 || trips.Count > 1))
            {
                Rank tripRank = trips[0];
                Rank pairRank = trips.Count > 1 ? trips[1] : pairs[0];
                if (pairs.Count > 0 && pairs[0] > pairRank) pairRank = pairs[0];
                return new HandValue(HandCategory.FullHouse, new[] { tripRank, pairRank });
            }

            // Flush.
            for (int s = 0; s < 4; s++)
            {
                if (suitCounts[s] >= 5)
                {
                    var flushRanks = new List<Rank>(5);
                    for (int r = 14; r >= 2 && flushRanks.Count < 5; r--)
                        if ((suitRankMasks[s] & (1 << r)) != 0)
                            flushRanks.Add((Rank)r);
                    return new HandValue(HandCategory.Flush, flushRanks);
                }
            }

            // Straight.
            int straightHigh = StraightHigh(rankMask);
            if (straightHigh > 0)
                return new HandValue(HandCategory.Straight, new[] { (Rank)straightHigh });

            if (trips.Count > 0)
            {
                var tieBreaks = new List<Rank> { trips[0] };
                AddKickers(tieBreaks, singles, 2);
                return new HandValue(HandCategory.ThreeOfAKind, tieBreaks);
            }

            if (pairs.Count >= 2)
            {
                var tieBreaks = new List<Rank> { pairs[0], pairs[1] };
                // Kicker: best remaining card, which may be a third pair's rank.
                Rank kicker = Rank.Two;
                bool found = false;
                for (int r = 14; r >= 2; r--)
                {
                    if (rankCounts[r] > 0 && (Rank)r != pairs[0] && (Rank)r != pairs[1])
                    {
                        kicker = (Rank)r;
                        found = true;
                        break;
                    }
                }
                if (found) tieBreaks.Add(kicker);
                return new HandValue(HandCategory.TwoPair, tieBreaks);
            }

            if (pairs.Count == 1)
            {
                var tieBreaks = new List<Rank> { pairs[0] };
                AddKickers(tieBreaks, singles, 3);
                return new HandValue(HandCategory.Pair, tieBreaks);
            }

            var highCards = new List<Rank>();
            AddKickers(highCards, singles, 5);
            return new HandValue(HandCategory.HighCard, highCards);
        }

        /// <summary>
        /// Returns the high card rank of the best straight in the rank bitmask,
        /// or 0 if none. Handles the wheel (A-2-3-4-5).
        /// </summary>
        private static int StraightHigh(int rankMask)
        {
            // Ace also plays low.
            int mask = rankMask;
            if ((mask & (1 << 14)) != 0)
                mask |= 1 << 1;

            for (int high = 14; high >= 5; high--)
            {
                int run = 0b11111 << (high - 4);
                if ((mask & run) == run)
                    return high;
            }
            return 0;
        }

        private static Rank HighestExcluding(Span<int> rankCounts, Rank excluded)
        {
            for (int r = 14; r >= 2; r--)
                if (rankCounts[r] > 0 && (Rank)r != excluded)
                    return (Rank)r;
            return Rank.Two;
        }

        private static void AddKickers(List<Rank> target, List<Rank> singlesDescending, int count)
        {
            for (int i = 0; i < singlesDescending.Count && count > 0; i++)
            {
                target.Add(singlesDescending[i]);
                count--;
            }
        }

        /// <summary>Convenience: evaluate hole cards plus board.</summary>
        public static HandValue Evaluate(IReadOnlyList<Card> holeCards, IReadOnlyList<Card> board)
        {
            var all = new List<Card>(holeCards.Count + board.Count);
            all.AddRange(holeCards);
            all.AddRange(board);
            return Evaluate(all);
        }
    }
}
