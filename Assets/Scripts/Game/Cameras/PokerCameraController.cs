using CinematicPoker.Engine.Poker;
using CinematicPoker.Game.Environments;
using CinematicPoker.Game.Presentation;
using UnityEngine;

namespace CinematicPoker.Game.Cameras
{
    /// <summary>
    /// First-person seated presence + occasional cinematic cuts.
    ///
    /// Touch: drag to look (limited pitch/yaw), double-tap to recentre.
    /// Cinematic cameras (WideTable, Showdown, NPCReaction...) are resolved
    /// from named anchors in the environment scene via CameraProfile, so
    /// cinematography is data-driven per environment. Cut budget per hand is
    /// limited by the profile — cuts must not be overused.
    /// </summary>
    public sealed class PokerCameraController : MonoBehaviour
    {
        [SerializeField] private PokerTableController controller;
        [SerializeField] private Transform playerViewPivot;
        [SerializeField] private float maxYaw = 40f;
        [SerializeField] private float maxPitch = 15f;
        [SerializeField] private float lookSensitivity = 0.15f;

        private CameraProfile _profile;
        private Vector2 _lookAngles;
        private int _cutsThisHand;

        public void Configure(CameraProfile profile) => _profile = profile;

        private void OnEnable()
        {
            if (controller != null)
                controller.EngineEvent += OnEngineEvent;
        }

        private void OnDisable()
        {
            if (controller != null)
                controller.EngineEvent -= OnEngineEvent;
        }

        private void OnEngineEvent(PokerEvent evt)
        {
            switch (evt)
            {
                case HandStarted _:
                    _cutsThisHand = 0;
                    break;
                case PlayerAllIn _:
                case ShowdownStarted _:
                    TryCinematicCut(evt is ShowdownStarted ? "Showdown" : "NPCReaction");
                    break;
            }
        }

        private void TryCinematicCut(string anchorId)
        {
            if (controller == null || controller.Mode == PlayMode.Quick) return;
            if (_profile == null || _cutsThisHand >= _profile.maxCutsPerHand) return;
            _cutsThisHand++;
            // Cinemachine virtual camera priority switch by anchor id happens
            // here once environment scenes exist.
        }

        /// <summary>Called by the input layer with drag deltas.</summary>
        public void Look(Vector2 dragDelta)
        {
            _lookAngles.x = Mathf.Clamp(_lookAngles.x + dragDelta.x * lookSensitivity, -maxYaw, maxYaw);
            _lookAngles.y = Mathf.Clamp(_lookAngles.y - dragDelta.y * lookSensitivity, -maxPitch, maxPitch);
            ApplyLook();
        }

        /// <summary>Double-tap: recentre the view.</summary>
        public void ResetLook()
        {
            _lookAngles = Vector2.zero;
            ApplyLook();
        }

        private void ApplyLook()
        {
            if (playerViewPivot != null)
                playerViewPivot.localRotation = Quaternion.Euler(_lookAngles.y, _lookAngles.x, 0f);
        }
    }
}
