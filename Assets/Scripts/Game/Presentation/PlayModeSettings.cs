using UnityEngine;

namespace CinematicPoker.Game.Presentation
{
    public enum PlayMode
    {
        /// <summary>Fast dealing, minimal cuts, short NPC think time. For travel and practice.</summary>
        Quick,
        /// <summary>Full dialogue, reactions, physical dealing and cinematic cameras.</summary>
        Cinematic
    }

    /// <summary>
    /// Timing knobs for the two presentation modes. Both modes use exactly the
    /// same poker engine and NPC decision logic — only pacing changes.
    /// </summary>
    [CreateAssetMenu(menuName = "Cinematic Poker/Play Mode Settings", fileName = "PlayModeSettings")]
    public sealed class PlayModeSettings : ScriptableObject
    {
        [Header("NPC think time (seconds)")]
        public Vector2 quickThinkTime = new Vector2(0.2f, 0.8f);
        public Vector2 cinematicThinkTime = new Vector2(1.0f, 4.0f);

        [Header("Dealing")]
        public float quickDealInterval = 0.05f;
        public float cinematicDealInterval = 0.35f;

        [Header("Between hands")]
        public float quickNextHandDelay = 0.5f;
        public float cinematicNextHandDelay = 3.5f;

        [Header("Quick Play conveniences")]
        public bool quickAutoMuck = true;
        public bool quickSkipDialogue = true;

        public Vector2 ThinkTime(PlayMode mode) => mode == PlayMode.Quick ? quickThinkTime : cinematicThinkTime;
        public float DealInterval(PlayMode mode) => mode == PlayMode.Quick ? quickDealInterval : cinematicDealInterval;
        public float NextHandDelay(PlayMode mode) => mode == PlayMode.Quick ? quickNextHandDelay : cinematicNextHandDelay;
    }
}
