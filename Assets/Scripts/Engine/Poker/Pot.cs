using System.Collections.Generic;

namespace CinematicPoker.Engine.Poker
{
    /// <summary>
    /// One pot at showdown: an amount of chips and the seats eligible to win it.
    /// Index 0 is the main pot; higher indices are side pots created by all-ins.
    /// </summary>
    public sealed class Pot
    {
        public long Amount { get; internal set; }
        public List<int> EligibleSeats { get; } = new List<int>();

        public override string ToString() =>
            $"Pot {Amount} (eligible: {string.Join(",", EligibleSeats)})";
    }
}
