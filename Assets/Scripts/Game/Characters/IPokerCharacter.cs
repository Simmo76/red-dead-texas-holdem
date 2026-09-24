using UnityEngine;

namespace CinematicPoker.Game.Characters
{
    public enum PokerReaction
    {
        Happy,
        Angry,
        Disappointed,
        Surprised,
        CelebrateSmall,
        CelebrateLarge,
        Neutral
    }

    public enum PokerGesture
    {
        CheckCards,
        FoldCards,
        PlaceBet,
        PushChips,
        CollectChips,
        Drink,
        ScratchFace,
        AdjustClothing,
        LeanForward,
        LeanBack,
        Knock // check by knocking the table
    }

    /// <summary>
    /// Abstraction over a seated character rig so standard humanoids and
    /// unusually proportioned characters (e.g. sumo-sized NPCs) share the same
    /// poker interaction anchors without affecting game logic.
    /// </summary>
    public interface IPokerCharacter
    {
        Transform HeadTransform { get; }
        Transform CardAnchor { get; }
        Transform ChipAnchor { get; }
        Transform VoiceAnchor { get; }

        void LookAt(Transform target, float weight);
        void PlayGesture(PokerGesture gesture);
        void PlayReaction(PokerReaction reaction, float intensity);
        /// <summary>Play a probabilistic tell behaviour by id (from TellProfileAsset).</summary>
        void PlayTell(string behaviourId);
    }
}
