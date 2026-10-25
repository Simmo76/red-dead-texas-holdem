using System;
using System.Collections.Generic;
using CinematicPoker.Engine.Telemetry;

namespace CinematicPoker.Engine.Coaching
{
    /// <summary>
    /// Local coaching metrics. Parameters never include hole cards, names or logs.
    /// </summary>
    public static class CoachAnalytics
    {
        public static AnalyticsEvent Available(string playerId, string sessionId, long sequence, string handId, string mode) =>
            Make(AnalyticsSchema.Events.ReviewAvailable, playerId, sessionId, sequence, handId, mode, null, null, null, null);

        public static AnalyticsEvent Opened(string playerId, string sessionId, long sequence, string handId, string mode, bool analysisReady) =>
            Make(AnalyticsSchema.Events.ReviewOpened, playerId, sessionId, sequence, handId, mode, null, null, null, analysisReady);

        public static AnalyticsEvent Closed(string playerId, string sessionId, long sequence, string handId, string mode, long viewSeconds) =>
            Make(AnalyticsSchema.Events.ReviewClosed, playerId, sessionId, sequence, handId, mode, null, null, viewSeconds, null);

        public static AnalyticsEvent KeyDecision(string playerId, string sessionId, long sequence, string handId, string reasonCode) =>
            Make(AnalyticsSchema.Events.ReviewKeyDecisionOpened, playerId, sessionId, sequence, handId, null, reasonCode, null, null, null);

        public static AnalyticsEvent Feedback(string playerId, string sessionId, long sequence, string handId, string reasonCode) =>
            Make(AnalyticsSchema.Events.ReviewFeedback, playerId, sessionId, sequence, handId, null, reasonCode, null, null, null);

        public static AnalyticsEvent ModeChanged(string playerId, string sessionId, long sequence, string mode) =>
            Make(AnalyticsSchema.Events.CoachModeChanged, playerId, sessionId, sequence, null, mode, null, null, null, null);

        public static AnalyticsEvent Failed(string playerId, string sessionId, long sequence, string handId, string reasonCode, long analysisMs) =>
            Make(AnalyticsSchema.Events.CoachAnalysisFailed, playerId, sessionId, sequence, handId, null, reasonCode, analysisMs, null, null);

        private static AnalyticsEvent Make(
            string name,
            string playerId,
            string sessionId,
            long sequence,
            string handId,
            string mode,
            string reasonCode,
            long? analysisMs,
            long? viewSeconds,
            bool? analysisReady)
        {
            var parameters = new Dictionary<string, object>
            {
                { AnalyticsSchema.Params.SchemaVersion, CoachSchema.Version }
            };
            if (!string.IsNullOrEmpty(handId)) parameters[AnalyticsSchema.Params.HandId] = handId;
            if (!string.IsNullOrEmpty(mode)) parameters[AnalyticsSchema.Params.CoachMode] = mode;
            if (!string.IsNullOrEmpty(reasonCode)) parameters[AnalyticsSchema.Params.ReasonCode] = reasonCode;
            if (analysisMs.HasValue) parameters[AnalyticsSchema.Params.AnalysisMs] = analysisMs.Value;
            if (viewSeconds.HasValue) parameters[AnalyticsSchema.Params.ViewSeconds] = viewSeconds.Value;
            if (analysisReady.HasValue) parameters[AnalyticsSchema.Params.AnalysisReady] = analysisReady.Value;
            return new AnalyticsEvent(name, DateTime.UtcNow, string.IsNullOrEmpty(playerId) ? "local" : playerId, string.IsNullOrEmpty(sessionId) ? "local" : sessionId, sequence, parameters);
        }
    }
}
