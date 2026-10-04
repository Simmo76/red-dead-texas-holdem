using System.Collections.Generic;

namespace CinematicPoker.Engine.Telemetry
{
    /// <summary>
    /// The canonical analytics event taxonomy — the single source of truth for
    /// every event name and parameter key the game may record. Code never
    /// tracks ad-hoc string literals; it tracks these constants, so the data
    /// warehouse, the dashboards and the game can never drift apart.
    ///
    /// The business questions each event answers are documented in
    /// docs/live-services-and-monetisation-plan.md.
    /// </summary>
    public static class AnalyticsSchema
    {
        public static class Events
        {
            // Lifecycle — retention and session-length KPIs.
            public const string SessionStart = "session_start";
            public const string SessionEnd = "session_end";

            // Core gameplay — engagement depth, difficulty tuning.
            public const string HandStarted = "hand_started";
            public const string HandCompleted = "hand_completed";
            public const string PlayerBusted = "player_busted";
            public const string GameOver = "game_over";

            // Environments — which tables players love (what to build next).
            public const string EnvironmentSelected = "environment_selected";

            // Coaching — is "Improve My Game" a retention driver?
            public const string CoachingReportViewed = "coaching_report_viewed";

            // Monetisation funnel — environment packs, never chips.
            public const string PackStoreViewed = "pack_store_viewed";
            public const string PackViewed = "pack_viewed";
            public const string PackPurchaseStarted = "pack_purchase_started";
            public const string PackPurchaseCompleted = "pack_purchase_completed";
            public const string PackPurchaseFailed = "pack_purchase_failed";

            // Optional rewarded ads (if ever enabled).
            public const string AdOffered = "ad_offered";
            public const string AdWatched = "ad_watched";

            // Account — adoption of login/profile features.
            public const string AccountCreated = "account_created";
            public const string AccountDetailsChanged = "account_details_changed";
            public const string AccountLinked = "account_linked";
        }

        public static class Params
        {
            public const string EnvironmentId = "environment_id";
            public const string HandNumber = "hand_number";
            public const string PlayersRemaining = "players_remaining";
            public const string HumanWonPot = "human_won_pot";
            public const string HumanStack = "human_stack";
            public const string PotSize = "pot_size";
            public const string DurationSeconds = "duration_seconds";
            public const string HandsPlayed = "hands_played";
            public const string PackId = "pack_id";
            public const string PriceTier = "price_tier";
            public const string FailureReason = "failure_reason";
            public const string Field = "field";
            public const string Provider = "provider";
            public const string PlayMode = "play_mode";
            public const string AppVersion = "app_version";
            public const string Platform = "platform";
        }

        /// <summary>
        /// Every event name declared above. Lets tests (and future tooling)
        /// verify the taxonomy stays valid and duplicate-free.
        /// </summary>
        public static IReadOnlyList<string> AllEventNames { get; } = new[]
        {
            Events.SessionStart, Events.SessionEnd,
            Events.HandStarted, Events.HandCompleted, Events.PlayerBusted, Events.GameOver,
            Events.EnvironmentSelected,
            Events.CoachingReportViewed,
            Events.PackStoreViewed, Events.PackViewed,
            Events.PackPurchaseStarted, Events.PackPurchaseCompleted, Events.PackPurchaseFailed,
            Events.AdOffered, Events.AdWatched,
            Events.AccountCreated, Events.AccountDetailsChanged, Events.AccountLinked
        };

        /// <summary>Valid names are non-empty lowercase snake_case: [a-z0-9_], starting with a letter.</summary>
        public static bool IsValidEventName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 64)
                return false;
            if (name[0] < 'a' || name[0] > 'z')
                return false;
            foreach (char c in name)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
                if (!ok)
                    return false;
            }
            return true;
        }
    }
}
