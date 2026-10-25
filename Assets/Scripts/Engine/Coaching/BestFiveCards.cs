using System.Collections.Generic;
using CinematicPoker.Engine.Poker;

namespace CinematicPoker.Engine.Coaching
{
    /// <summary>
    /// Picks one best five-card subset by asking the real evaluator.
    /// Ties between equal subsets break toward the lowest card indexes.
    /// </summary>
    public static class BestFiveCards
    {
        public static List<Card> Select(IReadOnlyList<Card> cards)
        {
            var picked = new List<Card>();
            if (cards == null || cards.Count < 5) return picked;
            if (cards.Count == 5)
            {
                picked.AddRange(cards);
                return picked;
            }

            HandValue best = HandEvaluator.Evaluate(cards);
            List<Card> winner = null;
            int n = cards.Count;
            for (int a = 0; a < n; a++)
            for (int b = a + 1; b < n; b++)
            for (int c = b + 1; c < n; c++)
            for (int d = c + 1; d < n; d++)
            for (int e = d + 1; e < n; e++)
            {
                var five = new List<Card>(5)
                {
                    cards[a], cards[b], cards[c], cards[d], cards[e]
                };
                HandValue value = HandEvaluator.Evaluate(five);
                if (value.Score != best.Score) continue;
                if (winner == null || ComesFirst(five, winner))
                    winner = five;
            }

            if (winner != null) picked.AddRange(winner);
            return picked;
        }

        public static string Code(Card card)
        {
            char suit = card.Suit switch
            {
                Suit.Clubs => 'c',
                Suit.Diamonds => 'd',
                Suit.Hearts => 'h',
                Suit.Spades => 's',
                _ => '?'
            };
            return Card.RankSymbol(card.Rank) + suit;
        }

        public static Rank[] TieBreaks(HandValue value)
        {
            long score = value.Score;
            var ranks = new Rank[5];
            for (int i = 4; i >= 0; i--)
            {
                ranks[i] = (Rank)(score & 0xF);
                score >>= 4;
            }
            return ranks;
        }

        private static bool ComesFirst(List<Card> candidate, List<Card> current)
        {
            for (int i = 0; i < candidate.Count; i++)
            {
                int cmp = candidate[i].Index.CompareTo(current[i].Index);
                if (cmp != 0) return cmp < 0;
            }
            return false;
        }
    }
}
