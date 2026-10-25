using System;
using System.Collections.Generic;
using CinematicPoker.Engine.Poker;

namespace CinematicPoker.Engine.Coaching
{
    /// <summary>
    /// Passive hand recorder. It copies public facts off engine events and
    /// never deals, bets, or reads the deck. Opponent hole cards are kept
    /// only when a showdown event reveals them.
    /// </summary>
    public sealed class HandReviewRecorder
    {
        private readonly CoachHistoryBuffer _history;
        private readonly int _heroSeat;
        private readonly long _smallBlind;
        private readonly long _bigBlind;
        private string _sessionId;
        private CompletedHandRecord _current;
        private int _nextDecisionId;
        private CoachStreet _street = CoachStreet.Preflop;
        private readonly List<UncalledReturn> _pendingReturns = new List<UncalledReturn>();

        public HandReviewRecorder(string sessionId, int heroSeat, long smallBlind, long bigBlind, CoachHistoryBuffer history = null)
        {
            _sessionId = string.IsNullOrEmpty(sessionId) ? "session" : sessionId;
            _heroSeat = heroSeat;
            _smallBlind = smallBlind;
            _bigBlind = bigBlind;
            _history = history ?? new CoachHistoryBuffer();
        }

        public CoachHistoryBuffer History => _history;
        public CompletedHandRecord Current => _current;
        public CompletedHandRecord LastCompleted => _history.Latest;

        public void BeginSession(string sessionId)
        {
            _sessionId = string.IsNullOrEmpty(sessionId) ? "session" : sessionId;
            _current = null;
            _pendingReturns.Clear();
        }

        /// <summary>
        /// Attach the hand already on the table. A restored sitting does not
        /// replay <see cref="HandStarted"/>, so without this the finishing
        /// <see cref="HandCompleted"/> is ignored and the review stays on the
        /// previous saved hand.
        /// </summary>
        public void ResumeHand(int handNumber, PokerRound round, IReadOnlyDictionary<int, string> names)
        {
            if (handNumber <= 0 || round == null || round.IsComplete) return;
            if (AlreadyRemembered(handNumber)) return;
            if (_current != null && !_current.Complete && _current.HandNumber == handNumber) return;

            _nextDecisionId = 0;
            _log.Clear();
            _pendingReturns.Clear();
            _street = StreetOfBoard(round.Board.Count);
            _current = BlankHand(handNumber, round.DealerSeat);
            foreach (SeatState seat in round.Seats)
            {
                _current.StartingStacks[seat.Seat] = seat.StackAtHandStart;
                if (seat.Seat != _heroSeat) continue;
                _current.HeroFolded = seat.Folded;
                if (seat.HoleCards.Count > 0)
                    _current.HeroHoleCards.AddRange(seat.HoleCards);
            }
            if (round.Board.Count > 0)
                _current.FinalBoard.AddRange(round.Board);
            CopyNames(_current, names);
        }

        /// <summary>
        /// Observe one engine event. <paramref name="round"/> is read only to
        /// copy stacks and the public board; it is not retained.
        /// </summary>
        public void Observe(PokerEvent evt, PokerRound round, IReadOnlyDictionary<int, string> names)
        {
            if (evt == null) return;

            switch (evt)
            {
                case HandStarted started:
                    BeginHand(started, round, names);
                    break;
                case BlindPosted blind when _current != null:
                    AddAction(blind.Seat, blind.IsBigBlind ? "big-blind" : "small-blind", 0, blind.Amount, CoachStreet.Preflop);
                    break;
                case CardDealt dealt when _current != null && dealt.Seat == _heroSeat && dealt.Card.Rank >= Rank.Two:
                    if (_current.HeroHoleCards.Count < 2)
                        _current.HeroHoleCards.Add(dealt.Card);
                    break;
                case FlopDealt flop when _current != null:
                    _street = CoachStreet.Flop;
                    _current.FinalBoard.Clear();
                    _current.FinalBoard.Add(flop.Card1);
                    _current.FinalBoard.Add(flop.Card2);
                    _current.FinalBoard.Add(flop.Card3);
                    break;
                case TurnDealt turn when _current != null:
                    _street = CoachStreet.Turn;
                    _current.FinalBoard.Add(turn.Card);
                    break;
                case RiverDealt river when _current != null:
                    _street = CoachStreet.River;
                    _current.FinalBoard.Add(river.Card);
                    break;
                case PlayerChecked check when _current != null:
                    AddAction(check.Seat, "check", 0, 0, _street);
                    break;
                case PlayerCalled call when _current != null:
                    AddAction(call.Seat, "call", 0, call.AddedAmount, _street);
                    break;
                case BetPlaced bet when _current != null:
                    AddAction(bet.Seat, "bet", bet.ToAmount, bet.AddedAmount, _street);
                    break;
                case PlayerRaised raise when _current != null:
                    AddAction(raise.Seat, "raise", raise.ToAmount, raise.AddedAmount, _street);
                    break;
                case PlayerFolded folded when _current != null:
                    if (folded.Seat == _heroSeat) _current.HeroFolded = true;
                    AddAction(folded.Seat, "fold", 0, 0, _street);
                    break;
                case PlayerAllIn allIn when _current != null:
                    AddAction(allIn.Seat, "all-in", allIn.ToAmount, allIn.AddedAmount, _street);
                    break;
                case ShowdownStarted showdown when _current != null:
                    CopyRevealed(showdown);
                    break;
                case UncalledBetReturned returned:
                    var uncalled = new UncalledReturn { Seat = returned.Seat, Amount = returned.Amount };
                    if (_current == null) _pendingReturns.Add(uncalled);
                    else _current.Returns.Add(uncalled);
                    break;
                case PotAwarded award when _current != null:
                    CopyAward(award);
                    break;
                case HandCompleted completed:
                    // HandStarted never arrived (a resumed hand, or a recorder
                    // attached after the deal). Leaving LastCompleted on the
                    // previous showdown is how a fold win gets explained as
                    // "their pair beat your pair".
                    if (completed.HandNumber > 0
                        && (_current == null || _current.Complete || _current.HandNumber != completed.HandNumber))
                    {
                        RecoverMissedHand(completed, round, names);
                    }
                    else if (_current != null && !_current.Complete)
                    {
                        Finish(completed, round, names);
                    }
                    break;
            }
        }

        /// <summary>
        /// Copy the offer the hero is looking at. Call this before the action
        /// is submitted. Returns null when it is not the hero's decision.
        /// </summary>
        public HeroDecisionSnapshot CaptureOffer(PokerRound round, LegalActions legal)
        {
            if (_current == null || round == null || legal == null) return null;
            if (round.CurrentSeat != _heroSeat) return null;

            SeatState hero = null;
            foreach (SeatState seat in round.Seats)
                if (seat.Seat == _heroSeat) hero = seat;
            if (hero == null) return null;

            var snapshot = new HeroDecisionSnapshot
            {
                SessionId = _sessionId,
                HandId = _current.HandId,
                DecisionId = ++_nextDecisionId,
                HeroSeat = _heroSeat,
                Street = StreetOf(round.Phase),
                ButtonSeat = round.DealerSeat,
                SmallBlind = _smallBlind,
                BigBlind = _bigBlind,
                PotBefore = round.PotTotal,
                IncrementalCall = legal.CallAmount,
                HeroStack = hero.Stack,
                HeroCommittedTotal = hero.TotalCommitted,
                HeroCommittedThisStreet = hero.CommittedThisStreet,
                MinRaiseTo = legal.MinBetOrRaiseTo,
                MaxRaiseTo = legal.MaxBetOrRaiseTo,
                CanFold = legal.CanFold,
                CanCheck = legal.CanCheck,
                CanCall = legal.CanCall,
                CanBet = legal.CanBet,
                CanRaise = legal.CanRaise,
                CanAllIn = legal.CanAllIn,
                HeroHoleCards = new List<Card>(hero.HoleCards),
                VisibleBoard = new List<Card>(round.Board),
                ActionHistory = CopyActions(_current),
                Seats = new List<SeatPublicState>()
            };

            int heroIndex = -1;
            int index = 0;
            foreach (SeatState seat in round.Seats)
            {
                if (seat.Seat == _heroSeat) heroIndex = index;
                snapshot.Seats.Add(new SeatPublicState
                {
                    Seat = seat.Seat,
                    Stack = seat.Stack,
                    TotalCommitted = seat.TotalCommitted,
                    CommittedThisStreet = seat.CommittedThisStreet,
                    Folded = seat.Folded,
                    AllIn = seat.AllIn,
                    HasActedThisStreet = seat.HasActedThisStreet,
                    CanStillAct = seat.CanStillAct
                });
                if (!seat.Folded && seat.Seat != _heroSeat) snapshot.ActiveOpponents++;
                index++;
            }

            if (heroIndex >= 0)
            {
                var seats = snapshot.Seats;
                for (int i = heroIndex + 1; i < seats.Count; i++)
                    if (seats[i].CanStillAct) snapshot.PlayersBehind++;
            }

            long heroAfter = hero.TotalCommitted + legal.CallAmount;
            foreach (SeatState seat in round.Seats)
            {
                if (seat.Seat == _heroSeat || seat.Folded) continue;
                if (seat.TotalCommitted < heroAfter)
                {
                    snapshot.SidePotSpot = true;
                    break;
                }
            }

            _current.Decisions.Add(snapshot);
            return snapshot;
        }

        public void Accept(HeroDecisionSnapshot offer, PlayerAction action)
        {
            if (_current == null || offer == null) return;
            if (offer.HandId != _current.HandId) return;

            long added = 0;
            long raiseTo = 0;
            string name = action.Type.ToString().ToLowerInvariant();
            switch (action.Type)
            {
                case ActionType.Call:
                    added = offer.IncrementalCall;
                    name = "call";
                    break;
                case ActionType.Bet:
                    raiseTo = action.Amount;
                    added = Math.Max(0, action.Amount - offer.HeroCommittedThisStreet);
                    name = "bet";
                    break;
                case ActionType.Raise:
                    raiseTo = action.Amount;
                    added = Math.Max(0, action.Amount - offer.HeroCommittedThisStreet);
                    name = "raise";
                    break;
                case ActionType.AllIn:
                    added = offer.HeroStack;
                    raiseTo = offer.HeroCommittedThisStreet + offer.HeroStack;
                    name = "all-in";
                    break;
                case ActionType.Check:
                    name = "check";
                    break;
                case ActionType.Fold:
                    name = "fold";
                    break;
            }

            _current.HeroActions.Add(new AcceptedHeroAction
            {
                DecisionId = offer.DecisionId,
                Action = name,
                AddedAmount = added,
                RaiseTo = raiseTo
            });
        }

        private void BeginHand(HandStarted started, PokerRound round, IReadOnlyDictionary<int, string> names)
        {
            _street = CoachStreet.Preflop;
            _nextDecisionId = 0;
            _log.Clear();
            _pendingReturns.Clear();
            _current = BlankHand(started.HandNumber, started.DealerSeat);
            if (round != null)
            {
                foreach (SeatState seat in round.Seats)
                    _current.StartingStacks[seat.Seat] = seat.StackAtHandStart;
            }
            CopyNames(_current, names);
        }

        private CompletedHandRecord BlankHand(int handNumber, int buttonSeat)
        {
            return new CompletedHandRecord
            {
                SessionId = _sessionId,
                HandId = _sessionId + ":" + handNumber,
                HandNumber = handNumber,
                HeroSeat = _heroSeat,
                ButtonSeat = buttonSeat,
                SmallBlind = _smallBlind,
                BigBlind = _bigBlind
            };
        }

        private bool AlreadyRemembered(int handNumber)
        {
            string id = _sessionId + ":" + handNumber;
            foreach (CompletedHandRecord hand in _history.Hands)
                if (hand != null && hand.HandId == id) return true;
            return false;
        }

        /// <summary>
        /// Build the finished hand from the round when its opening event was missed.
        /// Awards are rebuilt from the same pot layering the round uses, because
        /// the <see cref="PotAwarded"/> events were dropped with no open record.
        /// </summary>
        private void RecoverMissedHand(HandCompleted completed, PokerRound round, IReadOnlyDictionary<int, string> names)
        {
            if (round == null || !round.IsComplete) return;
            if (AlreadyRemembered(completed.HandNumber)) return;

            _nextDecisionId = 0;
            _log.Clear();
            _street = StreetOfBoard(round.Board.Count);
            _current = BlankHand(completed.HandNumber, round.DealerSeat);
            _current.FinalBoard = new List<Card>(round.Board);
            foreach (SeatState seat in round.Seats)
            {
                _current.StartingStacks[seat.Seat] = seat.StackAtHandStart;
                if (seat.Seat == _heroSeat)
                {
                    _current.HeroHoleCards = new List<Card>(seat.HoleCards);
                    _current.HeroFolded = seat.Folded;
                }
                if (completed.WonByFolds || seat.Folded || seat.HoleCards.Count < 2) continue;
                _current.RevealedHands.Add(new RevealedHand
                {
                    Seat = seat.Seat,
                    HoleCards = new List<Card>(seat.HoleCards)
                });
            }
            if (_pendingReturns.Count > 0)
            {
                _current.Returns.AddRange(_pendingReturns);
                _pendingReturns.Clear();
            }
            RebuildAwards(_current, round, completed.WonByFolds);
            CopyNames(_current, names);
            Finish(completed, round, names);
        }

        private static void RebuildAwards(CompletedHandRecord hand, PokerRound round, bool wonByFolds)
        {
            if (wonByFolds)
            {
                SeatState winner = null;
                foreach (SeatState seat in round.Seats)
                {
                    if (!seat.InHand) continue;
                    winner = seat;
                    break;
                }
                if (winner == null) return;
                hand.Awards.Add(new PotAwardRecord
                {
                    PotIndex = 0,
                    Amount = round.PotTotal,
                    ByFold = true,
                    WinnerSeats = new List<int> { winner.Seat },
                    Payouts = new Dictionary<int, long> { { winner.Seat, round.PotTotal } }
                });
                return;
            }

            if (round.Board == null || round.Board.Count < 5) return;

            var contributions = new Dictionary<int, long>();
            var folded = new Dictionary<int, bool>();
            var values = new Dictionary<int, HandValue>();
            foreach (SeatState seat in round.Seats)
            {
                contributions[seat.Seat] = seat.TotalCommitted;
                folded[seat.Seat] = seat.Folded;
                if (!seat.Folded && seat.HoleCards.Count >= 2)
                    values[seat.Seat] = HandEvaluator.Evaluate(seat.HoleCards, round.Board);
            }

            List<Pot> pots = SidePotCalculator.Build(contributions, folded);
            for (int potIndex = 0; potIndex < pots.Count; potIndex++)
            {
                Pot pot = pots[potIndex];
                HandValue best = default;
                bool any = false;
                var winners = new List<int>();
                foreach (SeatState seat in round.Seats)
                {
                    if (!pot.EligibleSeats.Contains(seat.Seat)) continue;
                    if (!values.TryGetValue(seat.Seat, out HandValue value)) continue;
                    if (!any || value > best)
                    {
                        best = value;
                        any = true;
                        winners.Clear();
                        winners.Add(seat.Seat);
                    }
                    else if (value == best)
                    {
                        winners.Add(seat.Seat);
                    }
                }
                if (!any) continue;
                hand.Awards.Add(new PotAwardRecord
                {
                    PotIndex = potIndex,
                    Amount = pot.Amount,
                    ByFold = false,
                    WinnerSeats = winners,
                    Payouts = SplitEven(pot.Amount, winners)
                });
            }
        }

        private static Dictionary<int, long> SplitEven(long amount, List<int> winners)
        {
            var payouts = new Dictionary<int, long>();
            if (winners == null || winners.Count == 0 || amount <= 0) return payouts;
            long share = amount / winners.Count;
            long odd = amount - share * winners.Count;
            foreach (int seat in winners)
            {
                long payout = share;
                if (odd > 0)
                {
                    payout++;
                    odd--;
                }
                payouts[seat] = payout;
            }
            return payouts;
        }

        private static CoachStreet StreetOfBoard(int boardCount)
        {
            if (boardCount >= 5) return CoachStreet.River;
            if (boardCount == 4) return CoachStreet.Turn;
            if (boardCount == 3) return CoachStreet.Flop;
            return CoachStreet.Preflop;
        }

        private void Finish(HandCompleted completed, PokerRound round, IReadOnlyDictionary<int, string> names)
        {
            _current.WonByFolds = completed.WonByFolds;
            _current.Complete = true;
            if (completed.StackChanges != null)
            {
                foreach (var kv in completed.StackChanges)
                    _current.StackChanges[kv.Key] = kv.Value;
                if (_current.StackChanges.TryGetValue(_heroSeat, out long net))
                    _current.HeroNet = net;
            }
            if (round != null)
            {
                _current.FinalBoard = new List<Card>(round.Board);
                foreach (SeatState seat in round.Seats)
                    _current.EndingStacks[seat.Seat] = seat.Stack;
            }
            CopyNames(_current, names);
            _history.Remember(_current);
        }

        private void CopyRevealed(ShowdownStarted showdown)
        {
            _current.RevealedHands.Clear();
            if (showdown.RevealedHands == null) return;
            foreach (var kv in showdown.RevealedHands)
            {
                _current.RevealedHands.Add(new RevealedHand
                {
                    Seat = kv.Key,
                    HoleCards = kv.Value == null ? new List<Card>() : new List<Card>(kv.Value)
                });
            }
        }

        private void CopyAward(PotAwarded award)
        {
            var copy = new PotAwardRecord
            {
                PotIndex = award.PotIndex,
                Amount = award.Amount,
                ByFold = !award.WinningHand.HasValue,
                WinnerSeats = award.WinnerSeats == null ? new List<int>() : new List<int>(award.WinnerSeats),
                Payouts = new Dictionary<int, long>()
            };
            if (award.Payouts != null)
            {
                foreach (var kv in award.Payouts)
                    copy.Payouts[kv.Key] = kv.Value;
            }
            _current.Awards.Add(copy);
        }

        private void AddAction(int seat, string action, long toAmount, long added, CoachStreet street)
        {
            // Action history lives on each later snapshot. Keep a hand-level
            // log by stashing it on the first decision's history only when
            // decisions exist; the dedicated list is Decisions' history plus
            // this running log stored in a side list via a synthetic field.
            _log.Add(new PublicAction
            {
                Seat = seat,
                Action = action,
                ToAmount = toAmount,
                AddedAmount = added,
                Street = street
            });
        }

        private readonly List<PublicAction> _log = new List<PublicAction>();

        private List<PublicAction> CopyActions(CompletedHandRecord hand)
        {
            var copy = new List<PublicAction>(_log.Count);
            foreach (PublicAction action in _log)
            {
                copy.Add(new PublicAction
                {
                    Seat = action.Seat,
                    Action = action.Action,
                    ToAmount = action.ToAmount,
                    AddedAmount = action.AddedAmount,
                    Street = action.Street
                });
            }
            return copy;
        }

        private static void CopyNames(CompletedHandRecord hand, IReadOnlyDictionary<int, string> names)
        {
            if (names == null) return;
            hand.SeatNames.Clear();
            foreach (var kv in names)
                if (!string.IsNullOrEmpty(kv.Value))
                    hand.SeatNames[kv.Key] = kv.Value;
        }

        private static CoachStreet StreetOf(HandPhase phase) => phase switch
        {
            HandPhase.Flop => CoachStreet.Flop,
            HandPhase.Turn => CoachStreet.Turn,
            HandPhase.River => CoachStreet.River,
            _ => CoachStreet.Preflop
        };
    }
}
