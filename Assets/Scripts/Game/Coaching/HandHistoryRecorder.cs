using System.Collections.Generic;
using CinematicPoker.Engine.Poker;
using CinematicPoker.Game.Presentation;
using UnityEngine;

namespace CinematicPoker.Game.Coaching
{
    /// <summary>One recorded hand: the raw event stream plus quick summary data.</summary>
    public sealed class RecordedHand
    {
        public int HandNumber;
        public readonly List<PokerEvent> Events = new List<PokerEvent>();
        public bool ReachedShowdown;
        public long BiggestPot;
        public bool HumanInvolvedAtEnd;

        /// <summary>Heuristic: is this hand worth offering for review?</summary>
        public bool IsNotable(long bigBlind) =>
            HumanInvolvedAtEnd && (ReachedShowdown || BiggestPot >= bigBlind * 20);
    }

    /// <summary>
    /// Records every hand's event stream so "Improve My Game" can offer
    /// post-hand review (REVIEW HAND) with equity, pot odds and alternatives.
    /// Recording is passive: it never influences the game.
    /// </summary>
    public sealed class HandHistoryRecorder : MonoBehaviour
    {
        [SerializeField] private PokerTableController controller;
        [SerializeField] private int humanSeat;
        [SerializeField] private int maxStoredHands = 50;

        private readonly LinkedList<RecordedHand> _hands = new LinkedList<RecordedHand>();
        private RecordedHand _current;

        public IEnumerable<RecordedHand> Hands => _hands;
        public RecordedHand LastHand => _hands.Count > 0 ? _hands.Last.Value : null;

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
            if (evt is HandStarted)
            {
                _current = new RecordedHand { HandNumber = evt.HandNumber };
                _hands.AddLast(_current);
                while (_hands.Count > maxStoredHands)
                    _hands.RemoveFirst();
            }

            if (_current == null) return;
            _current.Events.Add(evt);

            switch (evt)
            {
                case ShowdownStarted showdown:
                    _current.ReachedShowdown = true;
                    _current.HumanInvolvedAtEnd = showdown.RevealedHands.ContainsKey(humanSeat);
                    break;
                case PotAwarded award when award.Amount > _current.BiggestPot:
                    _current.BiggestPot = award.Amount;
                    break;
            }
        }
    }
}
