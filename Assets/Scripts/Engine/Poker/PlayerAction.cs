using System;

namespace CinematicPoker.Engine.Poker
{
    public enum ActionType
    {
        Fold,
        Check,
        Call,
        Bet,
        Raise,
        AllIn
    }

    /// <summary>
    /// An action submitted by a player. For Bet and Raise, <see cref="Amount"/>
    /// is the TOTAL the player will have committed to the current street
    /// after the action ("bet to" / "raise to" semantics).
    /// </summary>
    public readonly struct PlayerAction
    {
        public ActionType Type { get; }
        public long Amount { get; }

        private PlayerAction(ActionType type, long amount)
        {
            Type = type;
            Amount = amount;
        }

        public static PlayerAction Fold() => new PlayerAction(ActionType.Fold, 0);
        public static PlayerAction Check() => new PlayerAction(ActionType.Check, 0);
        public static PlayerAction Call() => new PlayerAction(ActionType.Call, 0);
        public static PlayerAction Bet(long toAmount) => new PlayerAction(ActionType.Bet, toAmount);
        public static PlayerAction Raise(long toAmount) => new PlayerAction(ActionType.Raise, toAmount);
        public static PlayerAction AllIn() => new PlayerAction(ActionType.AllIn, 0);

        public override string ToString() =>
            Type == ActionType.Bet || Type == ActionType.Raise ? $"{Type} to {Amount}" : Type.ToString();
    }

    /// <summary>
    /// The set of legal actions for the player currently facing a decision.
    /// Computed by the engine; presentation and AI must never invent their own.
    /// </summary>
    public sealed class LegalActions
    {
        public bool CanFold { get; internal set; }
        public bool CanCheck { get; internal set; }
        public bool CanCall { get; internal set; }
        /// <summary>Chips the player must add to call (already capped at stack).</summary>
        public long CallAmount { get; internal set; }
        public bool CanBet { get; internal set; }
        public bool CanRaise { get; internal set; }
        /// <summary>Minimum legal total for a bet/raise this street.</summary>
        public long MinBetOrRaiseTo { get; internal set; }
        /// <summary>Maximum legal total for a bet/raise this street (all-in).</summary>
        public long MaxBetOrRaiseTo { get; internal set; }
        public bool CanAllIn { get; internal set; }

        public override string ToString()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (CanFold) parts.Add("Fold");
            if (CanCheck) parts.Add("Check");
            if (CanCall) parts.Add($"Call {CallAmount}");
            if (CanBet) parts.Add($"Bet {MinBetOrRaiseTo}-{MaxBetOrRaiseTo}");
            if (CanRaise) parts.Add($"Raise {MinBetOrRaiseTo}-{MaxBetOrRaiseTo}");
            if (CanAllIn) parts.Add("AllIn");
            return string.Join(", ", parts);
        }
    }

    /// <summary>Thrown when a submitted action violates the rules of the game.</summary>
    public sealed class IllegalActionException : Exception
    {
        public IllegalActionException(string message) : base(message) { }
    }
}
