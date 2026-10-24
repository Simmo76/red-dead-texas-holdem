using System.Collections.Generic;
using System.Linq;
using CinematicPoker.Engine.AI;
using CinematicPoker.Engine.Players;
using CinematicPoker.Engine.Poker;
using NUnit.Framework;

namespace CinematicPoker.Engine.Tests
{
    /// <summary>
    /// Multiplayer guests replay the host's table from their own seat: the
    /// seats are relabelled so the guest is seat 0, the deck order and the
    /// actions come from the host. These tests prove that the relabelled
    /// table stays in lockstep with the authoritative one.
    /// </summary>
    [TestFixture]
    public class MirroredTableTests
    {
        private const int Seats = 6;

        private static int Rot(int seat, int by) => ((seat - by) % Seats + Seats) % Seats;

        private static List<PokerPlayer> NewPlayers(int by, long[] stacks, bool[] out_)
        {
            var players = new List<PokerPlayer>();
            for (int host = 0; host < Seats; host++)
            {
                int seat = Rot(host, by);
                var ai = new AIPlayer($"npc{host}", $"P{host}", seat, stacks[host],
                    AIProfile.Regular(), seed: 500 + host);
                if (out_[host]) ai.Status = PlayerStatus.Eliminated;
                players.Add(ai);
            }
            return players;
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void RotatedGuestStaysInLockstepWithHost(int by)
        {
            var stacks = Enumerable.Repeat(1000L, Seats).ToArray();
            var none = new bool[Seats];
            var host = new PokerGame(TableRules.Default, NewPlayers(0, stacks, none), seed: 11);
            var guests = new List<PokerGame>();

            for (int hand = 0; hand < 40 && !host.IsSessionOver; hand++)
            {
                host.StartHand();
                var order = host.CurrentRound.Capture().deck.Select(Card.FromIndex).ToList();

                var currentStacks = host.Players.OrderBy(p => p.Seat).Select(p => p.Stack).ToArray();
                // Rebuild the guest from authoritative per-hand state, as the live client does.
                long[] startStacks = host.Players.OrderBy(p => p.Seat)
                    .Select(p => host.CurrentRound.Seats.FirstOrDefault(s => s.Seat == p.Seat)?.StackAtHandStart ?? p.Stack)
                    .ToArray();
                bool[] eliminated = host.Players.OrderBy(p => p.Seat)
                    .Select(p => host.CurrentRound.Seats.All(s => s.Seat != p.Seat))
                    .ToArray();

                var guest = new PokerGame(TableRules.Default, NewPlayers(by, startStacks, eliminated),
                    seed: 1, deckFactory: () => new Deck(order, 0));
                guest.RestoreProgress(host.HandNumber - 1, -1);
                guest.StartHand(Rot(host.DealerSeat, by));
                guests.Add(guest);

                Assert.AreEqual(host.HandNumber, guest.HandNumber);
                Assert.AreEqual(Rot(host.DealerSeat, by), guest.DealerSeat);
                AssertSameHands(host, guest, by);

                int guard = 0;
                while (host.Phase == GamePhase.HandInProgress)
                {
                    Assert.Less(guard++, 200);
                    Assert.AreEqual(Rot(host.CurrentSeat, by), guest.CurrentSeat);
                    var npc = (AIPlayer)host.CurrentPlayer;
                    var action = npc.DecideAction(DecisionContext.ForCurrentActor(host, 20));
                    host.SubmitAction(action);
                    guest.SubmitAction(action);
                    Assert.AreEqual(host.Phase, guest.Phase);
                }

                foreach (PokerPlayer p in host.Players)
                {
                    PokerPlayer mirror = guest.GetPlayer(Rot(p.Seat, by));
                    Assert.AreEqual(p.Stack, mirror.Stack, $"hand {hand} seat {p.Seat}");
                    Assert.AreEqual(p.Status, mirror.Status);
                }
                Assert.AreEqual(host.CurrentRound.Board.Select(c => c.Index),
                    guest.CurrentRound.Board.Select(c => c.Index));
            }
        }

        [Test]
        public void RotatedSnapshotRestoresTheSameLiveHand()
        {
            var stacks = Enumerable.Repeat(1000L, Seats).ToArray();
            var host = new PokerGame(TableRules.Default, NewPlayers(0, stacks, new bool[Seats]), seed: 5);
            host.StartHand();
            for (int i = 0; i < 3 && host.Phase == GamePhase.HandInProgress; i++)
            {
                var npc = (AIPlayer)host.CurrentPlayer;
                host.SubmitAction(npc.DecideAction(DecisionContext.ForCurrentActor(host, 20)));
            }

            const int by = 3;
            var snapshot = host.CurrentRound.Capture().RotateSeats(by);
            var guest = new PokerGame(TableRules.Default,
                NewPlayers(by, Enumerable.Repeat(1000L, Seats).ToArray(), new bool[Seats]), seed: 9);
            guest.RestoreProgress(host.HandNumber, Rot(host.DealerSeat, by));
            guest.RestoreRound(snapshot);

            Assert.AreEqual(Rot(host.CurrentSeat, by), guest.CurrentSeat);
            AssertSameHands(host, guest, by);
            Assert.AreEqual(host.CurrentRound.PotTotal, guest.CurrentRound.PotTotal);
        }

        private static void AssertSameHands(PokerGame host, PokerGame guest, int by)
        {
            foreach (SeatState seat in host.CurrentRound.Seats)
            {
                SeatState mirror = guest.CurrentRound.GetSeat(Rot(seat.Seat, by));
                Assert.AreEqual(seat.HoleCards.Select(c => c.Index), mirror.HoleCards.Select(c => c.Index));
                Assert.AreEqual(seat.TotalCommitted, mirror.TotalCommitted);
            }
        }
    }
}
