using System;
using UnityEngine;

namespace CinematicPoker.Game.Environments
{
    [Serializable]
    public sealed class AmbientEventDefinition
    {
        [Tooltip("Id understood by the environment scene (e.g. 'HorseArrives', 'FridgeCompressor', 'TrainPasses').")]
        public string eventId;

        [Tooltip("Average minutes between occurrences.")]
        public float averageIntervalMinutes = 5f;

        [Tooltip("Only fires between hands (never interrupts a decision).")]
        public bool betweenHandsOnly;

        [Range(0f, 1f)] public float weight = 1f;
    }

    /// <summary>
    /// Non-poker background events for one environment (horse arrives, dog walks
    /// through, rain intensifies...). Scheduled by EnvironmentEventDirector.
    /// These NEVER mutate poker state.
    /// </summary>
    [CreateAssetMenu(menuName = "Cinematic Poker/Environment/Ambient Event Profile", fileName = "AmbientEventProfile")]
    public sealed class AmbientEventProfile : ScriptableObject
    {
        public AmbientEventDefinition[] events = Array.Empty<AmbientEventDefinition>();
    }
}
