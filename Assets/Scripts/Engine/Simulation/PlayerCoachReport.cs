using System.Collections.Generic;
using System.Linq;
using CinematicPoker.Engine.Poker;

namespace CinematicPoker.Engine.Simulation
{
    /// <summary>
    /// A coach-style read of one player's session, computed from the same
    /// hand records the CSV export uses: the classic leak indicators (how
    /// loose preflop, how passive postflop, how showdowns went) plus
    /// plain-English advice a table coach would give. Pure C#, no Unity.
    /// </summary>
    public sealed class PlayerCoachReport
    {
        public int HandsAnalysed;
        /// <summary>Hands where the player voluntarily put chips in preflop
        /// (limp, call or raise — posting a blind doesn't count).</summary>
        public int VoluntaryPreflopHands;
        /// <summary>Hands where the player raised (or raise-jammed) preflop.</summary>
        public int PreflopRaiseHands;
        public int PostflopAggressiveActions; // bets + raises after the flop
        public int PostflopCalls;
        public int ShowdownsReached;
        public int ShowdownsWon;
        public int HandsWon;
        public int PotsWonWithoutShowdown;
        public long NetProfit;
        public long BiggestLoss;      // most negative single-hand stack change
        public int BiggestLossHand;   // sequential hand number of that loss
        public readonly List<string> Advice = new List<string>();

        public double VpipPercent => Percent(VoluntaryPreflopHands, HandsAnalysed);
        public double PfrPercent => Percent(PreflopRaiseHands, HandsAnalysed);

        private static double Percent(int part, int whole) => whole == 0 ? 0 : 100.0 * part / whole;

        public static PlayerCoachReport Build(IReadOnlyList<HandSequenceLog.HandRecord> hands, int seat)
        {
            var report = new PlayerCoachReport();
            foreach (HandSequenceLog.HandRecord hand in hands)
            {
                // Only settled hands; the one still being played is excluded.
                if (hand.StackChanges == null || hand.StackChanges.Count == 0) continue;
                if (!hand.SeatsInHand.Contains(seat) && !hand.HoleCards.ContainsKey(seat)) continue;
                report.HandsAnalysed++;
                report.AnalyseHand(hand, seat);
            }
            report.WriteAdvice();
            return report;
        }

        private void AnalyseHand(HandSequenceLog.HandRecord hand, int seat)
        {
            bool preflop = true;
            long currentBet = 0; // highest commitment on the current street
            bool vpip = false, pfr = false, folded = false;

            foreach (PokerEvent evt in hand.Events)
            {
                switch (evt)
                {
                    case BlindPosted blind:
                        if (blind.Amount > currentBet) currentBet = blind.Amount;
                        break;
                    case FlopDealt _:
                    case TurnDealt _:
                    case RiverDealt _:
                        preflop = false;
                        currentBet = 0;
                        break;
                    case BetPlaced bet:
                        currentBet = bet.ToAmount;
                        if (bet.Seat == seat)
                        {
                            if (preflop) { vpip = true; pfr = true; }
                            else PostflopAggressiveActions++;
                        }
                        break;
                    case PlayerRaised raise:
                        currentBet = raise.ToAmount;
                        if (raise.Seat == seat)
                        {
                            if (preflop) { vpip = true; pfr = true; }
                            else PostflopAggressiveActions++;
                        }
                        break;
                    case PlayerAllIn allIn:
                    {
                        // A shove above the current bet is a raise; at or
                        // below it is a call for less.
                        bool isRaise = allIn.ToAmount > currentBet;
                        if (allIn.ToAmount > currentBet) currentBet = allIn.ToAmount;
                        if (allIn.Seat == seat)
                        {
                            if (preflop) { vpip = true; if (isRaise) pfr = true; }
                            else if (isRaise) PostflopAggressiveActions++;
                            else PostflopCalls++;
                        }
                        break;
                    }
                    case PlayerCalled called:
                        if (called.Seat == seat)
                        {
                            if (preflop) vpip = true;
                            else PostflopCalls++;
                        }
                        break;
                    case PlayerFolded fold:
                        if (fold.Seat == seat) folded = true;
                        break;
                }
            }

            if (vpip) VoluntaryPreflopHands++;
            if (pfr) PreflopRaiseHands++;

            bool won = hand.PotAwards.Any(pa => pa.WinnerSeats != null && pa.WinnerSeats.Contains(seat));
            if (won) HandsWon++;
            if (won && hand.WonByFolds) PotsWonWithoutShowdown++;

            if (!hand.WonByFolds && hand.PotAwards.Count > 0 && !folded)
            {
                ShowdownsReached++;
                if (won) ShowdownsWon++;
            }

            if (hand.StackChanges.TryGetValue(seat, out long delta))
            {
                NetProfit += delta;
                if (delta < BiggestLoss)
                {
                    BiggestLoss = delta;
                    BiggestLossHand = hand.SequentialNumber;
                }
            }
        }

        /// <summary>
        /// Turns the numbers into the things a coach would actually say.
        /// At most four notes, biggest leak first.
        /// </summary>
        private void WriteAdvice()
        {
            if (HandsAnalysed < 6)
            {
                Advice.Add("Too few hands for a reliable read on your game yet - "
                    + "play at least a dozen and export again for proper coaching notes.");
                return;
            }

            int vpip = (int)System.Math.Round(VpipPercent);
            int pfr = (int)System.Math.Round(PfrPercent);

            if (VpipPercent > 34)
            {
                Advice.Add($"You played {vpip}% of your hands preflop - at a six-player table strong "
                    + "players stick to roughly 20-25%. Fold the weak offsuit hands early; "
                    + "chips saved preflop are the cheapest chips you'll ever earn.");
            }
            else if (VpipPercent < 14)
            {
                Advice.Add($"You only played {vpip}% of your hands preflop - that's very tight. "
                    + "Add suited connectors and medium pairs in late position, or the table "
                    + "will steal your blinds all night.");
            }

            if (VoluntaryPreflopHands >= 4 && PreflopRaiseHands * 100 < VoluntaryPreflopHands * 40)
            {
                Advice.Add($"When you did play, you mostly called instead of raising ({pfr}% raised "
                    + $"vs {vpip}% played). Come in with a raise more often - it wins the blinds "
                    + "outright and puts you in charge of the hand.");
            }

            if (PostflopCalls >= 4 && PostflopAggressiveActions * 10 < PostflopCalls * 9)
            {
                Advice.Add($"After the flop you called {PostflopCalls} times but only bet or raised "
                    + $"{PostflopAggressiveActions} - that's paying to see cards. Bet your strong "
                    + "hands for value and let the weak draws go.");
            }
            else if (PostflopAggressiveActions >= 8 && PostflopAggressiveActions > PostflopCalls * 4)
            {
                Advice.Add($"You bet or raised {PostflopAggressiveActions} times postflop against only "
                    + $"{PostflopCalls} calls. Aggression is good, but relentless barrels get "
                    + "picked off - slow down when a passive player suddenly fights back.");
            }

            if (ShowdownsReached >= 4 && ShowdownsWon * 100 < ShowdownsReached * 40)
            {
                Advice.Add($"You reached {ShowdownsReached} showdowns and won just {ShowdownsWon}. "
                    + "That usually means calling big turn and river bets with second-best hands - "
                    + "when the pressure comes on late streets, give your opponent credit.");
            }

            if (NetProfit < 0 && BiggestLoss < 0 && Advice.Count < 4)
            {
                Advice.Add($"Your costliest hand was hand {BiggestLossHand} ({BiggestLoss} chips). "
                    + "Find it in the history below and ask at each call whether the pot odds "
                    + "really justified it - most sessions turn on one or two of these.");
            }

            if (Advice.Count == 0)
            {
                Advice.Add("No glaring leaks this session - your preflop discipline and postflop "
                    + "aggression are both in a healthy range. Keep the pressure up and start "
                    + "noting which opponents fold too much.");
            }
            else if (Advice.Count > 4)
            {
                Advice.RemoveRange(4, Advice.Count - 4);
            }
        }
    }
}
