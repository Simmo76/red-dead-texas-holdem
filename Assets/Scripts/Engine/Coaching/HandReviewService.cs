using System;
using System.Threading;
using System.Threading.Tasks;

namespace CinematicPoker.Engine.Coaching
{
    /// <summary>
    /// Read-only review. Outcome text is cheap; the chosen decision is priced
    /// only when a caller asks. Nothing here deals cards or spends the table RNG.
    /// </summary>
    public sealed class HandReviewService
    {
        public const string Assumptions =
            "At showdown, the result uses the saved awards and the evaluator. " +
            "A decision lesson uses only the cards and bets you had at the time. " +
            "Uniform random hands are a rough baseline, not a reconstruction of a raising range. " +
            "Equity is an expected share of the pot, including ties.";

        private readonly DecisionCoach _coach;

        public HandReviewService(DecisionCoach coach = null)
        {
            _coach = coach ?? new DecisionCoach();
        }

        public HandReview Explain(CompletedHandRecord hand)
        {
            HandReview review = HandOutcomeExplainer.Build(hand);
            review.Assumptions = Assumptions;
            if (hand == null || !hand.Complete)
                return review;

            LessonSelection selection = DecisionCoach.Select(hand);
            review.LessonKind = selection.Kind;
            review.Lesson = selection.Lesson;
            if (selection.Spot != null && selection.Kind != LessonKind.TerminalAllInCall
                && selection.Kind != LessonKind.LargeCall
                && selection.Kind != LessonKind.PricedCall)
            {
                review.KeyDecision = new DecisionAdvice
                {
                    DecisionId = selection.Spot.DecisionId,
                    Street = selection.Spot.Street.ToString(),
                    ActionTaken = selection.Action?.Action,
                    RecordedAction = selection.Lesson,
                    BoardText = Board(selection.Spot),
                    Lesson = selection.Lesson,
                    ReasonCode = selection.Kind.ToString(),
                    Caveat = "With the information you had then.",
                    Detail = selection.Kind == LessonKind.FreeCheckFold
                        ? "Checking was legal. Folding gave the pot away for nothing."
                        : "No exact equity is attached to this lesson."
                };
            }
            else if (selection.Spot != null)
            {
                review.KeyDecision = new DecisionAdvice
                {
                    DecisionId = selection.Spot.DecisionId,
                    Street = selection.Spot.Street.ToString(),
                    ActionTaken = selection.Action?.Action,
                    BoardText = Board(selection.Spot),
                    Lesson = selection.Lesson,
                    ReasonCode = "pending",
                    Caveat = "With the information you had then."
                };
            }
            return review;
        }

        public async Task<HandReview> ReviewAsync(CompletedHandRecord hand, int seed, CancellationToken cancellationToken, DateTime? deadlineUtc = null)
        {
            HandReview review = Explain(hand);
            if (hand == null || !hand.Complete) return review;

            LessonSelection selection = DecisionCoach.Select(hand);
            bool needsPrice = selection.Kind == LessonKind.TerminalAllInCall
                || selection.Kind == LessonKind.LargeCall
                || selection.Kind == LessonKind.PricedCall;
            if (!needsPrice || selection.Spot == null || selection.Action == null)
                return review;

            DecisionAdvice advice = await _coach.AnalyseAsync(
                selection.Spot, selection.Action, selection.Kind, seed, cancellationToken, deadlineUtc);
            review.KeyDecision = advice;
            review.Lesson = advice.Lesson;
            review.LessonKind = selection.Kind;
            if (advice.Cancelled || advice.ReasonCode == "equity_unavailable")
                review.Status = ReviewStatus.Partial;
            return review;
        }

        public static int FirstPanelWords(HandReview review)
        {
            if (review == null) return 0;
            return HandOutcomeExplainer.WordCount(review.WhyWon) + HandOutcomeExplainer.WordCount(review.Lesson);
        }

        private static string Board(HeroDecisionSnapshot spot)
        {
            if (spot?.VisibleBoard == null || spot.VisibleBoard.Count == 0) return "No board yet.";
            var parts = new System.Collections.Generic.List<string>();
            foreach (var card in spot.VisibleBoard) parts.Add(BestFiveCards.Code(card));
            return string.Join(" ", parts);
        }
    }
}
