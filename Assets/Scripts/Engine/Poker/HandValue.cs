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
        /// Packed score: category in the high bits, then up to five ranked
        /// tie-break values (pair ranks, kickers, straight high card...)
        /// in descending significance, four bits each.
        /// </summary>
        public long Score { get; }

        public HandValue(HandCategory category, IReadOnlyList<Rank> tieBreaks)
        {
            Category = category;
            long score = (long)category;
            for (int i = 0; i < 5; i++)
            {
                score <<= 4;
                if (tieBreaks != null && i < tieBreaks.Count)
                    score |= (long)tieBreaks[i] & 0xF;
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
    }
}
