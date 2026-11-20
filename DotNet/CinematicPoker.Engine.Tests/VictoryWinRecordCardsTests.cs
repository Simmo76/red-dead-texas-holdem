using CinematicPoker.Engine.Poker;
using CinematicPoker.Engine.Simulation;
using NUnit.Framework;

namespace CinematicPoker.Engine.Tests
{
    [TestFixture]
    public class VictoryWinRecordCardsTests
    {
        [Test]
        public void FormatCards_round_trips_through_parse_tokens()
        {
            var cards = new[]
            {
                new Card(Rank.Ace, Suit.Spades),
                new Card(Rank.King, Suit.Diamonds),
                new Card(Rank.Ten, Suit.Clubs)
            };
            string text = HandSequenceLog.FormatCards(cards);
            Assert.AreEqual("As Kd Tc", text);
            foreach (string token in text.Split(' '))
                Assert.DoesNotThrow(() => Card.Parse(token));
        }
    }
}