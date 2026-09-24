using CinematicPoker.Engine.Poker;
using UnityEngine;

namespace CinematicPoker.Game.Presentation
{
    /// <summary>
    /// Presents dealing: moves the dealer button, paces card animations
    /// (Quick vs Cinematic), and triggers deal audio. Purely visual —
    /// the engine has already dealt the cards by the time events arrive.
    /// </summary>
    public sealed class DealerController : MonoBehaviour
    {
        [SerializeField] private PokerTableController controller;
        [SerializeField] private Transform dealerButton;
        [SerializeField] private Transform[] seatButtonAnchors;

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
            if (evt is HandStarted started && dealerButton != null &&
                started.DealerSeat < seatButtonAnchors.Length &&
                seatButtonAnchors[started.DealerSeat] != null)
            {
                dealerButton.position = seatButtonAnchors[started.DealerSeat].position;
            }
        }
    }
}
