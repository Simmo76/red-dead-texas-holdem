using System.Collections.Generic;
using CinematicPoker.Engine.Poker;

namespace CinematicPoker.Engine.Coaching
{
    /// <summary>
    /// Post-hand coaching contracts. Decision-time knowledge and revealed
    /// outcome evidence are different types so a lesson cannot see cards the
    /// player did not have.
    /// </summary>
    public static class CoachSchema
    {
        public const int Version = 1;
        public const int HistoryCapacity = 50;
        public const int LargeCallBigBlinds = 8;
        public const int TerminalCallBigBlinds = 5;
        public const double CloseEquityBand = 0.04;
    }

    public enum CoachStreet
    {
        Preflop = 0,
        Flop = 1,
        Turn = 2,
        River = 3
    }

    public enum CoachMode
    {
        Off = 0,
        OnRequest = 1,
        Notable = 2,
        EveryHand = 3
    }

    public enum ReviewStatus
    {
        Ready,
        Partial,
        Unavailable
    }

    /// <summary>One public action. Amounts use the engine's raise-to convention where set.</summary>
    public sealed class PublicAction
    {
        public int Seat { get; set; }
        public string Action { get; set; }
        public long ToAmount { get; set; }
        public long AddedAmount { get; set; }
        public CoachStreet Street { get; set; }
    }

    /// <summary>Public seat facts at a decision. Hole cards are never copied here.</summary>
    public sealed class SeatPublicState
    {
        public int Seat { get; set; }
        public long Stack { get; set; }
        public long TotalCommitted { get; set; }
        public long CommittedThisStreet { get; set; }
        public bool Folded { get; set; }
        public bool AllIn { get; set; }
        public bool HasActedThisStreet { get; set; }
        public bool CanStillAct { get; set; }
    }

    /// <summary>
    /// Immutable copy of what the hero was offered, captured before the action
    /// was accepted. Later board cards and hidden hands are not fields on this type.
    /// </summary>
    public sealed class HeroDecisionSnapshot
    {
        public int SchemaVersion { get; set; } = CoachSchema.Version;
        public string SessionId { get; set; }
        public string HandId { get; set; }
        public int DecisionId { get; set; }
        public int HeroSeat { get; set; }
        public CoachStreet Street { get; set; }
        public List<Card> HeroHoleCards { get; set; } = new List<Card>();
        public List<Card> VisibleBoard { get; set; } = new List<Card>();
        public List<PublicAction> ActionHistory { get; set; } = new List<PublicAction>();
        public List<SeatPublicState> Seats { get; set; } = new List<SeatPublicState>();
        public int ButtonSeat { get; set; }
        public long SmallBlind { get; set; }
        public long BigBlind { get; set; }
        public long PotBefore { get; set; }
        public long IncrementalCall { get; set; }
        public long HeroStack { get; set; }
        public long HeroCommittedTotal { get; set; }
        public long HeroCommittedThisStreet { get; set; }
        public long MinRaiseTo { get; set; }
        public long MaxRaiseTo { get; set; }
        public bool CanFold { get; set; }
        public bool CanCheck { get; set; }
        public bool CanCall { get; set; }
        public bool CanBet { get; set; }
        public bool CanRaise { get; set; }
        public bool CanAllIn { get; set; }
        public int PlayersBehind { get; set; }
        public int ActiveOpponents { get; set; }
        public bool SidePotSpot { get; set; }
    }

    /// <summary>The action the hero actually took. Rejected UI attempts are not recorded.</summary>
    public sealed class AcceptedHeroAction
    {
        public int DecisionId { get; set; }
        public string Action { get; set; }
        public long AddedAmount { get; set; }
        /// <summary>Street total after a bet or raise. Zero for check, call and fold.</summary>
        public long RaiseTo { get; set; }
    }

    public sealed class RevealedHand
    {
        public int Seat { get; set; }
        public List<Card> HoleCards { get; set; } = new List<Card>();
    }

    public sealed class PotAwardRecord
    {
        public int PotIndex { get; set; }
        public long Amount { get; set; }
        public List<int> WinnerSeats { get; set; } = new List<int>();
        public Dictionary<int, long> Payouts { get; set; } = new Dictionary<int, long>();
        public bool ByFold { get; set; }
    }

    public sealed class UncalledReturn
    {
        public int Seat { get; set; }
        public long Amount { get; set; }
    }

    /// <summary>
    /// Sanitized completed hand. Opponent cards appear only when they were
    /// shown at showdown. Deck order and the RNG seed are not stored.
    /// </summary>
    public sealed class CompletedHandRecord
    {
        public int SchemaVersion { get; set; } = CoachSchema.Version;
        public string SessionId { get; set; }
        public string HandId { get; set; }
        public int HandNumber { get; set; }
        public int HeroSeat { get; set; }
        public int ButtonSeat { get; set; }
        public long SmallBlind { get; set; }
        public long BigBlind { get; set; }
        public List<Card> FinalBoard { get; set; } = new List<Card>();
        public List<Card> HeroHoleCards { get; set; } = new List<Card>();
        public List<RevealedHand> RevealedHands { get; set; } = new List<RevealedHand>();
        public bool HeroFolded { get; set; }
        public bool WonByFolds { get; set; }
        public List<PotAwardRecord> Awards { get; set; } = new List<PotAwardRecord>();
        public List<UncalledReturn> Returns { get; set; } = new List<UncalledReturn>();
        public Dictionary<int, long> StartingStacks { get; set; } = new Dictionary<int, long>();
        public Dictionary<int, long> EndingStacks { get; set; } = new Dictionary<int, long>();
        public Dictionary<int, long> StackChanges { get; set; } = new Dictionary<int, long>();
        public Dictionary<int, string> SeatNames { get; set; } = new Dictionary<int, string>();
        public List<HeroDecisionSnapshot> Decisions { get; set; } = new List<HeroDecisionSnapshot>();
        public List<AcceptedHeroAction> HeroActions { get; set; } = new List<AcceptedHeroAction>();
        public long HeroNet { get; set; }
        public bool Complete { get; set; }
    }

    public sealed class EquityEstimate
    {
        public double Share { get; set; }
        public string Method { get; set; }
        public int Samples { get; set; }
        public bool Exact { get; set; }
    }

    public enum LessonKind
    {
        None = 0,
        TerminalAllInCall,
        LargeCall,
        PricedCall,
        FreeCheckFold,
        UnpricedRaise,
        BoardAwareness,
        Neutral
    }

    public sealed class LessonSelection
    {
        public LessonKind Kind { get; set; }
        public HeroDecisionSnapshot Spot { get; set; }
        public AcceptedHeroAction Action { get; set; }
        public string Lesson { get; set; }
    }

    /// <summary>
    /// One decision lesson. Numeric fields stay null when the spot cannot
    /// support them — zero is never used as a stand-in for "unknown".
    /// </summary>
    public sealed class DecisionAdvice
    {
        public int DecisionId { get; set; }
        public string Street { get; set; }
        public string ActionTaken { get; set; }
        public string BoardText { get; set; }
        public string RecordedAction { get; set; }
        public string PriceLine { get; set; }
        public string Lesson { get; set; }
        public string Detail { get; set; }
        public string Method { get; set; }
        public string Caveat { get; set; }
        public string ReasonCode { get; set; }
        public double? EquityShare { get; set; }
        public double? BreakEvenEquity { get; set; }
        public double? CallEv { get; set; }
        public int? SampleCount { get; set; }
        public bool CloseDecision { get; set; }
        public bool Cancelled { get; set; }
    }

    public sealed class HandReview
    {
        public int SchemaVersion { get; set; } = CoachSchema.Version;
        public string HandId { get; set; }
        public ReviewStatus Status { get; set; }
        public string Heading { get; set; }
        public string WhyWon { get; set; }
        public string Lesson { get; set; }
        public string ChipDetail { get; set; }
        public List<string> PotLines { get; set; } = new List<string>();
        public List<string> HighlightCodes { get; set; } = new List<string>();
        public DecisionAdvice KeyDecision { get; set; }
        public string Assumptions { get; set; }
        public LessonKind LessonKind { get; set; }
    }
}
