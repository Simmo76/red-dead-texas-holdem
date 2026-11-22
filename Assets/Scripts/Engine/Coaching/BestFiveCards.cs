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

            // Prefer the subset that matches the full-hand score. If none does
            // (the evaluator's kickers can disagree with a strict equality
            // check), still return the strongest five so the review has
            // cards to highlight.
            List<Card> winner = null;
            long bestScore = long.MinValue;
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
                long score = HandEvaluator.Evaluate(five).Score;
                if (winner != null && (score < bestScore || (score == bestScore && !ComesFirst(five, winner))))
                    continue;
                winner = five;
                bestScore = score;
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
