using System;
using CinematicPoker.Engine.AI;
using CinematicPoker.Engine.Poker;
using NUnit.Framework;
using static CinematicPoker.Engine.Tests.TestHelpers;

namespace CinematicPoker.Engine.Tests
{
    [TestFixture]
    public class AITests
    {
        [Test]
        public void EquityFavoursAcesOverSevenTwo()
        {
            var rng = new Random(1);
            double aces = EquityCalculator.Estimate(Cards("As", "Ad"), Cards(), 1, 2000, rng);
            double sevenTwo = EquityCalculator.Estimate(Cards("7s", "2d"), Cards(), 1, 2000, rng);

            Assert.Greater(aces, 0.75, "AA heads-up preflop is ~85% equity.");
            Assert.Less(sevenTwo, 0.45, "72o heads-up preflop is ~35% equity.");
            Assert.Greater(aces, sevenTwo + 0.3);
        }

        [Test]
        public void EquityOfMadeNutsIsNearCertain()
        {
            // Royal flush on the river.
            double equity = EquityCalculator.Estimate(
                Cards("As", "Ks"), Cards("Qs", "Js", "Ts", "2h", "7d"), 3, 500, new Random(2));
            Assert.Greater(equity, 0.99);
        }

        [Test]
        public void DecisionEngineOnlyReturnsLegalActions()
        {
            // Fuzz the decision engine across many seeded spots: every action it
            // returns must be accepted by the engine without an exception.
            for (int seed = 0; seed < 30; seed++)
            {
                var game = CreateGame(new long[] { 300, 300, 300, 300 }, seed: seed);
                var brain = new PokerDecisionEngine(seed);
                var profile = (seed % 3) switch
                {
                    0 => AIProfile.Maniac(),
                    1 => AIProfile.Rock(),
                    _ => AIProfile.Professional()
                };
                var tilt = new TiltState { Tilt = seed % 5 * 0.25 };

                game.StartHand();
                int guard = 0;
                while (game.Phase == GamePhase.HandInProgress)
                {
                    if (++guard > 300) Assert.Fail("Deadlock in AI fuzz hand.");
                    DecisionContext context = DecisionContext.ForCurrentActor(game, equityIterations: 30);
                    PlayerAction action = brain.Decide(context, profile, tilt);
                    Assert.DoesNotThrow(() => game.SubmitAction(action),
                        $"AI produced illegal action {action} (seed {seed}).");
                }
            }
        }

        [Test]
        public void TiltRisesAfterBigLossesAndCoolsAfterWins()
        {
            var profile = AIProfile.Maniac();
            var tilt = new TiltState();

            tilt.OnHandResult(stackChange: -400, bigBlind: 10, profile: profile, biggestWinnerSeat: 3);
            double afterLoss = tilt.Tilt;
            Assert.Greater(afterLoss, 0, "A big loss must raise tilt.");
            Assert.AreEqual(3, tilt.RevengeSeat, "Losing a big pot creates a revenge target.");

            tilt.OnHandResult(stackChange: 400, bigBlind: 10, profile: profile, biggestWinnerSeat: -1);
            Assert.Less(tilt.Tilt, afterLoss, "A big win must cool tilt down.");
        }

        [Test]
        public void PlayerModelTracksTendencies()
        {
            var model = new PlayerModel();
            for (int hand = 0; hand < 40; hand++)
            {
                model.RecordHandStart();
                if (hand % 2 == 0)
                    model.RecordVoluntaryPreflopPlay(raised: hand % 4 == 0);
            }

            Assert.Greater(model.Vpip, 0.35, "Player who plays half their hands has high VPIP.");
            Assert.Less(model.Vpip, 0.65);
            Assert.Greater(model.PreflopRaiseFrequency, 0.1);
            Assert.AreEqual(1, model.HandsSinceVoluntaryPlay, "Tracks hands since last voluntary play.");
        }
    }
}
