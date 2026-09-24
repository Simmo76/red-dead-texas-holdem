using UnityEngine;

namespace CinematicPoker.Game.Characters
{
    /// <summary>
    /// Smooth head/eye gaze. In production this drives an Animation Rigging
    /// multi-aim constraint; the placeholder rotates the head bone directly.
    /// NPCs watch whoever is acting, glance at cards/chips and at each other.
    /// </summary>
    public sealed class CharacterLookController : MonoBehaviour
    {
        [SerializeField] private Transform headBone;
        [SerializeField] private float turnSpeed = 3f;
        [SerializeField] private float maxYawDegrees = 70f;

        private Transform _target;
        private float _weight;

        public void SetTarget(Transform target, float weight)
        {
            _target = target;
            _weight = Mathf.Clamp01(weight);
        }

        private void LateUpdate()
        {
            if (headBone == null || _target == null || _weight <= 0f) return;

            Vector3 toTarget = _target.position - headBone.position;
            if (toTarget.sqrMagnitude < 0.0001f) return;

            Quaternion desired = Quaternion.LookRotation(toTarget);
            Quaternion clamped = Quaternion.RotateTowards(transform.rotation, desired, maxYawDegrees);
            headBone.rotation = Quaternion.Slerp(
                headBone.rotation,
                Quaternion.Slerp(headBone.rotation, clamped, _weight),
                Time.deltaTime * turnSpeed);
        }
    }
}
