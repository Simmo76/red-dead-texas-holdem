using CinematicPoker.Engine.Poker;
using NUnit.Framework;
using static CinematicPoker.Engine.Tests.TestHelpers;

namespace CinematicPoker.Engine.Tests
{
    [TestFixture]
    public class HandEvaluatorTests
    {
        private static HandValue Eval(params string[] cards) =>
            HandEvaluator.Evaluate(Cards(cards));

        [Test]
        public void HighCardDetected()
        {
            var v = Eval("As", "Kd", "9h", "5c", "2s", "Jh", "7d");
            Assert.AreEqual(HandCategory.HighCard, v.Category);
        }

        [Test]
        public void PairDetected()
        {
            var v = Eval("As", "Ad", "9h", "5c", "2s", "Jh", "7d");
            Assert.AreEqual(HandCategory.Pair, v.Category);
        }

        [Test]
        public void TwoPairDetected()
        {
            var v = Eval("As", "Ad", "9h", "9c", "2s", "Jh", "7d");
            Assert.AreEqual(HandCategory.TwoPair, v.Category);
        }

        [Test]
        public void ThreeOfAKindDetected()
        {
            var v = Eval("As", "Ad", "Ah", "9c", "2s", "Jh", "7d");
            Assert.AreEqual(HandCategory.ThreeOfAKind, v.Category);
        }

        [Test]
        public void StraightDetected()
        {
            var v = Eval("9s", "8d", "7h", "6c", "5s", "Ah", "Kd");
            Assert.AreEqual(HandCategory.Straight, v.Category);
        }

        [Test]
        public void WheelStraightDetected()
        {
            // A-2-3-4-5: the ace plays low.
            var v = Eval("As", "2d", "3h", "4c", "5s", "Kh", "Qd");
            Assert.AreEqual(HandCategory.Straight, v.Category);

            // A wheel loses to a six-high straight.
            var sixHigh = Eval("6s", "2d", "3h", "4c", "5s", "Kh", "Qd");
            Assert.Less(v.Score, sixHigh.Score);
        }

        [Test]
        public void FlushDetected()
        {
            var v = Eval("As", "Js", "8s", "5s", "2s", "Kh", "Qd");
            Assert.AreEqual(HandCategory.Flush, v.Category);
        }

        [Test]
        public void FullHouseDetected()
        {
            var v = Eval("As", "Ad", "Ah", "9c", "9s", "Jh", "7d");
            Assert.AreEqual(HandCategory.FullHouse, v.Category);
        }

        [Test]
        public void FullHouseFromTwoTripsUsesBestPair()
        {
            // AAA + 999 in seven cards → aces full of nines.
            var v = Eval("As", "Ad", "Ah", "9c", "9s", "9h", "7d");
            Assert.AreEqual(HandCategory.FullHouse, v.Category);
            var acesFullOfNines = Eval("As", "Ad", "Ah", "9c", "9s", "2h", "7d");
            Assert.AreEqual(acesFullOfNines.Score, v.Score);
        }

        [Test]
        public void FourOfKindDetected()
        {
            var v = Eval("As", "Ad", "Ah", "Ac", "9s", "Jh", "7d");
            Assert.AreEqual(HandCategory.FourOfAKind, v.Category);
        }

        [Test]
        public void StraightFlushDetected()
        {
            var v = Eval("9s", "8s", "7s", "6s", "5s", "Ah", "Kd");
            Assert.AreEqual(HandCategory.StraightFlush, v.Category);
        }

        [Test]
        public void RoyalFlushDetected()
        {
            var v = Eval("As", "Ks", "Qs", "Js", "Ts", "2h", "7d");
            Assert.AreEqual(HandCategory.RoyalFlush, v.Category);
        }

        [Test]
        public void KickerDeterminesWinner()
        {
            // Same pair of aces on the same board; the king kicker must win.
            var board = new[] { "Ah", "7d", "5c", "9s", "2h" };
            var kingKicker = HandEvaluator.Evaluate(Cards("Ac", "Kd"), Cards(board));
            var queenKicker = HandEvaluator.Evaluate(Cards("As", "Qd"), Cards(board));

            Assert.AreEqual(HandCategory.Pair, kingKicker.Category);
            Assert.AreEqual(HandCategory.Pair, queenKicker.Category);
            Assert.Greater(kingKicker.Score, queenKicker.Score, "The king kicker must beat the queen kicker.");
        }

        [Test]
        public void CategoriesRankInCorrectOrder()
        {
            var ordered = new[]
            {
                Eval("As", "Kd", "9h", "5c", "2s"),                 // high card
                Eval("As", "Ad", "9h", "5c", "2s"),                 // pair
                Eval("As", "Ad", "9h", "9c", "2s"),                 // two pair
                Eval("As", "Ad", "Ah", "9c", "2s"),                 // trips
                Eval("9s", "8d", "7h", "6c", "5s"),                 // straight
                Eval("As", "Js", "8s", "5s", "2s"),                 // flush
                Eval("As", "Ad", "Ah", "9c", "9s"),                 // full house
                Eval("As", "Ad", "Ah", "Ac", "9s"),                 // quads
                Eval("9s", "8s", "7s", "6s", "5s"),                 // straight flush
                Eval("As", "Ks", "Qs", "Js", "Ts")                  // royal flush
            };

            for (int i = 1; i < ordered.Length; i++)
                Assert.Greater(ordered[i].Score, ordered[i - 1].Score,
                    $"{ordered[i].Category} must beat {ordered[i - 1].Category}.");
        }

        [Test]
        public void BoardPlaysWhenHoleCardsAddNothing()
        {
            // Both players play the board straight → identical values.
            var board = new[] { "Ah", "Kd", "Qc", "Js", "Th" };
            var a = HandEvaluator.Evaluate(Cards("2c", "3d"), Cards(board));
            var b = HandEvaluator.Evaluate(Cards("4h", "5s"), Cards(board));
            Assert.AreEqual(a.Score, b.Score);
        }
    }
}
