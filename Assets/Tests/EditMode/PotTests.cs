using System.Collections.Generic;
using System.Linq;
using CinematicPoker.Engine.Poker;
using NUnit.Framework;
using static CinematicPoker.Engine.Tests.TestHelpers;

namespace CinematicPoker.Engine.Tests
{
    [TestFixture]
    public class PotTests
    {
        [Test]
        public void SplitPotWorks()
        {
            // Heads-up; both players play the board (broadway straight) → split.
            var deck = StackedDeck.Parse(
                "2c", "3c", "2d", "3d",            // hole cards (dealer 22-ish junk, BB 33-ish junk)
                "Ah", "Kd", "Qc", "Js", "Th");     // board: A-K-Q-J-T
            var game = CreateGame(new long[] { 1000, 1000 }, deck);
            var events = Record(game);
            game.StartHand();

            game.SubmitAction(PlayerAction.Call());   // dealer/SB completes
            game.SubmitAction(PlayerAction.Check());  // BB option
            for (int street = 0; street < 3; street++)
            {
                game.SubmitAction(PlayerAction.Check());
                game.SubmitAction(PlayerAction.Check());
            }

            Assert.AreEqual(GamePhase.WaitingForHand, game.Phase);

            var award = EventsOf<PotAwarded>(events).Single();
            Assert.AreEqual(20, award.Amount);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, award.WinnerSeats);
            Assert.AreEqual(10, award.Payouts[0]);
            Assert.AreEqual(10, award.Payouts[1]);
            Assert.AreEqual(HandCategory.Straight, award.WinningHand.Value.Category);

            Assert.AreEqual(1000, game.GetPlayer(0).Stack, "Split pot: nobody wins or loses.");
            Assert.AreEqual(1000, game.GetPlayer(1).Stack);
        }

        [Test]
        public void SidePotWorks()
        {
            // Three players: seat 0 is short (100) and shoves with AA.
            // Seats 1 (KK) and 2 (QQ) call, then bet another 100 between
            // themselves on the flop, creating a side pot only they can win.
            // Hand order is SB(1), BB(2), dealer/UTG(0); cards deal in two passes.
            var deck = StackedDeck.Parse(
                "Kh", "Qh", "Ac",                  // first pass: seats 1, 2, 0
                "Kd", "Qd", "Ad",                  // second pass
                "2h", "5d", "9c",                  // flop
                "Jh",                              // turn
                "3s");                             // river
            var game = CreateGame(new long[] { 100, 300, 300 }, deck);
            var events = Record(game);
            game.StartHand();

            Assert.AreEqual(0, game.CurrentSeat);
            game.SubmitAction(PlayerAction.AllIn());          // seat 0: all-in 100
            Assert.AreEqual(1, game.CurrentSeat);
            game.SubmitAction(PlayerAction.Call());           // seat 1 calls 100
            Assert.AreEqual(2, game.CurrentSeat);
            game.SubmitAction(PlayerAction.Call());           // seat 2 calls 100

            Assert.AreEqual(HandPhase.Flop, game.CurrentRound.Phase);
            Assert.AreEqual(1, game.CurrentSeat, "Small blind opens the flop.");
            game.SubmitAction(PlayerAction.Bet(100));         // seat 1 bets into the side pot
            game.SubmitAction(PlayerAction.Call());           // seat 2 calls

            foreach (var _ in new int[4])                     // turn + river: check-check
                game.SubmitAction(PlayerAction.Check());

            Assert.AreEqual(GamePhase.WaitingForHand, game.Phase);

            var awards = EventsOf<PotAwarded>(events);
            Assert.AreEqual(2, awards.Count, "Main pot + one side pot.");

            PotAwarded sidePot = awards.Single(a => a.PotIndex == 1);
            Assert.AreEqual(200, sidePot.Amount);
            CollectionAssert.AreEqual(new[] { 1 }, sidePot.WinnerSeats, "KK wins the side pot (AA not eligible).");

            PotAwarded mainPot = awards.Single(a => a.PotIndex == 0);
            Assert.AreEqual(300, mainPot.Amount);
            CollectionAssert.AreEqual(new[] { 0 }, mainPot.WinnerSeats, "AA wins the main pot.");

            Assert.AreEqual(300, game.GetPlayer(0).Stack, "Short stack triples up through the main pot.");
            Assert.AreEqual(300, game.GetPlayer(1).Stack, "Seat 1 loses the main pot but recovers the side pot.");
            Assert.AreEqual(100, game.GetPlayer(2).Stack);

            long total = game.Players.Sum(p => p.Stack);
            Assert.AreEqual(700, total, "Money is conserved.");
        }

        [Test]
        public void SidePotCalculatorLayersContributions()
        {
            var contributions = new Dictionary<int, long> { { 0, 100 }, { 1, 200 }, { 2, 200 } };
            var folded = new Dictionary<int, bool> { { 0, false }, { 1, false }, { 2, false } };

            var pots = SidePotCalculator.Build(contributions, folded);

            Assert.AreEqual(2, pots.Count);
            Assert.AreEqual(300, pots[0].Amount);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, pots[0].EligibleSeats);
            Assert.AreEqual(200, pots[1].Amount);
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, pots[1].EligibleSeats);
        }

        [Test]
        public void SidePotCalculatorExcludesFoldedButKeepsTheirChips()
        {
            var contributions = new Dictionary<int, long> { { 0, 50 }, { 1, 100 }, { 2, 100 } };
            var folded = new Dictionary<int, bool> { { 0, true }, { 1, false }, { 2, false } };

            var pots = SidePotCalculator.Build(contributions, folded);

            Assert.AreEqual(1, pots.Count);
            Assert.AreEqual(250, pots[0].Amount, "Folded chips stay in the pot.");
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, pots[0].EligibleSeats, "Folded seats can never win.");
        }

        [Test]
        public void UncalledBetIsReturned()
        {
            // Heads-up: dealer raises big, BB folds; the raise excess must come back.
            var game = CreateGame(new long[] { 1000, 1000 });
            var events = Record(game);
            game.StartHand();

            game.SubmitAction(PlayerAction.Raise(200)); // dealer raises to 200
            game.SubmitAction(PlayerAction.Fold());     // BB folds

            var returned = EventsOf<UncalledBetReturned>(events).Single();
            Assert.AreEqual(0, returned.Seat);
            Assert.AreEqual(190, returned.Amount, "Raise to 200 over the BB's 10 is uncalled.");

            Assert.AreEqual(1010, game.GetPlayer(0).Stack, "Dealer wins only the big blind.");
            Assert.AreEqual(990, game.GetPlayer(1).Stack);
        }
    }
}
