using System;
using System.Collections.Generic;

namespace CinematicPoker.Engine.Telemetry
{
    /// <summary>
    /// Records gameplay and monetisation events against a pseudonymous player
    /// id and a per-launch session id, and fans them out to sinks. Pure C# and
    /// fully offline: with only the buffered sink attached, tracking costs a
    /// dictionary and a string — no network, no SDK, no account required.
    ///
    /// Thread-safe: engine events may arrive from the Monte Carlo worker
    /// thread while the UI tracks on the main thread.
    /// </summary>
    public sealed class AnalyticsTracker
    {
        private readonly object _gate = new object();
        private readonly List<IAnalyticsSink> _sinks = new List<IAnalyticsSink>();
        private readonly Func<DateTime> _utcNow;
        private long _sequence;
        private DateTime _sessionStartedUtc;
        private long _handsThisSession;

        public string PlayerId { get; }
        public string SessionId { get; private set; }

        public AnalyticsTracker(string playerId, Func<DateTime> utcNow = null)
        {
            if (string.IsNullOrEmpty(playerId))
                throw new ArgumentException("Player id is required.", nameof(playerId));
            PlayerId = playerId;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public void AddSink(IAnalyticsSink sink)
        {
            if (sink == null)
                throw new ArgumentNullException(nameof(sink));
            lock (_gate)
                _sinks.Add(sink);
        }

        /// <summary>Starts a new session (one per app launch / return from background) and emits session_start.</summary>
        public string StartSession(IReadOnlyDictionary<string, object> parameters = null)
        {
            lock (_gate)
            {
                SessionId = Guid.NewGuid().ToString("N");
                _sessionStartedUtc = _utcNow();
                _handsThisSession = 0;
            }
            Track(AnalyticsSchema.Events.SessionStart, parameters);
            return SessionId;
        }

        /// <summary>Emits session_end with duration and hands played, the raw material for session-length and engagement KPIs.</summary>
        public void EndSession()
        {
            DateTime startedUtc;
            long hands;
            lock (_gate)
            {
                if (SessionId == null)
                    return;
                startedUtc = _sessionStartedUtc;
                hands = _handsThisSession;
            }

            Track(AnalyticsSchema.Events.SessionEnd, new Dictionary<string, object>
            {
                { AnalyticsSchema.Params.DurationSeconds, (long)(_utcNow() - startedUtc).TotalSeconds },
                { AnalyticsSchema.Params.HandsPlayed, hands }
            });

            lock (_gate)
                SessionId = null;
        }

        public void Track(string eventName, IReadOnlyDictionary<string, object> parameters = null)
        {
            AnalyticsEvent evt;
            IAnalyticsSink[] sinks;
            lock (_gate)
            {
                if (SessionId == null)
                {
                    // Lenient: never lose an event because a caller fired before
                    // StartSession. The implicit session still groups correctly.
                    SessionId = Guid.NewGuid().ToString("N");
                    _sessionStartedUtc = _utcNow();
                    _handsThisSession = 0;
                }

                if (eventName == AnalyticsSchema.Events.HandCompleted)
                    _handsThisSession++;

                evt = new AnalyticsEvent(eventName, _utcNow(), PlayerId, SessionId, ++_sequence, parameters);
                sinks = _sinks.ToArray();
            }

            foreach (var sink in sinks)
                sink.Record(evt);
        }
    }
}
