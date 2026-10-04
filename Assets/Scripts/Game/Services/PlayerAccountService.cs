using System;
using System.Globalization;
using System.IO;
using CinematicPoker.Engine.Telemetry;
using UnityEngine;

namespace CinematicPoker.Game.Services
{
    /// <summary>
    /// Offline-first player account: a pseudonymous player id created on first
    /// launch plus player-editable details (display name), stored as atomic
    /// JSON next to the save file. Completely free — no server, no vendor.
    ///
    /// When online accounts arrive (Unity Authentication), this local identity
    /// is linked rather than replaced: the playerId becomes the analytics
    /// external id, so player history stays continuous. See
    /// docs/live-services-and-monetisation-plan.md.
    /// </summary>
    public static class PlayerAccountService
    {
        [Serializable]
        private class PlayerAccountData
        {
            public string playerId;
            public string displayName;
            public string createdUtc;
        }

        private const string FileName = "player_account.json";
        private static string SavePath => Path.Combine(Application.persistentDataPath, FileName);
        private static string TempPath => SavePath + ".tmp";

        private static PlayerIdentity _cached;

        /// <summary>Loads the local account, creating one on first launch.</summary>
        public static PlayerIdentity LoadOrCreate()
        {
            if (_cached != null)
                return _cached;

            try
            {
                if (File.Exists(SavePath))
                {
                    var data = JsonUtility.FromJson<PlayerAccountData>(File.ReadAllText(SavePath));
                    if (data != null && !string.IsNullOrEmpty(data.playerId))
                    {
                        DateTime created = ParseUtc(data.createdUtc);
                        string name = PlayerIdentity.IsValidDisplayName(data.displayName, out _)
                            ? data.displayName
                            : PlayerIdentity.DefaultDisplayName;
                        _cached = new PlayerIdentity(data.playerId, name, created);
                        return _cached;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Player account load failed, creating fresh identity: {ex.Message}");
            }

            _cached = PlayerIdentity.CreateNew();
            Save(_cached);
            TelemetryService.Tracker.Track(AnalyticsSchema.Events.AccountCreated);
            return _cached;
        }

        /// <summary>
        /// Changes the display name. Returns false with a UI-ready reason when
        /// the name is invalid. Free: a local write, no server round-trip.
        /// </summary>
        public static bool TryChangeDisplayName(string newDisplayName, out string reason)
        {
            if (!PlayerIdentity.IsValidDisplayName(newDisplayName, out reason))
                return false;

            _cached = LoadOrCreate().WithDisplayName(newDisplayName);
            Save(_cached);
            TelemetryService.Tracker.Track(AnalyticsSchema.Events.AccountDetailsChanged,
                new System.Collections.Generic.Dictionary<string, object>
                {
                    { AnalyticsSchema.Params.Field, "display_name" }
                });
            return true;
        }

        private static void Save(PlayerIdentity identity)
        {
            try
            {
                var data = new PlayerAccountData
                {
                    playerId = identity.PlayerId,
                    displayName = identity.DisplayName,
                    createdUtc = identity.CreatedUtc.ToString("o", CultureInfo.InvariantCulture)
                };
                File.WriteAllText(TempPath, JsonUtility.ToJson(data));
                if (File.Exists(SavePath))
                    File.Delete(SavePath);
                File.Move(TempPath, SavePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Player account save failed: {ex.Message}");
            }
        }

        private static DateTime ParseUtc(string iso)
        {
            return DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime parsed)
                ? parsed
                : DateTime.UtcNow;
        }
    }
}
