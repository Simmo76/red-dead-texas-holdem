using System;
using System.Collections.Generic;
using System.Linq;

namespace CinematicPoker.Engine.Poker
{
    public enum HandPhase
    {
        NotStarted,
        Preflop,
        Flop,
        Turn,
        River,
        Showdown,
        Complete
    }

    /// <summary>
    /// A single hand of Texas Hold'em from blinds to pot award.
    /// Driven as a state machine: the engine advances automatically through
    /// dealing and showdown, and pauses whenever a seat must make a decision.
    /// Pure C# — presentation layers observe via events only.
    /// </summary>
    public sealed class PokerRound
    {
        private readonly TableRules _rules;
        private readonly IDeck _deck;
        private readonly Action<PokerEvent> _emit;
        private readonly List<SeatState> _seats; // ordered: small blind first
        private readonly BettingRound _betting;
        private readonly int _dealerSeat;
        private readonly List<Card> _board = new List<Card>(5);
        private readonly HashSet<int> _dealtCardIndices = new HashSet<int>();

        public HandPhase Phase { get; private set; } = HandPhase.NotStarted;
        public IReadOnlyList<Card> Board => _board;
        public int DealerSeat => _dealerSeat;
        public int SmallBlindSeat { get; }
        public int BigBlindSeat { get; }

        /// <summary>Seat currently facing a decision, or -1 when none.</summary>
        public int CurrentSeat { get; private set; } = -1;

        public bool IsComplete => Phase == HandPhase.Complete;
        public bool WonByFolds { get; private set; }

        /// <summary>Final stack change per seat, available once complete.</summary>
        public IReadOnlyDictionary<int, long> StackChanges => _stackChanges;
        private readonly Dictionary<int, long> _stackChanges = new Dictionary<int, long>();

        /// <summary>Total chips in the middle (all streets).</summary>
        public long PotTotal => _seats.Sum(s => s.TotalCommitted);

        public long CurrentBet => _betting.CurrentBet;
        public long MinRaiseIncrement => _betting.MinRaiseIncrement;

        public IReadOnlyList<SeatState> Seats => _seats;

        /// <param name="playersInHand">
        /// (seat, stack) pairs in table seat order. The hand internally reorders
        /// so the small blind acts as position 0.
        /// </param>
        public PokerRound(
            TableRules rules,
            IReadOnlyList<(int seat, long stack)> playersInHand,
            int dealerSeat,
            IDeck deck,
            Action<PokerEvent> emit)
        {
            if (playersInHand.Count < 2 || playersInHand.Count > 6)
                throw new ArgumentException("A hand requires 2-6 players.");

            _rules = rules;
            _deck = deck;
            _emit = emit ?? (_ => { });
            _dealerSeat = dealerSeat;

            // Order seats so index 0 is the small blind.
            // Heads-up: the dealer IS the small blind.
            var tableOrder = playersInHand.Select(p => p.seat).ToList();
            int dealerPos = tableOrder.IndexOf(dealerSeat);
            if (dealerPos < 0)
                throw new ArgumentException("Dealer seat is not in the hand.");

            int sbOffset = playersInHand.Count == 2 ? 0 : 1;
            _seats = new List<SeatState>(playersInHand.Count);
            for (int i = 0; i < playersInHand.Count; i++)
            {
                var (seat, stack) = playersInHand[(dealerPos + sbOffset + i) % playersInHand.Count];
                _seats.Add(new SeatState { Seat = seat, Stack = stack, StackAtHandStart = stack });
            }

            SmallBlindSeat = _seats[0].Seat;
            BigBlindSeat = _seats[1].Seat;
            _betting = new BettingRound(_seats, rules.BigBlind, _emit);
        }

        public SeatState GetSeat(int seat) => _seats.First(s => s.Seat == seat);

        public void Start()
        {
            if (Phase != HandPhase.NotStarted)
                throw new InvalidOperationException("Hand already started.");

            _emit(new HandStarted
            {
                DealerSeat = _dealerSeat,
                SmallBlindSeat = SmallBlindSeat,
                BigBlindSeat = BigBlindSeat,
                SeatsInHand = _seats.Select(s => s.Seat).ToList()
            });

            // Post blinds (short stacks post what they can and are all-in).
            SeatState sb = _seats[0];
            SeatState bb = _seats[1];
            long sbAmount = Math.Min(_rules.SmallBlind, sb.Stack);
            sb.Commit(sbAmount);
            _emit(new BlindPosted { Seat = sb.Seat, Amount = sbAmount, IsBigBlind = false, IsAllIn = sb.AllIn });

            long bbAmount = Math.Min(_rules.BigBlind, bb.Stack);
            bb.Commit(bbAmount);
            _emit(new BlindPosted { Seat = bb.Seat, Amount = bbAmount, IsBigBlind = true, IsAllIn = bb.AllIn });

            // Deal hole cards: one at a time, two passes, starting with the
            // seat left of the dealer (the small blind, or the big blind heads-up).
            // No burn cards: they have no effect on fairness and complicate
            // deterministic tests.
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (SeatState seat in _seats)
                {
                    Card card = DrawChecked();
                    seat.HoleCards.Add(card);
                    _emit(new CardDealt { Seat = seat.Seat, Card = card });
                }
            }

            Phase = HandPhase.Preflop;
            // Preflop action starts left of the big blind (in heads-up that is
            // the dealer/small blind, which wraps around to seats index 0).
            _betting.Begin(2 % _seats.Count, _rules.BigBlind);
            AdvanceUntilDecision();
        }

        private Card DrawChecked()
        {
            Card card = _deck.Draw();
            if (!_dealtCardIndices.Add(card.Index))
                throw new InvalidOperationException($"Duplicate card dealt: {card}.");
            return card;
        }

        public LegalActions GetLegalActions()
        {
            if (CurrentSeat < 0)
                throw new InvalidOperationException("No player is currently facing a decision.");
            return _betting.GetLegalActions(GetSeat(CurrentSeat));
        }

        public void SubmitAction(PlayerAction action)
        {
            if (Phase == HandPhase.Complete || Phase == HandPhase.NotStarted)
                throw new InvalidOperationException($"Cannot act in phase {Phase}.");
            if (CurrentSeat < 0)
                throw new InvalidOperationException("No player is currently facing a decision.");

            SeatState seat = GetSeat(CurrentSeat);
            _betting.Apply(seat, action);
            CurrentSeat = -1;
            AdvanceUntilDecision();
        }

        private void AdvanceUntilDecision()
        {
            while (true)
            {
                // Hand ends immediately if everyone else folded.
                if (_betting.SeatsInHand == 1)
                {
                    FinishByFolds();
                    return;
                }

                if (!_betting.IsComplete)
                {
                    SeatState actor = _betting.MoveToNextActor();
                    if (actor != null)
                    {
                        CurrentSeat = actor.Seat;
                        return; // pause: awaiting a decision
                    }
                }

                // Street finished: deal the next street or go to showdown.
                if (Phase == HandPhase.River)
                {
                    RunShowdown();
                    return;
                }

                DealNextStreet();
                _betting.NextStreet();

                // Postflop action starts with the first eligible seat left of
                // the dealer. In multiway hands that is the small blind (seats
                // index 0); heads-up the dealer IS the small blind and acts
                // last, so the big blind (index 1) opens. MoveToNextActor skips
                // folded and all-in seats. If fewer than two seats can still
                // act there is no betting and the loop keeps dealing.
                if (_seats.Count(s => s.CanStillAct) >= 2)
                    _betting.Begin(_seats.Count == 2 ? 1 : 0, 0);
            }
        }

        private void DealNextStreet()
        {
            switch (Phase)
            {
                case HandPhase.Preflop:
                {
                    Card c1 = DrawChecked(), c2 = DrawChecked(), c3 = DrawChecked();
                    _board.Add(c1);
                    _board.Add(c2);
                    _board.Add(c3);
                    Phase = HandPhase.Flop;
                    _emit(new FlopDealt { Card1 = c1, Card2 = c2, Card3 = c3 });
                    break;
                }
                case HandPhase.Flop:
                {
                    Card c = DrawChecked();
                    _board.Add(c);
                    Phase = HandPhase.Turn;
                    _emit(new TurnDealt { Card = c });
                    break;
                }
                case HandPhase.Turn:
                {
                    Card c = DrawChecked();
                    _board.Add(c);
                    Phase = HandPhase.River;
                    _emit(new RiverDealt { Card = c });
                    break;
                }
                default:
                    throw new InvalidOperationException($"Cannot deal street in phase {Phase}.");
            }
        }

        private void ReturnUncalledExcess()
        {
            // If the biggest commitment was not matched by anyone, the excess
            // is returned to that seat (an uncalled bet or raise).
            if (_seats.Count < 2) return;
            var ordered = _seats.OrderByDescending(s => s.TotalCommitted).ToList();
            SeatState top = ordered[0];
            long secondMax = ordered[1].TotalCommitted;
            long excess = top.TotalCommitted - secondMax;
            if (excess > 0)
            {
                top.TotalCommitted -= excess;
                top.Stack += excess;
                top.AllIn = top.Stack == 0 && top.AllIn; // no longer all-in if chips returned
                _emit(new UncalledBetReturned { Seat = top.Seat, Amount = excess });
            }
        }

        private void FinishByFolds()
        {
            ReturnUncalledExcess();
            SeatState winner = _seats.First(s => s.InHand);
            long amount = PotTotal;
            winner.Stack += amount;

            WonByFolds = true;
            _emit(new PotAwarded
            {
                PotIndex = 0,
                Amount = amount,
                WinnerSeats = new List<int> { winner.Seat },
                Payouts = new Dictionary<int, long> { { winner.Seat, amount } },
                WinningHand = null
            });
            Complete();
        }

        private void RunShowdown()
        {
            Phase = HandPhase.Showdown;
            ReturnUncalledExcess();

            var revealed = new Dictionary<int, IReadOnlyList<Card>>();
            var values = new Dictionary<int, HandValue>();
            foreach (SeatState s in _seats.Where(s => s.InHand))
            {
                revealed[s.Seat] = s.HoleCards;
                values[s.Seat] = HandEvaluator.Evaluate(s.HoleCards, _board);
            }
            _emit(new ShowdownStarted { RevealedHands = revealed });

            var contributions = _seats.ToDictionary(s => s.Seat, s => s.TotalCommitted);
            var folded = _seats.ToDictionary(s => s.Seat, s => s.Folded);
            List<Pot> pots = SidePotCalculator.Build(contributions, folded);

            // Award side pots first (last created pot belongs to the deepest stacks).
            for (int potIndex = pots.Count - 1; potIndex >= 0; potIndex--)
            {
                Pot pot = pots[potIndex];
                HandValue best = pot.EligibleSeats.Max(seat => values[seat]);
                List<int> winners = pot.EligibleSeats.Where(seat => values[seat] == best).ToList();

                var payouts = SplitPot(pot.Amount, winners);
                foreach (var kv in payouts)
                    GetSeat(kv.Key).Stack += kv.Value;

                _emit(new PotAwarded
                {
                    PotIndex = potIndex,
                    Amount = pot.Amount,
                    WinnerSeats = winners,
                    Payouts = payouts,
                    WinningHand = best
                });
            }

            Complete();
        }

        /// <summary>
        /// Split a pot between winners; odd chips go to the earliest winners in
        /// hand order (left of the dealer first), per standard rules.
        /// </summary>
        private Dictionary<int, long> SplitPot(long amount, List<int> winnerSeats)
        {
            // Order winners by position in _seats (small blind first).
            var ordered = _seats
                .Where(s => winnerSeats.Contains(s.Seat))
                .Select(s => s.Seat)
                .ToList();

            long share = amount / ordered.Count;
            long odd = amount - share * ordered.Count;
            var payouts = new Dictionary<int, long>();
            foreach (int seat in ordered)
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

        private void Complete()
        {
            Phase = HandPhase.Complete;
            CurrentSeat = -1;
            foreach (SeatState s in _seats)
                _stackChanges[s.Seat] = s.Stack - s.StackAtHandStart;

            _emit(new HandCompleted
            {
                WonByFolds = WonByFolds,
                StackChanges = _stackChanges
            });
        }
    }
}
