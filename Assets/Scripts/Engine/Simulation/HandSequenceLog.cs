using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CinematicPoker.Engine.Players;
using CinematicPoker.Engine.Poker;

namespace CinematicPoker.Engine.Simulation
{
    /// <summary>
    /// Records every hand in play order and formats a markdown transcript for
    /// payout / conservation / showdown math checks.
    /// </summary>
    public sealed class HandSequenceLog
    {
        private readonly List<SessionHeader> _sessions = new List<SessionHeader>();
        private readonly List<HandRecord> _hands = new List<HandRecord>();
        private SessionHeader _currentSession;
        private HandRecord _currentHand;
        private int _sequentialHandNumber;

        public IReadOnlyList<HandRecord> Hands => _hands;

        /// <summary>Hands that have reached HandCompleted (safe to export).</summary>
        public int CompletedHandCount
        {
            get
            {
                int n = 0;
                foreach (HandRecord hand in _hands)
                {
                    if (hand.StackChanges != null && hand.StackChanges.Count > 0)
                        n++;
                }
                return n;
            }
        }

        public void BeginSession(int sessionIndex, TableRules rules, IReadOnlyList<PokerPlayer> players)
        {
            _currentSession = new SessionHeader
            {
                SessionIndex = sessionIndex,
                Rules = rules,
                PlayerNames = players.ToDictionary(p => p.Seat, p => p.Name)
            };
            _sessions.Add(_currentSession);
        }

        public void OnEvent(PokerEvent evt)
        {
            switch (evt)
            {
                case HandStarted hs:
                    _sequentialHandNumber++;
                    _currentHand = new HandRecord
                    {
                        SequentialNumber = _sequentialHandNumber,
                        EngineHandNumber = hs.HandNumber,
                        SessionIndex = _currentSession?.SessionIndex ?? 0,
                        DealerSeat = hs.DealerSeat,
                        SeatsInHand = hs.SeatsInHand?.ToList() ?? new List<int>()
                    };
                    _hands.Add(_currentHand);
                    break;
            }

            if (_currentHand == null) return;
            _currentHand.Events.Add(evt);

            switch (evt)
            {
                case CardDealt cd:
                    if (!_currentHand.HoleCards.TryGetValue(cd.Seat, out List<Card> cards))
                    {
                        cards = new List<Card>(2);
                        _currentHand.HoleCards[cd.Seat] = cards;
                    }
                    cards.Add(cd.Card);
                    break;
                case FlopDealt f:
                    _currentHand.Board.Add(f.Card1);
                    _currentHand.Board.Add(f.Card2);
                    _currentHand.Board.Add(f.Card3);
                    break;
                case TurnDealt t:
                    _currentHand.Board.Add(t.Card);
                    break;
                case RiverDealt r:
                    _currentHand.Board.Add(r.Card);
                    break;
                case PotAwarded pa:
                    _currentHand.PotAwards.Add(pa);
                    break;
                case HandCompleted hc:
                    _currentHand.WonByFolds = hc.WonByFolds;
                    _currentHand.StackChanges = hc.StackChanges != null
                        ? new Dictionary<int, long>(hc.StackChanges)
                        : new Dictionary<int, long>();
                    _currentHand.StackChangeSum = _currentHand.StackChanges.Values.Sum();
                    _currentHand = null;
                    break;
            }
        }

        public void SetChipsBeforeHand(long chipsBefore)
        {
            if (_currentHand != null)
                _currentHand.ChipsBefore = chipsBefore;
        }

        public void SetChipsAfterHand(long chipsAfter)
        {
            if (_hands.Count > 0 && _currentHand == null)
                _hands[_hands.Count - 1].ChipsAfter = chipsAfter;
        }

        /// <summary>
        /// Player-facing CSV: session summary stats plus one history row per hand
        /// (your cards, board, result, stack change, and action transcript).
        /// </summary>
        public string BuildPlayerCsv(int humanSeat, PlayerExportMeta meta = null)
        {
            var sb = new StringBuilder();
            WritePlayerCsv(new StringWriter(sb), humanSeat, meta);
            return sb.ToString();
        }

        public void WritePlayerCsv(TextWriter writer, int humanSeat, PlayerExportMeta meta = null)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            long handsWon = 0;
            long showdowns = 0;
            long biggestPotWon = 0;
            long netProfit = 0;
            var completed = new List<HandRecord>();
            foreach (HandRecord hand in _hands)
            {
                // Skip the live hand still in progress — export only settled ones.
                if (hand.StackChanges == null || hand.StackChanges.Count == 0)
                    continue;
                completed.Add(hand);
                if (hand.StackChanges.TryGetValue(humanSeat, out long delta))
                    netProfit += delta;
                if (PlayerWonHand(hand, humanSeat))
                {
                    handsWon++;
                    long won = PlayerWonAmount(hand, humanSeat);
                    if (won > biggestPotWon) biggestPotWon = won;
                }
                if (!hand.WonByFolds && hand.PotAwards.Count > 0)
                    showdowns++;
            }

            writer.WriteLine("Section,Metric,Value");
            writer.WriteLine(CsvRow("Summary", "Exported At (UTC)", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")));
            writer.WriteLine(CsvRow("Summary", "Hands Played", completed.Count.ToString()));
            writer.WriteLine(CsvRow("Summary", "Hands Won", handsWon.ToString()));
            writer.WriteLine(CsvRow("Summary", "Showdowns", showdowns.ToString()));
            writer.WriteLine(CsvRow("Summary", "Biggest Pot Won", biggestPotWon.ToString()));
            writer.WriteLine(CsvRow("Summary", "Net Profit", netProfit.ToString()));
            if (meta != null)
            {
                if (meta.StartingStack.HasValue)
                    writer.WriteLine(CsvRow("Summary", "Starting Stack", meta.StartingStack.Value.ToString()));
                if (meta.CurrentStack.HasValue)
                    writer.WriteLine(CsvRow("Summary", "Current Stack", meta.CurrentStack.Value.ToString()));
                if (!string.IsNullOrEmpty(meta.Difficulty))
                    writer.WriteLine(CsvRow("Summary", "Difficulty", meta.Difficulty));
                if (!string.IsNullOrEmpty(meta.PlayMode))
                    writer.WriteLine(CsvRow("Summary", "Play Mode", meta.PlayMode));
                if (!string.IsNullOrEmpty(meta.Backdrop))
                    writer.WriteLine(CsvRow("Summary", "Backdrop", meta.Backdrop));
            }
            writer.WriteLine();

            // Coach's corner: leak indicators plus what to work on, so the
            // export reads like a session review rather than a bare ledger.
            PlayerCoachReport coach = BuildCoachReport(humanSeat);
            writer.WriteLine(CsvRow("Coaching", "Preflop Hands Played (VPIP)",
                $"{(int)Math.Round(coach.VpipPercent)}% ({coach.VoluntaryPreflopHands} of {coach.HandsAnalysed})"));
            writer.WriteLine(CsvRow("Coaching", "Preflop Raise Rate (PFR)",
                $"{(int)Math.Round(coach.PfrPercent)}% ({coach.PreflopRaiseHands} of {coach.HandsAnalysed})"));
            writer.WriteLine(CsvRow("Coaching", "Postflop Bets+Raises vs Calls",
                $"{coach.PostflopAggressiveActions} vs {coach.PostflopCalls}"));
            writer.WriteLine(CsvRow("Coaching", "Showdowns Won",
                $"{coach.ShowdownsWon} of {coach.ShowdownsReached}"));
            writer.WriteLine(CsvRow("Coaching", "Pots Taken Without Showdown",
                coach.PotsWonWithoutShowdown.ToString()));
            int tipNumber = 1;
            foreach (string tip in coach.Advice)
                writer.WriteLine(CsvRow("Coach's Advice", (tipNumber++).ToString(), tip));
            writer.WriteLine();

            writer.WriteLine("Hand,Result,Your Cards,Board,Won Amount,Stack Change,Winning Hand,Won By Folds,Actions");
            foreach (HandRecord hand in completed)
            {
                Func<int, string> nameOf = PlayerNameFor(hand);
                string yourCards = hand.HoleCards.TryGetValue(humanSeat, out List<Card> hole)
                    ? FormatCards(hole)
                    : "";
                string board = FormatCards(hand.Board);
                bool won = PlayerWonHand(hand, humanSeat);
                long wonAmount = PlayerWonAmount(hand, humanSeat);
                long stackChange = 0;
                hand.StackChanges.TryGetValue(humanSeat, out stackChange);
                string result = won ? "Won" : PlayerFoldedHand(hand, humanSeat) ? "Folded" : "Lost";
                string winningHand = "";
                foreach (PotAwarded pa in hand.PotAwards)
                {
                    if (pa.WinnerSeats != null && pa.WinnerSeats.Contains(humanSeat) && pa.WinningHand.HasValue)
                    {
                        winningHand = pa.WinningHand.Value.Describe();
                        break;
                    }
                    if (string.IsNullOrEmpty(winningHand) && pa.WinningHand.HasValue)
                        winningHand = pa.WinningHand.Value.Describe();
                }
                string actions = string.Join("; ", DescribeActions(hand.Events, nameOf));
                writer.WriteLine(string.Join(",",
                    hand.SequentialNumber.ToString(),
                    CsvEscape(result),
                    CsvEscape(yourCards),
                    CsvEscape(board),
                    wonAmount.ToString(),
                    stackChange.ToString(),
                    CsvEscape(winningHand),
                    hand.WonByFolds ? "True" : "False",
                    CsvEscape(actions)));
            }
        }

        /// <summary>Coach-style leak analysis of the recorded hands for one seat.</summary>
        public PlayerCoachReport BuildCoachReport(int humanSeat) =>
            PlayerCoachReport.Build(_hands, humanSeat);

        private static bool PlayerWonHand(HandRecord hand, int humanSeat)
        {
            foreach (PotAwarded pa in hand.PotAwards)
            {
                if (pa.WinnerSeats != null && pa.WinnerSeats.Contains(humanSeat))
                    return true;
            }
            return false;
        }

        private static long PlayerWonAmount(HandRecord hand, int humanSeat)
        {
            long total = 0;
            foreach (PotAwarded pa in hand.PotAwards)
            {
                if (pa.Payouts != null && pa.Payouts.TryGetValue(humanSeat, out long paid))
                    total += paid;
                else if (pa.WinnerSeats != null && pa.WinnerSeats.Contains(humanSeat) && pa.WinnerSeats.Count > 0)
                    total += pa.Amount / pa.WinnerSeats.Count;
            }
            return total;
        }

        private static bool PlayerFoldedHand(HandRecord hand, int humanSeat)
        {
            foreach (PokerEvent evt in hand.Events)
            {
                if (evt is PlayerFolded folded && folded.Seat == humanSeat)
                    return true;
            }
            return false;
        }

        private static string CsvRow(string section, string metric, string value) =>
            string.Join(",", CsvEscape(section), CsvEscape(metric), CsvEscape(value));

        private static string CsvEscape(string value)
        {
            if (value == null) return "";
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        public void WriteMarkdown(TextWriter writer, int seed, int targetHands, SimulationResult summary = null)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            writer.WriteLine("# Hand sequence log");
            writer.WriteLine();
            writer.WriteLine($"Generated from the Cinematic Poker engine simulation runner.");
            writer.WriteLine();
            writer.WriteLine("| Parameter | Value |");
            writer.WriteLine("| --- | --- |");
            writer.WriteLine($"| Seed | {seed} |");
            writer.WriteLine($"| Hands recorded | {_hands.Count} (target {targetHands}) |");
            writer.WriteLine($"| Recorded at (UTC) | {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} |");
            if (summary != null)
            {
                writer.WriteLine($"| Showdowns | {summary.ShowdownCount} |");
                writer.WriteLine($"| Fold wins | {summary.FoldWinCount} |");
                writer.WriteLine($"| Side-pot hands | {summary.SidePotHands} |");
            }
            writer.WriteLine();
            writer.WriteLine("Hands appear in **strict play order** (sequential hand #). Use stack-change sums and chip totals to verify conservation.");
            writer.WriteLine();

            int sessionIdx = -1;
            foreach (HandRecord hand in _hands)
            {
                if (hand.SessionIndex != sessionIdx)
                {
                    sessionIdx = hand.SessionIndex;
                    SessionHeader session = _sessions.FirstOrDefault(s => s.SessionIndex == sessionIdx);
                    writer.WriteLine($"## Session {sessionIdx + 1}");
                    if (session != null)
                    {
                        writer.WriteLine();
                        writer.WriteLine(
                            $"Blinds **{session.Rules.SmallBlind}/{session.Rules.BigBlind}**, starting stack **{session.Rules.StartingStack}**, " +
                            $"{session.PlayerNames.Count} players.");
                        writer.WriteLine();
                        writer.WriteLine("| Seat | Player |");
                        writer.WriteLine("| --- | --- |");
                        foreach (KeyValuePair<int, string> kv in session.PlayerNames.OrderBy(k => k.Key))
                            writer.WriteLine($"| {kv.Key} | {kv.Value} |");
                        writer.WriteLine();
                    }
                }

                WriteHand(writer, hand, PlayerNameFor(hand));
            }
        }

        private Func<int, string> PlayerNameFor(HandRecord hand)
        {
            SessionHeader session = _sessions.FirstOrDefault(s => s.SessionIndex == hand.SessionIndex);
            return seat =>
            {
                if (session?.PlayerNames != null && session.PlayerNames.TryGetValue(seat, out string name))
                    return name;
                return $"Seat {seat}";
            };
        }

        private static void WriteHand(TextWriter writer, HandRecord hand, Func<int, string> nameOf)
        {
            writer.WriteLine($"### Hand {hand.SequentialNumber} (engine hand {hand.EngineHandNumber})");
            writer.WriteLine();
            writer.WriteLine("| Field | Value |");
            writer.WriteLine("| --- | --- |");
            writer.WriteLine($"| Dealer | {nameOf(hand.DealerSeat)} (seat {hand.DealerSeat}) |");
            writer.WriteLine($"| Seats dealt in | {string.Join(", ", hand.SeatsInHand.Select(s => nameOf(s)))} |");
            if (hand.ChipsBefore.HasValue)
                writer.WriteLine($"| Total chips before hand | {hand.ChipsBefore.Value} |");
            if (hand.ChipsAfter.HasValue)
                writer.WriteLine($"| Total chips after hand | {hand.ChipsAfter.Value} |");
            if (hand.ChipsBefore.HasValue && hand.ChipsAfter.HasValue)
            {
                bool conserved = hand.ChipsBefore.Value == hand.ChipsAfter.Value;
                writer.WriteLine($"| Chip conservation | {(conserved ? "OK" : "MISMATCH")} |");
            }
            if (hand.StackChangeSum.HasValue)
                writer.WriteLine($"| Sum of per-seat stack deltas | {hand.StackChangeSum.Value} (expect 0) |");
            writer.WriteLine();

            if (hand.HoleCards.Count > 0)
            {
                writer.WriteLine("**Hole cards**");
                writer.WriteLine();
                foreach (KeyValuePair<int, List<Card>> kv in hand.HoleCards.OrderBy(k => k.Key))
                    writer.WriteLine($"- {nameOf(kv.Key)}: {FormatCards(kv.Value)}");
                writer.WriteLine();
            }

            if (hand.Board.Count > 0)
            {
                writer.WriteLine($"**Board:** {FormatCards(hand.Board)}");
                writer.WriteLine();
            }

            List<string> actionLines = DescribeActions(hand.Events, nameOf);
            if (actionLines.Count > 0)
            {
                writer.WriteLine("**Actions**");
                writer.WriteLine();
                foreach (string line in actionLines)
                    writer.WriteLine($"- {line}");
                writer.WriteLine();
            }

            writer.WriteLine(hand.WonByFolds ? "**Outcome:** won by folds (no showdown)" : "**Outcome:** showdown");
            writer.WriteLine();

            if (hand.PotAwards.Count == 0)
            {
                writer.WriteLine("*No pot awards recorded.*");
                writer.WriteLine();
            }
            else
            {
                writer.WriteLine("**Pot awards**");
                writer.WriteLine();
                writer.WriteLine("| Pot | Amount | Winner(s) | Hand | Payouts |");
                writer.WriteLine("| --- | ---: | --- | --- | --- |");
                foreach (PotAwarded pa in hand.PotAwards)
                {
                    string potLabel = pa.PotIndex == 0 ? "Main" : $"Side {pa.PotIndex}";
                    string winners = pa.WinnerSeats != null
                        ? string.Join(", ", pa.WinnerSeats.Select(nameOf))
                        : "—";
                    string category = pa.WinningHand.HasValue ? pa.WinningHand.Value.Describe() : "—";
                    string payouts = FormatPayouts(pa.Payouts, nameOf);
                    writer.WriteLine($"| {potLabel} | {pa.Amount} | {winners} | {category} | {payouts} |");
                }
                writer.WriteLine();
            }

            if (hand.StackChanges != null && hand.StackChanges.Count > 0)
            {
                writer.WriteLine("**Stack changes**");
                writer.WriteLine();
                foreach (KeyValuePair<int, long> kv in hand.StackChanges.OrderBy(k => k.Key))
                {
                    long delta = kv.Value;
                    string sign = delta >= 0 ? "+" : "";
                    writer.WriteLine($"- {nameOf(kv.Key)}: {sign}{delta}");
                }
                writer.WriteLine();
            }
        }

        private static string FormatPayouts(IReadOnlyDictionary<int, long> payouts, Func<int, string> nameOf)
        {
            if (payouts == null || payouts.Count == 0) return "—";
            return string.Join("; ", payouts.OrderBy(k => k.Key).Select(kv => $"{nameOf(kv.Key)}: {kv.Value}"));
        }

        private static List<string> DescribeActions(IReadOnlyList<PokerEvent> events, Func<int, string> nameOf)
        {
            var lines = new List<string>();
            foreach (PokerEvent evt in events)
            {
                switch (evt)
                {
                    case BlindPosted b:
                        lines.Add($"{nameOf(b.Seat)} posts {(b.IsBigBlind ? "BB" : "SB")} {b.Amount}{(b.IsAllIn ? " (all-in)" : "")}");
                        break;
                    case PlayerChecked c:
                        lines.Add($"{nameOf(c.Seat)} checks");
                        break;
                    case PlayerCalled c:
                        lines.Add($"{nameOf(c.Seat)} calls {c.AddedAmount}");
                        break;
                    case BetPlaced b:
                        lines.Add($"{nameOf(b.Seat)} bets to {b.ToAmount} (+{b.AddedAmount})");
                        break;
                    case PlayerRaised r:
                        lines.Add($"{nameOf(r.Seat)} raises to {r.ToAmount} (+{r.AddedAmount})");
                        break;
                    case PlayerFolded f:
                        lines.Add($"{nameOf(f.Seat)} folds");
                        break;
                    case PlayerAllIn a:
                        lines.Add($"{nameOf(a.Seat)} all-in to {a.ToAmount} (+{a.AddedAmount})");
                        break;
                    case UncalledBetReturned u:
                        lines.Add($"{nameOf(u.Seat)} uncalled bet returned {u.Amount}");
                        break;
                }
            }
            return lines;
        }

        public static string FormatCard(Card card) =>
            Card.RankSymbol(card.Rank) + SuitLetter(card.Suit);

        public static string FormatCards(IEnumerable<Card> cards) =>
            string.Join(" ", cards.Select(FormatCard));

        private static string SuitLetter(Suit suit) => suit switch
        {
            Suit.Clubs => "c",
            Suit.Diamonds => "d",
            Suit.Hearts => "h",
            Suit.Spades => "s",
            _ => "?"
        };

        public sealed class SessionHeader
        {
            public int SessionIndex;
            public TableRules Rules;
            public Dictionary<int, string> PlayerNames;
        }

        public sealed class HandRecord
        {
            public int SequentialNumber;
            public int EngineHandNumber;
            public int SessionIndex;
            public int DealerSeat;
            public List<int> SeatsInHand = new List<int>();
            public long? ChipsBefore;
            public long? ChipsAfter;
            public long? StackChangeSum;
            public readonly List<PokerEvent> Events = new List<PokerEvent>();
            public readonly Dictionary<int, List<Card>> HoleCards = new Dictionary<int, List<Card>>();
            public readonly List<Card> Board = new List<Card>();
            public readonly List<PotAwarded> PotAwards = new List<PotAwarded>();
            public bool WonByFolds;
            public Dictionary<int, long> StackChanges = new Dictionary<int, long>();
        }

        /// <summary>Optional session context attached to a player CSV export.</summary>
        public sealed class PlayerExportMeta
        {
            public long? StartingStack;
            public long? CurrentStack;
            public string Difficulty;
            public string PlayMode;
            public string Backdrop;
        }
    }
}
