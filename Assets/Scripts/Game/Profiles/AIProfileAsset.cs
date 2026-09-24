using CinematicPoker.Engine.AI;
using UnityEngine;

namespace CinematicPoker.Game.Profiles
{
    /// <summary>
    /// Designer-editable wrapper around the engine's pure-C# AIProfile.
    /// Create one asset per NPC personality archetype.
    /// </summary>
    [CreateAssetMenu(menuName = "Cinematic Poker/AI Profile", fileName = "AIProfile")]
    public sealed class AIProfileAsset : ScriptableObject
    {
        [Range(0f, 1f)] public float skill = 0.5f;
        [Range(0f, 1f)] public float aggression = 0.5f;
        [Range(0f, 1f)] public float tightness = 0.5f;
        [Range(0f, 1f)] public float bluffFrequency = 0.15f;
        [Range(0f, 1f)] public float riskTolerance = 0.5f;
        [Range(0f, 1f)] public float tiltSensitivity = 0.5f;
        [Range(0f, 1f)] public float positionAwareness = 0.5f;
        [Range(0f, 1f)] public float opponentAwareness = 0.5f;
        [Range(0f, 1f)] public float potOddsAccuracy = 0.5f;
        [Range(0f, 1f)] public float handReadingAbility = 0.5f;
        [Range(0f, 1f)] public float decisionNoise = 0.1f;
        [Range(0f, 1f)] public float emotionalBias = 0.5f;

        public AIProfile ToProfile() => new AIProfile
        {
            Skill = skill,
            Aggression = aggression,
            Tightness = tightness,
            BluffFrequency = bluffFrequency,
            RiskTolerance = riskTolerance,
            TiltSensitivity = tiltSensitivity,
            PositionAwareness = positionAwareness,
            OpponentAwareness = opponentAwareness,
            PotOddsAccuracy = potOddsAccuracy,
            HandReadingAbility = handReadingAbility,
            DecisionNoise = decisionNoise,
            EmotionalBias = emotionalBias
        };
    }
}
