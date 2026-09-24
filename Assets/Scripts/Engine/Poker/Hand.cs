using System.Collections.Generic;

namespace CinematicPoker.Engine.Poker
{
    /// <summary>
    /// A player's two hole cards. Small convenience wrapper used by AI and
    /// presentation code; the authoritative copies live in SeatState.
    /// </summary>
    public readonly struct Hand
    {
        public Card First { get; }
        public Card Second { get; }

        public Hand(Card first, Card second)
        {
            First = first;
            Second = second;
        }

        public bool IsPair => First.Rank == Second.Rank;
        public bool IsSuited => First.Suit == Second.Suit;
        public Rank HighRank => First.Rank >= Second.Rank ? First.Rank : Second.Rank;
        public Rank LowRank => First.Rank >= Second.Rank ? Second.Rank : First.Rank;

        public IReadOnlyList<Card> ToList() => new[] { First, Second };

        public override string ToString() => $"{First}{Second}";
    }
}
