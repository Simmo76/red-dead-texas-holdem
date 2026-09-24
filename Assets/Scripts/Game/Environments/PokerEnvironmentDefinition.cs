using CinematicPoker.Game.Profiles;
using UnityEngine;

namespace CinematicPoker.Game.Environments
{
    /// <summary>
    /// One playable poker environment (Mates' Kitchen, Western 1860, Tokyo...).
    /// Everything an environment can change lives here, as data.
    ///
    /// HARD REQUIREMENT: adding a new environment must never require modifying
    /// PokerEngine, and shared gameplay code must never branch on environmentId.
    /// The architecture targets 50+ interchangeable environments.
    /// </summary>
    [CreateAssetMenu(menuName = "Cinematic Poker/Environment Definition", fileName = "PokerEnvironmentDefinition")]
    public sealed class PokerEnvironmentDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string environmentId;
        public string displayName;
        [TextArea] public string description;

        [Header("Content (Addressables keys — premium packs ship separately, playable offline once downloaded)")]
        [Tooltip("Addressables key of the environment scene.")]
        public string sceneAddress;
        public bool includedInBaseGame;

        [Header("People")]
        public NPCProfileAsset[] npcPool;

        [Header("Poker")]
        public PokerDifficultyProfile pokerDifficulty;

        [Header("Presentation")]
        public DialogueProfile tableTalkProfile;
        public AmbientEventProfile ambientEvents;
        public AnimationProfile animationProfile;
        public AudioProfile audioProfile;
        public MusicProfile musicProfile;
        public LightingProfile lightingProfile;
        public CardStyle cardStyle;
        public ChipStyle chipStyle;
        public TableStyle tableStyle;
        public CameraProfile cameraProfile;

        [Header("Cinematics (Addressables keys, optional)")]
        public string introSequenceAddress;
        public string outroSequenceAddress;
    }
}
