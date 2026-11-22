using System.Collections.Generic;
using CinematicPoker.Engine.Poker;

namespace CinematicPoker.Engine.Coaching
{
    /// <summary>
    /// Fills community cards that were never dealt because the hand ended by
    /// folds, so the coach table can still show the full board. Uses the next
    /// cards in the shuffled deck — the runout that would have happened — and
    /// does not advance or mutate the live deck.
    /// </summary>
    public static class CoachBoardRunout
    {
        public static void CompleteForCoach(CompletedHandRecord hand, PokerRound round)
        {
            if (hand == null || round == null) return;
            if (!hand.WonByFolds) return;
            if (hand.FinalBoard == null)
                hand.FinalBoard = new List<Card>();
            if (hand.FinalBoard.Count >= 5) return;
            if (!(round.Deck is Deck deck)) return;

            int pos = deck.Position;
            IReadOnlyList<Card> cards = deck.Cards;
            while (hand.FinalBoard.Count < 5 && pos < cards.Count)
                hand.FinalBoard.Add(cards[pos++]);
        }
    }
}