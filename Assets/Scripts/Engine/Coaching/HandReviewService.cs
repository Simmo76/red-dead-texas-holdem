using System;
using System.Collections.Generic;
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
        private readonly Queue<string> _shown = new Queue<string>();
        private const int ShownWindow = 5;
        private int _tipCursor;

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
            if (selection.Spot != null && !NeedsPrice(selection.Kind))
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
            if (!NeedsPrice(selection.Kind) || selection.Spot == null || selection.Action == null)
                return review;

            DecisionAdvice advice = await _coach.AnalyseAsync(
                selection.Spot, selection.Action, selection.Kind, seed, cancellationToken, deadlineUtc);
            review.KeyDecision = advice;
            review.LessonKind = selection.Kind;
            review.Lesson = PanelLesson(hand, selection, advice);
            if (advice.Cancelled || advice.ReasonCode == "equity_unavailable")
                review.Status = ReviewStatus.Partial;
            return review;
        }

        /// <summary>
        /// "You were fine" is one sentence. It was showing on almost every
        /// called hand, including hands the player folded. Prefer a board
        /// note, and don't repeat that reassurance inside a short window.
        /// The priced numbers stay on the key decision either way.
        /// </summary>
        private string PanelLesson(CompletedHandRecord hand, LessonSelection selection, DecisionAdvice advice)
        {
            bool routine = selection.Kind == LessonKind.PricedCall || selection.Kind == LessonKind.PricedFold;
            if (routine && IsReassurance(advice.ReasonCode))
            {
                string board = DecisionCoach.ActionableBoardLesson(hand);
                if (!string.IsNullOrEmpty(board) && !Shown(board))
                {
                    Remember(advice.ReasonCode);
                    Remember(board);
                    return board;
                }
                if (ReassuranceCoolingDown() || Shown(advice.Lesson))
                {
                    string tip = FreshTip(hand, selection);
                    Remember(advice.ReasonCode);
                    Remember(tip);
                    return tip;
                }
            }

            Remember(advice.ReasonCode);
            return advice.Lesson;
        }

        private static bool NeedsPrice(LessonKind kind) =>
            kind == LessonKind.TerminalAllInCall
            || kind == LessonKind.LargeCall
            || kind == LessonKind.PricedCall
            || kind == LessonKind.PricedFold;

        private static bool IsReassurance(string reason) =>
            reason == "favourable_call" || reason == "close_price" || reason == "folded_short_price";

        private bool ReassuranceCoolingDown()
        {
            foreach (string item in _shown)
                if (IsReassurance(item)) return true;
            return false;
        }

        private bool Shown(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            foreach (string item in _shown)
                if (item == key) return true;
            return false;
        }

        private void Remember(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            _shown.Enqueue(key);
            while (_shown.Count > ShownWindow) _shown.Dequeue();
        }

        private string FreshTip(CompletedHandRecord hand, LessonSelection selection)
        {
            var options = new List<string>();
            if (selection?.Spot != null && selection.Spot.PlayersBehind > 0)
                options.Add("Someone was still to act. One thing to try: the price changes if a player behind you raises.");
            if (hand != null && hand.HeroFolded && CalledEarlier(hand))
                options.Add("You called, then folded. One thing to try: decide which cards you will continue with before that first call.");
            options.Add(StreetTip(selection?.Spot == null ? CoachStreet.Preflop : selection.Spot.Street));
            options.Add("One thing to try: name the break-even percent before the chips go in.");
            options.Add("One thing to try: judge this street on its own, not on the last pot you lost.");
            for (int i = 0; i < options.Count; i++)
            {
                string tip = options[(_tipCursor + i) % options.Count];
                if (Shown(tip)) continue;
                _tipCursor = (_tipCursor + i + 1) % options.Count;
                return tip;
            }
            string again = options[_tipCursor % options.Count];
            _tipCursor = (_tipCursor + 1) % options.Count;
            return again;
        }

        private static bool CalledEarlier(CompletedHandRecord hand)
        {
            if (hand.HeroActions == null) return false;
            foreach (AcceptedHeroAction action in hand.HeroActions)
                if (action != null && action.Action == "call") return true;
            return false;
        }

        private static string StreetTip(CoachStreet street) => street switch
        {
            CoachStreet.Preflop => "One thing to try: before the flop, call only if you already know which flops you will give up.",
            CoachStreet.Flop => "One thing to try: on the flop, picture the turn bet before you call this one.",
            CoachStreet.Turn => "One thing to try: a turn call commits you to a river price. Name that price first.",
            _ => "One thing to try: on the river there is no next card. Let the price decide, then live with it."
        };

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
