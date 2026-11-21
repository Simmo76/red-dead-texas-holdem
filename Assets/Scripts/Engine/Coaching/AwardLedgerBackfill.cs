using System.Collections.Generic;
using CinematicPoker.Engine.Poker;

namespace CinematicPoker.Engine.Coaching
{
    /// <summary>
    /// Rebuilds pot awards from stack deltas when live <see cref="PotAwarded"/>
    /// events were not copied into the hand record (for example a hand that
    /// finished before <see cref="HandReviewRecorder"/> saw HandStarted).
    /// </summary>
    public static class AwardLedgerBackfill
    {
        public static IReadOnlyList<PotAwardRecord> Resolve(CompletedHandRecord hand)
        {
            if (hand?.Awards != null && hand.Awards.Count > 0)
                return hand.Awards;
            return InferFromStackChanges(hand);
        }

        public static List<PotAwardRecord> InferFromStackChanges(CompletedHandRecord hand)
        {
            var inferred = new List<PotAwardRecord>();
            if (hand?.StackChanges == null || hand.StackChanges.Count == 0)
                return inferred;

            var winners = new List<int>();
            var payouts = new Dictionary<int, long>();
            long potTotal = 0;
            foreach (KeyValuePair<int, long> kv in hand.StackChanges)
            {
                if (kv.Value > 0)
                {
                    winners.Add(kv.Key);
                    payouts[kv.Key] = kv.Value;
                }
                else if (kv.Value < 0)
                {
                    potTotal += -kv.Value;
                }
            }

            if (winners.Count == 0)
                return inferred;

            if (potTotal <= 0)
            {
                potTotal = 0;
                foreach (long paid in payouts.Values)
                    potTotal += paid;
            }

            inferred.Add(new PotAwardRecord
            {
                PotIndex = 0,
                Amount = potTotal,
                ByFold = hand.WonByFolds,
                WinnerSeats = winners,
                Payouts = payouts
            });
            return inferred;
        }

        public static void ApplyToHand(CompletedHandRecord hand)
        {
            if (hand == null || hand.Awards.Count > 0)
                return;
            foreach (PotAwardRecord award in InferFromStackChanges(hand))
                hand.Awards.Add(award);
        }
    }
}