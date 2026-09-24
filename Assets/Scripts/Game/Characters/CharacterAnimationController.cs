using UnityEngine;

namespace CinematicPoker.Game.Characters
{
    /// <summary>
    /// Default humanoid implementation of IPokerCharacter, driving an Animator
    /// with the layered setup from the plan: base pose, poker interaction,
    /// upper-body gesture, facial, and look/IK layers. Large-bodied characters
    /// (sumo rig) provide their own implementation of the same interface.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class CharacterAnimationController : MonoBehaviour, IPokerCharacter
    {
        [SerializeField] private Transform headTransform;
        [SerializeField] private Transform cardAnchor;
        [SerializeField] private Transform chipAnchor;
        [SerializeField] private Transform voiceAnchor;
        [SerializeField] private CharacterLookController lookController;

        private Animator _animator;

        private static readonly int ReactionHash = Animator.StringToHash("Reaction");
        private static readonly int ReactionIntensityHash = Animator.StringToHash("ReactionIntensity");

        public Transform HeadTransform => headTransform;
        public Transform CardAnchor => cardAnchor;
        public Transform ChipAnchor => chipAnchor;
        public Transform VoiceAnchor => voiceAnchor != null ? voiceAnchor : headTransform;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        public void LookAt(Transform target, float weight)
        {
            if (lookController != null)
                lookController.SetTarget(target, weight);
        }

        public void PlayGesture(PokerGesture gesture)
        {
            if (_animator != null)
                _animator.SetTrigger(gesture.ToString());
        }

        public void PlayReaction(PokerReaction reaction, float intensity)
        {
            if (_animator == null) return;
            _animator.SetFloat(ReactionIntensityHash, intensity);
            _animator.SetTrigger(reaction.ToString());
        }

        public void PlayTell(string behaviourId)
        {
            if (_animator != null && !string.IsNullOrEmpty(behaviourId))
                _animator.SetTrigger(behaviourId);
        }
    }
}
