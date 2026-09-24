using System;
using UnityEngine;

namespace CinematicPoker.Game.Profiles
{
    public enum TellContext
    {
        StrongHand,
        WeakHand,
        Bluffing,
        BigDecision,
        Waiting
    }

    [Serializable]
    public sealed class TellDefinition
    {
        [Tooltip("Animation trigger or behaviour id, e.g. 'ChipFiddle', 'CheckCardsAgain', 'GoStill'.")]
        public string behaviourId;

        public TellContext context;

        [Tooltip("Chance the tell plays when the context is true. Never 1: tells must stay probabilistic.")]
        [Range(0f, 0.9f)] public float tellProbability = 0.3f;

        [Tooltip("Chance the tell plays when the context is FALSE (a false tell). Prevents 'scratch nose == bluff'.")]
        [Range(0f, 0.5f)] public float falseTellProbability = 0.1f;

        [Tooltip("0 = fires regardless of pot size; 1 = only in big, tense moments.")]
        [Range(0f, 1f)] public float contextSensitivity = 0.5f;
    }

    /// <summary>
    /// Character-specific physical tells. Probabilistic and imperfect by design:
    /// players can gradually learn opponents, but tells never become
    /// deterministic cheats. Non-human characters (aliens, robots) simply use
    /// different behaviourIds (ear movement, colour shift...).
    /// </summary>
    [CreateAssetMenu(menuName = "Cinematic Poker/Tell Profile", fileName = "TellProfile")]
    public sealed class TellProfileAsset : ScriptableObject
    {
        public TellDefinition[] tells = Array.Empty<TellDefinition>();

        /// <summary>Roll the dice for a tell in the given context. May return a false tell.</summary>
        public TellDefinition Sample(TellContext context, float tension, System.Random rng)
        {
            foreach (TellDefinition tell in tells)
            {
                bool contextMatches = tell.context == context;
                float chance = contextMatches ? tell.tellProbability : tell.falseTellProbability;
                chance *= Mathf.Lerp(1f, tension, tell.contextSensitivity);
                if (rng.NextDouble() < chance)
                    return tell;
            }
            return null;
        }
    }
}
