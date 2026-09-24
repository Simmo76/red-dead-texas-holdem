using System.Collections.Generic;
using CinematicPoker.Engine.Poker;
using CinematicPoker.Game.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace CinematicPoker.Game.UI
{
    /// <summary>
    /// Minimal heads-up display: pot, stacks, current actor and blinds.
    /// Designed to keep the 3D world visible — this is a video game table,
    /// not a full-screen casino UI. Respects safe areas via SafeAreaFitter.
    /// </summary>
    public sealed class PokerHUD : MonoBehaviour
    {
        [SerializeField] private PokerTableController controller;
        [SerializeField] private Text potLabel;
        [SerializeField] private Text[] stackLabels;   // indexed by seat
        [SerializeField] private GameObject[] turnIndicators;
        [SerializeField] private BetSlider betSlider;

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

        private void LateUpdate()
        {
            if (controller?.Game == null) return;
            PokerGame game = controller.Game;

            long pot = game.Phase == GamePhase.HandInProgress ? game.CurrentRound.PotTotal : 0;
            if (potLabel != null)
                potLabel.text = pot > 0 ? pot.ToString() : string.Empty;
            betSlider?.SetPotSize(pot);

            foreach (var player in game.Players)
            {
                if (player.Seat < stackLabels.Length && stackLabels[player.Seat] != null)
                {
                    // Mid-hand, the live stack is on the seat state; between
                    // hands it is on the session player.
                    SeatState seatState = game.Phase == GamePhase.HandInProgress
                        ? TryGetSeat(game, player.Seat)
                        : null;
                    long stack = seatState?.Stack ?? player.Stack;
                    stackLabels[player.Seat].text = stack.ToString();
                }

                if (player.Seat < turnIndicators.Length && turnIndicators[player.Seat] != null)
                    turnIndicators[player.Seat].SetActive(game.CurrentSeat == player.Seat);
            }
        }

        private static SeatState TryGetSeat(PokerGame game, int seat)
        {
            IReadOnlyList<SeatState> seats = game.CurrentRound.Seats;
            for (int i = 0; i < seats.Count; i++)
                if (seats[i].Seat == seat)
                    return seats[i];
            return null;
        }

        private void OnEngineEvent(PokerEvent evt)
        {
            // Hook for toasts ("Mick raises to 50"), pot animations, etc.
        }
    }
}
