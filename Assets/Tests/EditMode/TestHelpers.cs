using System.Collections.Generic;
using System.Linq;
using CinematicPoker.Engine.Players;
using CinematicPoker.Engine.Poker;

namespace CinematicPoker.Engine.Tests
{
    /// <summary>A scripted, brainless NPC used to drive engine tests.</summary>
    public sealed class TestPlayer : PokerPlayer
    {
        public override bool IsHuman => false;

        public TestPlayer(int seat, long stack)
            : base($"test{seat}", $"Test {seat}", seat, stack) { }
    }

    public static class TestHelpers
    {
        public static readonly TableRules Rules = new TableRules(smallBlind: 5, bigBlind: 10, startingStack: 1000);

        /// <summary>
        /// Create a game with the given stacks (seat i gets stacks[i]) and an
        /// optional rigged deck used for every hand.
        /// </summary>
        public static PokerGame CreateGame(long[] stacks, IDeck deck = null, TableRules rules = null, int seed = 7)
        {
            rules ??= Rules;
            var players = new List<PokerPlayer>();
            for (int i = 0; i < stacks.Length; i++)
                players.Add(new TestPlayer(i, stacks[i]));

            return deck == null
                ? new PokerGame(rules, players, seed)
                : new PokerGame(rules, players, seed, () => deck);
        }

        /// <summary>Collects every event the game emits, for assertions.</summary>
        public static List<PokerEvent> Record(PokerGame game)
        {
            var events = new List<PokerEvent>();
            game.EventEmitted += events.Add;
            return events;
        }

        /// <summary>Everyone calls when facing a bet, otherwise checks, until the hand completes.</summary>
        public static void CheckCallDown(PokerGame game)
        {
            int guard = 0;
            while (game.Phase == GamePhase.HandInProgress)
            {
                if (++guard > 200) throw new System.Exception("Hand did not complete (deadlock guard).");
                LegalActions legal = game.GetLegalActions();
                game.SubmitAction(legal.CanCheck ? PlayerAction.Check() : PlayerAction.Call());
            }
        }

        /// <summary>Everyone folds until the hand completes (the big blind wins).</summary>
        public static void FoldAround(PokerGame game)
        {
            int guard = 0;
            while (game.Phase == GamePhase.HandInProgress)
            {
                if (++guard > 200) throw new System.Exception("Hand did not complete (deadlock guard).");
                game.SubmitAction(PlayerAction.Fold());
            }
        }

        public static List<T> EventsOf<T>(List<PokerEvent> events) where T : PokerEvent =>
            events.OfType<T>().ToList();

        public static List<Card> Cards(params string[] texts) =>
            texts.Select(Card.Parse).ToList();
    }
}
