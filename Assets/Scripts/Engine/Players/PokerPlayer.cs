using System;

namespace CinematicPoker.Engine.Players
{
    /// <summary>
    /// A participant in a poker session. Holds identity and session-level
    /// chip state; per-hand state lives in the engine's SeatState.
    /// </summary>
    public abstract class PokerPlayer
    {
        public string Id { get; }
        public string Name { get; }
        public int Seat { get; }
        public long Stack { get; set; }
        public PlayerStatus Status { get; set; } = PlayerStatus.Active;

        public abstract bool IsHuman { get; }

        protected PokerPlayer(string id, string name, int seat, long stack)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("Player id required.");
            if (seat < 0) throw new ArgumentException("Seat must be non-negative.");
            if (stack < 0) throw new ArgumentException("Stack cannot be negative.");
            Id = id;
            Name = name ?? id;
            Seat = seat;
            Stack = stack;
        }

        public override string ToString() => $"{Name} (seat {Seat}, stack {Stack})";
    }
}
