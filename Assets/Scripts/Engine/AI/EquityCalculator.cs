using System;
using System.Collections.Generic;
using CinematicPoker.Engine.Poker;

namespace CinematicPoker.Engine.AI
{
    /// <summary>
    /// Monte Carlo equity estimation: probability that a hand wins (ties counted
    /// as fractional wins) against N random opponent hands and a random runout.
    /// Pure C# and thread-safe per call — in Unity this runs on a worker thread
    /// (Task.Run) with adaptive iteration counts per device tier
    /// (~200 low / ~500 mid / ~1000-2000 high).
    /// </summary>
    public static class EquityCalculator
    {
        public static double Estimate(
            IReadOnlyList<Card> holeCards,
            IReadOnlyList<Card> board,
            int opponentCount,
            int iterations,
            Random rng)
        {
            if (holeCards == null || holeCards.Count != 2)
                throw new ArgumentException("Exactly two hole cards required.");
            if (opponentCount < 1 || opponentCount > 5)
                throw new ArgumentException("Opponent count must be 1-5.");
            if (iterations < 1)
                throw new ArgumentException("At least one iteration required.");

            // Remaining unseen cards.
            Span<bool> used = stackalloc bool[52];
            foreach (Card c in holeCards) used[c.Index] = true;
            foreach (Card c in board) used[c.Index] = true;

            var remaining = new int[52 - holeCards.Count - board.Count];
            int w = 0;
            for (int i = 0; i < 52; i++)
                if (!used[i])
                    remaining[w++] = i;

            int boardNeeded = 5 - board.Count;
            int drawCount = opponentCount * 2 + boardNeeded;

            var myCards = new List<Card>(7);
            var oppCards = new List<Card>(7);
            double winShare = 0;

            for (int iter = 0; iter < iterations; iter++)
            {
                // Partial Fisher-Yates: shuffle only the cards we need.
                for (int i = 0; i < drawCount; i++)
                {
                    int j = i + rng.Next(remaining.Length - i);
                    (remaining[i], remaining[j]) = (remaining[j], remaining[i]);
                }

                // Complete the board.
                myCards.Clear();
                myCards.AddRange(holeCards);
                myCards.AddRange(board);
                for (int i = 0; i < boardNeeded; i++)
                    myCards.Add(Card.FromIndex(remaining[opponentCount * 2 + i]));

                HandValue mine = HandEvaluator.Evaluate(myCards);

                int betterCount = 0;
                int tieCount = 0;
                for (int opp = 0; opp < opponentCount; opp++)
                {
                    oppCards.Clear();
                    oppCards.Add(Card.FromIndex(remaining[opp * 2]));
                    oppCards.Add(Card.FromIndex(remaining[opp * 2 + 1]));
                    foreach (Card c in board) oppCards.Add(c);
                    for (int i = 0; i < boardNeeded; i++)
                        oppCards.Add(Card.FromIndex(remaining[opponentCount * 2 + i]));

                    HandValue theirs = HandEvaluator.Evaluate(oppCards);
                    if (theirs > mine) { betterCount++; break; }
                    if (theirs == mine) tieCount++;
                }

                if (betterCount == 0)
                    winShare += tieCount == 0 ? 1.0 : 1.0 / (tieCount + 1);
            }

            return winShare / iterations;
        }
    }
}
