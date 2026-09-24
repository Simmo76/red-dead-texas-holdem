using System.Collections.Generic;
using System.Linq;
using CinematicPoker.Engine.Poker;

namespace CinematicPoker.Engine.AI
{
    /// <summary>
    /// Everything an AI needs to make one decision. Built from engine state by
    /// the table controller; the AI never touches PokerGame directly.
    /// </summary>
    public sealed class DecisionContext
    {
        public IReadOnlyList<Card> HoleCards;
        public IReadOnlyList<Card> Board;
        public HandPhase Street;
        public long Pot;
        public long ToCall;
        public long Stack;
        public long BigBlind;
        public long CurrentBet;
        public LegalActions Legal;

        /// <summary>Opponents still in the hand.</summary>
        public int OpponentsInHand;

        /// <summary>Players left to act behind us this street (position proxy: 0 = last to act).</summary>
        public int PlayersBehind;

        /// <summary>Optional model of the most relevant opponent (e.g. the human).</summary>
        public PlayerModel OpponentModel;

        /// <summary>Monte Carlo iterations to spend on this decision (device/mode dependent).</summary>
        public int EquityIterations = 300;

        public double PotOdds => ToCall <= 0 ? 0 : ToCall / (double)(Pot + ToCall);

        /// <summary>Build the context for the seat currently facing a decision.</summary>
        public static DecisionContext ForCurrentActor(PokerGame game, int equityIterations = 300, PlayerModel opponentModel = null)
        {
            PokerRound round = game.CurrentRound;
            SeatState seat = round.GetSeat(round.CurrentSeat);
            LegalActions legal = round.GetLegalActions();

            var seats = round.Seats;
            int myIndex = -1;
            for (int i = 0; i < seats.Count; i++)
                if (seats[i].Seat == seat.Seat) myIndex = i;

            int behind = 0;
            for (int i = myIndex + 1; i < seats.Count; i++)
                if (seats[i].CanStillAct) behind++;

            return new DecisionContext
            {
                HoleCards = seat.HoleCards,
                Board = round.Board,
                Street = round.Phase,
                Pot = round.PotTotal,
                ToCall = System.Math.Max(0, round.CurrentBet - seat.CommittedThisStreet),
                Stack = seat.Stack,
                BigBlind = game.Rules.BigBlind,
                CurrentBet = round.CurrentBet,
                Legal = legal,
                OpponentsInHand = seats.Count(s => s.InHand) - 1,
                PlayersBehind = behind,
                OpponentModel = opponentModel,
                EquityIterations = equityIterations
            };
        }
    }
}
