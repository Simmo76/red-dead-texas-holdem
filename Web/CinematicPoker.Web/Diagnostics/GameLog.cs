using System;
using System.Collections.Generic;
using System.Linq;
using CinematicPoker.Engine.Poker;

namespace CinematicPoker.Web.Diagnostics;

/// <summary>
/// Writes tagged lines ("[game] ...") to the browser console, where
/// wwwroot/js/poker-log.js captures them into the copyable debug log.
/// </summary>
public static class GameLog
{
    public static void Info(string source, string message) =>
        Console.WriteLine($"[{source}] {message}");

    public static void Warn(string source, string message) =>
        Console.Error.WriteLine($"[{source}] {message}");

    public static void Error(string source, string message, Exception ex = null) =>
        Console.Error.WriteLine(ex == null ? $"[{source}] {message}" : $"[{source}] {message}: {ex}");

    /// <summary>One-line description of an engine event (all cards included —
    /// this is a debugging log, not something shown to opponents).</summary>
    public static string Describe(PokerEvent evt)
    {
        string body = evt switch
        {
            HandStarted e => $"dealer={e.DealerSeat} sb={e.SmallBlindSeat} bb={e.BigBlindSeat} seats=[{Join(e.SeatsInHand)}]",
            BlindPosted e => $"seat={e.Seat} amount={e.Amount} {(e.IsBigBlind ? "bigBlind" : "smallBlind")}{(e.IsAllIn ? " allIn" : "")}",
            CardDealt e => $"seat={e.Seat} card={e.Card}",
            PlayerChecked e => $"seat={e.Seat}",
            BetPlaced e => $"seat={e.Seat} to={e.ToAmount} added={e.AddedAmount}",
            PlayerCalled e => $"seat={e.Seat} added={e.AddedAmount}",
            PlayerRaised e => $"seat={e.Seat} to={e.ToAmount} added={e.AddedAmount}",
            PlayerFolded e => $"seat={e.Seat}",
            PlayerAllIn e => $"seat={e.Seat} to={e.ToAmount} added={e.AddedAmount}",
            FlopDealt e => $"{e.Card1} {e.Card2} {e.Card3}",
            TurnDealt e => $"{e.Card}",
            RiverDealt e => $"{e.Card}",
            ShowdownStarted e => string.Join(" ", e.RevealedHands.Select(kv => $"seat{kv.Key}=[{string.Join(" ", kv.Value)}]")),
            UncalledBetReturned e => $"seat={e.Seat} amount={e.Amount}",
            PotAwarded e => $"pot={e.PotIndex} amount={e.Amount} winners=[{Join(e.WinnerSeats)}] hand={(e.WinningHand.HasValue ? e.WinningHand.Value.ToString() : "-")}",
            HandCompleted e => $"wonByFolds={e.WonByFolds} stackChanges={string.Join(",", e.StackChanges.Select(kv => $"seat{kv.Key}:{kv.Value:+#;-#;0}"))}",
            PlayerEliminated e => $"seat={e.Seat}",
            _ => "",
        };
        return $"hand#{evt.HandNumber} {evt.GetType().Name} {body}".TrimEnd();
    }

    private static string Join(IEnumerable<int> seats) =>
        seats == null ? "" : string.Join(",", seats);
}
