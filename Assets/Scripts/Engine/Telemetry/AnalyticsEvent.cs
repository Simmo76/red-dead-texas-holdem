using System;
using System.Collections.Generic;

namespace CinematicPoker.Engine.Telemetry
{
    /// <summary>
    /// A single immutable analytics event. Pure C# — no Unity dependency.
    ///
    /// Events are pseudonymous: they carry a random player id, never a name,
    /// email or device identifier. Parameter values are restricted to
    /// string / bool / long / double so every event can be serialised
    /// identically on-device and by any downstream vendor (Unity Analytics,
    /// Firebase, a CSV export…).
    /// </summary>
    public sealed class AnalyticsEvent
    {
        public string Name { get; }
        public DateTime UtcTimestamp { get; }
        public string PlayerId { get; }
        public string SessionId { get; }

        /// <summary>Monotonic per-tracker counter so dropped/duplicated events are detectable downstream.</summary>
        public long SequenceNumber { get; }

        public IReadOnlyDictionary<string, object> Parameters { get; }

        private static readonly IReadOnlyDictionary<string, object> Empty =
            new Dictionary<string, object>();

        public AnalyticsEvent(
            string name,
            DateTime utcTimestamp,
            string playerId,
            string sessionId,
            long sequenceNumber,
            IReadOnlyDictionary<string, object> parameters = null)
        {
            if (!AnalyticsSchema.IsValidEventName(name))
                throw new ArgumentException($"Invalid analytics event name '{name}'. Use lowercase snake_case.", nameof(name));
            if (string.IsNullOrEmpty(playerId))
                throw new ArgumentException("Player id is required.", nameof(playerId));
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("Session id is required.", nameof(sessionId));

            if (parameters != null)
            {
                foreach (var pair in parameters)
                {
                    if (!AnalyticsSchema.IsValidEventName(pair.Key))
                        throw new ArgumentException($"Invalid parameter key '{pair.Key}'. Use lowercase snake_case.", nameof(parameters));
                    if (!IsSupportedValue(pair.Value))
                        throw new ArgumentException(
                            $"Parameter '{pair.Key}' has unsupported type '{pair.Value?.GetType().Name ?? "null"}'. " +
                            "Use string, bool, int, long, float or double.", nameof(parameters));
                }
            }

            Name = name;
            UtcTimestamp = utcTimestamp;
            PlayerId = playerId;
            SessionId = sessionId;
            SequenceNumber = sequenceNumber;
            Parameters = parameters ?? Empty;
        }

        private static bool IsSupportedValue(object value) =>
            value is string || value is bool ||
            value is int || value is long ||
            value is float || value is double;
    }
}
