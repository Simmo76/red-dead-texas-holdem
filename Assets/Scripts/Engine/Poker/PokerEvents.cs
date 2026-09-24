using System.Collections.Generic;

namespace CinematicPoker.Engine.Poker
{
    /// <summary>
    /// Base class for all events emitted by the poker engine.
    /// Presentation systems (animation, audio, UI, cameras, NPC behaviour)
    /// subscribe to these; they never drive game state themselves.
    /// </summary>
    public abstract class PokerEvent
    {
        public int HandNumber { get; internal set; }
    }

    public sealed class HandStarted : PokerEvent
    {
        public int DealerSeat;
        public int SmallBlindSeat;
        public int BigBlindSeat;
        public IReadOnlyList<int> SeatsInHand;
    }

    public sealed class BlindPosted : PokerEvent
    {
        public int Seat;
        public long Amount;
        public bool IsBigBlind;
        public bool IsAllIn;
    }

    /// <summary>A hole card was dealt to a seat. The card itself is only meaningful
    /// to the owning player's presentation; other views should render it face down.</summary>
    public sealed class CardDealt : PokerEvent
    {
        public int Seat;
        public Card Card;
    }

    public sealed class PlayerChecked : PokerEvent
    {
        public int Seat;
    }

    public sealed class BetPlaced : PokerEvent
    {
        public int Seat;
        /// <summary>Total committed to this street after the bet.</summary>
        public long ToAmount;
        /// <summary>Chips added by this action.</summary>
        public long AddedAmount;
    }

    public sealed class PlayerCalled : PokerEvent
    {
        public int Seat;
        public long AddedAmount;
    }

    public sealed class PlayerRaised : PokerEvent
    {
        public int Seat;
        public long ToAmount;
        public long AddedAmount;
    }

    public sealed class PlayerFolded : PokerEvent
    {
        public int Seat;
    }

    public sealed class PlayerAllIn : PokerEvent
    {
        public int Seat;
        public long ToAmount;
        public long AddedAmount;
    }

    public sealed class FlopDealt : PokerEvent
    {
        public Card Card1, Card2, Card3;
    }

    public sealed class TurnDealt : PokerEvent
    {
        public Card Card;
    }

    public sealed class RiverDealt : PokerEvent
    {
        public Card Card;
    }

    public sealed class ShowdownStarted : PokerEvent
    {
        /// <summary>Seats that reached showdown, with their hole cards revealed.</summary>
        public IReadOnlyDictionary<int, IReadOnlyList<Card>> RevealedHands;
    }

    public sealed class UncalledBetReturned : PokerEvent
    {
        public int Seat;
        public long Amount;
    }

    public sealed class PotAwarded : PokerEvent
    {
        /// <summary>0 = main pot, 1+ = side pots.</summary>
        public int PotIndex;
        public long Amount;
        public IReadOnlyList<int> WinnerSeats;
        /// <summary>Amount each winner received (odd chips may make these differ by 1).</summary>
        public IReadOnlyDictionary<int, long> Payouts;
        /// <summary>Best hand category among winners; null if won by folds.</summary>
        public HandValue? WinningHand;
    }

    public sealed class HandCompleted : PokerEvent
    {
        public bool WonByFolds;
        public IReadOnlyDictionary<int, long> StackChanges;
    }

    public sealed class PlayerEliminated : PokerEvent
    {
        public int Seat;
    }
}
