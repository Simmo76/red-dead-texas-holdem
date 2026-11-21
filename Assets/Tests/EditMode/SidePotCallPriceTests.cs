using System.Collections.Generic;
using CinematicPoker.Engine.Coaching;
using NUnit.Framework;

namespace CinematicPoker.Engine.Tests
{
    [TestFixture]
    public class SidePotCallPriceTests
    {
        [Test]
        public void CallIntoExistingSidePotGoesToSideLayer()
        {
            var spot = new HeroDecisionSnapshot
            {
                HeroSeat = 2,
                HeroCommittedTotal = 100,
                IncrementalCall = 100,
                PotBefore = 400,
                SidePotSpot = true,
                ActiveOpponents = 1,
                Seats = new List<SeatPublicState>
                {
                    new SeatPublicState { Seat = 0, TotalCommitted = 100, Folded = false, AllIn = true },
                    new SeatPublicState { Seat = 1, TotalCommitted = 200, Folded = false },
                    new SeatPublicState { Seat = 2, TotalCommitted = 100, Folded = false }
                }
            };

            SidePotCallPrice.Split split = SidePotCallPrice.Analyse(spot);
            Assert.NotNull(split);
            Assert.AreEqual(300, split.MainPotAmount);
            Assert.AreEqual(200, split.SidePotAmount);
            Assert.AreEqual(0, split.CallToMainPot);
            Assert.AreEqual(100, split.CallToSidePot);
            Assert.AreEqual(0.5, split.SideBreakEven.Value, 0.001);
        }

        [Test]
        public void CallThatOnlyMatchesShortStackStaysInMainPot()
        {
            var spot = new HeroDecisionSnapshot
            {
                HeroSeat = 1,
                HeroCommittedTotal = 50,
                IncrementalCall = 50,
                PotBefore = 150,
                SidePotSpot = true,
                ActiveOpponents = 1,
                Seats = new List<SeatPublicState>
                {
                    new SeatPublicState { Seat = 0, TotalCommitted = 100, Folded = false, AllIn = true },
                    new SeatPublicState { Seat = 1, TotalCommitted = 50, Folded = false }
                }
            };

            SidePotCallPrice.Split split = SidePotCallPrice.Analyse(spot);
            Assert.NotNull(split);
            Assert.AreEqual(200, split.MainPotAmount);
            Assert.AreEqual(0, split.SidePotAmount);
            Assert.AreEqual(50, split.CallToMainPot);
            Assert.AreEqual(0, split.CallToSidePot);
            Assert.AreEqual(50.0 / 200.0, split.MainBreakEven.Value, 0.001);
        }
    }
}