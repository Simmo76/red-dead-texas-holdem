using UnityEngine;

namespace CinematicPoker.Game.Environments
{
    // Small data-only ScriptableObjects that let each environment restyle the
    // table without touching gameplay code. All heavy assets are referenced by
    // Addressables key (string address) so premium packs can ship separately
    // and remain fully usable offline once downloaded.

    [CreateAssetMenu(menuName = "Cinematic Poker/Environment/Card Style", fileName = "CardStyle")]
    public sealed class CardStyle : ScriptableObject
    {
        public string cardPrefabAddress;
        public string cardBackMaterialAddress;
        [Tooltip("Wear level 0 = pristine casino cards, 1 = battered saloon deck.")]
        [Range(0f, 1f)] public float wear;
    }

    [CreateAssetMenu(menuName = "Cinematic Poker/Environment/Chip Style", fileName = "ChipStyle")]
    public sealed class ChipStyle : ScriptableObject
    {
        public string chipPrefabAddress;
        [Tooltip("Display name of the currency, e.g. 'chips', 'credits', 'tokens'.")]
        public string currencyName = "chips";
        public string currencySymbol = "$";
    }

    [CreateAssetMenu(menuName = "Cinematic Poker/Environment/Table Style", fileName = "TableStyle")]
    public sealed class TableStyle : ScriptableObject
    {
        public string tablePrefabAddress;
        public Color feltColor = Color.green;
    }

    [CreateAssetMenu(menuName = "Cinematic Poker/Environment/Lighting Profile", fileName = "LightingProfile")]
    public sealed class LightingProfile : ScriptableObject
    {
        public string lightingDataAddress;
        public Color ambientColor = Color.gray;
        [Range(0f, 2f)] public float exposure = 1f;
    }

    [CreateAssetMenu(menuName = "Cinematic Poker/Environment/Audio Profile", fileName = "AudioProfile")]
    public sealed class AudioProfile : ScriptableObject
    {
        public string roomToneAddress;
        public string[] ambientLayerAddresses;
        [Range(0f, 1f)] public float ambientVolume = 0.6f;
    }

    [CreateAssetMenu(menuName = "Cinematic Poker/Environment/Music Profile", fileName = "MusicProfile")]
    public sealed class MusicProfile : ScriptableObject
    {
        public string[] trackAddresses;
        [Tooltip("Music must stay subtle: cards, chips and voices come first.")]
        [Range(0f, 1f)] public float volume = 0.35f;
    }

    [CreateAssetMenu(menuName = "Cinematic Poker/Environment/Animation Profile", fileName = "AnimationProfile")]
    public sealed class AnimationProfile : ScriptableObject
    {
        [Tooltip("Animator override controller address for environment-specific idles/gestures.")]
        public string overrideControllerAddress;
        [Range(0.5f, 2f)] public float dealSpeedMultiplier = 1f;
    }

    /// <summary>Named camera anchors so cinematography is data-driven per environment.</summary>
    [CreateAssetMenu(menuName = "Cinematic Poker/Environment/Camera Profile", fileName = "CameraProfile")]
    public sealed class CameraProfile : ScriptableObject
    {
        [Tooltip("Anchor ids expected in the environment scene: PlayerView, WideTable, CardView, PotView, NPCReaction, Showdown, EnvironmentEstablishing.")]
        public string[] anchorIds =
        {
            "PlayerView", "WideTable", "CardView", "PotView",
            "NPCReaction", "Showdown", "EnvironmentEstablishing"
        };

        [Tooltip("Maximum cinematic cuts per hand in Cinematic Play; keep low — do not overuse cuts.")]
        [Range(0, 6)] public int maxCutsPerHand = 3;
    }
}
