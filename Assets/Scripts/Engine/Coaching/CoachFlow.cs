using System.Collections.Generic;

namespace CinematicPoker.Engine.Coaching
{
    /// <summary>
    /// In-memory ring of sanitized hands. Persistence is the caller's job;
    /// a storage failure leaves this buffer usable.
    /// </summary>
    public sealed class CoachHistoryBuffer
    {
        private readonly List<CompletedHandRecord> _hands = new List<CompletedHandRecord>();

        public CoachHistoryBuffer(int capacity = CoachSchema.HistoryCapacity)
        {
            Capacity = capacity < 1 ? 1 : capacity;
        }

        public int Capacity { get; }
        public IReadOnlyList<CompletedHandRecord> Hands => _hands;
        public CompletedHandRecord Latest => _hands.Count == 0 ? null : _hands[_hands.Count - 1];

        public void Remember(CompletedHandRecord hand)
        {
            if (hand == null || !hand.Complete || string.IsNullOrEmpty(hand.HandId)) return;
            // A hand that just finished is the one REVIEW HAND explains, even
            // when its id was already in the ring.
            for (int i = 0; i < _hands.Count; i++)
            {
                if (_hands[i].HandId == hand.HandId)
                {
                    _hands.RemoveAt(i);
                    break;
                }
            }
            _hands.Add(hand);
            while (_hands.Count > Capacity)
                _hands.RemoveAt(0);
        }

        public void Clear() => _hands.Clear();
    }

    /// <summary>
    /// Open/close rules for the review. Closing asks the table to deal the
    /// next hand at most once, and only when that same hand is still waiting.
    /// </summary>
    public sealed class CoachFlowController
    {
        public bool ReviewOpen { get; private set; }
        public bool OpenedBetweenHands { get; private set; }
        public string OpenedHandId { get; private set; }
        public int RequestId { get; private set; }

        public int Open(bool betweenHands, string handId)
        {
            ReviewOpen = true;
            OpenedBetweenHands = betweenHands;
            OpenedHandId = handId;
            return ++RequestId;
        }

        public void Invalidate()
        {
            ReviewOpen = false;
            OpenedBetweenHands = false;
            OpenedHandId = null;
            RequestId++;
        }

        public bool IsCurrent(int requestId) => ReviewOpen && requestId == RequestId;

        /// <summary>True when the table should advance exactly one hand.</summary>
        public bool Close(bool nextHand, bool stillBetweenHands, string currentHandId)
        {
            if (!ReviewOpen) return false;
            ReviewOpen = false;
            bool advance = nextHand
                && OpenedBetweenHands
                && stillBetweenHands
                && currentHandId == OpenedHandId;
            OpenedBetweenHands = false;
            return advance;
        }
    }
}
