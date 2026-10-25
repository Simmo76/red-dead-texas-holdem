using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CinematicPoker.Engine.Coaching;
using CinematicPoker.Engine.Poker;
using NUnit.Framework;
using static CinematicPoker.Engine.Tests.TestHelpers;

namespace CinematicPoker.Engine.Tests
{
    [TestFixture]
    public class HandReviewTests
    {
        [Test]
        public void FixtureA_FlushBeatsTwoPair()
        {
            HandReview review = new HandReviewService().Explain(CoachFixtures.FlushOverTwoPair());

            Assert.AreEqual("WHY THEY WON", review.Heading);
            Assert.AreEqual("Their flush beat your two pair.", review.WhyWon);
            Assert.AreEqual("When the third heart lands, pause before calling a big bet.", review.Lesson);
            CollectionAssert.AreEquivalent(new[] { "Ah", "Kh", "Qh", "Jh", "8h" }, review.HighlightCodes);
            Assert.LessOrEqual(HandReviewService.FirstPanelWords(review), 65);
            Assert.AreEqual(ReviewStatus.Ready, review.Status);
        }

        [Test]
        public async Task FixtureB_PotPriceArithmetic()
        {
            var source = new FixedEquitySource(0.35);
            var coach = new DecisionCoach(source);
            HeroDecisionSnapshot spot = CoachFixtures.PricedCall(150, 50);
            var action = new AcceptedHeroAction { DecisionId = 1, Action = "call", AddedAmount = 50 };
            DecisionAdvice advice = await coach.AnalyseAsync(spot, action, LessonKind.PricedCall, seed: 7, CancellationToken.None);

            Assert.AreEqual(0.25, advice.BreakEvenEquity.Value, 1e-9);
            Assert.AreEqual(20, advice.CallEv.Value, 1e-6);
            Assert.AreEqual(
                "You needed 25% equity to break even. Our estimate was 35%, assuming no further betting.",
                advice.PriceLine);
            Assert.IsFalse(advice.CloseDecision);
            Assert.AreEqual(1, source.Calls);

            DecisionAdvice again = await coach.AnalyseAsync(spot, action, LessonKind.PricedCall, seed: 7, CancellationToken.None);
            Assert.AreEqual(1, source.Calls, "Identical spot, seed and model should be cached.");
            Assert.AreEqual(advice.CallEv, again.CallEv);
        }

        [Test]
        public void FixtureC_Kicker()
        {
            HandReview review = new HandReviewService().Explain(CoachFixtures.Kicker());
            Assert.AreEqual("Their pair of aces, king kicker, beat your queen kicker.", review.WhyWon);
            CollectionAssert.Contains(review.HighlightCodes, "Kc");
            CollectionAssert.Contains(review.HighlightCodes, "Ah");
        }

        [Test]
        public void FixtureD_BoardPlaysSplit()
        {
            HandReview review = new HandReviewService().Explain(CoachFixtures.BoardPlays());
            Assert.AreEqual("WHY IT SPLIT", review.Heading);
            Assert.AreEqual("You split the pot. Both of you play the royal flush on the board.", review.WhyWon);
            Assert.AreEqual("When the third spade lands, pause before calling a big bet.", review.Lesson);
            Assert.That(review.Lesson, Does.Not.Contain("mistake"));
        }

        [Test]
        public void FixtureE_SidePotsBothAwards()
        {
            HandReview review = new HandReviewService().Explain(CoachFixtures.TwoPots());
            Assert.AreEqual("HAND RESULT", review.Heading);
            Assert.That(review.WhyWon, Does.Contain("main pot"));
            Assert.That(review.WhyWon, Does.Contain("side pot"));
            Assert.That(string.Join(" ", review.PotLines), Does.Contain("150"));
            Assert.That(string.Join(" ", review.PotLines), Does.Contain("100"));
            Assert.That(review.ChipDetail, Does.Contain("+100"));
            Assert.IsNull(review.KeyDecision == null ? null : review.KeyDecision.CallEv);
        }

        [Test]
        public void FixtureF_UncalledReturnIsNotProfit()
        {
            HandReview review = new HandReviewService().Explain(CoachFixtures.UncalledReturn());
            Assert.AreEqual("Net +40. 60 was returned uncalled and is not profit.", review.ChipDetail);
            Assert.That(review.ChipDetail, Does.Not.Contain("60 profit"));
            Assert.That(review.WhyWon, Does.Not.Contain("60 profit"));
            Assert.That(string.Join(" ", review.PotLines), Does.Contain("80"));
        }

        [Test]
        public void FoldWithoutShowdownDoesNotInventStrength()
        {
            HandReview review = new HandReviewService().Explain(CoachFixtures.FoldedBlind());
            Assert.AreEqual("WHAT HAPPENED", review.Heading);
            Assert.That(review.WhyWon, Does.Contain("You folded"));
            Assert.That(review.WhyWon, Does.Contain("Sam"));
            Assert.That(review.WhyWon, Does.Contain("without a showdown"));
            Assert.That(review.WhyWon, Does.Contain("unknown"));
            Assert.IsEmpty(review.HighlightCodes);
        }

        [Test]
        public void FoldedHeroStillNamesTheShowdownWinner()
        {
            CompletedHandRecord hand = CoachFixtures.FoldedBlind();
            hand.WonByFolds = false;
            hand.FinalBoard = new List<Card>
            {
                Card.Parse("Ah"), Card.Parse("Kh"), Card.Parse("8h"), Card.Parse("8c"), Card.Parse("2s")
            };
            hand.RevealedHands.Add(new RevealedHand
            {
                Seat = 1,
                HoleCards = new List<Card> { Card.Parse("Qh"), Card.Parse("Jh") }
            });
            hand.Awards[0].ByFold = false;

            HandReview review = new HandReviewService().Explain(hand);

            Assert.AreEqual("WHAT HAPPENED", review.Heading);
            Assert.That(review.WhyWon, Does.Contain("You folded"));
            Assert.That(review.WhyWon, Does.Contain("Sam"));
            Assert.That(review.WhyWon, Does.Contain("flush"));
            Assert.That(review.WhyWon, Does.Not.Contain("saved record"));
            Assert.That(review.Lesson, Does.Not.Contain("third heart"));
            Assert.That(review.Lesson, Does.Contain("folded before the flop"));
        }

        [Test]
        public void HeroWinsBecauseEveryoneFolded()
        {
            CompletedHandRecord hand = CoachFixtures.FoldedBlind();
            hand.HeroFolded = false;
            hand.Awards[0].WinnerSeats = new List<int> { 0 };
            hand.Awards[0].Payouts = new Dictionary<int, long> { { 0, 15 } };
            hand.HeroNet = 10;
            HandReview review = new HandReviewService().Explain(hand);
            Assert.AreEqual("You won because everyone else folded.", review.WhyWon);
        }

        [Test]
        public void FoldWinAfterAMissedDealDoesNotReuseThePreviousShowdown()
        {
            var history = new CoachHistoryBuffer();
            CompletedHandRecord earlier = CoachFixtures.FlushOverTwoPair();
            history.Remember(earlier);
            var recorder = new HandReviewRecorder("resumed-sitting", 0, 5, 10, history);
            var names = new Dictionary<int, string> { { 0, "You" }, { 1, "Tommy Gunn" } };

            PokerGame game = CreateGame(new long[] { 1000, 1000 });
            game.StartHand();
            FoldAround(game);
            game.StartHand();
            game.EventEmitted += evt => recorder.Observe(evt, game.CurrentRound, names);
            Assert.AreEqual(1, game.CurrentSeat, "Heads-up, the button moved, so the opponent folds into the hero.");
            game.SubmitAction(PlayerAction.Fold());

            CompletedHandRecord latest = recorder.LastCompleted;
            Assert.AreNotEqual(earlier.HandId, latest.HandId);
            Assert.AreEqual(game.HandNumber, latest.HandNumber);
            Assert.IsTrue(latest.WonByFolds);
            Assert.Greater(latest.HeroNet, 0);
            Assert.AreEqual(2, latest.HeroHoleCards.Count);
            HandReview review = new HandReviewService().Explain(latest);
            Assert.AreEqual("WHY YOU WON", review.Heading);
            Assert.AreEqual("You won because everyone else folded.", review.WhyWon);
            Assert.That(review.ChipDetail, Does.Contain("returned uncalled"));
            Assert.That(review.WhyWon, Does.Not.Contain("pair"));
            Assert.That(review.Lesson, Does.Not.Contain("all-in for less"));
            Assert.AreEqual("WHY THEY WON", new HandReviewService().Explain(earlier).Heading);

            int stored = recorder.History.Hands.Count;
            recorder.Observe(new HandCompleted { WonByFolds = true }, game.CurrentRound, names);
            Assert.AreEqual(stored, recorder.History.Hands.Count);
        }

        [Test]
        public void ResumedHandExplainsTheFoldThatFinishesIt()
        {
            var history = new CoachHistoryBuffer();
            history.Remember(CoachFixtures.Kicker());
            var recorder = new HandReviewRecorder("resumed-sitting", 0, 5, 10, history);
            var names = new Dictionary<int, string> { { 0, "You" }, { 1, "Tommy Gunn" } };

            PokerGame game = CreateGame(new long[] { 1000, 1000 });
            game.StartHand();
            FoldAround(game);
            game.StartHand();
            recorder.ResumeHand(game.HandNumber, game.CurrentRound, names);
            game.EventEmitted += evt => recorder.Observe(evt, game.CurrentRound, names);
            game.SubmitAction(PlayerAction.Fold());

            HandReview review = new HandReviewService().Explain(recorder.LastCompleted);
            Assert.AreEqual("You won because everyone else folded.", review.WhyWon);
            Assert.AreEqual(game.HandNumber, recorder.LastCompleted.HandNumber);
            Assert.AreEqual("resumed-sitting", recorder.LastCompleted.SessionId);
            Assert.AreEqual(2, recorder.History.Hands.Count(h => h.Complete));
        }

        [Test]
        public void ObservedFoldWinStillSaysEveryoneFolded()
        {
            var recorder = new HandReviewRecorder("live", 0, 5, 10);
            var names = new Dictionary<int, string> { { 0, "You" }, { 1, "Sam" } };
            PokerGame game = CreateGame(new long[] { 1000, 1000 });
            game.EventEmitted += evt => recorder.Observe(evt, game.CurrentRound, names);
            game.StartHand();
            FoldAround(game);
            game.StartHand();
            game.SubmitAction(PlayerAction.Fold());

            Assert.AreEqual(2, recorder.History.Hands.Count);
            HandReview review = new HandReviewService().Explain(recorder.LastCompleted);
            Assert.AreEqual("You won because everyone else folded.", review.WhyWon);
            Assert.AreEqual(1, recorder.LastCompleted.Awards.Count);
            Assert.IsTrue(recorder.LastCompleted.Awards[0].ByFold);
            Assert.IsTrue(recorder.LastCompleted.Awards[0].WinnerSeats.Contains(0));
        }

        [Test]
        public void MissedDealStillNamesTheShowdownWinner()
        {
            var names = new Dictionary<int, string> { { 0, "You" }, { 1, "Opponent" } };
            string expected = ExplainShowdown(names);

            var history = new CoachHistoryBuffer();
            history.Remember(CoachFixtures.FlushOverTwoPair());
            var deck = StackedDeck.Parse("As", "Qh", "Kd", "Jh", "Ah", "Kh", "8h", "8c", "2s");
            PokerGame game = CreateGame(new long[] { 1000, 1000 }, deck);
            game.StartHand();
            var recorder = new HandReviewRecorder("late", 0, 5, 10, history);
            game.EventEmitted += evt => recorder.Observe(evt, game.CurrentRound, names);
            CheckCallDown(game);

            HandReview review = new HandReviewService().Explain(recorder.LastCompleted);
            Assert.AreEqual(expected, review.WhyWon);
            Assert.AreNotEqual("fixture-a", recorder.LastCompleted.HandId);
            Assert.That(review.WhyWon, Does.Contain("flush"));
        }

        [Test]
        public void PlayedFlushUsesEvaluatorAndLedger()
        {
            var deck = StackedDeck.Parse("As", "Qh", "Kd", "Jh", "Ah", "Kh", "8h", "8c", "2s");
            PokerGame game = CreateGame(new long[] { 1000, 1000 }, deck);
            var recorder = new HandReviewRecorder("played", heroSeat: 0, smallBlind: 5, bigBlind: 10);
            var names = new Dictionary<int, string> { { 0, "You" }, { 1, "Opponent" } };
            game.EventEmitted += evt => recorder.Observe(evt, game.CurrentRound, names);
            game.StartHand();
            CheckCallDown(game);
            int deckAfterHand = deck.Remaining;

            CompletedHandRecord record = recorder.LastCompleted;
            Assert.IsNotNull(record);
            Assert.IsTrue(record.Complete);
            Assert.AreEqual(record.HeroNet, record.StackChanges[0]);
            HandReview review = new HandReviewService().Explain(record);
            Assert.AreEqual("Their flush beat your two pair.", review.WhyWon);
            Assert.AreEqual(deckAfterHand, deck.Remaining, "Explaining the hand must not draw cards.");
            long chips = game.Players.Sum(p => p.Stack);
            new HandReviewService().Explain(record);
            Assert.AreEqual(chips, game.Players.Sum(p => p.Stack));

            int stored = recorder.History.Hands.Count;
            recorder.Observe(new HandCompleted { WonByFolds = false, StackChanges = record.StackChanges }, game.CurrentRound, names);
            Assert.AreEqual(stored, recorder.History.Hands.Count);
        }

        [Test]
        public void HiddenOpponentCardsNeverReachTheRecord()
        {
            var deck = StackedDeck.Parse("7c", "Qh", "2d", "Jh");
            PokerGame game = CreateGame(new long[] { 1000, 1000 }, deck);
            var recorder = new HandReviewRecorder("hidden", 0, 5, 10);
            game.EventEmitted += evt => recorder.Observe(evt, game.CurrentRound, new Dictionary<int, string> { { 1, "Sam" } });
            game.StartHand();
            Assert.AreEqual(0, game.CurrentSeat);
            HeroDecisionSnapshot offer = recorder.CaptureOffer(game.CurrentRound, game.GetLegalActions());
            game.SubmitAction(PlayerAction.Fold());
            recorder.Accept(offer, PlayerAction.Fold());

            CompletedHandRecord record = recorder.LastCompleted;
            Assert.IsTrue(record.HeroFolded);
            Assert.IsEmpty(record.RevealedHands);
            Assert.IsFalse(ContainsCard(record, "Qh"));
            Assert.IsFalse(ContainsCard(record, "Jh"));
            Assert.IsTrue(ContainsCard(record, "7c"));
            HandReview review = new HandReviewService().Explain(record);
            Assert.That(review.WhyWon, Does.Contain("without a showdown"));
        }

        [Test]
        public void SnapshotIgnoresLaterMutationAndHiddenCards()
        {
            var deck = StackedDeck.Parse("As", "Qh", "Kd", "Jh", "Ah", "Kh", "8h");
            PokerGame game = CreateGame(new[] { 1000L, 1000L }, deck);
            var recorder = new HandReviewRecorder("mut", 0, 5, 10);
            game.EventEmitted += evt => recorder.Observe(evt, game.CurrentRound, null);
            game.StartHand();
            while (game.Phase == GamePhase.HandInProgress && game.CurrentSeat != 0)
                game.SubmitAction(game.GetLegalActions().CanCheck ? PlayerAction.Check() : PlayerAction.Call());

            HeroDecisionSnapshot snap = recorder.CaptureOffer(game.CurrentRound, game.GetLegalActions());
            int boardCount = snap.VisibleBoard.Count;
            int holeCount = snap.HeroHoleCards.Count;
            ((List<Card>)game.CurrentRound.Board).Add(Card.Parse("2c"));
            game.CurrentRound.GetSeat(0).HoleCards.Clear();
            game.CurrentRound.GetSeat(1).HoleCards.Add(Card.Parse("9d"));

            Assert.AreEqual(boardCount, snap.VisibleBoard.Count);
            Assert.AreEqual(holeCount, snap.HeroHoleCards.Count);
            Assert.IsFalse(snap.HeroHoleCards.Any(c => c == Card.Parse("Qh") || c == Card.Parse("Jh") || c == Card.Parse("9d")));
            Assert.IsFalse(snap.VisibleBoard.Any(c => c == Card.Parse("Qh") || c == Card.Parse("Jh")));
        }

        [Test]
        public async Task DecisionAdviceIgnoresFutureBoardAndHiddenCards()
        {
            HeroDecisionSnapshot spot = CoachFixtures.PricedCall(150, 50);
            var action = new AcceptedHeroAction { DecisionId = 1, Action = "call", AddedAmount = 50 };
            var coach = new DecisionCoach(new UniformEquitySource());
            DecisionAdvice first = await coach.AnalyseAsync(spot, action, LessonKind.PricedCall, 42, CancellationToken.None);
            var futureBoard = new List<Card>(spot.VisibleBoard) { Card.Parse("3c") };
            futureBoard.Clear();
            spot.VisibleBoard.Add(Card.Parse("3d"));
            // The cached public snapshot is what was analysed. A second analysis of the
            // original cards (restored) must match. Hidden cards are not inputs.
            spot.VisibleBoard.RemoveAt(spot.VisibleBoard.Count - 1);
            DecisionAdvice second = await coach.AnalyseAsync(spot, action, LessonKind.PricedCall, 42, CancellationToken.None);
            Assert.AreEqual(first.EquityShare, second.EquityShare);
            Assert.IsNotNull(first.EquityShare);
            Assert.Greater(first.EquityShare.Value, 0);
            Assert.Less(first.EquityShare.Value, 1);
            Assert.That(first.Method, Does.Contain("Exact").Or.Contain("uniform").IgnoreCase);
        }

        [Test]
        public async Task MonteCarloIsStableForTheSameSeed()
        {
            var spot = CoachFixtures.PricedCall(80, 20);
            spot.Street = CoachStreet.Flop;
            spot.VisibleBoard = new List<Card> { Card.Parse("Ah"), Card.Parse("7d"), Card.Parse("2c") };
            var action = new AcceptedHeroAction { DecisionId = 1, Action = "call", AddedAmount = 20 };
            var coach = new DecisionCoach(new UniformEquitySource());
            DecisionAdvice a = await coach.AnalyseAsync(spot, action, LessonKind.PricedCall, 99, CancellationToken.None);
            DecisionAdvice b = await coach.AnalyseAsync(spot, action, LessonKind.PricedCall, 99, CancellationToken.None);
            Assert.AreEqual(a.EquityShare, b.EquityShare);
            Assert.That(a.Method, Does.Contain("Monte Carlo"));
        }

        [Test]
        public async Task SidePotOmitsEv()
        {
            var source = new FixedEquitySource(0.9);
            HeroDecisionSnapshot spot = CoachFixtures.PricedCall(200, 80);
            spot.SidePotSpot = true;
            var action = new AcceptedHeroAction { DecisionId = 1, Action = "call", AddedAmount = 80 };
            DecisionAdvice advice = await new DecisionCoach(source).AnalyseAsync(spot, action, LessonKind.PricedCall, 1, CancellationToken.None);
            Assert.IsNull(advice.CallEv);
            Assert.IsNull(advice.BreakEvenEquity);
            Assert.IsNull(advice.EquityShare);
            Assert.AreEqual(0, source.Calls);
            Assert.That(advice.Lesson, Does.Contain("side-pot"));
        }

        [Test]
        public async Task FreeCheckIsNotToldToFold()
        {
            var source = new FixedEquitySource(0.1);
            var spot = CoachFixtures.PricedCall(40, 0);
            spot.IncrementalCall = 0;
            spot.CanCheck = true;
            spot.CanFold = true;
            spot.CanCall = false;
            var action = new AcceptedHeroAction { DecisionId = 1, Action = "fold" };
            var hand = CoachFixtures.FoldedBlind();
            hand.Decisions.Add(spot);
            hand.HeroActions.Add(action);
            hand.FinalBoard.Clear();
            HandReview review = await new HandReviewService(new DecisionCoach(source)).ReviewAsync(hand, 1, CancellationToken.None);
            Assert.That(review.Lesson, Does.Contain("checking was free").IgnoreCase);
            Assert.That(review.Lesson, Does.Not.Contain("you should fold").IgnoreCase);
            Assert.AreEqual(0, source.Calls);
            Assert.IsNull(review.KeyDecision.EquityShare);
        }

        [Test]
        public void ShortCallUsesCappedAmount()
        {
            double? threshold = PotPrice.BreakEvenEquity(150, 40);
            Assert.AreEqual(40d / 190d, threshold.Value, 1e-9);
            Assert.IsNull(PotPrice.BreakEvenEquity(150, 0));
            Assert.IsNull(PotPrice.CallEv(0.5, 150, 0));
        }

        [Test]
        public async Task CancelledAnalysisDoesNotInventZero()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();
            HeroDecisionSnapshot spot = CoachFixtures.PricedCall(150, 50);
            var action = new AcceptedHeroAction { DecisionId = 1, Action = "call", AddedAmount = 50 };
            DecisionAdvice advice = await new DecisionCoach(new FixedEquitySource(0.35))
                .AnalyseAsync(spot, action, LessonKind.PricedCall, 1, cts.Token);
            Assert.IsTrue(advice.Cancelled);
            Assert.IsNull(advice.EquityShare);
            Assert.IsNull(advice.CallEv);
            Assert.AreEqual("cancelled", advice.ReasonCode);

            advice = await new DecisionCoach(new FixedEquitySource(0.35))
                .AnalyseAsync(spot, action, LessonKind.PricedCall, 1, CancellationToken.None, DateTime.UtcNow.AddMinutes(-1));
            Assert.IsNull(advice.EquityShare);
            Assert.AreEqual("cancelled", advice.ReasonCode);
        }

        [Test]
        public async Task RaiseIsNotPricedFromShowdownEquity()
        {
            var source = new FixedEquitySource(0.8);
            var spot = CoachFixtures.PricedCall(100, 0);
            spot.IncrementalCall = 0;
            spot.CanBet = true;
            spot.CanCheck = true;
            var action = new AcceptedHeroAction { DecisionId = 1, Action = "bet", AddedAmount = 30, RaiseTo = 30 };
            var hand = CoachFixtures.FlushOverTwoPair();
            hand.FinalBoard = new List<Card> { Card.Parse("As"), Card.Parse("Kd"), Card.Parse("9c") };
            hand.Decisions.Add(spot);
            hand.HeroActions.Add(action);
            HandReview review = await new HandReviewService(new DecisionCoach(source)).ReviewAsync(hand, 3, CancellationToken.None);
            Assert.AreEqual(0, source.Calls);
            Assert.That(review.Lesson, Does.Contain("fold equity").Or.Contain("raise"));
            Assert.IsNull(review.KeyDecision.CallEv);
        }

        [Test]
        public void HistoryKeepsFiftyAndIgnoresDuplicates()
        {
            var buffer = new CoachHistoryBuffer(50);
            for (int i = 0; i < 60; i++)
            {
                buffer.Remember(new CompletedHandRecord
                {
                    HandId = "h" + i,
                    Complete = true,
                    HeroNet = i
                });
            }
            Assert.AreEqual(50, buffer.Hands.Count);
            Assert.AreEqual("h10", buffer.Hands[0].HandId);
            buffer.Remember(buffer.Latest);
            Assert.AreEqual(50, buffer.Hands.Count);
            buffer.Remember(new CompletedHandRecord { HandId = "nope", Complete = false });
            Assert.AreEqual(50, buffer.Hands.Count);
        }

        [Test]
        public void CloseAdvancesAtMostOnceAndOnlyBetweenHands()
        {
            var flow = new CoachFlowController();
            int id = flow.Open(betweenHands: true, handId: "s:1");
            Assert.IsTrue(flow.IsCurrent(id));
            Assert.IsTrue(flow.Close(nextHand: true, stillBetweenHands: true, currentHandId: "s:1"));
            Assert.IsFalse(flow.Close(nextHand: true, stillBetweenHands: true, currentHandId: "s:1"));

            flow.Open(true, "s:2");
            Assert.IsFalse(flow.Close(nextHand: true, stillBetweenHands: false, currentHandId: "s:2"));

            int stale = flow.Open(true, "s:3");
            flow.Invalidate();
            Assert.IsFalse(flow.IsCurrent(stale));
            Assert.IsFalse(flow.Close(true, true, "s:3"));
        }

        [Test]
        public void RecordRoundTripsWithoutDeckOrHiddenFields()
        {
            CompletedHandRecord hand = CoachFixtures.Kicker();
            var options = new JsonSerializerOptions();
            options.Converters.Add(new CardJsonConverter());
            string json = JsonSerializer.Serialize(hand, options);
            Assert.That(json, Does.Not.Contain("seed").IgnoreCase);
            CompletedHandRecord back = JsonSerializer.Deserialize<CompletedHandRecord>(json, options);
            Assert.AreEqual(hand.HandId, back.HandId);
            Assert.AreEqual(hand.HeroHoleCards[0], back.HeroHoleCards[0]);
            Assert.AreEqual(hand.FinalBoard.Count, back.FinalBoard.Count);
            Assert.AreEqual(hand.HeroNet, back.HeroNet);
        }

        [Test]
        public void WheelIsNamedFromTheEvaluator()
        {
            var hand = CoachFixtures.Kicker();
            hand.HandId = "wheel";
            hand.FinalBoard = new List<Card>
            {
                Card.Parse("5c"), Card.Parse("4d"), Card.Parse("3h"), Card.Parse("2s"), Card.Parse("9c")
            };
            hand.HeroHoleCards = new List<Card> { Card.Parse("Ac"), Card.Parse("Kd") };
            hand.RevealedHands = new List<RevealedHand>
            {
                new RevealedHand { Seat = 0, HoleCards = new List<Card> { Card.Parse("Ac"), Card.Parse("Kd") } },
                new RevealedHand { Seat = 1, HoleCards = new List<Card> { Card.Parse("7h"), Card.Parse("6h") } }
            };
            hand.Awards[0].WinnerSeats = new List<int> { 1 };
            hand.Awards[0].Payouts = new Dictionary<int, long> { { 1, 80 } };
            HandReview review = new HandReviewService().Explain(hand);
            Assert.That(review.WhyWon, Does.Contain("straight"));
        }

        [Test]
        public void IncompleteRecordDoesNotGuess()
        {
            var hand = new CompletedHandRecord { HandId = "partial", Complete = false };
            HandReview review = new HandReviewService().Explain(hand);
            Assert.AreEqual(ReviewStatus.Partial, review.Status);
            Assert.IsEmpty(review.HighlightCodes);
            Assert.That(review.WhyWon, Does.Contain("part of this hand"));
        }

        [Test]
        public void AnalyticsEventsCarryNoCards()
        {
            var evt = CoachAnalytics.Opened("local", "sess", 1, "sess:4", "request", true);
            Assert.AreEqual("review_opened", evt.Name);
            string blob = JsonSerializer.Serialize(evt.Parameters);
            Assert.That(blob, Does.Not.Contain("Ah"));
            Assert.That(blob, Does.Not.Contain("hole"));
            Assert.IsTrue(evt.Parameters.ContainsKey("hand_id"));
            Assert.IsFalse(evt.Parameters.ContainsKey("player_name"));
        }

        private sealed class CardJsonConverter : JsonConverter<Card>
        {
            public override Card Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                return Card.Parse(reader.GetString());
            }

            public override void Write(Utf8JsonWriter writer, Card value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(BestFiveCards.Code(value));
            }
        }

        private static string ExplainShowdown(Dictionary<int, string> names)
        {
            var deck = StackedDeck.Parse("As", "Qh", "Kd", "Jh", "Ah", "Kh", "8h", "8c", "2s");
            PokerGame game = CreateGame(new long[] { 1000, 1000 }, deck);
            var recorder = new HandReviewRecorder("full", 0, 5, 10);
            game.EventEmitted += evt => recorder.Observe(evt, game.CurrentRound, names);
            game.StartHand();
            CheckCallDown(game);
            return new HandReviewService().Explain(recorder.LastCompleted).WhyWon;
        }

        private static bool ContainsCard(CompletedHandRecord record, string code)
        {
            Card card = Card.Parse(code);
            if (record.HeroHoleCards.Any(c => c == card)) return true;
            if (record.FinalBoard.Any(c => c == card)) return true;
            foreach (RevealedHand shown in record.RevealedHands)
                if (shown.HoleCards.Any(c => c == card)) return true;
            foreach (HeroDecisionSnapshot snap in record.Decisions)
            {
                if (snap.HeroHoleCards.Any(c => c == card)) return true;
                if (snap.VisibleBoard.Any(c => c == card)) return true;
            }
            return false;
        }
    }
}
