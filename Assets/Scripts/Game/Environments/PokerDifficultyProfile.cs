using UnityEngine;
using CinematicPoker.Game.Profiles;

namespace CinematicPoker.Game.Environments
{
    /// <summary>
    /// Poker skill distribution for an environment (e.g. Vegas hosts the
    /// strongest AI; Mates' Kitchen is friendly amateurs).
    /// </summary>
    [CreateAssetMenu(menuName = "Cinematic Poker/Environment/Poker Difficulty Profile", fileName = "PokerDifficultyProfile")]
    public sealed class PokerDifficultyProfile : ScriptableObject
    {
        [Tooltip("Global skill multiplier applied on top of each NPC's own AIProfile.")]
        [Range(0.5f, 1.5f)] public float skillMultiplier = 1f;

        public long smallBlind = 5;
        public long bigBlind = 10;
        public long startingStack = 1000;

        [Tooltip("Optional overrides: if set, these profiles replace the NPCs' defaults at this table.")]
        public AIProfileAsset[] tableProfileOverrides;
    }
}
