using System.Collections.Generic;
using CinematicPoker.Engine.Players;
using CinematicPoker.Engine.Poker;
using CinematicPoker.Engine.Simulation;
using NUnit.Framework;

namespace CinematicPoker.Engine.Tests
{
    [TestFixture]
    public class PlayerCoachReportTests
    {
        private static HandSequenceLog NewLog()
        {
            var log = new HandSequenceLog();
            var players = new List<PokerPlayer>
            {
                new HumanPlayer("human", "You", 0, 1000),
                new TestPlayer(1, 1000),
                new TestPlayer(2, 1000)
            };
            log.BeginSession(0, TestHelpers.Rules, players);
            return log;
        }

        // One scripted hand: the human calls preflop, calls a flop bet, loses
        // the showdown. Loose-passive on every street.
        private static void PlayCallAndLoseHand(HandSequenceLog log)
        {
            log.OnEvent(new HandStarted
            {
                DealerSeat = 2,
                SmallBlindSeat = 0,
                BigBlindSeat = 1,
                SeatsInHand = new List<int> { 0, 1, 2 }
            });
            log.OnEvent(new BlindPosted { Seat = 0, Amount = 5 });
            log.OnEvent(new BlindPosted { Seat = 1, Amount = 10, IsBigBlind = true });
            log.OnEvent(new PlayerCalled { Seat = 2, AddedAmount = 10 });
            log.OnEvent(new PlayerCalled { Seat = 0, AddedAmount = 5 });   // human limps in
            log.OnEvent(new PlayerChecked { Seat = 1 });
            log.OnEvent(new FlopDealt { Card1 = Card.Parse("2h"), Card2 = Card.Parse("7d"), Card3 = Card.Parse("Jc") });
            log.OnEvent(new BetPlaced { Seat = 1, ToAmount = 20, AddedAmount = 20 });
            log.OnEvent(new PlayerCalled { Seat = 2, AddedAmount = 20 });
            log.OnEvent(new PlayerCalled { Seat = 0, AddedAmount = 20 }); // human calls again
            log.OnEvent(new PotAwarded
            {
                PotIndex = 0,
                Amount = 90,
                WinnerSeats = new List<int> { 1 },
                Payouts = new Dictionary<int, long> { [1] = 90 },
                WinningHand = HandEvaluator.Evaluate(new List<Card>
                {
                    Card.Parse("Jh"), Card.Parse("Js"), Card.Parse("2h"),
                    Card.Parse("7d"), Card.Parse("Jc")
                })
            });
            log.OnEvent(new HandCompleted
            {
                WonByFolds = false,
                StackChanges = new Dictionary<int, long> { [0] = -30, [1] = 60, [2] = -30 }
            });
        }

        [Test]
        public void Build_CountsVpipShowdownsAndLosses()
        {
            HandSequenceLog log = NewLog();
            for (int i = 0; i < 8; i++) PlayCallAndLoseHand(log);

            PlayerCoachReport report = log.BuildCoachReport(0);

            Assert.AreEqual(8, report.HandsAnalysed);
            Assert.AreEqual(8, report.VoluntaryPreflopHands); // limped every hand
            Assert.AreEqual(0, report.PreflopRaiseHands);     // never raised
            Assert.AreEqual(8, report.PostflopCalls);
            Assert.AreEqual(0, report.PostflopAggressiveActions);
            Assert.AreEqual(8, report.ShowdownsReached);
            Assert.AreEqual(0, report.ShowdownsWon);
            Assert.AreEqual(-240, report.NetProfit);
            Assert.AreEqual(-30, report.BiggestLoss);

            // Loose-passive with lost showdowns: the coach must have plenty to say.
            Assert.IsNotEmpty(report.Advice);
            Assert.LessOrEqual(report.Advice.Count, 4);
        }

        [Test]
        public void Build_SmallSampleGetsSingleCaveatNote()
        {
            HandSequenceLog log = NewLog();
            PlayCallAndLoseHand(log);

            PlayerCoachReport report = log.BuildCoachReport(0);

            Assert.AreEqual(1, report.HandsAnalysed);
            Assert.AreEqual(1, report.Advice.Count);
            Assert.That(report.Advice[0], Does.Contain("Too few hands"));
        }

        [Test]
        public void Build_BlindPostsDoNotCountAsVoluntaryPlay()
        {
            HandSequenceLog log = NewLog();
            // Human posts BB and checks it through; folds a flop bet.
            log.OnEvent(new HandStarted
            {
                DealerSeat = 1,
                SmallBlindSeat = 2,
                BigBlindSeat = 0,
                SeatsInHand = new List<int> { 0, 1, 2 }
            });
            log.OnEvent(new BlindPosted { Seat = 2, Amount = 5 });
            log.OnEvent(new BlindPosted { Seat = 0, Amount = 10, IsBigBlind = true });
            log.OnEvent(new PlayerCalled { Seat = 1, AddedAmount = 10 });
            log.OnEvent(new PlayerCalled { Seat = 2, AddedAmount = 5 });
            log.OnEvent(new PlayerChecked { Seat = 0 }); // free look, not VPIP
            log.OnEvent(new FlopDealt { Card1 = Card.Parse("2h"), Card2 = Card.Parse("7d"), Card3 = Card.Parse("Jc") });
            log.OnEvent(new BetPlaced { Seat = 1, ToAmount = 25, AddedAmount = 25 });
            log.OnEvent(new PlayerFolded { Seat = 0 });
            log.OnEvent(new PlayerFolded { Seat = 2 });
            log.OnEvent(new PotAwarded
            {
                PotIndex = 0,
                Amount = 55,
                WinnerSeats = new List<int> { 1 },
                Payouts = new Dictionary<int, long> { [1] = 55 }
            });
            log.OnEvent(new HandCompleted
            {
                WonByFolds = true,
                StackChanges = new Dictionary<int, long> { [0] = -10, [1] = 20, [2] = -10 }
            });

            PlayerCoachReport report = log.BuildCoachReport(0);

            Assert.AreEqual(1, report.HandsAnalysed);
            Assert.AreEqual(0, report.VoluntaryPreflopHands);
            Assert.AreEqual(0, report.ShowdownsReached); // folded before showdown
        }

        [Test]
        public void PlayerCsv_IncludesCoachingSectionAndAdvice()
        {
            HandSequenceLog log = NewLog();
            for (int i = 0; i < 8; i++) PlayCallAndLoseHand(log);

            string csv = log.BuildPlayerCsv(0);

            Assert.That(csv, Does.Contain("Coaching,Preflop Hands Played (VPIP)"));
            Assert.That(csv, Does.Contain("Coaching,Preflop Raise Rate (PFR)"));
            Assert.That(csv, Does.Contain("Coaching,Postflop Bets+Raises vs Calls"));
            Assert.That(csv, Does.Contain("Coaching,Showdowns Won,0 of 8"));
            Assert.That(csv, Does.Contain("Coach's Advice,1,"));
        }
    }
}
