using System;
using CinematicPoker.Engine.AI;
using CinematicPoker.Engine.Poker;

namespace CinematicPoker.Engine.Players
{
    /// <summary>
    /// An NPC opponent. Owns its poker brain (decision engine + profile),
    /// emotional state, and its model of the human player. Character
    /// presentation (animation, dialogue, tells) lives in the Unity layer's
    /// NPCBehaviourController and only observes this state — it never drives
    /// poker decisions.
    /// </summary>
    public sealed class AIPlayer : PokerPlayer
    {
        public AIProfile Profile { get; }
        public TiltState Tilt { get; } = new TiltState();

        /// <summary>This NPC's read on the human player. Local only.</summary>
        public PlayerModel HumanModel { get; } = new PlayerModel();

        private readonly PokerDecisionEngine _brain;

        public override bool IsHuman => false;

        public AIPlayer(string id, string name, int seat, long stack, AIProfile profile, int seed)
            : base(id, name, seat, stack)
        {
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _brain = new PokerDecisionEngine(seed);
        }

        public PlayerAction DecideAction(DecisionContext context)
        {
            // Skilled, aware NPCs use their model of the human.
            if (Profile.OpponentAwareness > 0.3 && context.OpponentModel == null)
                context.OpponentModel = HumanModel;
            return _brain.Decide(context, Profile, Tilt);
        }

        public void OnHandCompleted(long stackChange, long bigBlind, int biggestWinnerSeat)
        {
            Tilt.OnHandResult(stackChange, bigBlind, Profile, biggestWinnerSeat);
        }
    }
}
