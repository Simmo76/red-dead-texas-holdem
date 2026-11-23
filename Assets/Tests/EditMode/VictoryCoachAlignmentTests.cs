using System.Collections.Generic;
using CinematicPoker.Engine.Coaching;
using NUnit.Framework;

namespace CinematicPoker.Engine.Tests
{
    [TestFixture]
    public class VictoryCoachAlignmentTests
    {
        [Test]
        public void FoldWinRejectsAShowdownLossWrittenOntoIt()
        {
            Assert.IsFalse(VictoryCoachAlignment.CopyAgrees(
                "YOU WON — everyone folded",
                "WHY THEY WON",
                "Their pair beat your pair."));
        }

        [Test]
        public void TwoPairWinRejectsAnOpponentPairExplanation()
        {
            Assert.IsFalse(VictoryCoachAlignment.CopyAgrees(
                "YOU WON with two pair",
                "WHY THEY WON",
                "Their pair beat your pair."));
        }

        [Test]
        public void TripsWinRejectsAStraightExplanation()
        {
            Assert.IsFalse(VictoryCoachAlignment.CopyAgrees(
                "YOU WON with three of a kind",
                "WHY YOU WON",
                "Your straight beat their high card."));
        }

        [Test]
        public void FoldWinKeepsTheFoldExplanation()
        {
            Assert.IsTrue(VictoryCoachAlignment.CopyAgrees(
                "YOU WON — everyone folded",
                "WHY YOU WON",
                "You won because everyone else folded."));
        }

        [Test]
        public void PairWinKeepsThePairExplanation()
        {
            Assert.IsTrue(VictoryCoachAlignment.CopyAgrees(
                "YOU WON with a pair",
                "WHY YOU WON",
                "Your pair beat their high card."));
        }

        [Test]
        public void FullHouseWinIsNotReadAsTwoPair()
        {
            Assert.IsTrue(VictoryCoachAlignment.CopyAgrees(
                "YOU WON with a full house",
                "WHY YOU WON",
                "Your full house beat their two pair."));
            Assert.IsFalse(VictoryCoachAlignment.CopyAgrees(
                "YOU WON with two pair",
                "WHY YOU WON",
                "Your pair beat their high card."));
        }

        [Test]
        public void StraightFlushIsNotReadAsAStraight()
        {
            Assert.IsFalse(VictoryCoachAlignment.CopyAgrees(
                "YOU WON with a straight flush",
                "WHY YOU WON",
                "Your straight beat their pair."));
            Assert.IsTrue(VictoryCoachAlignment.CopyAgrees(
                "YOU WON with a straight flush",
                "WHY YOU WON",
                "Your straight flush beat their flush."));
        }

        [Test]
        public void EmptyCopyAgrees()
        {
            Assert.IsTrue(VictoryCoachAlignment.CopyAgrees("YOU WON with a pair", "", ""));
        }

        [Test]
        public void OpponentPotWinRejectsNoWinnerCoachLine()
        {
            Assert.IsFalse(VictoryCoachAlignment.CopyAgrees(
                "Reese Blood-Odds takes the pot",
                "WHAT HAPPENED",
                "The result was saved without a pot award, so no winner is named."));
        }

        [Test]
        public void SameHandRequiresBothIds()
        {
            Assert.IsFalse(VictoryCoachAlignment.SameHand(null, "s:1"));
            Assert.IsFalse(VictoryCoachAlignment.SameHand("s:1", "s:2"));
            Assert.IsTrue(VictoryCoachAlignment.SameHand("s:4", "s:4"));
        }

        [Test]
        public void FindMatchingHandIgnoresAnotherSittingWithTheSameNumber()
        {
            CompletedHandRecord flush = CoachFixtures.FlushOverTwoPair();
            flush.HandNumber = 4;
            flush.HandId = "earlier:4";

            CompletedHandRecord pair = CoachFixtures.PairBeatsHighCard();
            pair.HandNumber = 4;
            pair.HandId = "this-sitting:4";

            CompletedHandRecord found = VictoryCoachAlignment.FindMatchingHand(
                handNumber: 4,
                coachHandId: "earlier:4",
                holeCards: "2s 8h",
                boardCards: "Ad Kh 8s 4d 6h",
                headline: "YOU WON with a pair",
                hands: new List<CompletedHandRecord> { flush, pair });

            Assert.AreSame(pair, found);
        }

        [Test]
        public void FindMatchingHandWillNotExplainAFoldAsAShowdown()
        {
            CompletedHandRecord showdown = CoachFixtures.PairBeatsHighCard();
            showdown.HandNumber = 7;
            showdown.HandId = "s:7";

            CompletedHandRecord fold = CoachFixtures.FoldedBlind();
            fold.HeroFolded = false;
            fold.Awards[0].WinnerSeats = new List<int> { 0 };
            fold.HandNumber = 7;
            fold.HandId = "s:7b";
            fold.HeroHoleCards = new List<CinematicPoker.Engine.Poker.Card>
            {
                CinematicPoker.Engine.Poker.Card.Parse("6h"),
                CinematicPoker.Engine.Poker.Card.Parse("6c")
            };

            CompletedHandRecord found = VictoryCoachAlignment.FindMatchingHand(
                handNumber: 7,
                coachHandId: "s:7",
                holeCards: "6h 6c",
                boardCards: "",
                headline: "YOU WON — everyone folded",
                hands: new List<CompletedHandRecord> { showdown, fold });

            Assert.AreSame(fold, found);
        }
    }
}
