using System;
using System.Collections.Generic;
using System.Linq;
using CinematicPoker.Engine.Players;

namespace CinematicPoker.Engine.Poker
{
    public enum GamePhase
    {
        WaitingForHand,
        HandInProgress,
        SessionOver
    }

    /// <summary>
    /// A full poker session at one table: seats, stacks, dealer rotation,
    /// hand lifecycle and eliminations. Pure C# — the Unity layer drives it
    /// by calling <see cref="StartHand"/> and <see cref="SubmitAction"/> and
    /// listens to <see cref="EventEmitted"/> for presentation.
    /// </summary>
    public sealed class PokerGame
    {
        private readonly List<PokerPlayer> _players;
        private readonly Func<IDeck> _deckFactory;
        private PokerRound _round;

        public TableRules Rules { get; }
        public int HandNumber { get; private set; }
        public int DealerSeat { get; private set; } = -1;
        public GamePhase Phase { get; private set; } = GamePhase.WaitingForHand;

        /// <summary>All events from the engine flow through here, in order.</summary>
        public event Action<PokerEvent> EventEmitted;

        public IReadOnlyList<PokerPlayer> Players => _players;
        public PokerRound CurrentRound => _round;

        public PokerGame(TableRules rules, IEnumerable<PokerPlayer> players, int? seed = null, Func<IDeck> deckFactory = null)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _players = players?.ToList() ?? throw new ArgumentNullException(nameof(players));

            if (_players.Count < rules.MinPlayers || _players.Count > rules.MaxPlayers)
                throw new ArgumentException($"Player count must be {rules.MinPlayers}-{rules.MaxPlayers}.");
            if (_players.Select(p => p.Seat).Distinct().Count() != _players.Count)
                throw new ArgumentException("Players must occupy distinct seats.");
            if (_players.Count(p => p.IsHuman) > 1)
                throw new ArgumentException("At most one human player is supported (single-player game).");

            var rng = seed.HasValue ? new Random(seed.Value) : new Random();
            _deckFactory = deckFactory ?? (() =>
            {
                var deck = new Deck(rng);
                deck.Shuffle();
                return deck;
            });

            _players.Sort((a, b) => a.Seat.CompareTo(b.Seat));
        }

        /// <summary>Total chips across all stacks and the current pot. Constant for the session.</summary>
        public long TotalChipsInPlay =>
            _players.Sum(p => p.Stack) + (_round != null && !_round.IsComplete ? _round.PotTotal : 0);

        public IReadOnlyList<PokerPlayer> AlivePlayers =>
            _players.Where(p => p.Status != PlayerStatus.Eliminated).ToList();

        public bool IsSessionOver => AlivePlayers.Count <= 1;

        /// <summary>Seat currently facing a decision, or -1.</summary>
        public int CurrentSeat => _round != null && Phase == GamePhase.HandInProgress ? _round.CurrentSeat : -1;

        public PokerPlayer CurrentPlayer =>
            CurrentSeat >= 0 ? GetPlayer(CurrentSeat) : null;

        public PokerPlayer GetPlayer(int seat) => _players.First(p => p.Seat == seat);

        /// <summary>
        /// Start the next hand. May complete instantly (e.g. everyone all-in from
        /// the blinds), so callers must check <see cref="Phase"/> afterwards.
        /// </summary>
        public void StartHand()
        {
            if (Phase == GamePhase.HandInProgress)
                throw new InvalidOperationException("A hand is already in progress.");
            if (IsSessionOver)
                throw new InvalidOperationException("Session is over; not enough players with chips.");

            var alive = AlivePlayers;
            HandNumber++;
            DealerSeat = NextDealerSeat(alive);

            var entries = alive
                .OrderBy(p => p.Seat)
                .Select(p => (p.Seat, p.Stack))
                .ToList();

            _round = new PokerRound(Rules, entries, DealerSeat, _deckFactory(), Emit);
            Phase = GamePhase.HandInProgress;
            _round.Start();
            SyncAfterAdvance();
        }

        private int NextDealerSeat(IReadOnlyList<PokerPlayer> alive)
        {
            var seats = alive.Select(p => p.Seat).OrderBy(s => s).ToList();
            if (DealerSeat < 0)
                return seats[0];

            // Button moves to the next surviving seat clockwise.
            foreach (int seat in seats)
                if (seat > DealerSeat)
                    return seat;
            return seats[0];
        }

        public LegalActions GetLegalActions()
        {
            EnsureHandInProgress();
            return _round.GetLegalActions();
        }

        public void SubmitAction(PlayerAction action)
        {
            EnsureHandInProgress();
            _round.SubmitAction(action);
            SyncAfterAdvance();
        }

        private void SyncAfterAdvance()
        {
            if (!_round.IsComplete)
                return;

            // Copy final stacks back to the session players and eliminate busts.
            foreach (SeatState seat in _round.Seats)
            {
                PokerPlayer player = GetPlayer(seat.Seat);
                player.Stack = seat.Stack;
                if (player.Stack == 0 && player.Status != PlayerStatus.Eliminated)
                {
                    player.Status = PlayerStatus.Eliminated;
                    Emit(new PlayerEliminated { Seat = player.Seat });
                }
            }

            Phase = IsSessionOver ? GamePhase.SessionOver : GamePhase.WaitingForHand;
        }

        private void EnsureHandInProgress()
        {
            if (Phase != GamePhase.HandInProgress || _round == null)
                throw new InvalidOperationException("No hand in progress.");
        }

        private void Emit(PokerEvent evt)
        {
            evt.HandNumber = HandNumber;
            EventEmitted?.Invoke(evt);
        }
    }
}
