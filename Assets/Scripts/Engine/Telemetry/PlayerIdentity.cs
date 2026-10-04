using System;

namespace CinematicPoker.Engine.Telemetry
{
    /// <summary>
    /// The player's identity, offline-first and free: a random, pseudonymous
    /// player id generated on first launch plus a display name the player can
    /// change at any time on-device. This same id becomes the external id when
    /// Unity Authentication is linked later, so analytics history survives the
    /// upgrade from "no account" to "signed-in account" unbroken.
    ///
    /// Immutable: changing details produces a new instance, so a half-applied
    /// edit can never be observed.
    /// </summary>
    public sealed class PlayerIdentity
    {
        public const int MinDisplayNameLength = 2;
        public const int MaxDisplayNameLength = 20;
        public const string DefaultDisplayName = "Player";

        public string PlayerId { get; }
        public string DisplayName { get; }
        public DateTime CreatedUtc { get; }

        public PlayerIdentity(string playerId, string displayName, DateTime createdUtc)
        {
            if (string.IsNullOrEmpty(playerId))
                throw new ArgumentException("Player id is required.", nameof(playerId));
            if (!IsValidDisplayName(displayName, out string reason))
                throw new ArgumentException(reason, nameof(displayName));

            PlayerId = playerId;
            DisplayName = displayName.Trim();
            CreatedUtc = createdUtc;
        }

        public static PlayerIdentity CreateNew(string displayName = DefaultDisplayName, DateTime? createdUtc = null) =>
            new PlayerIdentity(Guid.NewGuid().ToString("N"), displayName, createdUtc ?? DateTime.UtcNow);

        /// <summary>Returns a copy with the new display name, or throws ArgumentException if it is invalid.</summary>
        public PlayerIdentity WithDisplayName(string newDisplayName) =>
            new PlayerIdentity(PlayerId, newDisplayName, CreatedUtc);

        /// <summary>
        /// Display names: 2–20 characters after trimming; letters, digits,
        /// spaces, apostrophes, hyphens and underscores. The reason string is
        /// suitable for showing directly in the profile UI.
        /// </summary>
        public static bool IsValidDisplayName(string name, out string reason)
        {
            string trimmed = name?.Trim() ?? string.Empty;

            if (trimmed.Length < MinDisplayNameLength)
            {
                reason = $"Name must be at least {MinDisplayNameLength} characters.";
                return false;
            }
            if (trimmed.Length > MaxDisplayNameLength)
            {
                reason = $"Name must be at most {MaxDisplayNameLength} characters.";
                return false;
            }
            foreach (char c in trimmed)
            {
                bool ok = char.IsLetterOrDigit(c) || c == ' ' || c == '\'' || c == '-' || c == '_';
                if (!ok)
                {
                    reason = "Name can only contain letters, numbers, spaces, apostrophes, hyphens and underscores.";
                    return false;
                }
            }

            reason = null;
            return true;
        }
    }
}
