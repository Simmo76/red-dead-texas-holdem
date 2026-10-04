using CinematicPoker.Engine.AI;
using CinematicPoker.Engine.Players;
using CinematicPoker.Engine.Poker;
using NUnit.Framework;
using static CinematicPoker.Engine.Tests.TestHelpers;

namespace CinematicPoker.Engine.Tests
{
    /// <summary>
    /// End-to-end session flow exactly as the Unity prototype drives it:
    /// one human (scripted check/call here) + five NPCs with real decision
    /// engines, playing from buy-in until the human or all NPC opponents
    /// are eliminated.
    /// </summary>
    [TestFixture]
    public class GameFlowTests
    {
        [Test]
        public void CompleteSessionFromBuyInToElimination()
        {
            var rules = new TableRules(smallBlind: 10, bigBlind: 20, startingStack: 400);
            var human = new HumanPlayer("human", "You", seat: 0, stack: rules.StartingStack);

            var profiles = new[]
            {
                AIProfile.Maniac(), AIProfile.Regular(), AIProfile.Rock(),
                AIProfile.CallingStation(), AIProfile.Professional()
            };

            var players = new System.Collections.Generic.List<PokerPlayer> { human };
            for (int i = 0; i < 5; i++)
                players.Add(new AIPlayer($"npc{i}", $"NPC {i}", seat: i + 1, stack: rules.StartingStack,
                    profile: profiles[i], seed: 100 + i));

            var game = new PokerGame(rules, players, seed: 2026);
            long totalChips = game.TotalChipsInPlay;

            int hands = 0;
            while (!game.IsSessionOver && hands < 2000)
            {
                game.StartHand();
                hands++;

                int guard = 0;
                while (game.Phase == GamePhase.HandInProgress)
                {
                    Assert.Less(++guard, 400, "Deadlock inside a hand.");

                    PokerPlayer actor = game.CurrentPlayer;
                    if (actor.IsHuman)
                    {
                        // The scripted human: check when free, call when facing a bet.
                        LegalActions legal = game.GetLegalActions();
                        game.SubmitAction(legal.CanCheck ? PlayerAction.Check() : PlayerAction.Call());
                    }
                    else
                    {
                        var npc = (AIPlayer)actor;
                        DecisionContext context = DecisionContext.ForCurrentActor(game, equityIterations: 40);
                        game.SubmitAction(npc.DecideAction(context));
                    }
                }

                Assert.AreEqual(totalChips, game.TotalChipsInPlay, $"Money not conserved after hand {hands}.");
                foreach (PokerPlayer p in game.Players)
                    Assert.GreaterOrEqual(p.Stack, 0, $"Negative stack after hand {hands}.");
            }

            Assert.IsTrue(game.IsSessionOver, $"Session should finish within 2000 hands (played {hands}).");
            Assert.AreEqual(1, game.AlivePlayers.Count, "Exactly one player holds all the chips at the end.");
            Assert.AreEqual(totalChips, game.AlivePlayers[0].Stack, "Winner holds every chip in play.");
        }

        [Test]
        public void HumanBustMarksHasHumanLostWhileOthersRemain()
        {
            var rules = new TableRules(smallBlind: 5, bigBlind: 10, startingStack: 1000);
            // Three-handed: SB=1, BB=2, dealer/UTG=0. Cards deal SB, BB, dealer × 2.
            var deck = StackedDeck.Parse(
                "As", "4c", "2c",
                "Ah", "5d", "7h",
                "Kd", "Qs", "Jd", "9c", "8d");
            var human = new HumanPlayer("human", "James", seat: 0, stack: 50);
            var players = new System.Collections.Generic.List<PokerPlayer>
            {
                human,
                new TestPlayer(1, 1000),
                new TestPlayer(2, 1000)
            };
            var game = new PokerGame(rules, players, seed: 1, deckFactory: () => deck);
            game.StartHand();

            Assert.AreEqual(0, game.CurrentSeat);
            game.SubmitAction(PlayerAction.AllIn());
            game.SubmitAction(PlayerAction.Call());
            game.SubmitAction(PlayerAction.Call());

            while (game.Phase == GamePhase.HandInProgress)
            {
                LegalActions legal = game.GetLegalActions();
                game.SubmitAction(legal.CanCheck ? PlayerAction.Check() : PlayerAction.Call());
            }

            Assert.AreEqual(PlayerStatus.Eliminated, human.Status);
            Assert.AreEqual(0, human.Stack);
            Assert.IsTrue(game.HasHumanLost);
            Assert.IsFalse(game.IsSessionOver, "NPCs still have chips; the table is not empty.");
            Assert.Greater(game.AlivePlayers.Count, 1);
        }
    }
}
