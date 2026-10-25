using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CinematicPoker.Engine.Poker;

namespace CinematicPoker.Engine.Coaching
{
    /// <summary>
    /// Builds the outcome half of a review from the evaluator and the recorded
    /// award ledger. It does not decide who won — the ledger already did.
    /// </summary>
    public static class HandOutcomeExplainer
    {
        public static HandReview Build(CompletedHandRecord hand)
        {
            var review = new HandReview
            {
                HandId = hand?.HandId,
                Status = ReviewStatus.Partial,
                Heading = "HAND REVIEW",
                WhyWon = "Only part of this hand was saved, so the missing cards are left out.",
                Lesson = "There isn't a complete result to learn from yet."
            };
            if (hand == null) 
            {
                review.Status = ReviewStatus.Unavailable;
                review.WhyWon = "No completed hand is available to review.";
                return review;
            }
            if (!hand.Complete)
                return review;

            review.Status = ReviewStatus.Ready;
            review.ChipDetail = ChipDetail(hand);
            review.PotLines = PotLines(hand);

            if (hand.Awards == null || hand.Awards.Count == 0)
            {
                review.Status = ReviewStatus.Partial;
                review.Heading = "WHAT HAPPENED";
                review.WhyWon = "The result was saved without a pot award, so no winner is named.";
                review.HighlightCodes = new List<string>();
                return review;
            }

            bool showdown = hand.Awards.Any(a => !a.ByFold) && !hand.WonByFolds && hand.FinalBoard != null && hand.FinalBoard.Count >= 5;
            if (!showdown || hand.WonByFolds || hand.Awards.All(a => a.ByFold))
            {
                BuildFoldResult(hand, review);
                return review;
            }

            BuildShowdown(hand, review);
            return review;
        }

        private static void DescribeFoldedShowdown(
            CompletedHandRecord hand,
            HandReview review,
            Dictionary<int, HandValue> values,
            Dictionary<int, List<Card>> fives)
        {
            PotAwardRecord pot = Main(hand);
            int winner = -1;
            if (pot?.WinnerSeats != null)
            {
                foreach (int seat in pot.WinnerSeats)
                {
                    if (seat != hand.HeroSeat) { winner = seat; break; }
                }
                if (winner < 0 && pot.WinnerSeats.Count > 0) winner = pot.WinnerSeats[0];
            }
            string name = SeatName(hand, winner);
            review.Heading = "WHAT HAPPENED";
            review.HighlightCodes = new List<string>();
            if (values.TryGetValue(winner, out HandValue value))
            {
                review.WhyWon = "You folded. " + name + " won at showdown with " + WithArticle(CategoryPhrase(value)) + ".";
                if (fives.TryGetValue(winner, out List<Card> five))
                    review.HighlightCodes = Codes(five);
                return;
            }
            review.WhyWon = "You folded. " + name + " won the pot at showdown.";
        }

        private static void BuildFoldResult(CompletedHandRecord hand, HandReview review)
        {
            PotAwardRecord pot = Main(hand);
            bool heroWon = pot != null && pot.WinnerSeats != null && pot.WinnerSeats.Contains(hand.HeroSeat);
            review.HighlightCodes = new List<string>();
            if (heroWon)
            {
                review.Heading = "WHY YOU WON";
                review.WhyWon = "You won because everyone else folded.";
                return;
            }

            int winner = pot?.WinnerSeats != null && pot.WinnerSeats.Count > 0 ? pot.WinnerSeats[0] : -1;
            string name = SeatName(hand, winner);
            review.Heading = "WHAT HAPPENED";
            if (hand.HeroFolded)
                review.WhyWon = "You folded; " + name + " collected the pot without a showdown. Strength is unknown.";
            else
                review.WhyWon = name + " collected the pot without a showdown. Strength is unknown.";
        }

        private static void BuildShowdown(CompletedHandRecord hand, HandReview review)
        {
            var values = new Dictionary<int, HandValue>();
            var fives = new Dictionary<int, List<Card>>();
            foreach (RevealedHand shown in hand.RevealedHands)
            {
                if (shown.HoleCards == null || shown.HoleCards.Count != 2) continue;
                if (hand.FinalBoard == null || hand.FinalBoard.Count < 5) continue;
                var all = new List<Card>(shown.HoleCards);
                all.AddRange(hand.FinalBoard);
                values[shown.Seat] = HandEvaluator.Evaluate(all);
                fives[shown.Seat] = BestFiveCards.Select(all);
            }

            if (hand.HeroFolded && !values.ContainsKey(hand.HeroSeat))
            {
                if (hand.Awards.Count > 1)
                {
                    BuildMultiPot(hand, review, values, fives);
                    review.Heading = "WHAT HAPPENED";
                    if (review.WhyWon != null && review.WhyWon.IndexOf("You folded", StringComparison.Ordinal) < 0)
                        review.WhyWon = "You folded. " + review.WhyWon;
                    return;
                }
                DescribeFoldedShowdown(hand, review, values, fives);
                return;
            }

            if (hand.Awards.Count > 1)
            {
                BuildMultiPot(hand, review, values, fives);
                return;
            }

            PotAwardRecord pot = Main(hand);
            DescribePot(hand, review, pot, values, fives, solePot: true);
        }

        private static void BuildMultiPot(
            CompletedHandRecord hand,
            HandReview review,
            Dictionary<int, HandValue> values,
            Dictionary<int, List<Card>> fives)
        {
            bool heroWonAny = false;
            bool heroLostAny = false;
            var bits = new List<string>();
            foreach (PotAwardRecord pot in hand.Awards.OrderBy(a => a.PotIndex))
            {
                bool heroWins = pot.WinnerSeats != null && pot.WinnerSeats.Contains(hand.HeroSeat);
                bool sole = pot.WinnerSeats != null && pot.WinnerSeats.Count == 1;
                if (heroWins && sole) heroWonAny = true;
                if (!heroWins) heroLostAny = true;
                bits.Add(PotSentence(hand, pot, values));
            }

            review.WhyWon = string.Join(" ", bits);
            review.Heading = heroWonAny && heroLostAny
                ? "HAND RESULT"
                : heroWonAny && !heroLostAny
                    ? "WHY YOU WON"
                    : heroLostAny ? "WHY THEY WON" : "HAND RESULT";

            PotAwardRecord focus = hand.Awards.FirstOrDefault(a => a.WinnerSeats != null && !a.WinnerSeats.Contains(hand.HeroSeat))
                ?? Main(hand);
            review.HighlightCodes = HighlightsFor(hand, focus, values, fives);
            review.LessonKind = LessonKind.Neutral;
        }

        private static void DescribePot(
            CompletedHandRecord hand,
            HandReview review,
            PotAwardRecord pot,
            Dictionary<int, HandValue> values,
            Dictionary<int, List<Card>> fives,
            bool solePot)
        {
            var winners = pot?.WinnerSeats ?? new List<int>();
            bool heroWins = winners.Contains(hand.HeroSeat);
            bool split = winners.Count > 1;

            if (split && heroWins && values.TryGetValue(hand.HeroSeat, out HandValue heroValue))
            {
                bool boardOnly = fives.TryGetValue(hand.HeroSeat, out List<Card> five) && EntirelyOnBoard(five, hand.FinalBoard);
                string cat = CategoryPhrase(heroValue);
                review.Heading = "WHY IT SPLIT";
                review.WhyWon = boardOnly
                    ? "You split the pot. Both of you play the " + cat + " on the board."
                    : "You split the pot. Both play the same " + cat + ".";
                review.HighlightCodes = Codes(five);
                return;
            }

            if (!values.TryGetValue(hand.HeroSeat, out HandValue hero))
            {
                review.Status = ReviewStatus.Partial;
                review.Heading = heroWins ? "WHY YOU WON" : "WHAT HAPPENED";
                review.WhyWon = heroWins
                    ? "You won the pot. The saved cards are not complete enough to name the hand."
                    : "The pot was awarded, but your cards were not in the saved record.";
                review.HighlightCodes = new List<string>();
                return;
            }

            int otherSeat = winners.FirstOrDefault(s => s != hand.HeroSeat);
            if (!heroWins && winners.Count > 0) otherSeat = winners[0];
            bool haveOther = values.TryGetValue(otherSeat, out HandValue other);

            if (heroWins && !haveOther)
            {
                review.Heading = "WHY YOU WON";
                review.WhyWon = "You won with " + WithArticle(CategoryPhrase(hero)) + ".";
                review.HighlightCodes = fives.TryGetValue(hand.HeroSeat, out List<Card> own) ? Codes(own) : new List<string>();
                return;
            }

            if (!haveOther)
            {
                review.Status = ReviewStatus.Partial;
                review.Heading = "WHY THEY WON";
                review.WhyWon = SeatName(hand, otherSeat) + " won the pot. Their cards were not shown, so the hand is not named.";
                review.HighlightCodes = new List<string>();
                return;
            }

            if (heroWins)
            {
                review.Heading = "WHY YOU WON";
                review.WhyWon = Comparison(winner: hero, loser: other, winnerIsHero: true);
                review.HighlightCodes = fives.TryGetValue(hand.HeroSeat, out List<Card> own) ? Codes(own) : new List<string>();
                return;
            }

            review.Heading = "WHY THEY WON";
            review.WhyWon = Comparison(winner: other, loser: hero, winnerIsHero: false);
            review.HighlightCodes = fives.TryGetValue(otherSeat, out List<Card> theirs) ? Codes(theirs) : new List<string>();
        }

        public static string Comparison(HandValue winner, HandValue loser, bool winnerIsHero)
        {
            string lead = winnerIsHero ? "Your" : "Their";
            string other = winnerIsHero ? "their" : "your";
            if (winner.Category != loser.Category)
                return lead + " " + ShortCategory(winner.Category) + " beat " + other + " " + ShortCategory(loser.Category) + ".";

            Rank[] winBreaks = BestFiveCards.TieBreaks(winner);
            Rank[] loseBreaks = BestFiveCards.TieBreaks(loser);
            if (winner.Category == HandCategory.Pair && winBreaks[0] != 0 && winBreaks[1] != loseBreaks[1] && loseBreaks[1] != 0)
            {
                return lead + " pair of " + Plural(winBreaks[0]) + ", " + Word(winBreaks[1]) + " kicker, beat " + other + " " + Word(loseBreaks[1]) + " kicker.";
            }

            if (winner.Category == HandCategory.Straight || winner.Category == HandCategory.StraightFlush)
            {
                return lead + " " + StraightName(winBreaks[0]) + " beat " + other + " " + StraightName(loseBreaks[0]) + ".";
            }

            if (winner.Score == loser.Score)
                return "You split the pot. Both play the same " + CategoryPhrase(winner) + ".";

            return lead + " " + ShortCategory(winner.Category) + " beat " + other + " " + ShortCategory(loser.Category) + ".";
        }

        private static string PotSentence(CompletedHandRecord hand, PotAwardRecord pot, Dictionary<int, HandValue> values)
        {
            bool heroWins = pot.WinnerSeats != null && pot.WinnerSeats.Contains(hand.HeroSeat);
            bool split = pot.WinnerSeats != null && pot.WinnerSeats.Count > 1;
            string potName = pot.PotIndex == 0 ? "the main pot" : "the side pot";
            if (split && heroWins)
                return "You split " + potName + ".";
            if (heroWins)
            {
                string cat = values.TryGetValue(hand.HeroSeat, out HandValue value) ? " with " + ShortCategory(value.Category) : "";
                return "You won " + potName + cat + ".";
            }

            string winner = SeatName(hand, pot.WinnerSeats != null && pot.WinnerSeats.Count > 0 ? pot.WinnerSeats[0] : -1);
            string theirCat = "";
            if (pot.WinnerSeats != null && pot.WinnerSeats.Count > 0 && values.TryGetValue(pot.WinnerSeats[0], out HandValue winnerValue))
                theirCat = " with " + ShortCategory(winnerValue.Category);
            return winner + " won " + potName + theirCat + ".";
        }

        private static List<string> PotLines(CompletedHandRecord hand)
        {
            var lines = new List<string>();
            if (hand.Awards == null) return lines;
            foreach (PotAwardRecord pot in hand.Awards.OrderBy(a => a.PotIndex))
            {
                string name = pot.PotIndex == 0 ? "Main pot" : "Side pot";
                string who = pot.WinnerSeats == null || pot.WinnerSeats.Count == 0
                    ? "nobody"
                    : string.Join(" and ", pot.WinnerSeats.Select(s => s == hand.HeroSeat ? "you" : SeatName(hand, s)));
                lines.Add(name + " " + pot.Amount + " to " + who + ".");
            }
            return lines;
        }

        private static string ChipDetail(CompletedHandRecord hand)
        {
            var text = new StringBuilder();
            text.Append("Net ");
            text.Append(hand.HeroNet > 0 ? "+" + hand.HeroNet : hand.HeroNet.ToString());
            text.Append('.');
            long returned = 0;
            if (hand.Returns != null)
            {
                foreach (UncalledReturn item in hand.Returns)
                    if (item.Seat == hand.HeroSeat) returned += item.Amount;
            }
            if (returned > 0)
                text.Append(' ').Append(returned).Append(" was returned uncalled and is not profit.");
            return text.ToString();
        }

        private static List<string> HighlightsFor(
            CompletedHandRecord hand,
            PotAwardRecord pot,
            Dictionary<int, HandValue> values,
            Dictionary<int, List<Card>> fives)
        {
            if (pot?.WinnerSeats == null || pot.WinnerSeats.Count == 0) return new List<string>();
            int seat = pot.WinnerSeats.Contains(hand.HeroSeat) ? hand.HeroSeat : pot.WinnerSeats[0];
            return fives.TryGetValue(seat, out List<Card> five) ? Codes(five) : new List<string>();
        }

        private static List<string> Codes(List<Card> cards)
        {
            var codes = new List<string>();
            if (cards == null) return codes;
            foreach (Card card in cards) codes.Add(BestFiveCards.Code(card));
            return codes;
        }

        private static bool EntirelyOnBoard(List<Card> five, IReadOnlyList<Card> board)
        {
            if (five == null || board == null) return false;
            var onBoard = new HashSet<int>();
            foreach (Card card in board) onBoard.Add(card.Index);
            foreach (Card card in five)
                if (!onBoard.Contains(card.Index)) return false;
            return true;
        }

        private static PotAwardRecord Main(CompletedHandRecord hand) =>
            hand.Awards?.FirstOrDefault(a => a.PotIndex == 0) ?? hand.Awards?.FirstOrDefault();

        public static string SeatName(CompletedHandRecord hand, int seat)
        {
            if (seat == hand.HeroSeat) return "you";
            if (hand.SeatNames != null && hand.SeatNames.TryGetValue(seat, out string name) && !string.IsNullOrEmpty(name))
                return name;
            return "the winner";
        }

        public static string ShortCategory(HandCategory category) => category switch
        {
            HandCategory.HighCard => "high card",
            HandCategory.Pair => "pair",
            HandCategory.TwoPair => "two pair",
            HandCategory.ThreeOfAKind => "three of a kind",
            HandCategory.Straight => "straight",
            HandCategory.Flush => "flush",
            HandCategory.FullHouse => "full house",
            HandCategory.FourOfAKind => "four of a kind",
            HandCategory.StraightFlush => "straight flush",
            HandCategory.RoyalFlush => "royal flush",
            _ => "hand"
        };

        public static string CategoryPhrase(HandValue value)
        {
            if (value.Category == HandCategory.Straight || value.Category == HandCategory.StraightFlush)
            {
                Rank high = BestFiveCards.TieBreaks(value)[0];
                if (high == Rank.Five) return "five-high straight";
            }
            if (value.Category == HandCategory.RoyalFlush) return "royal flush";
            return ShortCategory(value.Category);
        }

        private static string WithArticle(string phrase) =>
            phrase.StartsWith("a ") || phrase.StartsWith("the ") ? phrase : (
                "aeiou".IndexOf(char.ToLowerInvariant(phrase[0])) >= 0 ? "an " + phrase : "a " + phrase);

        private static string StraightName(Rank high) =>
            high == Rank.Five ? "five-high straight" : Word(high) + "-high straight";

        public static string Word(Rank rank) => rank switch
        {
            Rank.Ace => "ace",
            Rank.King => "king",
            Rank.Queen => "queen",
            Rank.Jack => "jack",
            Rank.Ten => "ten",
            Rank.Nine => "nine",
            Rank.Eight => "eight",
            Rank.Seven => "seven",
            Rank.Six => "six",
            Rank.Five => "five",
            Rank.Four => "four",
            Rank.Three => "three",
            Rank.Two => "two",
            _ => "card"
        };

        public static string Plural(Rank rank)
        {
            string word = Word(rank);
            if (word == "six") return "sixes";
            return word + "s";
        }

        public static int WordCount(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            return text.Split(new[] { ' ', '\n', '\t' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
        }
    }
}
