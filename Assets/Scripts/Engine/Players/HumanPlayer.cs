namespace CinematicPoker.Engine.Players
{
    /// <summary>
    /// The single human player. Decisions come from the UI layer, which calls
    /// PokerGame.SubmitAction directly when the player taps an action button.
    /// </summary>
    public sealed class HumanPlayer : PokerPlayer
    {
        public override bool IsHuman => true;

        public HumanPlayer(string id, string name, int seat, long stack)
            : base(id, name, seat, stack) { }
    }
}
