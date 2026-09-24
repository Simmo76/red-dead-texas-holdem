using System;
using System.Collections.Generic;

namespace CinematicPoker.Engine.Poker
{
    /// <summary>
    /// Per-hand state for one seat. Owned by <see cref="PokerRound"/>.
    /// </summary>
    public sealed class SeatState
    {
        public int Seat;
        public long Stack;
        public long StackAtHandStart;
        public readonly List<Card> HoleCards = new List<Card>(2);
        public bool Folded;
        public bool AllIn;
        public long CommittedThisStreet;
        public long TotalCommitted;
        public bool HasActedThisStreet;

        public bool CanStillAct => !Folded && !AllIn;
        public bool InHand => !Folded;

        /// <summary>Move chips from stack into the current street's commitment.</summary>
        internal long Commit(long amount)
        {
            if (amount < 0) throw new IllegalActionException("Cannot commit a negative amount.");
            if (amount > Stack) throw new IllegalActionException("Cannot commit more than stack.");
            Stack -= amount;
            CommittedThisStreet += amount;
            TotalCommitted += amount;
            if (Stack == 0) AllIn = true;
            return amount;
        }
    }

    /// <summary>
    /// One street of betting. Tracks the current bet, minimum raise, and whose
    /// turn it is. Emits engine events through the callback provided by PokerRound.
    /// </summary>
    public sealed class BettingRound
    {
        private readonly List<SeatState> _seats; // hand seating order (small blind first)
        private readonly long _bigBlind;
        private readonly Action<PokerEvent> _emit;
        private int _actorIndex = -1; // index into _seats

        /// <summary>Highest total committed to this street that others must match.</summary>
        public long CurrentBet { get; private set; }

        /// <summary>Minimum increment for the next full raise.</summary>
        public long MinRaiseIncrement { get; private set; }

        public BettingRound(List<SeatState> seats, long bigBlind, Action<PokerEvent> emit)
        {
            _seats = seats;
            _bigBlind = bigBlind;
            _emit = emit;
            MinRaiseIncrement = bigBlind;
        }

        /// <summary>
        /// Begin the street. For preflop, blinds have already been committed and
        /// <paramref name="openingBet"/> is the big blind amount.
        /// </summary>
        public void Begin(int firstActorSeatsIndex, long openingBet)
        {
            CurrentBet = openingBet;
            MinRaiseIncrement = _bigBlind;
            _actorIndex = PreviousIndex(firstActorSeatsIndex);
        }

        public SeatState CurrentActor =>
            _actorIndex >= 0 ? _seats[_actorIndex] : null;

        private int PreviousIndex(int index) => (index - 1 + _seats.Count) % _seats.Count;

        /// <summary>Number of seats still in the hand (not folded).</summary>
        public int SeatsInHand
        {
            get
            {
                int count = 0;
                foreach (SeatState s in _seats)
                    if (s.InHand) count++;
                return count;
            }
        }

        /// <summary>
        /// True when no further action is possible or required on this street:
        /// every non-folded seat is all-in, or has acted and matched the current
        /// bet. A single remaining non-all-in seat cannot bet into opponents who
        /// are all all-in, so the street also completes once it has matched.
        /// </summary>
        public bool IsComplete
        {
            get
            {
                if (SeatsInHand <= 1) return true;

                SeatState loneActive = null;
                int activeCount = 0;
                foreach (SeatState s in _seats)
                {
                    if (!s.CanStillAct) continue;
                    activeCount++;
                    loneActive = s;
                }

                if (activeCount == 0) return true;
                if (activeCount == 1)
                    return loneActive.CommittedThisStreet == CurrentBet;

                foreach (SeatState s in _seats)
                {
                    if (!s.CanStillAct) continue;
                    if (!s.HasActedThisStreet) return false;
                    if (s.CommittedThisStreet != CurrentBet) return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Advance to the next seat that must act. Returns null if the round is complete.
        /// </summary>
        public SeatState MoveToNextActor()
        {
            if (IsComplete)
            {
                _actorIndex = -1;
                return null;
            }
            for (int step = 1; step <= _seats.Count; step++)
            {
                int idx = (_actorIndex + step) % _seats.Count;
                SeatState s = _seats[idx];
                if (!s.CanStillAct) continue;
                if (!s.HasActedThisStreet || s.CommittedThisStreet != CurrentBet)
                {
                    _actorIndex = idx;
                    return s;
                }
            }
            _actorIndex = -1;
            return null;
        }

        public LegalActions GetLegalActions(SeatState seat)
        {
            long toCall = CurrentBet - seat.CommittedThisStreet;
            long maxTo = seat.CommittedThisStreet + seat.Stack;

            var legal = new LegalActions
            {
                CanFold = true,
                CanCheck = toCall == 0,
                CanCall = toCall > 0 && seat.Stack > 0,
                CallAmount = Math.Min(toCall, seat.Stack),
                CanAllIn = seat.Stack > 0,
                MaxBetOrRaiseTo = maxTo
            };

            if (CurrentBet == 0)
            {
                legal.CanBet = seat.Stack > 0;
                legal.MinBetOrRaiseTo = Math.Min(_bigBlind, maxTo);
            }
            else
            {
                legal.CanRaise = maxTo > CurrentBet;
                legal.MinBetOrRaiseTo = Math.Min(CurrentBet + MinRaiseIncrement, maxTo);
            }

            return legal;
        }

        /// <summary>
        /// Validate and apply the current actor's action, emitting the
        /// corresponding engine event.
        /// </summary>
        public void Apply(SeatState seat, PlayerAction action)
        {
            LegalActions legal = GetLegalActions(seat);

            switch (action.Type)
            {
                case ActionType.Fold:
                    seat.Folded = true;
                    seat.HasActedThisStreet = true;
                    _emit(new PlayerFolded { Seat = seat.Seat });
                    break;

                case ActionType.Check:
                    if (!legal.CanCheck)
                        throw new IllegalActionException($"Seat {seat.Seat} cannot check facing a bet of {CurrentBet}.");
                    seat.HasActedThisStreet = true;
                    _emit(new PlayerChecked { Seat = seat.Seat });
                    break;

                case ActionType.Call:
                {
                    if (!legal.CanCall)
                        throw new IllegalActionException($"Seat {seat.Seat} has nothing to call.");
                    long added = seat.Commit(legal.CallAmount);
                    seat.HasActedThisStreet = true;
                    if (seat.AllIn)
                        _emit(new PlayerAllIn { Seat = seat.Seat, ToAmount = seat.CommittedThisStreet, AddedAmount = added });
                    else
                        _emit(new PlayerCalled { Seat = seat.Seat, AddedAmount = added });
                    break;
                }

                case ActionType.Bet:
                {
                    if (!legal.CanBet)
                        throw new IllegalActionException($"Seat {seat.Seat} cannot bet (facing bet {CurrentBet}).");
                    ValidateSizing(action.Amount, legal, seat);
                    ApplyAggression(seat, action.Amount, isBet: true);
                    break;
                }

                case ActionType.Raise:
                {
                    if (!legal.CanRaise)
                        throw new IllegalActionException($"Seat {seat.Seat} cannot raise.");
                    ValidateSizing(action.Amount, legal, seat);
                    ApplyAggression(seat, action.Amount, isBet: false);
                    break;
                }

                case ActionType.AllIn:
                {
                    if (!legal.CanAllIn)
                        throw new IllegalActionException($"Seat {seat.Seat} has no chips.");
                    long target = seat.CommittedThisStreet + seat.Stack;
                    if (target > CurrentBet)
                    {
                        ApplyAggression(seat, target, isBet: CurrentBet == 0);
                    }
                    else
                    {
                        long added = seat.Commit(seat.Stack);
                        seat.HasActedThisStreet = true;
                        _emit(new PlayerAllIn { Seat = seat.Seat, ToAmount = seat.CommittedThisStreet, AddedAmount = added });
                    }
                    break;
                }

                default:
                    throw new IllegalActionException($"Unknown action {action.Type}.");
            }
        }

        private void ValidateSizing(long toAmount, LegalActions legal, SeatState seat)
        {
            if (toAmount <= 0)
                throw new IllegalActionException("Bet/raise amount must be positive.");
            long added = toAmount - seat.CommittedThisStreet;
            if (added <= 0)
                throw new IllegalActionException("Bet/raise must increase the amount committed.");
            if (added > seat.Stack)
                throw new IllegalActionException($"Seat {seat.Seat} cannot bet {added} with stack {seat.Stack}.");
            if (toAmount > legal.MaxBetOrRaiseTo)
                throw new IllegalActionException($"Bet to {toAmount} exceeds maximum {legal.MaxBetOrRaiseTo}.");
            if (toAmount < legal.MinBetOrRaiseTo && toAmount != legal.MaxBetOrRaiseTo)
                throw new IllegalActionException($"Bet to {toAmount} is below minimum {legal.MinBetOrRaiseTo} (and is not all-in).");
        }

        private void ApplyAggression(SeatState seat, long toAmount, bool isBet)
        {
            long increment = toAmount - CurrentBet;
            long added = seat.Commit(toAmount - seat.CommittedThisStreet);
            bool fullRaise = increment >= MinRaiseIncrement;

            CurrentBet = toAmount;
            if (fullRaise)
            {
                MinRaiseIncrement = increment;
                // A full bet/raise reopens the action for everyone else.
                foreach (SeatState other in _seats)
                    if (other != seat && other.CanStillAct)
                        other.HasActedThisStreet = false;
            }
            // Note: an all-in that is less than a full raise does NOT reopen
            // action, and players who already acted may only call the extra.
            // (Simplification: the engine still lets them re-raise; this only
            // matters for short all-ins and is documented as a v1 limitation.)

            seat.HasActedThisStreet = true;

            if (seat.AllIn)
                _emit(new PlayerAllIn { Seat = seat.Seat, ToAmount = toAmount, AddedAmount = added });
            else if (isBet)
                _emit(new BetPlaced { Seat = seat.Seat, ToAmount = toAmount, AddedAmount = added });
            else
                _emit(new PlayerRaised { Seat = seat.Seat, ToAmount = toAmount, AddedAmount = added });
        }

        /// <summary>Reset per-street state for the next street.</summary>
        public void NextStreet()
        {
            CurrentBet = 0;
            MinRaiseIncrement = _bigBlind;
            _actorIndex = -1;
            foreach (SeatState s in _seats)
            {
                s.CommittedThisStreet = 0;
                s.HasActedThisStreet = false;
            }
        }
    }
}
