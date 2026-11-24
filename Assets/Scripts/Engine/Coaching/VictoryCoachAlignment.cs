using System;
using System.Collections.Generic;
using CinematicPoker.Engine.Poker;

namespace CinematicPoker.Engine.Coaching
{
    /// <summary>
    /// A victory certificate keeps its own headline and cards. Coach copy may
    /// be shown on that certificate only when it describes the same hand.
    /// Hand numbers restart every sitting, so they are not an identity.
    /// </summary>
    public static class VictoryCoachAlignment
    {
        private static readonly string[] CategoriesLongestFirst =
        {
            "royal flush",
            "straight flush",
            "four of a kind",
            "full house",
            "three of a kind",
            "two pair",
            "high card",
            "straight",
            "flush",
            "pair"
        };

        public static bool SameHand(string victoryHandId, string reviewHandId) =>
            !string.IsNullOrEmpty(victoryHandId)
            && !string.IsNullOrEmpty(reviewHandId)
            && string.Equals(victoryHandId, reviewHandId, StringComparison.Ordinal);

        /// <summary>
        /// True when the stored coach paragraph does not contradict the
        /// certificate headline. Empty copy agrees, because there is nothing
        /// on the certificate to argue with the headline.
        /// </summary>
        public static bool CopyAgrees(string headline, string heading, string whyWon)
        {
            bool hasCopy = !string.IsNullOrWhiteSpace(whyWon) || !string.IsNullOrWhiteSpace(heading);
            if (!hasCopy || string.IsNullOrWhiteSpace(headline)) return true;

            if (whyWon != null
                && whyWon.IndexOf("no winner is named", StringComparison.OrdinalIgnoreCase) >= 0
                && HeadlineNamesPotWinner(headline))
                return false;

            bool youWon = headline.StartsWith("YOU WON", StringComparison.Ordinal);
            bool theyHeading = heading != null && heading.IndexOf("THEY", StringComparison.Ordinal) >= 0;
            bool opponentWonLine = whyWon != null
                && whyWon.TrimStart().StartsWith("Their ", StringComparison.Ordinal);
            if (youWon && (theyHeading || opponentWonLine)) return false;

            bool folded = headline.IndexOf("everyone folded", StringComparison.OrdinalIgnoreCase) >= 0;
            if (folded)
            {
                if (string.IsNullOrWhiteSpace(whyWon)) return false;
                if (whyWon.IndexOf("folded", StringComparison.OrdinalIgnoreCase) < 0) return false;
                if (whyWon.IndexOf(" beat ", StringComparison.OrdinalIgnoreCase) >= 0) return false;
                return true;
            }

            string headlineCategory = FirstCategory(headline);
            string winnerCategory = WinnerCategory(whyWon);
            if (headlineCategory != null && winnerCategory != null
                && !string.Equals(headlineCategory, winnerCategory, StringComparison.Ordinal))
                return false;

            if (headlineCategory != null && whyWon != null
                && whyWon.IndexOf("everyone else folded", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            return true;
        }

        public static bool HoleAndBoardMatch(string holeText, string boardText, CompletedHandRecord hand)
        {
            if (hand == null) return false;
            if (!TryParseCards(holeText, out List<Card> hole) || hole == null || hole.Count == 0)
                return false;
            if (!SameCards(hole, hand.HeroHoleCards)) return false;
            if (string.IsNullOrWhiteSpace(boardText)) return true;
            if (!TryParseCards(boardText, out List<Card> board) || board == null)
                return false;
            if (hand.FinalBoard == null || hand.FinalBoard.Count == 0)
                return true;
            if (SameCards(board, hand.FinalBoard)) return true;
            // Fold-win certificates store the board that was on the table when
            // the pot was won; the saved hand may add the synthetic runout.
            if (hand.WonByFolds && board.Count <= hand.FinalBoard.Count)
            {
                for (int i = 0; i < board.Count; i++)
                {
                    if (board[i].Index != hand.FinalBoard[i].Index)
                        return false;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Picks the saved hand this certificate actually shows. A matching
        /// hand number is not enough: another sitting reuses those numbers,
        /// and a review left open from an earlier hand must not be reused.
        /// </summary>
        public static CompletedHandRecord FindMatchingHand(
            int handNumber,
            string coachHandId,
            string holeCards,
            string boardCards,
            string headline,
            IEnumerable<CompletedHandRecord> hands)
        {
            if (hands == null) return null;
            CompletedHandRecord best = null;
            int bestScore = 0;
            foreach (CompletedHandRecord hand in hands)
            {
                if (hand == null || !hand.Complete) continue;
                if (!HoleAndBoardMatch(holeCards, boardCards, hand)) continue;
                HandReview review = HandOutcomeExplainer.Build(hand);
                if (!CopyAgrees(headline, review.Heading, review.WhyWon)) continue;

                int score = 1;
                if (hand.HandNumber == handNumber) score += 2;
                if (SameHand(coachHandId, hand.HandId)) score += 4;
                if (score < bestScore) continue;
                best = hand;
                bestScore = score;
            }
            return best;
        }

        private static string WinnerCategory(string whyWon)
        {
            if (string.IsNullOrWhiteSpace(whyWon)) return null;
            string text = whyWon.Trim();
            string slice;
            if (text.StartsWith("Your ", StringComparison.OrdinalIgnoreCase))
                slice = text.Substring(5);
            else if (text.StartsWith("You won with ", StringComparison.OrdinalIgnoreCase))
                slice = text.Substring("You won with ".Length);
            else if (text.StartsWith("Their ", StringComparison.OrdinalIgnoreCase))
                slice = text.Substring(6);
            else
                return null;

            int beat = slice.IndexOf(" beat ", StringComparison.OrdinalIgnoreCase);
            if (beat >= 0) slice = slice.Substring(0, beat);
            return FirstCategory(slice);
        }

        private static bool HeadlineNamesPotWinner(string headline)
        {
            if (string.IsNullOrWhiteSpace(headline)) return false;
            return headline.IndexOf("take the pot", StringComparison.OrdinalIgnoreCase) >= 0
                || headline.IndexOf("takes the pot", StringComparison.OrdinalIgnoreCase) >= 0
                || headline.StartsWith("YOU WON", StringComparison.Ordinal)
                || headline.StartsWith("POT SPLIT", StringComparison.Ordinal);
        }

        private static string FirstCategory(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string lower = text.ToLowerInvariant();
            foreach (string category in CategoriesLongestFirst)
            {
                if (lower.IndexOf(category, StringComparison.Ordinal) >= 0)
                    return category;
            }
            return null;
        }

        private static bool TryParseCards(string text, out List<Card> cards)
        {
            cards = new List<Card>();
            if (string.IsNullOrWhiteSpace(text)) return true;
            foreach (string token in text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                try { cards.Add(Card.Parse(token)); }
                catch (FormatException)
                {
                    cards = null;
                    return false;
                }
            }
            return true;
        }

        private static bool SameCards(List<Card> expected, IReadOnlyList<Card> actual)
        {
            if (expected == null || actual == null || expected.Count != actual.Count) return false;
            var seen = new HashSet<int>();
            foreach (Card card in actual) seen.Add(card.Index);
            if (seen.Count != expected.Count) return false;
            foreach (Card card in expected)
                if (!seen.Contains(card.Index)) return false;
            return true;
        }
    }
}
