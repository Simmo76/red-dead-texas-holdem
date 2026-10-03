using System;
using System.Collections.Generic;

namespace CinematicPoker.Engine.Poker
{
    public enum HandCategory
    {
        HighCard = 0,
        Pair = 1,
        TwoPair = 2,
        ThreeOfAKind = 3,
        Straight = 4,
        Flush = 5,
        FullHouse = 6,
        FourOfAKind = 7,
        StraightFlush = 8,
        RoyalFlush = 9
    }

    /// <summary>
    /// The strength of a best five-card hand. Comparable so that a higher
    /// HandValue always beats a lower one, with kickers fully accounted for.
    /// </summary>
    public readonly struct HandValue : IComparable<HandValue>, IEquatable<HandValue>
    {
        public HandCategory Category { get; }

        /// <summary>
        /// Tie-break ranks in descending significance (pair rank, then
        /// kickers; straight/flush high card; full-house trips then pair…).
        /// </summary>
        public IReadOnlyList<Rank> TieBreaks { get; }

        /// <summary>
        /// Packed score: category in the high bits, then up to five ranked
        /// tie-break values (pair ranks, kickers, straight high card...)
        /// in descending significance, four bits each.
        /// </summary>
        public long Score { get; }

        public HandValue(HandCategory category, IReadOnlyList<Rank> tieBreaks)
        {
            Category = category;
            TieBreaks = CopyRanks(tieBreaks);
            long score = (long)category;
            for (int i = 0; i < 5; i++)
            {
                score <<= 4;
                if (i < TieBreaks.Count)
                    score |= (long)TieBreaks[i] & 0xF;
            }
            Score = score;
        }

        public int CompareTo(HandValue other) => Score.CompareTo(other.Score);
        public bool Equals(HandValue other) => Score == other.Score;
        public override bool Equals(object obj) => obj is HandValue other && Equals(other);
        public override int GetHashCode() => Score.GetHashCode();

        public static bool operator >(HandValue a, HandValue b) => a.Score > b.Score;
        public static bool operator <(HandValue a, HandValue b) => a.Score < b.Score;
        public static bool operator >=(HandValue a, HandValue b) => a.Score >= b.Score;
        public static bool operator <=(HandValue a, HandValue b) => a.Score <= b.Score;
        public static bool operator ==(HandValue a, HandValue b) => a.Score == b.Score;
        public static bool operator !=(HandValue a, HandValue b) => a.Score != b.Score;

        public override string ToString() => Category.ToString();

        /// <summary>
        /// Player-facing description including the ranks that decide ties
        /// ("a pair of Aces, King kicker"). Pass <paramref name="emphasizeIndex"/>
        /// to shout the deciding rank when two hands share a category.
        /// </summary>
        public string Describe(int emphasizeIndex = -1)
        {
            IReadOnlyList<Rank> t = TieBreaks;
            string R(int i, bool plural = false)
            {
                if (i >= t.Count) return "";
                string word = RankWord(t[i], plural);
                return i == emphasizeIndex ? word.ToUpperInvariant() : word;
            }

            switch (Category)
            {
                case HandCategory.HighCard:
                    return t.Count > 1 ? $"{R(0)} high, {R(1)} kicker" : $"{R(0)} high";
                case HandCategory.Pair:
                    return t.Count > 1 ? $"a pair of {R(0, true)}, {R(1)} kicker" : $"a pair of {R(0, true)}";
                case HandCategory.TwoPair:
                    return t.Count > 2
                        ? $"{R(0, true)} and {R(1, true)}, {R(2)} kicker"
                        : $"{R(0, true)} and {R(1, true)}";
                case HandCategory.ThreeOfAKind:
                    return t.Count > 1 ? $"three {R(0, true)}, {R(1)} kicker" : $"three {R(0, true)}";
                case HandCategory.Straight:
                    return $"{R(0)}-high straight";
                case HandCategory.Flush:
                    return t.Count > 1 ? $"{R(0)}-high flush, {R(1)} kicker" : $"{R(0)}-high flush";
                case HandCategory.FullHouse:
                    return $"{R(0, true)} full of {R(1, true)}";
                case HandCategory.FourOfAKind:
                    return t.Count > 1 ? $"four {R(0, true)}, {R(1)} kicker" : $"four {R(0, true)}";
                case HandCategory.StraightFlush:
                    return $"{R(0)}-high straight flush";
                case HandCategory.RoyalFlush:
                    return "a royal flush";
                default:
                    return Category.ToString();
            }
        }

        /// <summary>
        /// First tie-break index that differs from <paramref name="other"/>,
        /// or -1 if the hands are equal / incomparable.
        /// </summary>
        public int DecidingIndex(HandValue other)
        {
            if (Category != other.Category) return -1;
            int n = Math.Min(TieBreaks.Count, other.TieBreaks.Count);
            for (int i = 0; i < n; i++)
                if (TieBreaks[i] != other.TieBreaks[i])
                    return i;
            return -1;
        }

        public static string RankWord(Rank rank, bool plural = false)
        {
            string name = rank switch
            {
                Rank.Ace => "Ace",
                Rank.King => "King",
                Rank.Queen => "Queen",
                Rank.Jack => "Jack",
                Rank.Ten => "Ten",
                Rank.Nine => "Nine",
                Rank.Eight => "Eight",
                Rank.Seven => "Seven",
                Rank.Six => "Six",
                Rank.Five => "Five",
                Rank.Four => "Four",
                Rank.Three => "Three",
                Rank.Two => "Two",
                _ => rank.ToString()
            };
            if (!plural) return name;
            return rank == Rank.Six ? "Sixes" : name + "s";
        }

        private static IReadOnlyList<Rank> CopyRanks(IReadOnlyList<Rank> tieBreaks)
        {
            if (tieBreaks == null || tieBreaks.Count == 0)
                return Array.Empty<Rank>();
            var copy = new Rank[tieBreaks.Count];
            for (int i = 0; i < copy.Length; i++)
                copy[i] = tieBreaks[i];
            return copy;
        }
    }
}
