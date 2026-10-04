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

        /// <summary>The seated human, or null in NPC-only simulations.</summary>
        public PokerPlayer Human => _players.FirstOrDefault(p => p.IsHuman);

        /// <summary>
        /// True once the human is out of chips. Presentation treats this as
        /// game over even if remaining NPCs still have chips among themselves.
        /// </summary>
        public bool HasHumanLost => Human != null && Human.Status == PlayerStatus.Eliminated;

        /// <summary>
        /// True when James still has chips and is the only player left.
        /// He has money to keep going — seat a new table rather than GAME OVER.
        /// </summary>
        public bool HasHumanWon
        {
            get
            {
                PokerPlayer human = Human;
                return human != null
                    && human.Status != PlayerStatus.Eliminated
                    && human.Stack > 0
                    && AlivePlayers.Count == 1
                    && AlivePlayers[0].Seat == human.Seat;
            }
        }

        /// <summary>
        /// True when the human can sit another hand: they still have a stack,
        /// or they busted and can buy back in against remaining opponents.
        /// </summary>
        public bool CanHumanKeepPlaying =>
            HasHumanWon
            || (Human != null && Human.Status != PlayerStatus.Eliminated && Human.Stack >= Rules.BigBlind)
            || HasHumanLost;

        /// <summary>
        /// Buy the human back in after a bust. Adds a fresh starting stack so
        /// they can sit the next hand against whoever is still at the table.
        /// Does not reset NPC stacks, the dealer, or the hand counter.
        /// </summary>
        public void RebuyHuman(long? stack = null)
        {
            PokerPlayer human = Human
                ?? throw new InvalidOperationException("No human player to rebuy.");
            if (Phase == GamePhase.HandInProgress)
                throw new InvalidOperationException("Cannot rebuy during a hand.");
            if (human.Status != PlayerStatus.Eliminated)
                throw new InvalidOperationException("Human is still in the session.");

            long buyIn = stack ?? Rules.StartingStack;
            if (buyIn < Rules.BigBlind)
                throw new ArgumentException("Rebuy must cover the big blind.");

            human.Stack = buyIn;
            human.Status = PlayerStatus.Active;
            Phase = GamePhase.WaitingForHand;
        }

        /// <summary>
        /// Seat a fresh set of opponents after James wins the table. He keeps
        /// his stack; the new players buy in for a starting stack each.
        /// </summary>
        public void ContinueWithNewOpponents(IEnumerable<PokerPlayer> opponents)
        {
            if (Phase == GamePhase.HandInProgress)
                throw new InvalidOperationException("Cannot reseat the table during a hand.");
            PokerPlayer human = Human
                ?? throw new InvalidOperationException("No human player to continue.");
            if (human.Stack < Rules.BigBlind)
                throw new InvalidOperationException("Human does not have enough chips to continue.");

            var incoming = opponents?.ToList() ?? throw new ArgumentNullException(nameof(opponents));
            if (incoming.Count == 0 || incoming.Any(p => p == null || p.IsHuman))
                throw new ArgumentException("Opponents must be non-human players.");

            _players.RemoveAll(p => !p.IsHuman);
            _players.AddRange(incoming);
            if (_players.Count < Rules.MinPlayers || _players.Count > Rules.MaxPlayers)
                throw new ArgumentException($"Player count must be {Rules.MinPlayers}-{Rules.MaxPlayers}.");
            if (_players.Select(p => p.Seat).Distinct().Count() != _players.Count)
                throw new ArgumentException("Players must occupy distinct seats.");

            human.Status = PlayerStatus.Active;
            _round = null;
            DealerSeat = -1;
            Phase = GamePhase.WaitingForHand;
        }

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
