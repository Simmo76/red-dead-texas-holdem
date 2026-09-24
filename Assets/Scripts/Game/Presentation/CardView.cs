using CinematicPoker.Engine.Poker;
using UnityEngine;

namespace CinematicPoker.Game.Presentation
{
    /// <summary>
    /// A physical 3D card. Pooled by PokerTableView — never instantiated and
    /// destroyed in bulk during play. Tap to lift/inspect, tap again to lower.
    /// </summary>
    public sealed class CardView : MonoBehaviour
    {
        [SerializeField] private Renderer faceRenderer;
        [SerializeField] private float inspectLift = 0.15f;

        public Card Card { get; private set; }
        public bool FaceUp { get; private set; }
        public bool Lifted { get; private set; }

        private Vector3 _restPosition;

        public void Bind(Card card, Material faceMaterial)
        {
            Card = card;
            if (faceRenderer != null && faceMaterial != null)
                faceRenderer.sharedMaterial = faceMaterial;
            FaceUp = false;
            Lifted = false;
        }

        public void SetFaceUp(bool faceUp)
        {
            FaceUp = faceUp;
            // Flip animation is driven by the table view / DOTween-free tween later.
            transform.localRotation = faceUp ? Quaternion.identity : Quaternion.Euler(0f, 0f, 180f);
        }

        public void SetRestPosition(Vector3 position)
        {
            _restPosition = position;
            transform.position = position;
        }

        /// <summary>Tap interaction: lift for inspection or lower again.</summary>
        public void ToggleInspect()
        {
            Lifted = !Lifted;
            transform.position = _restPosition + (Lifted ? Vector3.up * inspectLift : Vector3.zero);
        }
    }
}
