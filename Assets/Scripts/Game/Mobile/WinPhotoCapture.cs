using System;
using System.Collections;
using CinematicPoker.Engine.Poker;
using CinematicPoker.Game.Presentation;
using UnityEngine;

namespace CinematicPoker.Game.Mobile
{
    /// <summary>
    /// Saves a celebratory photo to the player's phone gallery whenever they
    /// win: each pot they take down (at most one photo per hand, so split and
    /// side pots don't spam the gallery) and the moment they win the whole
    /// table. The screenshot is taken at the end of the frame so the winning
    /// board, chip counts and HUD banner are all in the shot.
    /// </summary>
    public sealed class WinPhotoCapture : MonoBehaviour
    {
        [SerializeField] private bool captureHandWins = true;
        [SerializeField] private bool captureTableWin = true;
        [SerializeField] private int humanSeat;

        private PokerTableController _controller;
        private int _lastCapturedHand = -1;
        private bool _captureQueued;

        private void Start()
        {
            // Scene-serialized setups can drop this next to a controller;
            // the code-generated prototype calls Attach explicitly.
            if (_controller == null)
            {
                var controller = GetComponent<PokerTableController>();
                if (controller != null) Attach(controller);
            }
        }

        public void Attach(PokerTableController controller)
        {
            Detach();
            _controller = controller;
            _controller.EngineEvent += OnEngineEvent;
            _controller.SessionFinished += OnSessionFinished;
        }

        private void OnDestroy() => Detach();

        private void Detach()
        {
            if (_controller == null) return;
            _controller.EngineEvent -= OnEngineEvent;
            _controller.SessionFinished -= OnSessionFinished;
            _controller = null;
        }

        private void OnEngineEvent(PokerEvent evt)
        {
            if (!captureHandWins) return;
            if (!(evt is PotAwarded award)) return;
            if (award.HandNumber == _lastCapturedHand) return;

            bool humanWon = false;
            for (int i = 0; i < award.WinnerSeats.Count; i++)
            {
                if (award.WinnerSeats[i] == humanSeat)
                {
                    humanWon = true;
                    break;
                }
            }
            if (!humanWon) return;

            _lastCapturedHand = award.HandNumber;
            QueueCapture("win");
        }

        private void OnSessionFinished()
        {
            if (!captureTableWin) return;
            if (_controller == null || _controller.Game == null || !_controller.Game.HasHumanWon) return;
            QueueCapture("table-win");
        }

        private void QueueCapture(string label)
        {
            // A table win fires PotAwarded and SessionFinished in the same
            // frame; one screenshot covers both.
            if (_captureQueued || !isActiveAndEnabled) return;
            _captureQueued = true;
            StartCoroutine(CaptureAtEndOfFrame(label));
        }

        private IEnumerator CaptureAtEndOfFrame(string label)
        {
            yield return new WaitForEndOfFrame();
            _captureQueued = false;

            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            byte[] png = shot.EncodeToPNG();
            Destroy(shot);

            string fileName = $"CinematicPoker_{label}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            NativeGallerySaver.SavePng(png, fileName);
        }
    }
}
