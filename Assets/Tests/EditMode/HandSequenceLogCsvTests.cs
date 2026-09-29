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
                Backdrop = "ARCADE"
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
    }
}
