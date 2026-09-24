using System;
using UnityEngine;

namespace CinematicPoker.Game.Profiles
{
    public enum RelationshipHistoryFlag
    {
        WorkedTogether,
        KnownForYears,
        Family,
        SavedLife,
        PreviousArgument,
        BusinessRelationship,
        Rival
    }

    [Serializable]
    public sealed class NPCRelationship
    {
        public NPCProfileAsset other;
        [Range(-1f, 1f)] public float friendship;
        [Range(-1f, 1f)] public float respect;
        [Range(0f, 1f)] public float rivalry;
        [Range(0f, 1f)] public float authority; // how much this NPC defers to the other
        [Range(-1f, 1f)] public float trust;
        public RelationshipHistoryFlag[] sharedHistory = Array.Empty<RelationshipHistoryFlag>();
    }

    /// <summary>
    /// Everything that defines one NPC character: identity, poker brain,
    /// presentation profiles, and relationships to other NPCs. Environments
    /// reference pools of these; the future Impossible Table mode mixes them
    /// freely, so nothing here may depend on a specific environment.
    /// </summary>
    [CreateAssetMenu(menuName = "Cinematic Poker/NPC Profile", fileName = "NPCProfile")]
    public sealed class NPCProfileAsset : ScriptableObject
    {
        [Header("Identity")]
        public string npcId;
        public string displayName;
        [TextArea] public string bio;

        [Header("Poker Brain")]
        public AIProfileAsset aiProfile;

        [Header("Presentation")]
        public TellProfileAsset tellProfile;
        public DialogueProfile dialogueProfile;
        [Tooltip("Addressables key of the character prefab (must implement IPokerCharacter).")]
        public string characterPrefabAddress;
        [Tooltip("Addressables key or id of the voice bank.")]
        public string voiceBankAddress;

        [Header("Social")]
        public NPCRelationship[] relationships = Array.Empty<NPCRelationship>();

        [Header("Progression")]
        [Tooltip("Can this character be unlocked for the Impossible Table meta-mode?")]
        public bool unlockableForImpossibleTable = true;
    }
}
