using System.Linq;
using CinematicPoker.Engine.Players;
using CinematicPoker.Engine.Poker;
using NUnit.Framework;
using static CinematicPoker.Engine.Tests.TestHelpers;

namespace CinematicPoker.Engine.Tests
{
    [TestFixture]
    public class BettingRuleTests
    {
        [Test]
        public void HeadsUpBlindOrderWorks()
        {
            var game = CreateGame(new long[] { 1000, 1000 });
            game.StartHand();

            // Heads-up: the dealer posts the small blind and acts FIRST preflop.
            Assert.AreEqual(0, game.DealerSeat);
            Assert.AreEqual(0, game.CurrentRound.SmallBlindSeat, "Heads-up dealer must be the small blind.");
            Assert.AreEqual(1, game.CurrentRound.BigBlindSeat);
            Assert.AreEqual(0, game.CurrentSeat, "Heads-up dealer/small blind acts first preflop.");

            game.SubmitAction(PlayerAction.Call());
            Assert.AreEqual(1, game.CurrentSeat, "Big blind has the option preflop.");
            game.SubmitAction(PlayerAction.Check());

            // Postflop the non-dealer (big blind) acts first.
            Assert.AreEqual(HandPhase.Flop, game.CurrentRound.Phase);
            Assert.AreEqual(1, game.CurrentSeat, "Heads-up big blind acts first postflop.");
        }

        [Test]
        public void SixPlayerBlindOrderWorks()
        {
            var game = CreateGame(new long[] { 1000, 1000, 1000, 1000, 1000, 1000 });
            var events = Record(game);
            game.StartHand();

            Assert.AreEqual(0, game.DealerSeat);
            var blinds = EventsOf<BlindPosted>(events);
            Assert.AreEqual(2, blinds.Count);
            Assert.AreEqual(1, blinds[0].Seat, "Small blind is left of the dealer.");
            Assert.AreEqual(5, blinds[0].Amount);
            Assert.IsFalse(blinds[0].IsBigBlind);
            Assert.AreEqual(2, blinds[1].Seat, "Big blind is left of the small blind.");
            Assert.AreEqual(10, blinds[1].Amount);
            Assert.IsTrue(blinds[1].IsBigBlind);

            // Under the gun (left of the big blind) opens preflop.
            Assert.AreEqual(3, game.CurrentSeat);

            // Everyone calls; big blind checks the option.
            foreach (int expectedSeat in new[] { 3, 4, 5, 0, 1 })
            {
                Assert.AreEqual(expectedSeat, game.CurrentSeat, "Preflop action must proceed clockwise.");
                game.SubmitAction(PlayerAction.Call());
            }
            Assert.AreEqual(2, game.CurrentSeat, "Big blind gets the option.");
            game.SubmitAction(PlayerAction.Check());

            // Postflop the small blind acts first.
            Assert.AreEqual(HandPhase.Flop, game.CurrentRound.Phase);
            Assert.AreEqual(1, game.CurrentSeat, "Small blind acts first postflop.");
        }

        [Test]
        public void DealerRotatesCorrectly()
        {
            var game = CreateGame(new long[] { 1000, 1000, 1000 });

            game.StartHand();
            Assert.AreEqual(0, game.DealerSeat);
            FoldAround(game);

            game.StartHand();
            Assert.AreEqual(1, game.DealerSeat, "Button moves clockwise each hand.");
            FoldAround(game);

            game.StartHand();
            Assert.AreEqual(2, game.DealerSeat);
            FoldAround(game);

            game.StartHand();
            Assert.AreEqual(0, game.DealerSeat, "Button wraps around the table.");
            FoldAround(game);

            // Eliminated players are skipped by the button.
            // Previous dealer was seat 0, so with seat 2 gone (and seat 1 next
            // alive) the button lands on seat 1, then wraps to 0 again.
            PokerPlayer seat2 = game.GetPlayer(2);
            seat2.Stack = 0;
            seat2.Status = PlayerStatus.Eliminated;
            game.StartHand();
            Assert.AreEqual(1, game.DealerSeat);
            FoldAround(game);
            game.StartHand();
            Assert.AreEqual(0, game.DealerSeat, "Button skips eliminated seats.");
        }

        [Test]
        public void FoldEndsHandCorrectly()
        {
            var game = CreateGame(new long[] { 1000, 1000, 1000 });
            var events = Record(game);
            game.StartHand();

            // Seat 3-handed: dealer 0, SB 1, BB 2, UTG = dealer.
            Assert.AreEqual(0, game.CurrentSeat);
            game.SubmitAction(PlayerAction.Fold());
            Assert.AreEqual(1, game.CurrentSeat);
            game.SubmitAction(PlayerAction.Fold());

            // Hand must end instantly: no flop, no showdown.
            Assert.AreEqual(GamePhase.WaitingForHand, game.Phase);
            Assert.IsEmpty(EventsOf<ShowdownStarted>(events), "No showdown when everyone folds.");
            Assert.IsEmpty(EventsOf<FlopDealt>(events), "No board when the hand ends preflop.");

            var completed = EventsOf<HandCompleted>(events).Single();
            Assert.IsTrue(completed.WonByFolds);

            // Big blind wins the small blind; its own uncalled excess is returned.
            Assert.AreEqual(1005, game.GetPlayer(2).Stack);
            Assert.AreEqual(995, game.GetPlayer(1).Stack);
            Assert.AreEqual(1000, game.GetPlayer(0).Stack);

            var award = EventsOf<PotAwarded>(events).Single();
            Assert.AreEqual(new[] { 2 }, award.WinnerSeats.ToArray());
            Assert.AreEqual(10, award.Amount, "Pot is SB 5 + BB 5 after the uncalled 5 returns to the big blind.");
        }

        [Test]
        public void AllInWorks()
        {
            // Heads-up: dealer shoves AA, big blind calls with 22, board pairs nobody.
            var deck = StackedDeck.Parse(
                "As", "2c", "Ad", "2d",            // hole cards: dealer AA, BB 22
                "Kh", "7s", "8c", "9h", "Jd");     // board
            var game = CreateGame(new long[] { 100, 100 }, deck);
            var events = Record(game);
            game.StartHand();

            Assert.AreEqual(0, game.CurrentSeat);
            game.SubmitAction(PlayerAction.AllIn());
            Assert.AreEqual(1, game.CurrentSeat);
            game.SubmitAction(PlayerAction.Call());

            // Both all-in: board runs out automatically and the hand completes.
            Assert.AreEqual(GamePhase.SessionOver, game.Phase);
            Assert.AreEqual(200, game.GetPlayer(0).Stack, "Winner takes both stacks.");
            Assert.AreEqual(0, game.GetPlayer(1).Stack);

            var award = EventsOf<PotAwarded>(events).Single();
            Assert.AreEqual(200, award.Amount);
            Assert.AreEqual(new[] { 0 }, award.WinnerSeats.ToArray());

            var eliminated = EventsOf<PlayerEliminated>(events).Single();
            Assert.AreEqual(1, eliminated.Seat);
            Assert.IsTrue(game.IsSessionOver);
        }

        [Test]
        public void PlayerCannotBetNegative()
        {
            var game = CreateGame(new long[] { 1000, 1000, 1000 });
            game.StartHand();

            int actor = game.CurrentSeat;
            long stackBefore = game.GetPlayer(actor).Stack;

            Assert.Throws<IllegalActionException>(() => game.SubmitAction(PlayerAction.Raise(-50)));
            Assert.Throws<IllegalActionException>(() => game.SubmitAction(PlayerAction.Bet(-50)));
            Assert.Throws<IllegalActionException>(() => game.SubmitAction(PlayerAction.Raise(0)));

            Assert.AreEqual(actor, game.CurrentSeat, "Rejected actions must not consume the turn.");
            Assert.AreEqual(stackBefore, game.GetPlayer(actor).Stack, "Rejected actions must not move chips.");

            // Same postflop, where Bet is the legal opener.
            game.SubmitAction(PlayerAction.Call());
            game.SubmitAction(PlayerAction.Call());
            game.SubmitAction(PlayerAction.Check());
            Assert.AreEqual(HandPhase.Flop, game.CurrentRound.Phase);
            Assert.Throws<IllegalActionException>(() => game.SubmitAction(PlayerAction.Bet(-1)));
        }

        [Test]
        public void PlayerCannotBetAboveStack()
        {
            var game = CreateGame(new long[] { 1000, 1000, 1000 });
            game.StartHand();

            int actor = game.CurrentSeat;
            long stackBefore = game.GetPlayer(actor).Stack;

            Assert.Throws<IllegalActionException>(() => game.SubmitAction(PlayerAction.Raise(1001)));
            Assert.Throws<IllegalActionException>(() => game.SubmitAction(PlayerAction.Raise(999999)));

            Assert.AreEqual(actor, game.CurrentSeat);
            Assert.AreEqual(stackBefore, game.GetPlayer(actor).Stack);

            // Raising exactly all-in is legal. (Session stacks sync at hand end,
            // so check the live seat state.)
            game.SubmitAction(PlayerAction.Raise(1000));
            Assert.AreEqual(0, game.CurrentRound.GetSeat(actor).Stack);
            Assert.IsTrue(game.CurrentRound.GetSeat(actor).AllIn);
        }

        [Test]
        public void BigBlindOptionCanRaise()
        {
            var game = CreateGame(new long[] { 1000, 1000, 1000 });
            game.StartHand();

            game.SubmitAction(PlayerAction.Call()); // UTG (dealer)
            game.SubmitAction(PlayerAction.Call()); // SB completes
            Assert.AreEqual(2, game.CurrentSeat, "Big blind must get the option.");

            LegalActions legal = game.GetLegalActions();
            Assert.IsTrue(legal.CanCheck);
            Assert.IsTrue(legal.CanRaise);
            game.SubmitAction(PlayerAction.Raise(30));

            // Action reopens for the callers.
            Assert.AreEqual(0, game.CurrentSeat);
        }

        [Test]
        public void ShortStackBlindGoesAllIn()
        {
            // Big blind has fewer chips than the blind.
            var game = CreateGame(new long[] { 1000, 1000, 4 });
            var events = Record(game);
            game.StartHand();

            var bbPost = EventsOf<BlindPosted>(events).Single(b => b.IsBigBlind);
            Assert.AreEqual(2, bbPost.Seat);
            Assert.AreEqual(4, bbPost.Amount, "Short stack posts what it can.");
            Assert.IsTrue(bbPost.IsAllIn);

            // Others must still call the full big blind amount.
            Assert.AreEqual(10, game.GetLegalActions().CallAmount);
        }
    }
}
