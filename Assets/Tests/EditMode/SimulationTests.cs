using CinematicPoker.Engine.Poker;
using CinematicPoker.Engine.Simulation;
using NUnit.Framework;

namespace CinematicPoker.Engine.Tests
{
    /// <summary>
    /// Large automated AI-vs-AI soak runs. SimulationRunner throws
    /// SimulationInvariantException on any violation of:
    /// no exceptions, no deadlocks, no duplicate cards, no negative stacks,
    /// correct turn order, and money conservation (before == after each hand).
    /// </summary>
    [TestFixture]
    public class SimulationTests
    {
        [Test]
        public void MoneyIsConservedAcrossFiveHundredHands()
        {
            SimulationResult result = SimulationRunner.Run(targetHands: 500, seed: 7, equityIterations: 30);
            Assert.GreaterOrEqual(result.HandsPlayed, 500);
        }

        [Test]
        [Category("Simulation")]
        public void TenThousandHandsSimulation()
        {
            SimulationResult result = SimulationRunner.Run(targetHands: 10000, seed: 42, equityIterations: 40);

            Assert.GreaterOrEqual(result.HandsPlayed, 10000);
            Assert.Greater(result.ShowdownCount, 0, "Some hands must reach showdown.");
            Assert.Greater(result.FoldWinCount, 0, "Some hands must end by folds.");
            Assert.Greater(result.SidePotHands, 0, "Side pots must occur across 10k hands.");
            Assert.Greater(result.EliminationCount, 0, "Players must bust across 10k hands.");

            // The winning-hand distribution should look like poker: pairs and
            // two pairs dominate; monsters are rare but present.
            result.WinningCategories.TryGetValue(HandCategory.FourOfAKind, out int quads);
            Assert.Greater(result.WinningCategories[HandCategory.Pair], quads,
                "Pairs must win far more often than quads.");
        }
    }
}
