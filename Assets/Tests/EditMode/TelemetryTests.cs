using System;
using System.Collections.Generic;
using System.Linq;
using CinematicPoker.Engine.Telemetry;
using NUnit.Framework;

namespace CinematicPoker.Engine.Tests
{
    [TestFixture]
    public class TelemetryTests
    {
        private static AnalyticsTracker NewTracker(BufferedAnalyticsSink sink, Func<DateTime> clock = null)
        {
            var tracker = new AnalyticsTracker("player-abc", clock);
            tracker.AddSink(sink);
            return tracker;
        }

        [Test]
        public void TrackAttachesPlayerSessionAndIncrementingSequence()
        {
            var sink = new RecordingSink();
            var tracker = new AnalyticsTracker("player-abc");
            tracker.AddSink(sink);

            tracker.StartSession();
            tracker.Track(AnalyticsSchema.Events.HandStarted);
            tracker.Track(AnalyticsSchema.Events.HandCompleted);

            Assert.AreEqual(3, sink.Events.Count); // session_start + 2
            Assert.That(sink.Events.All(e => e.PlayerId == "player-abc"));
            Assert.That(sink.Events.All(e => e.SessionId == sink.Events[0].SessionId));
            CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, sink.Events.Select(e => e.SequenceNumber).ToArray());
        }

        [Test]
        public void TrackBeforeStartSessionStillRecordsWithImplicitSession()
        {
            var sink = new RecordingSink();
            var tracker = new AnalyticsTracker("player-abc");
            tracker.AddSink(sink);

            tracker.Track(AnalyticsSchema.Events.EnvironmentSelected);

            Assert.AreEqual(1, sink.Events.Count);
            Assert.IsNotEmpty(sink.Events[0].SessionId);
        }

        [Test]
        public void EndSessionEmitsDurationAndHandsPlayed()
        {
            var now = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
            var sink = new RecordingSink();
            var tracker = new AnalyticsTracker("player-abc", () => now);
            tracker.AddSink(sink);

            tracker.StartSession();
            tracker.Track(AnalyticsSchema.Events.HandCompleted);
            tracker.Track(AnalyticsSchema.Events.HandCompleted);
            now = now.AddSeconds(90);
            tracker.EndSession();

            var end = sink.Events.Single(e => e.Name == AnalyticsSchema.Events.SessionEnd);
            Assert.AreEqual(90L, end.Parameters[AnalyticsSchema.Params.DurationSeconds]);
            Assert.AreEqual(2L, end.Parameters[AnalyticsSchema.Params.HandsPlayed]);
        }

        [Test]
        public void NewSessionsGetDistinctIdsAndResetHandCount()
        {
            var sink = new RecordingSink();
            var tracker = new AnalyticsTracker("player-abc");
            tracker.AddSink(sink);

            string first = tracker.StartSession();
            tracker.Track(AnalyticsSchema.Events.HandCompleted);
            tracker.EndSession();
            string second = tracker.StartSession();
            tracker.EndSession();

            Assert.AreNotEqual(first, second);
            var secondEnd = sink.Events.Last(e => e.Name == AnalyticsSchema.Events.SessionEnd);
            Assert.AreEqual(0L, secondEnd.Parameters[AnalyticsSchema.Params.HandsPlayed]);
        }

        [Test]
        public void InvalidEventNamesAndParameterTypesAreRejected()
        {
            var tracker = NewTracker(new BufferedAnalyticsSink());
            tracker.StartSession();

            Assert.Throws<ArgumentException>(() => tracker.Track("Bad Name!"));
            Assert.Throws<ArgumentException>(() => tracker.Track("camelCase"));
            Assert.Throws<ArgumentException>(() =>
                tracker.Track(AnalyticsSchema.Events.HandStarted,
                    new Dictionary<string, object> { { "pot", new object() } }));
        }

        [Test]
        public void SchemaEventNamesAreValidAndUnique()
        {
            foreach (string name in AnalyticsSchema.AllEventNames)
                Assert.IsTrue(AnalyticsSchema.IsValidEventName(name), $"'{name}' is not valid snake_case");

            Assert.AreEqual(AnalyticsSchema.AllEventNames.Count, AnalyticsSchema.AllEventNames.Distinct().Count());
        }

        [Test]
        public void JsonSerialisationIsCultureInvariantAndEscaped()
        {
            var evt = new AnalyticsEvent(
                AnalyticsSchema.Events.PackPurchaseFailed,
                new DateTime(2026, 10, 4, 9, 30, 15, 250, DateTimeKind.Utc),
                "pid", "sid", 7,
                new Dictionary<string, object>
                {
                    { "failure_reason", "user \"cancelled\"\nat checkout" },
                    { "pot_size", 1234L },
                    { "duration_seconds", 1.5 },
                    { "human_won_pot", true }
                });

            string json = AnalyticsJson.Serialize(evt);

            StringAssert.Contains("\"name\":\"pack_purchase_failed\"", json);
            StringAssert.Contains("\"ts\":\"2026-10-04T09:30:15.250Z\"", json);
            StringAssert.Contains("\"seq\":7", json);
            StringAssert.Contains("\"failure_reason\":\"user \\\"cancelled\\\"\\nat checkout\"", json);
            StringAssert.Contains("\"pot_size\":1234", json);
            StringAssert.Contains("\"duration_seconds\":1.5", json);
            StringAssert.Contains("\"human_won_pot\":true", json);
            Assert.IsFalse(json.Contains("1,5"), "decimal separator must be invariant");
        }

        [Test]
        public void BufferedSinkDrainsAndBoundsMemory()
        {
            var sink = new BufferedAnalyticsSink(capacity: 3);
            var tracker = NewTracker(sink);
            tracker.StartSession();

            for (int i = 0; i < 5; i++)
                tracker.Track(AnalyticsSchema.Events.HandStarted);

            Assert.AreEqual(3, sink.Count);
            Assert.AreEqual(3, sink.DroppedCount); // session_start + 2 oldest hands

            var lines = sink.DrainLines();
            Assert.AreEqual(3, lines.Count);
            Assert.AreEqual(0, sink.Count);
            Assert.That(lines.All(l => l.StartsWith("{") && l.EndsWith("}")));
        }

        [Test]
        public void PlayerIdentityCreatesUniquePseudonymousIds()
        {
            var a = PlayerIdentity.CreateNew();
            var b = PlayerIdentity.CreateNew();

            Assert.AreNotEqual(a.PlayerId, b.PlayerId);
            Assert.AreEqual(PlayerIdentity.DefaultDisplayName, a.DisplayName);
        }

        [Test]
        public void DisplayNameChangesAreValidatedAndImmutable()
        {
            var identity = PlayerIdentity.CreateNew("James");

            var renamed = identity.WithDisplayName("  Ace-High_Jim  ");
            Assert.AreEqual("Ace-High_Jim", renamed.DisplayName);
            Assert.AreEqual(identity.PlayerId, renamed.PlayerId);
            Assert.AreEqual("James", identity.DisplayName, "original must be unchanged");

            Assert.Throws<ArgumentException>(() => identity.WithDisplayName("x"));
            Assert.Throws<ArgumentException>(() => identity.WithDisplayName(new string('x', 21)));
            Assert.Throws<ArgumentException>(() => identity.WithDisplayName("bad<script>"));
            Assert.Throws<ArgumentException>(() => identity.WithDisplayName(null));

            Assert.IsFalse(PlayerIdentity.IsValidDisplayName("", out string reason));
            Assert.IsNotEmpty(reason);
            Assert.IsTrue(PlayerIdentity.IsValidDisplayName("O'Brien 2", out _));
        }

        private sealed class RecordingSink : IAnalyticsSink
        {
            public List<AnalyticsEvent> Events { get; } = new List<AnalyticsEvent>();
            public void Record(AnalyticsEvent analyticsEvent) => Events.Add(analyticsEvent);
        }
    }
}
