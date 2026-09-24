using System;

namespace CinematicPoker.Engine.AI
{
    /// <summary>
    /// Personality and skill parameters for an NPC poker brain. All values are
    /// 0..1 unless stated otherwise. Pure data: the Unity layer wraps this in a
    /// ScriptableObject, and environments assign different profiles per NPC.
    /// NPCs are deliberately imperfect — DecisionNoise and the emotional
    /// parameters create human-like mistakes.
    /// </summary>
    [Serializable]
    public sealed class AIProfile
    {
        /// <summary>Overall poker competence. Scales equity accuracy and discipline.</summary>
        public double Skill = 0.5;

        /// <summary>Tendency to bet/raise rather than check/call.</summary>
        public double Aggression = 0.5;

        /// <summary>Hand selection strictness. Higher folds more marginal spots.</summary>
        public double Tightness = 0.5;

        /// <summary>Base probability of turning a weak hand into a bluff.</summary>
        public double BluffFrequency = 0.15;

        /// <summary>Willingness to gamble in high-variance spots.</summary>
        public double RiskTolerance = 0.5;

        /// <summary>How strongly losses push this NPC onto tilt.</summary>
        public double TiltSensitivity = 0.5;

        /// <summary>How much position influences decisions.</summary>
        public double PositionAwareness = 0.5;

        /// <summary>How much opponent tendencies (PlayerModel) influence decisions.</summary>
        public double OpponentAwareness = 0.5;

        /// <summary>Accuracy of pot-odds arithmetic. Lower = sloppier calls.</summary>
        public double PotOddsAccuracy = 0.5;

        /// <summary>Ability to narrow opponent ranges.</summary>
        public double HandReadingAbility = 0.5;

        /// <summary>Random noise applied to perceived equity. Higher = more mistakes.</summary>
        public double DecisionNoise = 0.1;

        /// <summary>How strongly emotional state (tilt, fear, confidence) shifts decisions.</summary>
        public double EmotionalBias = 0.5;

        public AIProfile Clone() => (AIProfile)MemberwiseClone();

        // ---- Presets (used by tests, the simulation and placeholder NPCs) ----

        /// <summary>Loose-passive: calls far too much, rarely raises.</summary>
        public static AIProfile CallingStation() => new AIProfile
        {
            Skill = 0.25, Aggression = 0.2, Tightness = 0.15, BluffFrequency = 0.05,
            RiskTolerance = 0.6, TiltSensitivity = 0.4, PositionAwareness = 0.1,
            OpponentAwareness = 0.1, PotOddsAccuracy = 0.25, HandReadingAbility = 0.2,
            DecisionNoise = 0.2, EmotionalBias = 0.5
        };

        /// <summary>Tight-passive: plays few hands, rarely bluffs.</summary>
        public static AIProfile Rock() => new AIProfile
        {
            Skill = 0.45, Aggression = 0.25, Tightness = 0.85, BluffFrequency = 0.03,
            RiskTolerance = 0.2, TiltSensitivity = 0.3, PositionAwareness = 0.4,
            OpponentAwareness = 0.3, PotOddsAccuracy = 0.5, HandReadingAbility = 0.4,
            DecisionNoise = 0.1, EmotionalBias = 0.3
        };

        /// <summary>Loose-aggressive: raises constantly, bluffs a lot, tilts hard.</summary>
        public static AIProfile Maniac() => new AIProfile
        {
            Skill = 0.35, Aggression = 0.9, Tightness = 0.1, BluffFrequency = 0.4,
            RiskTolerance = 0.9, TiltSensitivity = 0.8, PositionAwareness = 0.2,
            OpponentAwareness = 0.2, PotOddsAccuracy = 0.3, HandReadingAbility = 0.3,
            DecisionNoise = 0.25, EmotionalBias = 0.8
        };

        /// <summary>Strong all-round player (e.g. the Vegas Pro).</summary>
        public static AIProfile Professional() => new AIProfile
        {
            Skill = 0.9, Aggression = 0.65, Tightness = 0.6, BluffFrequency = 0.2,
            RiskTolerance = 0.5, TiltSensitivity = 0.1, PositionAwareness = 0.9,
            OpponentAwareness = 0.9, PotOddsAccuracy = 0.95, HandReadingAbility = 0.9,
            DecisionNoise = 0.03, EmotionalBias = 0.1
        };

        /// <summary>Reasonable recreational player (e.g. a mate at the kitchen table).</summary>
        public static AIProfile Regular() => new AIProfile();
    }
}
