using System.Collections.Generic;
using CinematicPoker.Engine.Poker;

namespace CinematicPoker.Engine.Coaching
{
    /// <summary>The deterministic hands named in the coaching plan. Cards are public facts only.</summary>
    public static class CoachFixtures
    {
        public static CompletedHandRecord FlushOverTwoPair()
        {
            var hand = Base("fixture-a", 0, "You");
            hand.SeatNames[1] = "Opponent";
            hand.FinalBoard = Cards("Ah", "Kh", "8h", "8c", "2s");
            hand.HeroHoleCards = Cards("As", "Kd");
            hand.RevealedHands.Add(Shown(0, "As", "Kd"));
            hand.RevealedHands.Add(Shown(1, "Qh", "Jh"));
            Award(hand, 0, 100, 1, false);
            hand.HeroNet = -50;
            hand.Complete = true;
            return hand;
        }

        public static HeroDecisionSnapshot PricedCall(long potBefore, long call, int opponents = 1)
        {
            return new HeroDecisionSnapshot
            {
                SessionId = "fixture",
                HandId = "fixture-b",
                DecisionId = 1,
                HeroSeat = 0,
                Street = CoachStreet.River,
                HeroHoleCards = Cards("As", "Kd"),
                VisibleBoard = Cards("Ah", "Kh", "8h", "8c", "2s"),
                PotBefore = potBefore,
                IncrementalCall = call,
                HeroStack = 1000,
                HeroCommittedThisStreet = 0,
                BigBlind = 10,
                SmallBlind = 5,
                CanCall = true,
                CanFold = true,
                ActiveOpponents = opponents,
                PlayersBehind = 0,
                SidePotSpot = false
            };
        }

        public static CompletedHandRecord Kicker()
        {
            var hand = Base("fixture-c", 0, "You");
            hand.SeatNames[1] = "Opponent";
            hand.FinalBoard = Cards("Ac", "9d", "7s", "4h", "2c");
            hand.HeroHoleCards = Cards("Ad", "Qc");
            hand.RevealedHands.Add(Shown(0, "Ad", "Qc"));
            hand.RevealedHands.Add(Shown(1, "Ah", "Kc"));
            Award(hand, 0, 80, 1, false);
            hand.HeroNet = -40;
            hand.Complete = true;
            return hand;
        }

        public static CompletedHandRecord BoardPlays()
        {
            var hand = Base("fixture-d", 0, "You");
            hand.SeatNames[1] = "Opponent";
            hand.FinalBoard = Cards("As", "Ks", "Qs", "Js", "Ts");
            hand.HeroHoleCards = Cards("2c", "3d");
            hand.RevealedHands.Add(Shown(0, "2c", "3d"));
            hand.RevealedHands.Add(Shown(1, "4c", "5d"));
            var award = new PotAwardRecord
            {
                PotIndex = 0,
                Amount = 100,
                ByFold = false,
                WinnerSeats = new List<int> { 0, 1 },
                Payouts = new Dictionary<int, long> { { 0, 50 }, { 1, 50 } }
            };
            hand.Awards.Add(award);
            hand.HeroNet = 0;
            hand.Complete = true;
            return hand;
        }

        public static CompletedHandRecord TwoPots()
        {
            var hand = Base("fixture-e", 0, "A");
            hand.SeatNames[0] = "A";
            hand.SeatNames[1] = "B";
            hand.SeatNames[2] = "C";
            hand.Awards.Add(new PotAwardRecord
            {
                PotIndex = 0,
                Amount = 150,
                ByFold = false,
                WinnerSeats = new List<int> { 0 },
                Payouts = new Dictionary<int, long> { { 0, 150 } }
            });
            hand.Awards.Add(new PotAwardRecord
            {
                PotIndex = 1,
                Amount = 100,
                ByFold = false,
                WinnerSeats = new List<int> { 1 },
                Payouts = new Dictionary<int, long> { { 1, 100 } }
            });
            hand.FinalBoard = Cards("Ah", "Kh", "8h", "8c", "2s");
            hand.HeroHoleCards = Cards("As", "Kd");
            hand.RevealedHands.Add(Shown(0, "As", "Kd"));
            hand.RevealedHands.Add(Shown(1, "Qh", "Jh"));
            hand.RevealedHands.Add(Shown(2, "9c", "7d"));
            hand.HeroNet = 100;
            hand.StackChanges[0] = 100;
            hand.StackChanges[1] = 0;
            hand.StackChanges[2] = -100;
            hand.Complete = true;
            return hand;
        }

        public static CompletedHandRecord UncalledReturn()
        {
            var hand = Base("fixture-f", 0, "A");
            hand.SeatNames[1] = "B";
            hand.FinalBoard = Cards("Ah", "Kd", "9c", "5s", "2h");
            hand.HeroHoleCards = Cards("Ac", "Qc");
            hand.RevealedHands.Add(Shown(0, "Ac", "Qc"));
            hand.RevealedHands.Add(Shown(1, "Jd", "Td"));
            Award(hand, 0, 80, 0, false);
            hand.Returns.Add(new UncalledReturn { Seat = 0, Amount = 60 });
            hand.HeroNet = 40;
            hand.Complete = true;
            return hand;
        }

        public static CompletedHandRecord FoldedBlind()
        {
            var hand = Base("fixture-fold", 0, "You");
            hand.SeatNames[1] = "Sam";
            hand.HeroHoleCards = Cards("7c", "2d");
            hand.HeroFolded = true;
            hand.WonByFolds = true;
            Award(hand, 0, 15, 1, true);
            hand.HeroNet = -5;
            hand.Complete = true;
            return hand;
        }

        private static CompletedHandRecord Base(string id, int hero, string heroName)
        {
            return new CompletedHandRecord
            {
                SessionId = "fixture",
                HandId = id,
                HandNumber = 1,
                HeroSeat = hero,
                ButtonSeat = 1,
                SmallBlind = 5,
                BigBlind = 10,
                SeatNames = new Dictionary<int, string> { { hero, heroName } }
            };
        }

        private static List<Card> Cards(params string[] codes)
        {
            var list = new List<Card>();
            foreach (string code in codes) list.Add(Card.Parse(code));
            return list;
        }

        private static RevealedHand Shown(int seat, string a, string b) =>
            new RevealedHand { Seat = seat, HoleCards = Cards(a, b) };

        private static void Award(CompletedHandRecord hand, int index, long amount, int winner, bool byFold)
        {
            hand.Awards.Add(new PotAwardRecord
            {
                PotIndex = index,
                Amount = amount,
                ByFold = byFold,
                WinnerSeats = new List<int> { winner },
                Payouts = new Dictionary<int, long> { { winner, amount } }
            });
        }
    }
}
