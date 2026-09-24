using System.Collections.Generic;
using System.Linq;
using CinematicPoker.Engine.Poker;
using NUnit.Framework;

namespace CinematicPoker.Engine.Tests
{
    [TestFixture]
    public class DeckTests
    {
        [Test]
        public void DeckContains52UniqueCards()
        {
            var deck = new Deck(seed: 1);
            var drawn = new List<Card>();
            while (deck.Remaining > 0)
                drawn.Add(deck.Draw());

            Assert.AreEqual(52, drawn.Count);
            Assert.AreEqual(52, drawn.Distinct().Count());
            // Every rank/suit combination exactly once.
            foreach (Suit suit in System.Enum.GetValues(typeof(Suit)))
                foreach (Rank rank in System.Enum.GetValues(typeof(Rank)))
                    Assert.AreEqual(1, drawn.Count(c => c.Rank == rank && c.Suit == suit),
                        $"Expected exactly one {rank} of {suit}.");
        }

        [Test]
        public void ShufflePreservesCards()
        {
            var deck = new Deck(seed: 99);
            var before = deck.Cards.ToList();
            deck.Shuffle();
            var after = deck.Cards.ToList();

            Assert.AreEqual(52, after.Count);
            CollectionAssert.AreEquivalent(before, after, "Shuffle must not add, remove or duplicate cards.");
        }

        [Test]
        public void ShuffleWithSameSeedIsDeterministic()
        {
            var a = new Deck(seed: 1234);
            var b = new Deck(seed: 1234);
            a.Shuffle();
            b.Shuffle();
            CollectionAssert.AreEqual(a.Cards.ToList(), b.Cards.ToList());
        }

        [Test]
        public void NoDuplicateCardsDealt()
        {
            // Play 100 seeded six-player hands to showdown and verify every
            // card seen in each hand is unique.
            for (int seed = 0; seed < 100; seed++)
            {
                var game = TestHelpers.CreateGame(new long[] { 500, 500, 500, 500, 500, 500 }, seed: seed);
                var seen = new HashSet<int>();
                game.EventEmitted += evt =>
                {
                    switch (evt)
                    {
                        case CardDealt cd:
                            Assert.IsTrue(seen.Add(cd.Card.Index), $"Duplicate card {cd.Card} (seed {seed}).");
                            break;
                        case FlopDealt f:
                            Assert.IsTrue(seen.Add(f.Card1.Index), $"Duplicate card {f.Card1} (seed {seed}).");
                            Assert.IsTrue(seen.Add(f.Card2.Index), $"Duplicate card {f.Card2} (seed {seed}).");
                            Assert.IsTrue(seen.Add(f.Card3.Index), $"Duplicate card {f.Card3} (seed {seed}).");
                            break;
                        case TurnDealt t:
                            Assert.IsTrue(seen.Add(t.Card.Index), $"Duplicate card {t.Card} (seed {seed}).");
                            break;
                        case RiverDealt r:
                            Assert.IsTrue(seen.Add(r.Card.Index), $"Duplicate card {r.Card} (seed {seed}).");
                            break;
                    }
                };

                game.StartHand();
                TestHelpers.CheckCallDown(game);
                Assert.AreEqual(6 * 2 + 5, seen.Count, "Six players checked down: 12 hole cards + 5 board cards.");
            }
        }
    }
}
