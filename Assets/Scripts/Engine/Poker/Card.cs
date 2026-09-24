using System;

namespace CinematicPoker.Engine.Poker
{
    public enum Suit
    {
        Clubs = 0,
        Diamonds = 1,
        Hearts = 2,
        Spades = 3
    }

    public enum Rank
    {
        Two = 2,
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6,
        Seven = 7,
        Eight = 8,
        Nine = 9,
        Ten = 10,
        Jack = 11,
        Queen = 12,
        King = 13,
        Ace = 14
    }

    /// <summary>
    /// An immutable playing card. Pure C# — no Unity dependency.
    /// </summary>
    public readonly struct Card : IEquatable<Card>, IComparable<Card>
    {
        public Rank Rank { get; }
        public Suit Suit { get; }

        public Card(Rank rank, Suit suit)
        {
            Rank = rank;
            Suit = suit;
        }

        /// <summary>Unique index in [0, 51]. Useful for bitmasks and duplicate detection.</summary>
        public int Index => ((int)Rank - 2) * 4 + (int)Suit;

        public static Card FromIndex(int index)
        {
            if (index < 0 || index > 51)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new Card((Rank)(index / 4 + 2), (Suit)(index % 4));
        }

        public bool Equals(Card other) => Rank == other.Rank && Suit == other.Suit;
        public override bool Equals(object obj) => obj is Card other && Equals(other);
        public override int GetHashCode() => Index;
        public int CompareTo(Card other) => Index.CompareTo(other.Index);

        public static bool operator ==(Card a, Card b) => a.Equals(b);
        public static bool operator !=(Card a, Card b) => !a.Equals(b);

        public override string ToString() => $"{RankSymbol(Rank)}{SuitSymbol(Suit)}";

        public static string RankSymbol(Rank rank) => rank switch
        {
            Rank.Ten => "T",
            Rank.Jack => "J",
            Rank.Queen => "Q",
            Rank.King => "K",
            Rank.Ace => "A",
            _ => ((int)rank).ToString()
        };

        public static string SuitSymbol(Suit suit) => suit switch
        {
            Suit.Clubs => "\u2663",
            Suit.Diamonds => "\u2666",
            Suit.Hearts => "\u2665",
            Suit.Spades => "\u2660",
            _ => "?"
        };

        /// <summary>Parse cards written like "As", "Td", "9h", "2c".</summary>
        public static Card Parse(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length != 2)
                throw new FormatException($"Invalid card '{text}'. Expected e.g. \"As\" or \"9h\".");

            Rank rank = char.ToUpperInvariant(text[0]) switch
            {
                '2' => Rank.Two,
                '3' => Rank.Three,
                '4' => Rank.Four,
                '5' => Rank.Five,
                '6' => Rank.Six,
                '7' => Rank.Seven,
                '8' => Rank.Eight,
                '9' => Rank.Nine,
                'T' => Rank.Ten,
                'J' => Rank.Jack,
                'Q' => Rank.Queen,
                'K' => Rank.King,
                'A' => Rank.Ace,
                _ => throw new FormatException($"Invalid rank in '{text}'.")
            };

            Suit suit = char.ToLowerInvariant(text[1]) switch
            {
                'c' => Suit.Clubs,
                'd' => Suit.Diamonds,
                'h' => Suit.Hearts,
                's' => Suit.Spades,
                _ => throw new FormatException($"Invalid suit in '{text}'.")
            };

            return new Card(rank, suit);
        }
    }
}
