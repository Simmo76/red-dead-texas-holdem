using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CinematicPoker.Engine.AI;
using CinematicPoker.Engine.Poker;

namespace CinematicPoker.Engine.Coaching
{
    public interface IEquitySource
    {
        Task<EquityEstimate> EstimateAsync(
            HeroDecisionSnapshot spot,
            Random rng,
            int iterations,
            CancellationToken cancellationToken,
            DateTime? deadlineUtc);
    }

    /// <summary>
    /// Uniform random opponent hands. On the river against one opponent the
    /// unseen hands are enumerated exactly; otherwise a seeded Monte Carlo
    /// sample is used. This is a rough baseline, not a raising range.
    /// </summary>
    public sealed class UniformEquitySource : IEquitySource
    {
        public const int DefaultIterations = 400;

        public async Task<EquityEstimate> EstimateAsync(
            HeroDecisionSnapshot spot,
            Random rng,
            int iterations,
            CancellationToken cancellationToken,
            DateTime? deadlineUtc)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (deadlineUtc.HasValue && DateTime.UtcNow >= deadlineUtc.Value)
                throw new OperationCanceledException(cancellationToken);
            if (spot.HeroHoleCards == null || spot.HeroHoleCards.Count != 2)
                throw new ArgumentException("Hero hole cards are required.");
            if (spot.ActiveOpponents < 1 || spot.ActiveOpponents > 5)
                throw new ArgumentException("Opponent count must be 1-5.");

            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            if (spot.VisibleBoard != null && spot.VisibleBoard.Count >= 5 && spot.ActiveOpponents == 1)
                return ExactOneOpponent(spot, cancellationToken, deadlineUtc);

            int samples = iterations < 1 ? DefaultIterations : iterations;
            double share = EquityCalculator.Estimate(
                spot.HeroHoleCards,
                spot.VisibleBoard ?? new List<Card>(),
                spot.ActiveOpponents,
                samples,
                rng ?? new Random(1));
            return new EquityEstimate
            {
                Share = share,
                Method = "Monte Carlo, uniform random hands",
                Samples = samples,
                Exact = false
            };
        }

        private static EquityEstimate ExactOneOpponent(HeroDecisionSnapshot spot, CancellationToken cancellationToken, DateTime? deadlineUtc)
        {
            var used = new bool[52];
            foreach (Card card in spot.HeroHoleCards) used[card.Index] = true;
            foreach (Card card in spot.VisibleBoard) used[card.Index] = true;
            var remaining = new List<int>();
            for (int i = 0; i < 52; i++)
                if (!used[i]) remaining.Add(i);

            var mine = new List<Card>(spot.HeroHoleCards);
            mine.AddRange(spot.VisibleBoard);
            HandValue hero = HandEvaluator.Evaluate(mine);

            double share = 0;
            int samples = 0;
            var opp = new List<Card>(7);
            for (int a = 0; a < remaining.Count; a++)
            {
                for (int b = a + 1; b < remaining.Count; b++)
                {
                    if ((samples & 63) == 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (deadlineUtc.HasValue && DateTime.UtcNow >= deadlineUtc.Value)
                            throw new OperationCanceledException(cancellationToken);
                    }
                    opp.Clear();
                    opp.Add(Card.FromIndex(remaining[a]));
                    opp.Add(Card.FromIndex(remaining[b]));
                    opp.AddRange(spot.VisibleBoard);
                    HandValue theirs = HandEvaluator.Evaluate(opp);
                    if (theirs < hero) share += 1;
                    else if (theirs == hero) share += 0.5;
                    samples++;
                }
            }

            return new EquityEstimate
            {
                Share = samples == 0 ? 0 : share / samples,
                Method = "Exact enumeration of unseen hands",
                Samples = samples,
                Exact = true
            };
        }
    }

    /// <summary>Test double. The share is supplied by the test, not by cards.</summary>
    public sealed class FixedEquitySource : IEquitySource
    {
        public double Share { get; set; }
        public int Calls { get; private set; }

        public FixedEquitySource(double share)
        {
            Share = share;
        }

        public Task<EquityEstimate> EstimateAsync(
            HeroDecisionSnapshot spot,
            Random rng,
            int iterations,
            CancellationToken cancellationToken,
            DateTime? deadlineUtc)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (deadlineUtc.HasValue && DateTime.UtcNow >= deadlineUtc.Value)
                throw new OperationCanceledException(cancellationToken);
            Calls++;
            return Task.FromResult(new EquityEstimate
            {
                Share = Share,
                Method = "injected",
                Samples = 1,
                Exact = true
            });
        }
    }

    /// <summary>
    /// Turns one recorded decision into a single qualified lesson.
    /// Showdown equity is never used to claim a bet or raise was better.
    /// </summary>
    public sealed class DecisionCoach
    {
        private readonly IEquitySource _equity;
        private readonly Dictionary<string, DecisionAdvice> _cache = new Dictionary<string, DecisionAdvice>();

        public DecisionCoach(IEquitySource equity = null)
        {
            _equity = equity ?? new UniformEquitySource();
        }

        public static LessonSelection Select(CompletedHandRecord hand)
        {
            if (hand?.Decisions == null || hand.HeroActions == null)
                return BoardOrNeutral(hand);

            var actions = new Dictionary<int, AcceptedHeroAction>();
            foreach (AcceptedHeroAction action in hand.HeroActions)
                actions[action.DecisionId] = action;

            LessonSelection terminal = null;
            LessonSelection large = null;
            LessonSelection priced = null;
            LessonSelection pricedFold = null;
            LessonSelection freeCheck = null;
            LessonSelection raise = null;

            foreach (HeroDecisionSnapshot spot in hand.Decisions)
            {
                if (!actions.TryGetValue(spot.DecisionId, out AcceptedHeroAction action)) continue;
                if (IsFreeCheckFold(spot, action))
                    freeCheck = Qualitative(spot, action, LessonKind.FreeCheckFold, FreeCheckLesson());
                else if (IsPricedCall(spot, action))
                {
                    var pick = Qualitative(spot, action, LessonKind.PricedCall, "Reading the price you were offered.");
                    if (IsTerminal(spot, action) && (terminal == null || spot.IncrementalCall > terminal.Spot.IncrementalCall))
                    {
                        pick.Kind = LessonKind.TerminalAllInCall;
                        terminal = pick;
                    }
                    else if (IsLarge(spot, action) && (large == null || spot.IncrementalCall > large.Spot.IncrementalCall))
                    {
                        pick.Kind = LessonKind.LargeCall;
                        large = pick;
                    }
                    else if (priced == null || spot.IncrementalCall > priced.Spot.IncrementalCall)
                    {
                        priced = pick;
                    }
                }
                else if (IsPricedFold(spot, action))
                {
                    // A fold for a price is the decision the result describes.
                    // An earlier routine call must not replace it with the same
                    // "the call was fine" line on every folded hand.
                    var pick = Qualitative(spot, action, LessonKind.PricedFold, "Reading the price you folded.");
                    if (pricedFold == null || spot.IncrementalCall > pricedFold.Spot.IncrementalCall)
                        pricedFold = pick;
                }
                else if (IsUnpricedRaise(action))
                {
                    raise = Qualitative(spot, action, LessonKind.UnpricedRaise, RaiseLesson());
                }
            }

            if (terminal != null) return terminal;
            if (large != null) return large;
            if (freeCheck != null) return freeCheck;
            if (pricedFold != null) return pricedFold;
            if (priced != null) return priced;
            if (raise != null && !BoardHasFlushTexture(hand)) return raise;
            return BoardOrNeutral(hand);
        }

        public async Task<DecisionAdvice> AnalyseAsync(
            HeroDecisionSnapshot spot,
            AcceptedHeroAction action,
            LessonKind kind,
            int seed,
            CancellationToken cancellationToken,
            DateTime? deadlineUtc = null)
        {
            if (spot == null || action == null)
                return Cancelled(null, null);

            if (cancellationToken.IsCancellationRequested || (deadlineUtc.HasValue && DateTime.UtcNow >= deadlineUtc.Value))
                return Cancelled(spot, action);

            if (kind == LessonKind.FreeCheckFold)
                return FromSelection(Qualitative(spot, action, kind, FreeCheckLesson()));
            if (kind == LessonKind.UnpricedRaise)
                return FromSelection(Qualitative(spot, action, kind, RaiseLesson()));
            if (kind != LessonKind.TerminalAllInCall && kind != LessonKind.LargeCall
                && kind != LessonKind.PricedCall && kind != LessonKind.PricedFold)
                return FromSelection(Qualitative(spot, action, kind, NeutralLesson()));

            if (spot.SidePotSpot)
                return SidePotAdvice(spot, action);
            if (spot.ActiveOpponents < 1 || spot.IncrementalCall <= 0)
                return SidePotAdvice(spot, action);

            string key = CacheKey(spot, action, seed, kind);
            if (_cache.TryGetValue(key, out DecisionAdvice cached))
                return cached;

            EquityEstimate estimate;
            try
            {
                estimate = await _equity.EstimateAsync(
                    spot,
                    new Random(seed),
                    UniformEquitySource.DefaultIterations,
                    cancellationToken,
                    deadlineUtc);
            }
            catch (OperationCanceledException)
            {
                return Cancelled(spot, action);
            }
            catch (ArgumentException)
            {
                var missing = Blank(spot, action);
                missing.ReasonCode = "equity_unavailable";
                missing.Lesson = "The price estimate wasn't available. The result above still stands.";
                return missing;
            }

            if (estimate == null || double.IsNaN(estimate.Share) || double.IsInfinity(estimate.Share))
            {
                var missing = Blank(spot, action);
                missing.ReasonCode = "equity_unavailable";
                missing.Lesson = "The price estimate wasn't available. The result above is still the one to trust.";
                missing.EquityShare = null;
                return missing;
            }

            double? breakEven = PotPrice.BreakEvenEquity(spot.PotBefore, spot.IncrementalCall);
            double? ev = breakEven.HasValue ? PotPrice.CallEv(estimate.Share, spot.PotBefore, spot.IncrementalCall) : null;
            int bePct = breakEven.HasValue ? (int)Math.Round(breakEven.Value * 100) : 0;
            int eqPct = (int)Math.Round(estimate.Share * 100);
            bool close = breakEven.HasValue && Math.Abs(estimate.Share - breakEven.Value) < CoachSchema.CloseEquityBand;
            bool called = action.Action == "call" || (action.Action == "all-in" && IsPricedCall(spot, action));
            bool ahead = breakEven.HasValue && estimate.Share > breakEven.Value + CoachSchema.CloseEquityBand;

            var advice = Blank(spot, action);
            advice.EquityShare = estimate.Share;
            advice.BreakEvenEquity = breakEven;
            advice.CallEv = ev;
            advice.SampleCount = estimate.Samples;
            advice.Method = estimate.Method;
            advice.CloseDecision = close;
            advice.PriceLine = breakEven.HasValue
                ? "You needed " + bePct + "% equity to break even. Our estimate was " + eqPct + "%, assuming no further betting."
                : null;
            advice.Caveat = spot.PlayersBehind > 0
                ? "Someone could still bet after you. The price ignores that."
                : "Uniform random hands are a rough baseline, not a tight player's raising range.";
            advice.Detail = advice.PriceLine
                + " Method: " + estimate.Method
                + ". Samples: " + estimate.Samples + "."
                + (estimate.Exact ? "" : " This is a bounded sample, not every possible hand.")
                + " " + advice.Caveat;

            string where = StreetPhrase(spot.Street);
            if (close)
            {
                advice.ReasonCode = "close_price";
                advice.Lesson = "The price " + where + " was close (" + bePct + "% to break even, about " + eqPct
                    + "% estimated). Either call or fold can be fine.";
            }
            else if (called && ahead)
            {
                advice.ReasonCode = "favourable_call";
                advice.Lesson = "Calling " + where + " had a favourable price (" + bePct + "% to break even, about " + eqPct
                    + "% estimated). One hand's result doesn't make it a bad call.";
            }
            else if (called && !ahead)
            {
                advice.ReasonCode = "short_price";
                advice.Lesson = "You needed " + bePct + "% equity to break even. Our estimate was " + eqPct + "%, assuming no further betting.";
            }
            else if (!called && ahead)
            {
                advice.ReasonCode = "folded_favourable_price";
                advice.Lesson = "Folding " + where + " gave up a price that looked favourable (" + bePct
                    + "% needed, about " + eqPct + "% estimated).";
            }
            else
            {
                advice.ReasonCode = "folded_short_price";
                advice.Lesson = "Folding " + where + " matched the price (" + bePct + "% needed, about " + eqPct
                    + "% estimated). Winning the pot was not required.";
            }

            if (spot.CanCheck)
            {
                advice.ReasonCode = "free_check_available";
                advice.Lesson = FreeCheckLesson();
            }

            _cache[key] = advice;
            return advice;
        }

        /// <summary>
        /// A board note is something to try. Skip it when the hero folded
        /// before the flop — they never saw the suit land.
        /// </summary>
        public static string ActionableBoardLesson(CompletedHandRecord hand)
        {
            if (FoldedBeforeFlop(hand)) return null;
            return BoardLesson(hand);
        }

        public static string BoardLesson(CompletedHandRecord hand)
        {
            if (hand?.FinalBoard == null) return null;
            int best = 0;
            Suit suit = Suit.Hearts;
            var counts = new int[4];
            foreach (Card card in hand.FinalBoard)
                counts[(int)card.Suit]++;
            for (int i = 0; i < 4; i++)
            {
                if (counts[i] > best)
                {
                    best = counts[i];
                    suit = (Suit)i;
                }
            }
            if (best >= 3)
                return "When the third " + SuitWord(suit) + " lands, pause before calling a big bet.";
            return null;
        }

        public static string NeutralLesson() =>
            "No clear mistake in the spots we could price. One thing to try: name the price before you call.";

        public static string FreeCheckLesson() =>
            "You folded when checking was free. A free check keeps the pot without paying more.";

        public static string RaiseLesson() =>
            "You raised. Showdown equity alone doesn't say a raise was better — that needs fold equity and a size.";

        private static bool FoldedBeforeFlop(CompletedHandRecord hand)
        {
            if (hand == null || !hand.HeroFolded) return false;
            if (hand.Decisions == null || hand.Decisions.Count == 0) return true;
            CoachStreet last = CoachStreet.Preflop;
            foreach (HeroDecisionSnapshot spot in hand.Decisions)
                if (spot != null && spot.Street > last) last = spot.Street;
            return last == CoachStreet.Preflop;
        }

        private static LessonSelection BoardOrNeutral(CompletedHandRecord hand)
        {
            string board = FoldedBeforeFlop(hand) ? null : BoardLesson(hand);
            if (!string.IsNullOrEmpty(board))
            {
                return new LessonSelection
                {
                    Kind = LessonKind.BoardAwareness,
                    Lesson = board
                };
            }
            if (FoldedBeforeFlop(hand))
            {
                return new LessonSelection
                {
                    Kind = LessonKind.Neutral,
                    Lesson = "You folded before the flop. A routine fold does not need a manufactured mistake."
                };
            }
            return new LessonSelection { Kind = LessonKind.Neutral, Lesson = NeutralLesson() };
        }

        private static bool BoardHasFlushTexture(CompletedHandRecord hand) => BoardLesson(hand) != null;

        private static bool IsFreeCheckFold(HeroDecisionSnapshot spot, AcceptedHeroAction action) =>
            action.Action == "fold" && spot.CanCheck;

        private static bool IsPricedFold(HeroDecisionSnapshot spot, AcceptedHeroAction action) =>
            action.Action == "fold" && spot.IncrementalCall > 0 && !spot.CanCheck;

        private static bool IsUnpricedRaise(AcceptedHeroAction action) =>
            action.Action == "bet" || action.Action == "raise" ||
            (action.Action == "all-in" && action.RaiseTo > 0 && action.AddedAmount > 0);

        private static bool IsPricedCall(HeroDecisionSnapshot spot, AcceptedHeroAction action)
        {
            if (spot.IncrementalCall <= 0) return false;
            if (action.Action == "call") return true;
            if (action.Action != "all-in") return false;
            // A shove for more than the call is a raise. A short stack calling off is a call.
            return spot.HeroStack <= spot.IncrementalCall;
        }

        private static bool IsTerminal(HeroDecisionSnapshot spot, AcceptedHeroAction action)
        {
            if (!IsPricedCall(spot, action) || spot.SidePotSpot) return false;
            if (spot.PlayersBehind > 0) return false;
            long threshold = Math.Max(1, spot.BigBlind) * CoachSchema.TerminalCallBigBlinds;
            bool callingOff = action.Action == "all-in" || action.AddedAmount >= spot.HeroStack;
            return callingOff && spot.IncrementalCall >= threshold;
        }

        private static bool IsLarge(HeroDecisionSnapshot spot, AcceptedHeroAction action)
        {
            if (!IsPricedCall(spot, action) || spot.SidePotSpot) return false;
            long threshold = Math.Max(1, spot.BigBlind) * CoachSchema.LargeCallBigBlinds;
            return spot.IncrementalCall >= threshold;
        }

        private static LessonSelection Qualitative(HeroDecisionSnapshot spot, AcceptedHeroAction action, LessonKind kind, string lesson)
        {
            return new LessonSelection
            {
                Kind = kind,
                Spot = spot,
                Action = action,
                Lesson = lesson
            };
        }

        private static DecisionAdvice FromSelection(LessonSelection selection)
        {
            var advice = Blank(selection.Spot, selection.Action);
            advice.Lesson = selection.Lesson;
            advice.ReasonCode = selection.Kind.ToString();
            advice.EquityShare = null;
            advice.BreakEvenEquity = null;
            advice.CallEv = null;
            if (selection.Kind == LessonKind.FreeCheckFold)
                advice.Detail = "With the information you had then, checking was legal and free. Folding was not required.";
            else if (selection.Kind == LessonKind.UnpricedRaise)
                advice.Detail = "With the information you had then, this review does not price the raise. No fold equity was estimated.";
            return advice;
        }

        private static DecisionAdvice SidePotAdvice(HeroDecisionSnapshot spot, AcceptedHeroAction action)
        {
            var advice = Blank(spot, action);
            if (spot.ActiveOpponents < 1 || spot.IncrementalCall <= 0)
            {
                advice.ReasonCode = spot.ActiveOpponents < 1 ? "no_opponent" : "no_price";
                advice.Lesson = spot.ActiveOpponents < 1
                    ? "No one was left to call. There is no price to compare."
                    : "No call price to compare on this action.";
                advice.Detail = advice.Lesson;
                advice.EquityShare = null;
                advice.BreakEvenEquity = null;
                advice.CallEv = null;
                return advice;
            }

            SidePotCallPrice.Split split = SidePotCallPrice.Analyse(spot);
            advice.ReasonCode = "side_pot";
            if (split == null)
            {
                advice.Lesson =
                    "One thing to try: a shorter stack is all-in — name the main-pot price and any side-pot price before you call.";
                advice.Detail = advice.Lesson + " Full pot odds mix those slices when stacks differ.";
            }
            else
            {
                advice.Lesson = SidePotLesson(split, spot.IncrementalCall);
                advice.PriceLine = SidePotPriceLine(split);
                advice.Detail = SidePotDetail(split);
            }

            advice.Caveat = "Side-pot spots price each slice of your call, not one blended pot odds number.";
            advice.EquityShare = null;
            advice.BreakEvenEquity = null;
            advice.CallEv = null;
            return advice;
        }

        private static string SidePotLesson(SidePotCallPrice.Split split, long call)
        {
            if (split.CallToSidePot <= 0)
            {
                int mainPct = split.MainBreakEven.HasValue ? (int)System.Math.Round(split.MainBreakEven.Value * 100) : 0;
                return "One thing to try: a shorter stack is all-in. Your " + call + "-chip call only contests the "
                    + split.MainPotAmount + "-chip main pot"
                    + (split.MainBreakEven.HasValue ? " — about " + mainPct + "% equity to break even there" : "")
                    + ", with no new chips in a side pot.";
            }

            int main = split.MainBreakEven.HasValue ? (int)System.Math.Round(split.MainBreakEven.Value * 100) : 0;
            int side = split.SideBreakEven.HasValue ? (int)System.Math.Round(split.SideBreakEven.Value * 100) : 0;
            var lesson = "One thing to try: split the price. " + split.CallToMainPot + " of your " + call
                + "-chip call goes to the main pot (" + split.MainPotAmount + " chips)";
            if (split.MainBreakEven.HasValue)
                lesson += " — about " + main + "% equity to break even on that slice";
            lesson += ". The other " + split.CallToSidePot + " chips join a side pot";
            if (split.SidePotAmount > 0)
                lesson += " (" + split.SidePotAmount + " chips)";
            if (split.SideBreakEven.HasValue)
                lesson += " — about " + side + "% needed on that part";
            lesson += ". Decide each slice on its own.";
            return lesson;
        }

        private static string SidePotPriceLine(SidePotCallPrice.Split split)
        {
            if (split.CallToSidePot <= 0)
                return "Main pot: " + split.MainPotAmount + " chips. Your call adds " + split.CallToMainPot
                    + " chips there.";
            return "Main pot: " + split.MainPotAmount + " chips (" + split.CallToMainPot + " from this call). Side pot: "
                + split.SidePotAmount + " chips (" + split.CallToSidePot + " from this call).";
        }

        private static string SidePotDetail(SidePotCallPrice.Split split)
        {
            string main = split.MainBreakEven.HasValue
                ? "Main slice break-even: " + (int)System.Math.Round(split.MainBreakEven.Value * 100) + "%."
                : "Main slice break-even was not computed.";
            if (split.CallToSidePot <= 0)
                return main + " No side-pot chips are added by this call.";
            string side = split.SideBreakEven.HasValue
                ? " Side slice break-even: " + (int)System.Math.Round(split.SideBreakEven.Value * 100) + "%."
                : "";
            return main + side + " Showdown equity is not blended across pots.";
        }

        private static DecisionAdvice Cancelled(HeroDecisionSnapshot spot, AcceptedHeroAction action)
        {
            var advice = Blank(spot, action);
            advice.Cancelled = true;
            advice.ReasonCode = "cancelled";
            advice.Lesson = "The price estimate didn't finish. The result above still stands.";
            advice.EquityShare = null;
            advice.BreakEvenEquity = null;
            advice.CallEv = null;
            return advice;
        }

        private static DecisionAdvice Blank(HeroDecisionSnapshot spot, AcceptedHeroAction action)
        {
            return new DecisionAdvice
            {
                DecisionId = spot?.DecisionId ?? 0,
                Street = spot == null ? "" : spot.Street.ToString(),
                ActionTaken = action?.Action ?? "",
                RecordedAction = Describe(spot, action),
                BoardText = Cards(spot?.VisibleBoard),
                Caveat = "With the information you had then."
            };
        }

        private static string Describe(HeroDecisionSnapshot spot, AcceptedHeroAction action)
        {
            if (spot == null || action == null) return "";
            string street = spot.Street.ToString().ToLowerInvariant();
            if (action.Action == "call")
                return "On the " + street + " you called " + spot.IncrementalCall + ".";
            if (action.Action == "all-in")
                return "On the " + street + " you moved all-in for " + action.AddedAmount + ".";
            if (action.Action == "bet" || action.Action == "raise")
                return "On the " + street + " you " + action.Action + "d to " + action.RaiseTo + ".";
            return "On the " + street + " you " + action.Action + ".";
        }

        private static string Cards(IReadOnlyList<Card> cards)
        {
            if (cards == null || cards.Count == 0) return "No board yet.";
            var parts = new List<string>();
            foreach (Card card in cards) parts.Add(BestFiveCards.Code(card));
            return string.Join(" ", parts);
        }

        private static string StreetPhrase(CoachStreet street) => street switch
        {
            CoachStreet.Preflop => "before the flop",
            CoachStreet.Flop => "on the flop",
            CoachStreet.Turn => "on the turn",
            _ => "on the river"
        };

        private static string SuitWord(Suit suit) => suit switch
        {
            Suit.Clubs => "club",
            Suit.Diamonds => "diamond",
            Suit.Hearts => "heart",
            Suit.Spades => "spade",
            _ => "suit"
        };

        private static string CacheKey(HeroDecisionSnapshot spot, AcceptedHeroAction action, int seed, LessonKind kind)
        {
            var board = new System.Text.StringBuilder();
            if (spot.VisibleBoard != null)
                foreach (Card card in spot.VisibleBoard) board.Append(card.Index).Append(',');
            var hole = new System.Text.StringBuilder();
            if (spot.HeroHoleCards != null)
                foreach (Card card in spot.HeroHoleCards) hole.Append(card.Index).Append(',');
            return spot.HandId + "|" + spot.DecisionId + "|" + action.Action + "|" + seed + "|v" + CoachSchema.Version
                + "|" + kind + "|" + spot.PotBefore + "|" + spot.IncrementalCall + "|" + hole + "|" + board;
        }
    }
}
