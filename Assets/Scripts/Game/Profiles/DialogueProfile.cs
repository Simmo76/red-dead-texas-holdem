using System;
using UnityEngine;

namespace CinematicPoker.Game.Profiles
{
    /// <summary>
    /// Contextual dialogue triggers. Fired by NPCBehaviourController in response
    /// to engine events and social state — never randomly.
    /// </summary>
    public enum DialogueTrigger
    {
        PlayerJoinsTable,
        PlayerWinsHand,
        PlayerLosesHand,
        PlayerRaises,
        PlayerAllIn,
        PlayerCaughtBluffing,
        NpcCaughtBluffing,
        BigPot,
        BadBeat,
        LongThinkingTime,
        RepeatedRaises,
        RepeatedFolds,
        NpcDrinks,
        NpcWinsHand,
        NpcLosesHand,
        NpcTilted,
        RivalWins,
        EnvironmentIdle
    }

    [Serializable]
    public sealed class DialogueLine
    {
        public DialogueTrigger trigger;
        [TextArea] public string text;
        [Tooltip("Optional: only fires when directed at this NPC id (e.g. 'Shut up, Davo').")]
        public string targetNpcId;
        [Tooltip("Minimum tilt for this line (0 = always available).")]
        [Range(0f, 1f)] public float minTilt;
        [Range(0f, 1f)] public float weight = 1f;
    }

    /// <summary>
    /// Authored dialogue for one NPC (initial release). The provider interface
    /// below allows a local/remote LLM provider later for banter only —
    /// LLMs never control cards, bets, legal actions or game state.
    /// </summary>
    [CreateAssetMenu(menuName = "Cinematic Poker/Dialogue Profile", fileName = "DialogueProfile")]
    public sealed class DialogueProfile : ScriptableObject
    {
        public DialogueLine[] lines = Array.Empty<DialogueLine>();
    }

    public interface INpcDialogueProvider
    {
        /// <summary>Return a line for the trigger, or null to stay silent.</summary>
        string GetLine(DialogueTrigger trigger, string targetNpcId, float tilt, System.Random rng);
    }

    /// <summary>Default provider: weighted random selection from authored lines.</summary>
    public sealed class AuthoredDialogueProvider : INpcDialogueProvider
    {
        private readonly DialogueProfile _profile;

        public AuthoredDialogueProvider(DialogueProfile profile) => _profile = profile;

        public string GetLine(DialogueTrigger trigger, string targetNpcId, float tilt, System.Random rng)
        {
            if (_profile == null) return null;

            float totalWeight = 0f;
            foreach (DialogueLine line in _profile.lines)
                if (Matches(line, trigger, targetNpcId, tilt))
                    totalWeight += line.weight;

            if (totalWeight <= 0f) return null;

            double roll = rng.NextDouble() * totalWeight;
            foreach (DialogueLine line in _profile.lines)
            {
                if (!Matches(line, trigger, targetNpcId, tilt)) continue;
                roll -= line.weight;
                if (roll <= 0) return line.text;
            }
            return null;
        }

        private static bool Matches(DialogueLine line, DialogueTrigger trigger, string targetNpcId, float tilt) =>
            line.trigger == trigger &&
            tilt >= line.minTilt &&
            (string.IsNullOrEmpty(line.targetNpcId) || line.targetNpcId == targetNpcId);
    }
}
