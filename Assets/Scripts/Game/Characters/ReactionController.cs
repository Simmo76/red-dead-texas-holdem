using CinematicPoker.Game.Profiles;
using UnityEngine;
using Random = System.Random;

namespace CinematicPoker.Game.Characters
{
    /// <summary>
    /// Rolls probabilistic tells and idle behaviours for one NPC. Tells are
    /// character-specific and imperfect (TellProbability / FalseTellProbability):
    /// 'scratch nose == bluff' must never exist.
    /// </summary>
    public sealed class ReactionController : MonoBehaviour
    {
        [SerializeField] private TellProfileAsset tellProfile;
        [SerializeField] private float idleGestureIntervalSeconds = 12f;

        private IPokerCharacter _character;
        private Random _rng;
        private float _nextIdleTime;

        public void Bind(IPokerCharacter character, TellProfileAsset profile, int seed)
        {
            _character = character;
            tellProfile = profile;
            _rng = new Random(seed);
            _nextIdleTime = Time.time + idleGestureIntervalSeconds;
        }

        /// <summary>Called by NPCBehaviourController when the NPC is in a tell-relevant context.</summary>
        public void RollTell(TellContext context, float tension)
        {
            if (tellProfile == null || _character == null || _rng == null) return;
            TellDefinition tell = tellProfile.Sample(context, tension, _rng);
            if (tell != null)
                _character.PlayTell(tell.behaviourId);
        }

        private void Update()
        {
            if (_character == null || _rng == null || Time.time < _nextIdleTime) return;
            _nextIdleTime = Time.time + idleGestureIntervalSeconds * (0.5f + (float)_rng.NextDouble());

            // Ambient life: drink, shuffle chips, adjust clothing, lean...
            PokerGesture[] idles =
            {
                PokerGesture.Drink, PokerGesture.ScratchFace,
                PokerGesture.AdjustClothing, PokerGesture.LeanForward, PokerGesture.LeanBack
            };
            _character.PlayGesture(idles[_rng.Next(idles.Length)]);
        }
    }
}
