using System;

namespace CinematicPoker.Engine.Poker
{
    /// <summary>
    /// Immutable configuration for a poker session. Environment packs can
    /// present different chips/currency, but all map onto these values.
    /// </summary>
    public sealed class TableRules
    {
        public long SmallBlind { get; }
        public long BigBlind { get; }
        public long StartingStack { get; }
        public int MinPlayers => 2;
        public int MaxPlayers => 6;

        public TableRules(long smallBlind, long bigBlind, long startingStack)
        {
            if (smallBlind <= 0) throw new ArgumentException("Small blind must be positive.");
            if (bigBlind < smallBlind) throw new ArgumentException("Big blind must be >= small blind.");
            if (startingStack < bigBlind) throw new ArgumentException("Starting stack must cover the big blind.");
            SmallBlind = smallBlind;
            BigBlind = bigBlind;
            StartingStack = startingStack;
        }

        public static TableRules Default => new TableRules(smallBlind: 5, bigBlind: 10, startingStack: 1000);
    }
}
