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
            Assert.AreEqual("WHY YOU WON", review.Heading);
            Assert.AreEqual("Your pair beat their high card.", review.WhyWon);
            Assert.That(review.WhyWon, Does.Not.Contain("split"));
        }

        [Test]
        public void PairWinOverHighCardIsNotDescribedAsASplit()
        {
            HandReview review = new HandReviewService().Explain(CoachFixtures.PairBeatsHighCard());
            Assert.AreEqual("WHY YOU WON", review.Heading);
            Assert.AreEqual("Your pair beat their high card.", review.WhyWon);
            Assert.That(review.WhyWon, Does.Not.Contain("split"));
            Assert.That(review.ChipDetail, Does.Contain("+300"));
        }

        [Test]
        public void FullHouseWinOverBoardTwoPairIsNotDescribedAsASplit()
        {
            HandReview review = new HandReviewService().Explain(CoachFixtures.FullHouseBeatsTwoPair());
            Assert.AreEqual("WHY YOU WON", review.Heading);
            Assert.AreEqual("Your full house beat their two pair.", review.WhyWon);
            Assert.That(review.WhyWon, Does.Not.Contain("split"));
        }

        [Test]
        public async Task FavourableCallLessonDoesNotAssumeALoss()
        {
            var source = new FixedEquitySource(0.35);
            var coach = new DecisionCoach(source);
            HeroDecisionSnapshot spot = CoachFixtures.PricedCall(150, 50);
            var action = new AcceptedHeroAction { DecisionId = 1, Action = "call", AddedAmount = 50 };
            DecisionAdvice advice = await coach.AnalyseAsync(spot, action, LessonKind.PricedCall, seed: 7, CancellationToken.None);
            Assert.AreEqual("favourable_call", advice.ReasonCode);
            Assert.That(advice.Lesson, Does.Contain("favourable price"));
            Assert.That(advice.Lesson, Does.Not.Contain("Losing"));
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
        public void ResumedHandNeverReviewsAnOlderSessionsHand()
        {
            var history = new CoachHistoryBuffer();
            CompletedHandRecord old = CoachFixtures.FoldedBlind();
            old.SessionId = "earlier-session";
            history.Remember(old);

            var recorder = new HandReviewRecorder("resumed", 0, 5, 10, history);
            var names = new Dictionary<int, string> { { 0, "You" }, { 1, "Frankie" } };
            recorder.Observe(new HandCompleted { WonByFolds = true, StackChanges = new Dictionary<int, long> { { 1, 200 } } }, null, names);

            Assert.IsNull(recorder.LastCompleted, "A hand resumed mid-hand has no record; the older session's hand must not stand in for it.");
            Assert.IsNull(recorder.LastFinished);
        }

        [Test]
        public void EachFinishedHandStaysReviewable()
        {
            PokerGame game = CreateGame(new long[] { 1000, 1000 });
            var recorder = new HandReviewRecorder("sitting", 0, 5, 10);
            var names = new Dictionary<int, string> { { 0, "You" }, { 1, "Sam" } };
            game.EventEmitted += evt => recorder.Observe(evt, game.CurrentRound, names);

            game.StartHand();
            FoldAround(game);
            Assert.IsNotNull(recorder.LastFinished);
            Assert.AreEqual(1, recorder.LastFinished.HandNumber);
            string firstId = recorder.LastFinished.HandId;

            game.StartHand();
            FoldAround(game);
            Assert.AreEqual(2, recorder.LastFinished.HandNumber);
            Assert.AreNotEqual(firstId, recorder.LastFinished.HandId);
            Assert.AreEqual(recorder.LastFinished.HandId, recorder.LastCompleted.HandId);
        }

        [Test]
        public void FinishedRoundWithoutAStartIsStillThisSitting()
        {
            var history = new CoachHistoryBuffer();
            CompletedHandRecord old = CoachFixtures.FoldedBlind();
            old.SessionId = "earlier-session";
            history.Remember(old);

            PokerGame game = CreateGame(new long[] { 1000, 1000 });
            var played = new List<PokerEvent>();
            game.EventEmitted += played.Add;
            game.StartHand();
            FoldAround(game);

            var recorder = new HandReviewRecorder("resumed", 0, 5, 10, history);
            var names = new Dictionary<int, string> { { 0, "You" }, { 1, "Frankie" } };
            foreach (PokerEvent evt in played)
            {
                if (evt is HandStarted) continue;
                recorder.Observe(evt, game.CurrentRound, names);
            }

            Assert.IsNotNull(recorder.LastFinished);
            Assert.AreEqual("resumed", recorder.LastFinished.SessionId);
            Assert.AreEqual(recorder.LastFinished.HandId, recorder.LastCompleted.HandId);
            Assert.AreNotEqual(old.HandId, recorder.LastCompleted.HandId);
            Assert.IsNotEmpty(recorder.LastFinished.Awards, "Fold wins must record who took the pot.");
            HandReview review = new HandReviewService().Explain(recorder.LastFinished);
            Assert.That(review.WhyWon, Does.Not.Contain("no winner is named"));
        }

        [Test]
        public void StackChangesBackfillNamesFoldWinner()
        {
            var hand = new CompletedHandRecord
            {
                HandId = "backfill",
                Complete = true,
                HeroSeat = 0,
                WonByFolds = true,
                SeatNames = new Dictionary<int, string> { { 1, "Sam Six-Shot" } },
                StackChanges = new Dictionary<int, long> { { 0, -50 }, { 1, 50 } }
            };
            HandReview review = new HandReviewService().Explain(hand);
            Assert.That(review.WhyWon, Does.Contain("Sam Six-Shot"));
            Assert.That(review.WhyWon, Does.Not.Contain("no winner is named"));
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
            HeroDecisionSnapshot spot = new HeroDecisionSnapshot
            {
                HeroSeat = 1,
                HeroCommittedTotal = 50,
                IncrementalCall = 50,
                PotBefore = 150,
                SidePotSpot = true,
                ActiveOpponents = 1,
                Street = CoachStreet.Flop,
                BigBlind = 10,
                Seats = new List<SeatPublicState>
                {
                    new SeatPublicState { Seat = 0, TotalCommitted = 100, Folded = false, AllIn = true },
                    new SeatPublicState { Seat = 1, TotalCommitted = 50, Folded = false }
                }
            };
            var action = new AcceptedHeroAction { DecisionId = 1, Action = "call", AddedAmount = 50 };
            DecisionAdvice advice = await new DecisionCoach(source).AnalyseAsync(spot, action, LessonKind.PricedCall, 1, CancellationToken.None);
            Assert.IsNull(advice.CallEv);
            Assert.IsNull(advice.BreakEvenEquity);
            Assert.IsNull(advice.EquityShare);
            Assert.AreEqual(0, source.Calls);
            Assert.That(advice.Lesson, Does.Contain("One thing to try").IgnoreCase);
            Assert.That(advice.Lesson, Does.Contain("main pot").IgnoreCase);
            Assert.That(advice.PriceLine, Does.Contain("Main pot"));
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
        public void ShowdownNamesHeroFromHoleCardsWhenRevealsOmitThem()
        {
            CompletedHandRecord hand = CoachFixtures.Kicker();
            hand.RevealedHands = new List<RevealedHand>
            {
                new RevealedHand { Seat = 1, HoleCards = new List<Card> { Card.Parse("Ah"), Card.Parse("Kc") } }
            };
            HandReview review = new HandReviewService().Explain(hand);
            Assert.That(review.WhyWon, Does.Not.Contain("saved record"));
            Assert.That(review.WhyWon, Does.Contain("kicker"));
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
        public void AFoldFacingABetOutranksAnEarlierCall()
        {
            CompletedHandRecord hand = FoldedAfterACall();
            LessonSelection selected = DecisionCoach.Select(hand);
            Assert.AreEqual(LessonKind.PricedFold, selected.Kind);
            Assert.AreEqual(2, selected.Spot.DecisionId);
        }

        [Test]
        public async Task FoldedHandWithAFlushBoardDoesNotRepeatTheFavourableCallLine()
        {
            var source = new FixedEquitySource(0.2);
            CompletedHandRecord hand = FoldedAfterACall();
            hand.FinalBoard = new List<Card>
            {
                Card.Parse("Ac"), Card.Parse("9c"), Card.Parse("4d"), Card.Parse("8c"), Card.Parse("2h")
            };
            HandReview review = await new HandReviewService(new DecisionCoach(source))
                .ReviewAsync(hand, 1, CancellationToken.None);

            Assert.AreEqual("fold", review.KeyDecision.ActionTaken);
            Assert.That(review.Lesson, Does.Contain("third club"));
            Assert.That(review.Lesson, Does.Not.Contain("favourable price"));
            Assert.That(review.Lesson, Does.Not.Contain("bad call"));
        }

        [Test]
        public async Task RoutineFavourableCallYieldsToTheBoard()
        {
            var source = new FixedEquitySource(0.5);
            CompletedHandRecord hand = CoachFixtures.Kicker();
            hand.FinalBoard = new List<Card>
            {
                Card.Parse("Ac"), Card.Parse("9c"), Card.Parse("4d"), Card.Parse("8c"), Card.Parse("2h")
            };
            HeroDecisionSnapshot spot = CoachFixtures.PricedCall(100, 20);
            spot.VisibleBoard = hand.FinalBoard;
            hand.Decisions.Add(spot);
            hand.HeroActions.Add(new AcceptedHeroAction { DecisionId = 1, Action = "call", AddedAmount = 20 });

            HandReview review = await new HandReviewService(new DecisionCoach(source))
                .ReviewAsync(hand, 1, CancellationToken.None);

            Assert.AreEqual("call", review.KeyDecision.ActionTaken);
            Assert.That(review.Lesson, Does.Contain("third club"));
            Assert.That(review.Lesson, Does.Not.Contain("favourable price"));
            Assert.That(review.KeyDecision.Lesson, Does.Contain("favourable price"));
        }

        [Test]
        public async Task FoldingAGoodPriceStaysTheLessonOnAFlushBoard()
        {
            var source = new FixedEquitySource(0.8);
            CompletedHandRecord hand = CoachFixtures.FoldedBlind();
            hand.FinalBoard = new List<Card>
            {
                Card.Parse("Ac"), Card.Parse("9c"), Card.Parse("4d"), Card.Parse("8c"), Card.Parse("2h")
            };
            HeroDecisionSnapshot spot = CoachFixtures.PricedCall(100, 20);
            spot.CanCheck = false;
            hand.Decisions.Add(spot);
            hand.HeroActions.Add(new AcceptedHeroAction { DecisionId = 1, Action = "fold" });

            HandReview review = await new HandReviewService(new DecisionCoach(source))
                .ReviewAsync(hand, 1, CancellationToken.None);

            Assert.That(review.Lesson, Does.Contain("Folding"));
            Assert.That(review.Lesson, Does.Contain("favourable"));
            Assert.That(review.Lesson, Does.Not.Contain("third club"));
        }

        [Test]
        public async Task LargeFavourableCallIsNotReplacedByTheBoard()
        {
            var source = new FixedEquitySource(0.6);
            CompletedHandRecord hand = CoachFixtures.Kicker();
            hand.FinalBoard = new List<Card>
            {
                Card.Parse("Ah"), Card.Parse("Kh"), Card.Parse("8h"), Card.Parse("2c"), Card.Parse("9d")
            };
            HeroDecisionSnapshot spot = CoachFixtures.PricedCall(100, 80);
            spot.BigBlind = 10;
            spot.HeroStack = 1000;
            hand.Decisions.Add(spot);
            hand.HeroActions.Add(new AcceptedHeroAction { DecisionId = 1, Action = "call", AddedAmount = 80 });

            HandReview review = await new HandReviewService(new DecisionCoach(source))
                .ReviewAsync(hand, 1, CancellationToken.None);

            Assert.AreEqual(LessonKind.LargeCall, review.LessonKind);
            Assert.That(review.Lesson, Does.Contain("favourable price"));
            Assert.That(review.Lesson, Does.Not.Contain("third heart"));
        }

        [Test]
        public async Task RepeatedFinePriceDoesNotReuseTheSameTry()
        {
            var source = new FixedEquitySource(0.1);
            var service = new HandReviewService(new DecisionCoach(source));

            HandReview first = await service.ReviewAsync(PricedFoldHand("fold-1", 100, 100), 1, CancellationToken.None);
            HandReview second = await service.ReviewAsync(SmallCallHand("call-2"), 1, CancellationToken.None);

            Assert.That(first.Lesson, Does.Contain("matched the price"));
            Assert.AreNotEqual(first.Lesson, second.Lesson);
            Assert.That(second.Lesson, Does.Not.Contain("matched the price"));
            Assert.That(second.Lesson, Does.Not.Contain("favourable price"));
            Assert.That(second.Lesson, Does.Not.Contain("bad call"));
        }

        [Test]
        public async Task AShortPriceIsStillShownAfterAFineCall()
        {
            var source = new FixedEquitySource(0.5);
            var service = new HandReviewService(new DecisionCoach(source));
            HandReview fine = await service.ReviewAsync(SmallCallHand("fine"), 1, CancellationToken.None);
            Assert.That(fine.Lesson, Does.Contain("favourable price"));

            source.Share = 0.1;
            HandReview shortCall = await service.ReviewAsync(PricedCallHand("short", 40, 40), 1, CancellationToken.None);
            Assert.That(shortCall.Lesson, Does.Contain("to break even"));
            Assert.That(shortCall.Lesson, Does.Not.Contain("favourable price"));
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

        private static CompletedHandRecord FoldedAfterACall()
        {
            CompletedHandRecord hand = CoachFixtures.FoldedBlind();
            hand.HandId = "fold-after-call";
            hand.HeroFolded = true;
            hand.WonByFolds = true;
            hand.FinalBoard = new List<Card>
            {
                Card.Parse("Ah"), Card.Parse("Kd"), Card.Parse("9c"), Card.Parse("5s"), Card.Parse("2h")
            };
            HeroDecisionSnapshot call = CoachFixtures.PricedCall(80, 20);
            call.DecisionId = 1;
            call.Street = CoachStreet.Flop;
            call.HandId = hand.HandId;
            HeroDecisionSnapshot fold = CoachFixtures.PricedCall(180, 80);
            fold.DecisionId = 2;
            fold.Street = CoachStreet.River;
            fold.CanCheck = false;
            fold.HandId = hand.HandId;
            hand.Decisions.Add(call);
            hand.Decisions.Add(fold);
            hand.HeroActions.Add(new AcceptedHeroAction { DecisionId = 1, Action = "call", AddedAmount = 20 });
            hand.HeroActions.Add(new AcceptedHeroAction { DecisionId = 2, Action = "fold" });
            return hand;
        }

        private static CompletedHandRecord PricedFoldHand(string id, long pot, long call)
        {
            CompletedHandRecord hand = CoachFixtures.FoldedBlind();
            hand.HandId = id;
            hand.HeroFolded = true;
            hand.FinalBoard = new List<Card>
            {
                Card.Parse("Ah"), Card.Parse("Kd"), Card.Parse("9c"), Card.Parse("5s"), Card.Parse("2h")
            };
            HeroDecisionSnapshot spot = CoachFixtures.PricedCall(pot, call);
            spot.HandId = id;
            spot.CanCheck = false;
            hand.Decisions.Add(spot);
            hand.HeroActions.Add(new AcceptedHeroAction { DecisionId = 1, Action = "fold" });
            return hand;
        }

        private static CompletedHandRecord SmallCallHand(string id) => PricedCallHand(id, 100, 5);

        private static CompletedHandRecord PricedCallHand(string id, long pot, long call)
        {
            CompletedHandRecord hand = CoachFixtures.Kicker();
            hand.HandId = id;
            hand.HeroFolded = false;
            hand.FinalBoard = new List<Card>
            {
                Card.Parse("Ah"), Card.Parse("Kd"), Card.Parse("9c"), Card.Parse("5s"), Card.Parse("2h")
            };
            HeroDecisionSnapshot spot = CoachFixtures.PricedCall(pot, call);
            spot.HandId = id;
            spot.VisibleBoard = hand.FinalBoard;
            hand.Decisions.Add(spot);
            hand.HeroActions.Add(new AcceptedHeroAction { DecisionId = 1, Action = "call", AddedAmount = call });
            return hand;
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
