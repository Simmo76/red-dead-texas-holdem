using System.Collections.Generic;
using System.Linq;
using CinematicPoker.Engine.Players;
using CinematicPoker.Engine.Poker;
using CinematicPoker.Engine.Simulation;
using NUnit.Framework;

namespace CinematicPoker.Engine.Tests
{
    [TestFixture]
    public class HandSequenceLogCsvTests
    {
        [Test]
        public void BuildPlayerCsv_IncludesSummaryAndHandRows()
        {
            var rules = TestHelpers.Rules;
            PokerGame game = TestHelpers.CreateGame(new long[] { 1000, 1000, 1000, 1000, 1000, 1000 }, seed: 99);
            var log = new HandSequenceLog();
            log.BeginSession(0, rules, game.Players.ToList());
            game.EventEmitted += log.OnEvent;

            game.StartHand();
            TestHelpers.FoldAround(game);
            game.StartHand();
            TestHelpers.FoldAround(game);

            Assert.AreEqual(2, log.Hands.Count);

            string csv = log.BuildPlayerCsv(0, new HandSequenceLog.PlayerExportMeta
            {
                StartingStack = rules.StartingStack,
                CurrentStack = game.GetPlayer(0).Stack,
                Difficulty = "AI NORMAL",
                PlayMode = "CINEMATIC",
                Backdrop = "SALOON"
            });

            Assert.That(csv, Does.Contain("Section,Metric,Value"));
            Assert.That(csv, Does.Contain("Hands Played"));
            Assert.That(csv, Does.Contain(",2"));
            Assert.That(csv, Does.Contain("Hand,Result,Your Cards,Board"));
            Assert.That(csv, Does.Contain("Difficulty"));
            Assert.That(csv, Does.Contain("AI NORMAL"));
            Assert.That(csv, Does.Contain("Starting Stack"));
            Assert.Greater(csv.Split('\n').Count(l => l.Length > 0 && char.IsDigit(l[0])), 0);
        }

        [Test]
        public void BuildPlayerCsv_EscapesCommasInActions()
        {
            var log = new HandSequenceLog();
            var players = new List<PokerPlayer>
            {
                new HumanPlayer("human", "You", 0, 1000),
                new TestPlayer(1, 1000)
            };
            log.BeginSession(0, TestHelpers.Rules, players);
            log.OnEvent(new HandStarted
            {
                DealerSeat = 0,
                SmallBlindSeat = 0,
                BigBlindSeat = 1,
                SeatsInHand = new List<int> { 0, 1 }
            });
            log.OnEvent(new CardDealt { Seat = 0, Card = Card.Parse("Ah") });
            log.OnEvent(new CardDealt { Seat = 0, Card = Card.Parse("Kd") });
            log.OnEvent(new BetPlaced { Seat = 0, ToAmount = 50, AddedAmount = 40 });
            log.OnEvent(new PlayerFolded { Seat = 1 });
            log.OnEvent(new PotAwarded
            {
                PotIndex = 0,
                Amount = 60,
                WinnerSeats = new List<int> { 0 },
                Payouts = new Dictionary<int, long> { [0] = 60 }
            });
            log.OnEvent(new HandCompleted
            {
                WonByFolds = true,
                StackChanges = new Dictionary<int, long> { [0] = 10, [1] = -10 }
            });

            string csv = log.BuildPlayerCsv(0);
            Assert.That(csv, Does.Contain("Hands Won,1"));
            Assert.That(csv, Does.Contain("Won"));
            Assert.That(csv, Does.Contain("Ah Kd"));
            Assert.That(csv, Does.Contain("You bets to 50 (+40); Test 1 folds"));
            Assert.That(csv, Does.Contain("Biggest Pot Won,60"));
            Assert.That(csv, Does.Contain("Net Profit,10"));
        }

        [Test]
        public void ArchiveCompletedHands_KeepsCsvIdentical()
        {
            HandSequenceLog log = LogWithOneWonHand();

            string before = StripTimestamp(log.BuildPlayerCsv(0));
            int moved = log.ArchiveCompletedHands(0);
            string after = StripTimestamp(log.BuildPlayerCsv(0));

            Assert.AreEqual(1, moved);
            Assert.AreEqual(0, log.Hands.Count);
            Assert.AreEqual(1, log.CompletedHandCount);
            Assert.AreEqual(before, after);
        }

        [Test]
        public void LoadArchive_RestoresHistoryAndContinuesNumbering()
        {
            HandSequenceLog first = LogWithOneWonHand();
            first.ArchiveCompletedHands(0);

            // A fresh log (new table / page reload) seeded with the archive.
            var log = new HandSequenceLog();
            log.LoadArchive(first.ArchivedHands);
            var players = new List<PokerPlayer>
            {
                new HumanPlayer("human", "You", 0, 1000),
                new TestPlayer(1, 1000)
            };
            log.BeginSession(0, TestHelpers.Rules, players);
            Assert.AreEqual(1, log.CompletedHandCount);

            log.OnEvent(new HandStarted
            {
                DealerSeat = 1,
                SmallBlindSeat = 1,
                BigBlindSeat = 0,
                SeatsInHand = new List<int> { 0, 1 }
            });
            log.OnEvent(new CardDealt { Seat = 0, Card = Card.Parse("2c") });
            log.OnEvent(new CardDealt { Seat = 0, Card = Card.Parse("7d") });
            log.OnEvent(new PlayerFolded { Seat = 0 });
            log.OnEvent(new PotAwarded
            {
                PotIndex = 0,
                Amount = 30,
                WinnerSeats = new List<int> { 1 },
                Payouts = new Dictionary<int, long> { [1] = 30 }
            });
            log.OnEvent(new HandCompleted
            {
                WonByFolds = true,
                StackChanges = new Dictionary<int, long> { [0] = -20, [1] = 20 }
            });

            Assert.AreEqual(2, log.CompletedHandCount);
            string csv = log.BuildPlayerCsv(0);
            Assert.That(csv, Does.Contain("Hands Played,2"));
            Assert.That(csv, Does.Contain("Net Profit,-10"));
            // The archived hand keeps row 1; the new hand continues as row 2.
            Assert.That(csv, Does.Contain("\n1,Won,Ah Kd"));
            Assert.That(csv, Does.Contain("\n2,Folded,2c 7d"));
        }

        [Test]
        public void LoadArchive_TrimsOldestHandsPastCap()
        {
            var archive = new List<HandSequenceLog.ArchivedHand>();
            for (int i = 1; i <= HandSequenceLog.MaxArchivedHands + 5; i++)
                archive.Add(new HandSequenceLog.ArchivedHand { Number = i, Result = "Lost" });

            var log = new HandSequenceLog();
            log.LoadArchive(archive);

            Assert.AreEqual(HandSequenceLog.MaxArchivedHands, log.ArchivedHands.Count);
            Assert.AreEqual(6, log.ArchivedHands[0].Number);
            Assert.AreEqual(HandSequenceLog.MaxArchivedHands, log.CompletedHandCount);
        }

        private static HandSequenceLog LogWithOneWonHand()
        {
            var log = new HandSequenceLog();
            var players = new List<PokerPlayer>
            {
                new HumanPlayer("human", "You", 0, 1000),
                new TestPlayer(1, 1000)
            };
            log.BeginSession(0, TestHelpers.Rules, players);
            log.OnEvent(new HandStarted
            {
                DealerSeat = 0,
                SmallBlindSeat = 0,
                BigBlindSeat = 1,
                SeatsInHand = new List<int> { 0, 1 }
            });
            log.OnEvent(new CardDealt { Seat = 0, Card = Card.Parse("Ah") });
            log.OnEvent(new CardDealt { Seat = 0, Card = Card.Parse("Kd") });
            log.OnEvent(new BetPlaced { Seat = 0, ToAmount = 50, AddedAmount = 40 });
            log.OnEvent(new PlayerFolded { Seat = 1 });
            log.OnEvent(new PotAwarded
            {
                PotIndex = 0,
                Amount = 60,
                WinnerSeats = new List<int> { 0 },
                Payouts = new Dictionary<int, long> { [0] = 60 }
            });
            log.OnEvent(new HandCompleted
            {
                WonByFolds = true,
                StackChanges = new Dictionary<int, long> { [0] = 10, [1] = -10 }
            });
            return log;
        }

        private static string StripTimestamp(string csv) =>
            string.Join("\n", csv.Split('\n').Where(l => !l.Contains("Exported At")));

        [Test]
        public void BuildPlayerCsv_SkipsIncompleteInProgressHand()
        {
            var log = new HandSequenceLog();
            var players = new List<PokerPlayer>
            {
                new HumanPlayer("human", "You", 0, 1000),
                new TestPlayer(1, 1000)
            };
            log.BeginSession(0, TestHelpers.Rules, players);
            log.OnEvent(new HandStarted
            {
                DealerSeat = 0,
                SmallBlindSeat = 0,
                BigBlindSeat = 1,
                SeatsInHand = new List<int> { 0, 1 }
            });
            log.OnEvent(new CardDealt { Seat = 0, Card = Card.Parse("Ah") });
            log.OnEvent(new CardDealt { Seat = 0, Card = Card.Parse("Kd") });
            // No HandCompleted yet — still in progress.

            Assert.AreEqual(0, log.CompletedHandCount);
            string csv = log.BuildPlayerCsv(0);
            Assert.That(csv, Does.Contain("Hands Played,0"));
            Assert.That(csv, Does.Not.Contain("\n1,"));
        }
    }
}
