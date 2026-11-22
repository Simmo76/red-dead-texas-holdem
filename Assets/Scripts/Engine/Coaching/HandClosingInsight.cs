using System;
using System.Collections.Generic;
using System.Linq;
using CinematicPoker.Engine.Poker;

namespace CinematicPoker.Engine.Coaching
{
    /// <summary>
    /// Closing note for the key-decision panel. Uses the finished hand — cards
    /// that were shown, who took the pot, and the priced lesson — instead of a
    /// fixed methodology disclaimer that repeated every review.
    /// </summary>
    public static class HandClosingInsight
    {
        public static string Build(
            CompletedHandRecord hand,
            HandReview review,
            LessonSelection selection,
            DecisionAdvice advice)
        {
            if (hand == null || !hand.Complete) return null;

            string note = FromFoldedWinner(hand)
                ?? FromShowdown(hand)
                ?? FromFoldOut(hand)
                ?? FromPricedSpot(hand, selection, advice)
                ?? FromBoard(hand)
                ?? Fallback(hand, review);

            if (string.IsNullOrEmpty(note)) return null;
            if (Repeats(note, review?.Lesson) || Repeats(note, review?.WhyWon))
                return Soften(hand, review, advice);
            return note;
        }

        /// <summary>
        /// Hero folded, but the hole cards they abandoned would have beaten the
        /// hand that took the pot at showdown.
        /// </summary>
        private static string FromFoldedWinner(CompletedHandRecord hand)
        {
            if (!hand.HeroFolded) return null;
            if (hand.FinalBoard == null || hand.FinalBoard.Count < 5) return null;
            if (hand.HeroHoleCards == null || hand.HeroHoleCards.Count != 2) return null;
            if (hand.WonByFolds) return null;

            HandValue? hero = Score(hand.HeroHoleCards, hand.FinalBoard);
            if (!hero.HasValue) return null;

            int winnerSeat = MainWinnerSeat(hand);
            if (winnerSeat < 0 || winnerSeat == hand.HeroSeat) return null;
            List<Card> winnerHole = HoleFor(hand, winnerSeat);
            HandValue? winner = Score(winnerHole, hand.FinalBoard);
            if (!winner.HasValue) return null;
            if (hero.Value.CompareTo(winner.Value) <= 0) return null;

            string name = HandOutcomeExplainer.SeatName(hand, winnerSeat);
            string their = HandOutcomeExplainer.CategoryPhrase(winner.Value);
            string yours = HandOutcomeExplainer.CategoryPhrase(hero.Value);
            return "You had already folded, but your " + yours + " would have beaten "
                + name + "'s " + their
                + ". One thing to try: decide which turn and river cards keep you in before the flop call.";
        }

        private static string FromShowdown(CompletedHandRecord hand)
        {
            if (hand.WonByFolds) return null;
            if (hand.FinalBoard == null || hand.FinalBoard.Count < 5) return null;
            if (hand.HeroHoleCards == null || hand.HeroHoleCards.Count != 2) return null;
            if (hand.HeroFolded) return null;

            HandValue? hero = Score(hand.HeroHoleCards, hand.FinalBoard);
            if (!hero.HasValue) return null;

            int otherSeat = BestRevealedOpponent(hand);
            if (otherSeat < 0) return SoloShowdown(hand, hero.Value);

            List<Card> otherHole = HoleFor(hand, otherSeat);
            HandValue? other = Score(otherHole, hand.FinalBoard);
            if (!other.HasValue) return SoloShowdown(hand, hero.Value);

            bool heroWins = HeroWonMain(hand);
            bool split = IsSplitMain(hand);
            if (split)
            {
                if (BoardPlays(hand, hero.Value))
                    return "The " + HandOutcomeExplainer.CategoryPhrase(hero.Value)
                        + " lived entirely on the board. One thing to try: when the board is the nuts, stop paying to find out.";
                return "You chopped with the same " + HandOutcomeExplainer.CategoryPhrase(hero.Value)
                    + ". One thing to try: once the board pairs the nuts for everyone, smaller pots are the win.";
            }

            HandValue winner = heroWins ? hero.Value : other.Value;
            HandValue loser = heroWins ? other.Value : hero.Value;
            string name = HandOutcomeExplainer.SeatName(hand, otherSeat);

            if (winner.Category == loser.Category)
                return SameCategoryNote(hand, heroWins, winner, loser, name);

            if (heroWins)
            {
                return "You were ahead in hand class at showdown. One thing to try: once you beat their likely hand class, prefer value sizes that still get called.";
            }

            return CategoryLossNote(hand, winner, loser, name);
        }

        private static string SameCategoryNote(
            CompletedHandRecord hand,
            bool heroWins,
            HandValue winner,
            HandValue loser,
            string otherName)
        {
            Rank[] winBreaks = BestFiveCards.TieBreaks(winner);
            Rank[] loseBreaks = BestFiveCards.TieBreaks(loser);

            if (winner.Category == HandCategory.Pair
                && winBreaks.Length > 1
                && loseBreaks.Length > 1
                && winBreaks[0] == loseBreaks[0]
                && winBreaks[1] != loseBreaks[1]
                && winBreaks[1] != 0
                && loseBreaks[1] != 0)
            {
                if (heroWins)
                {
                    return "Same pair of " + HandOutcomeExplainer.Plural(winBreaks[0])
                        + " — your " + HandOutcomeExplainer.Word(winBreaks[1])
                        + " kicker beat their " + HandOutcomeExplainer.Word(loseBreaks[1])
                        + ". One thing to try: with top pair, name the kickers that still beat you before you pay off a big bet.";
                }

                return "Same pair of " + HandOutcomeExplainer.Plural(winBreaks[0])
                    + " — " + otherName + "'s " + HandOutcomeExplainer.Word(winBreaks[1])
                    + " kicker beat your " + HandOutcomeExplainer.Word(loseBreaks[1])
                    + ". One thing to try: with top pair, name the kickers that still beat you before you pay off a big bet.";
            }

            if (winner.Category == HandCategory.Straight || winner.Category == HandCategory.StraightFlush)
            {
                return heroWins
                    ? "Your " + StraightLabel(winBreaks[0]) + " outran their "
                        + StraightLabel(loseBreaks[0]) + ". One thing to try: on connected boards, ask who owns the higher end before you stack off."
                    : otherName + "'s " + StraightLabel(winBreaks[0]) + " beat your "
                        + StraightLabel(loseBreaks[0]) + ". One thing to try: on connected boards, ask who owns the higher end before you stack off.";
            }

            return heroWins
                ? "You won the same " + HandOutcomeExplainer.ShortCategory(winner.Category)
                    + " class on a better kicker line. One thing to try: when categories match, the side cards decide — name them before the chips go in."
                : otherName + " won the same " + HandOutcomeExplainer.ShortCategory(winner.Category)
                    + " class on a better kicker line. One thing to try: when categories match, the side cards decide — name them before the chips go in.";
        }

        private static string CategoryLossNote(
            CompletedHandRecord hand,
            HandValue winner,
            HandValue loser,
            string winnerName)
        {
            if (winner.Category == HandCategory.Flush
                || winner.Category == HandCategory.StraightFlush
                || winner.Category == HandCategory.RoyalFlush)
            {
                return "The board had enough of one suit for "
                    + winnerName + " to make "
                    + WithArticle(HandOutcomeExplainer.ShortCategory(winner.Category))
                    + ". One thing to try: once three of a suit are out, treat made pairs as vulnerable to one card.";
            }

            if (winner.Category == HandCategory.Straight
                && (loser.Category == HandCategory.Pair
                    || loser.Category == HandCategory.TwoPair
                    || loser.Category == HandCategory.ThreeOfAKind))
            {
                return "The board was connected enough for a straight. One thing to try: ask whether that straight already got there before you call a big bet.";
            }

            if (winner.Category >= HandCategory.FullHouse
                && loser.Category <= HandCategory.TwoPair)
            {
                return "The board paired into "
                    + WithArticle(HandOutcomeExplainer.ShortCategory(winner.Category))
                    + ". One thing to try: when the board pairs, re-check whether two pair or trips still win.";
            }

            return winnerName + " showed "
                + WithArticle(HandOutcomeExplainer.ShortCategory(winner.Category))
                + " against your "
                + HandOutcomeExplainer.ShortCategory(loser.Category)
                + ". One thing to try: name the hand class that beats you on this board before the last chips go in.";
        }

        private static string SoloShowdown(CompletedHandRecord hand, HandValue hero)
        {
            if (HeroWonMain(hand))
            {
                return "You took it with " + WithArticle(HandOutcomeExplainer.CategoryPhrase(hero))
                    + ". One thing to try: when opponents never show, judge the hand by the price you paid, not by the cards you held.";
            }

            string name = HandOutcomeExplainer.SeatName(hand, MainWinnerSeat(hand));
            return name + " took the pot and did not show. One thing to try: without their cards, trust the price you faced more than the story you invent.";
        }

        private static string FromFoldOut(CompletedHandRecord hand)
        {
            if (!hand.WonByFolds) return null;
            if (HeroWonMain(hand))
            {
                return "Everyone else folded, so the cards never mattered. One thing to try: when a bet takes it down, ask whether a smaller size would have done the same work.";
            }

            if (hand.HeroFolded)
            {
                string name = HandOutcomeExplainer.SeatName(hand, MainWinnerSeat(hand));
                return "You folded and " + name
                    + " collected without a showdown. One thing to try: only continue when you already know which turn and river cards keep you in.";
            }

            return null;
        }

        private static string FromPricedSpot(
            CompletedHandRecord hand,
            LessonSelection selection,
            DecisionAdvice advice)
        {
            if (advice == null) return null;
            string code = advice.ReasonCode ?? "";

            if (code == "side_pot")
            {
                return "Side pots price each slice of your call on its own. One thing to try: name the main-pot break-even and the side-pot break-even before the chips go in.";
            }

            if (code == "favourable_call" && hand.HeroNet < 0)
            {
                return "The call cleared the price; this pot still went the other way. One thing to try: judge the decision by the price, not by one bad runout.";
            }

            if (code == "short_price" && (advice.ActionTaken == "call" || advice.ActionTaken == "all-in"))
            {
                return "The estimate said this call was short of break-even. One thing to try: walk away when the percent you need is higher than the share you think you have.";
            }

            if (code == "folded_favourable_price")
            {
                return "Folding gave up a price that looked good on the numbers. One thing to try: when the break-even percent is low, default to continuing unless the board is scary.";
            }

            if (code == "close_price")
            {
                return "The price was close enough that either action can be fine. One thing to try: in coin-flip spots, pick a rule and stick to it so one result does not rewrite your plan.";
            }

            if (selection?.Kind == LessonKind.FreeCheckFold)
            {
                return "A free check costs nothing and keeps your hand alive. One thing to try: if checking is legal, fold only when you are giving up on purpose.";
            }

            if (selection?.Kind == LessonKind.UnpricedRaise)
            {
                return "A raise needs fold equity and a size, not just showdown strength. One thing to try: name who folds and what you do when they call before you put the raise in.";
            }

            return null;
        }

        private static string FromBoard(CompletedHandRecord hand)
        {
            string board = DecisionCoach.ActionableBoardLesson(hand);
            if (string.IsNullOrEmpty(board)) return null;
            // Prefer a showdown or priced note above. This only fires when the
            // board texture is the strongest remaining observation.
            return board;
        }

        private static string Fallback(CompletedHandRecord hand, HandReview review)
        {
            if (hand.HeroNet > 0)
                return "You booked a win on this one. One thing to try: replay the street where most of the chips went in and name the price you faced there.";
            if (hand.HeroNet < 0)
                return "This one cost chips. One thing to try: find the street where the pot grew fastest and ask whether the price was still right.";
            return "The hand washed even. One thing to try: chopped or quiet pots are still worth a quick price check for next time.";
        }

        private static string Soften(CompletedHandRecord hand, HandReview review, DecisionAdvice advice)
        {
            if (advice != null && advice.EquityShare.HasValue)
            {
                return "The percent above assumes every leftover hand is equally likely. One thing to try: tighten that picture by crossing out hands a careful player would already have folded.";
            }

            if (!string.IsNullOrEmpty(review?.WhyWon)
                && review.WhyWon.IndexOf("folded", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "No showdown hand was priced here. One thing to try: write down the price you faced on the street you gave up.";
            }

            return Fallback(hand, review);
        }

        private static bool Repeats(string note, string other)
        {
            if (string.IsNullOrWhiteSpace(note) || string.IsNullOrWhiteSpace(other)) return false;
            if (string.Equals(note.Trim(), other.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            // Same lead sentence (before the try) means the footer adds nothing.
            string noteLead = Lead(note);
            string otherLead = Lead(other);
            return noteLead.Length >= 12
                && otherLead.Length >= 12
                && string.Equals(noteLead, otherLead, StringComparison.OrdinalIgnoreCase);
        }

        private static string Lead(string text)
        {
            int cut = text.IndexOf('.');
            return (cut > 0 ? text.Substring(0, cut) : text).Trim();
        }

        private static HandValue? Score(IReadOnlyList<Card> hole, IReadOnlyList<Card> board)
        {
            if (hole == null || hole.Count != 2 || board == null || board.Count < 5) return null;
            var all = new List<Card>(hole);
            all.AddRange(board);
            return HandEvaluator.Evaluate(all);
        }

        private static List<Card> HoleFor(CompletedHandRecord hand, int seat)
        {
            if (seat == hand.HeroSeat) return hand.HeroHoleCards;
            if (hand.RevealedHands == null) return null;
            foreach (RevealedHand shown in hand.RevealedHands)
            {
                if (shown != null && shown.Seat == seat && shown.HoleCards != null && shown.HoleCards.Count == 2)
                    return shown.HoleCards;
            }
            return null;
        }

        private static int BestRevealedOpponent(CompletedHandRecord hand)
        {
            if (hand.RevealedHands == null) return -1;
            int bestSeat = -1;
            HandValue best = default;
            foreach (RevealedHand shown in hand.RevealedHands)
            {
                if (shown == null || shown.Seat == hand.HeroSeat) continue;
                HandValue? value = Score(shown.HoleCards, hand.FinalBoard);
                if (!value.HasValue) continue;
                if (bestSeat < 0 || value.Value.CompareTo(best) > 0)
                {
                    bestSeat = shown.Seat;
                    best = value.Value;
                }
            }
            return bestSeat;
        }

        private static int MainWinnerSeat(CompletedHandRecord hand)
        {
            IReadOnlyList<PotAwardRecord> awards = AwardLedgerBackfill.Resolve(hand);
            PotAwardRecord pot = awards?.FirstOrDefault(a => a.PotIndex == 0) ?? awards?.FirstOrDefault();
            if (pot?.WinnerSeats == null || pot.WinnerSeats.Count == 0) return -1;
            foreach (int seat in pot.WinnerSeats)
                if (seat != hand.HeroSeat) return seat;
            return pot.WinnerSeats[0];
        }

        private static bool HeroWonMain(CompletedHandRecord hand)
        {
            IReadOnlyList<PotAwardRecord> awards = AwardLedgerBackfill.Resolve(hand);
            PotAwardRecord pot = awards?.FirstOrDefault(a => a.PotIndex == 0) ?? awards?.FirstOrDefault();
            return pot?.WinnerSeats != null && pot.WinnerSeats.Contains(hand.HeroSeat);
        }

        private static bool IsSplitMain(CompletedHandRecord hand)
        {
            IReadOnlyList<PotAwardRecord> awards = AwardLedgerBackfill.Resolve(hand);
            PotAwardRecord pot = awards?.FirstOrDefault(a => a.PotIndex == 0) ?? awards?.FirstOrDefault();
            return pot?.WinnerSeats != null
                && pot.WinnerSeats.Count > 1
                && pot.WinnerSeats.Contains(hand.HeroSeat);
        }

        private static bool BoardPlays(CompletedHandRecord hand, HandValue hero)
        {
            if (hand.FinalBoard == null || hand.FinalBoard.Count < 5) return false;
            HandValue boardOnly = HandEvaluator.Evaluate(hand.FinalBoard.ToList());
            return boardOnly.Score == hero.Score;
        }

        private static string StraightLabel(Rank high) =>
            high == Rank.Five ? "five-high straight" : HandOutcomeExplainer.Word(high) + "-high straight";

        private static string WithArticle(string phrase)
        {
            if (string.IsNullOrEmpty(phrase)) return phrase;
            if (phrase.StartsWith("a ", StringComparison.Ordinal) || phrase.StartsWith("an ", StringComparison.Ordinal)
                || phrase.StartsWith("the ", StringComparison.Ordinal))
                return phrase;
            char c = char.ToLowerInvariant(phrase[0]);
            return "aeiou".IndexOf(c) >= 0 ? "an " + phrase : "a " + phrase;
        }
    }
}
