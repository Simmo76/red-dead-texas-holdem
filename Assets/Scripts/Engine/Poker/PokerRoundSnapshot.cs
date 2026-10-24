using System;
using System.Collections.Generic;
using System.Linq;

namespace CinematicPoker.Engine.Poker
{
    /// <summary>
    /// JSON-friendly representation of a live hand. It deliberately contains
    /// the shuffled deck so a restored hand continues with the same runout.
    /// </summary>
    [Serializable]
    public sealed class PokerRoundSnapshot
    {
        public HandPhase phase;
        public int dealerSeat;
        public int smallBlindSeat;
        public int bigBlindSeat;
        public int currentSeat;
        public bool wonByFolds;
        public long currentBet;
        public long minRaiseIncrement;
        public int actorIndex;
        public int deckPosition;
        public List<int> deck = new List<int>();
        public List<int> board = new List<int>();
        public List<SeatSnapshot> seats = new List<SeatSnapshot>();
    }

    [Serializable]
    public sealed class SeatSnapshot
    {
        public int seat;
        public long stack;
        public long stackAtHandStart;
        public List<int> holeCards = new List<int>();
        public bool folded;
        public bool allIn;
        public long committedThisStreet;
        public long totalCommitted;
        public bool hasActedThisStreet;
    }

    public static class PokerRoundSnapshotExtensions
    {
        /// <summary>
        /// Relabels every seat so seat <paramref name="by"/> becomes seat 0
        /// on a six-seat table. Clockwise order is unchanged, so the hand
        /// plays out identically; only the point of view moves.
        /// </summary>
        public static PokerRoundSnapshot RotateSeats(this PokerRoundSnapshot snapshot, int by, int tableSize = 6)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            int Rot(int seat) => seat < 0 ? seat : ((seat - by) % tableSize + tableSize) % tableSize;
            snapshot.dealerSeat = Rot(snapshot.dealerSeat);
            snapshot.smallBlindSeat = Rot(snapshot.smallBlindSeat);
            snapshot.bigBlindSeat = Rot(snapshot.bigBlindSeat);
            snapshot.currentSeat = Rot(snapshot.currentSeat);
            foreach (SeatSnapshot seat in snapshot.seats)
                seat.seat = Rot(seat.seat);
            return snapshot;
        }

        public static PokerRoundSnapshot Capture(this PokerRound round)
        {
            var deck = round.Deck as Deck;
            if (deck == null)
                throw new InvalidOperationException("Only standard decks can be persisted.");

            return new PokerRoundSnapshot
            {
                phase = round.Phase,
                dealerSeat = round.DealerSeat,
                smallBlindSeat = round.SmallBlindSeat,
                bigBlindSeat = round.BigBlindSeat,
                currentSeat = round.CurrentSeat,
                wonByFolds = round.WonByFolds,
                currentBet = round.CurrentBet,
                minRaiseIncrement = round.MinRaiseIncrement,
                actorIndex = round.ActorIndex,
                deckPosition = deck.Position,
                deck = deck.Cards.Select(c => c.Index).ToList(),
                board = round.Board.Select(c => c.Index).ToList(),
                seats = round.Seats.Select(s => new SeatSnapshot
                {
                    seat = s.Seat,
                    stack = s.Stack,
                    stackAtHandStart = s.StackAtHandStart,
                    holeCards = s.HoleCards.Select(c => c.Index).ToList(),
                    folded = s.Folded,
                    allIn = s.AllIn,
                    committedThisStreet = s.CommittedThisStreet,
                    totalCommitted = s.TotalCommitted,
                    hasActedThisStreet = s.HasActedThisStreet
                }).ToList()
            };
        }
    }
}
