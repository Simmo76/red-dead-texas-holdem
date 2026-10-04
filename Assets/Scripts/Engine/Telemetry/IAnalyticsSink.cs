namespace CinematicPoker.Engine.Telemetry
{
    /// <summary>
    /// A destination for analytics events. The tracker fans events out to any
    /// number of sinks: the on-device JSON-lines buffer today, a vendor SDK
    /// adapter (Unity Analytics, Firebase…) tomorrow — without the game code
    /// changing a single Track call.
    /// </summary>
    public interface IAnalyticsSink
    {
        void Record(AnalyticsEvent analyticsEvent);
    }
}
