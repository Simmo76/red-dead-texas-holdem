using CinematicPoker.Engine.AI;
using CinematicPoker.Engine.Players;
using CinematicPoker.Engine.Poker;
using Microsoft.AspNetCore.SignalR;

namespace CinematicPoker.Server;

public sealed class PokerHub : Hub
{
    private static readonly RoomStore Rooms = new();

    public async Task<TableView> Join(string roomCode, string playerName)
    {
        Room room = Rooms.Get(roomCode);
        int seat = room.Join(Context.ConnectionId, CleanName(playerName));
        await Groups.AddToGroupAsync(Context.ConnectionId, room.Code);
        await room.AdvanceAsync();
        await room.BroadcastAsync(Clients);
        return room.View(seat);
    }

    public async Task Act(string roomCode, string action, long amount = 0)
    {
        Room room = Rooms.Get(roomCode);
        room.Submit(Context.ConnectionId, action, amount);
        await room.AdvanceAsync();
        await room.BroadcastAsync(Clients);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Rooms.TryFind(Context.ConnectionId, out Room room))
        {
            room.Leave(Context.ConnectionId);
            await room.BroadcastAsync(Clients);
        }
        await base.OnDisconnectedAsync(exception);
    }

    private static string CleanName(string? name) =>
        string.IsNullOrWhiteSpace(name)
            ? "Friend"
            : new string(name.Trim().Where(char.IsLetterOrDigit).Take(16).ToArray());
}

internal sealed class RoomStore
{
    private readonly Dictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public Room Get(string rawCode)
    {
        string code = new string((rawCode ?? "").Where(char.IsLetterOrDigit).Take(12).ToArray()).ToUpperInvariant();
        if (code.Length < 4) throw new HubException("A valid room code is required.");
        lock (_gate)
        {
            if (!_rooms.TryGetValue(code, out Room? room))
                _rooms[code] = room = new Room(code);
            return room;
        }
    }

    public bool TryFind(string connectionId, out Room room)
    {
        lock (_gate)
        {
            room = _rooms.Values.FirstOrDefault(r => r.HasConnection(connectionId))!;
            return room != null;
        }
    }
}

internal sealed class Room
{
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _connections = new();
    private readonly PokerGame _game;
    private readonly Dictionary<int, string> _names = new() { [0] = "Host", [1] = "Friend" };
    private bool _started;

    public string Code { get; }

    public Room(string code)
    {
        Code = code;
        var players = new List<PokerPlayer>
        {
            new HumanPlayer("human-0", "Host", 0, TableRules.Default.StartingStack),
            new HumanPlayer("human-1", "Friend", 1, TableRules.Default.StartingStack)
        };
        var profiles = new Func<AIProfile>[]
        {
            AIProfile.Maniac, AIProfile.Regular, AIProfile.Rock, AIProfile.Professional
        };
        for (int i = 0; i < profiles.Length; i++)
            players.Add(new AIPlayer($"npc{i}", $"NPC {i + 1}", i + 2,
                TableRules.Default.StartingStack, profiles[i](), Environment.TickCount + i));
        _game = new PokerGame(TableRules.Default, players, Environment.TickCount);
    }

    public bool HasConnection(string id)
    {
        lock (_gate) return _connections.ContainsKey(id);
    }

    public int Join(string connectionId, string name)
    {
        lock (_gate)
        {
            if (_connections.TryGetValue(connectionId, out int existing)) return existing;
            int seat = _connections.Values.Contains(0) ? 1 : 0;
            if (_connections.Values.Contains(seat))
                throw new HubException("This table already has two human players.");
            _connections[connectionId] = seat;
            _names[seat] = name;
            _game.GetPlayer(seat).Status = PlayerStatus.Active;
            _game.GetPlayer(seat).Stack = TableRules.Default.StartingStack;
            return seat;
        }
    }

    public void Leave(string connectionId)
    {
        lock (_gate) _connections.Remove(connectionId);
    }

    public void Submit(string connectionId, string action, long amount)
    {
        lock (_gate)
        {
            if (!_connections.TryGetValue(connectionId, out int seat))
                throw new HubException("Join the table before acting.");
            if (_game.CurrentSeat != seat)
                throw new HubException("It is not your turn.");
            _game.SubmitAction(ParseAction(action, amount));
        }
    }

    public async Task AdvanceAsync()
    {
        lock (_gate)
        {
            if (!_started)
            {
                _game.StartHand();
                _started = true;
            }

            // The engine remains authoritative. NPC decisions are made on the
            // server, while either human seat pauses the loop for its client.
            for (int i = 0; i < 48 && _game.Phase == GamePhase.HandInProgress; i++)
            {
                PokerPlayer? actor = _game.CurrentPlayer;
                if (actor == null || actor.IsHuman) break;
                var context = DecisionContext.ForCurrentActor(_game, 120);
                _game.SubmitAction(((AIPlayer)actor).DecideAction(context));
            }

            if (_game.Phase == GamePhase.WaitingForHand && !_game.IsSessionOver)
                _game.StartHand();
        }
        await Task.CompletedTask;
    }

    public async Task BroadcastAsync(IHubCallerClients clients)
    {
        List<(string id, int seat)> targets;
        lock (_gate) targets = _connections.Select(p => (p.Key, p.Value)).ToList();
        foreach ((string id, int seat) in targets)
            await clients.Client(id).SendAsync("state", View(seat));
    }

    public TableView View(int viewerSeat)
    {
        lock (_gate)
        {
            PokerRound? round = _game.CurrentRound;
            return new TableView
            {
                Room = Code,
                YourSeat = viewerSeat,
                CurrentSeat = _game.CurrentSeat,
                Pot = round?.PotTotal ?? 0,
                Board = round?.Board.Select(CardText).ToArray() ?? Array.Empty<string>(),
                Hole = round?.GetSeat(viewerSeat).HoleCards.Select(CardText).ToArray() ?? Array.Empty<string>(),
                Players = _game.Players.Select(p => new PlayerView
                {
                    Seat = p.Seat,
                    Name = p.Seat < 2 ? _names[p.Seat] : p.Name,
                    IsHuman = p.IsHuman,
                    Connected = p.Seat >= 2 || _connections.Values.Contains(p.Seat),
                    Stack = round?.Seats.FirstOrDefault(s => s.Seat == p.Seat)?.Stack ?? p.Stack,
                    Folded = round?.Seats.FirstOrDefault(s => s.Seat == p.Seat)?.Folded ?? false,
                    Bet = round?.Seats.FirstOrDefault(s => s.Seat == p.Seat)?.CommittedThisStreet ?? 0
                }).ToArray(),
                Legal = _game.CurrentSeat == viewerSeat && round != null
                    ? LegalView.From(_game.GetLegalActions())
                    : null,
                Status = _game.CurrentSeat < 0 ? "Dealing…" : $"Seat {_game.CurrentSeat}'s turn"
            };
        }
    }

    private static string CardText(Card card) =>
        $"{Card.RankSymbol(card.Rank)}{Card.SuitSymbol(card.Suit)}";

    private static PlayerAction ParseAction(string raw, long amount) =>
        (raw ?? "").ToLowerInvariant() switch
        {
            "fold" => PlayerAction.Fold(),
            "check" => PlayerAction.Check(),
            "call" => PlayerAction.Call(),
            "bet" => PlayerAction.Bet(amount),
            "raise" => PlayerAction.Raise(amount),
            "allin" => PlayerAction.AllIn(),
            _ => throw new HubException("Unknown poker action.")
        };
}

public sealed class TableView
{
    public string Room { get; set; } = "";
    public int YourSeat { get; set; }
    public int CurrentSeat { get; set; }
    public long Pot { get; set; }
    public string[] Board { get; set; } = Array.Empty<string>();
    public string[] Hole { get; set; } = Array.Empty<string>();
    public PlayerView[] Players { get; set; } = Array.Empty<PlayerView>();
    public LegalView? Legal { get; set; }
    public string Status { get; set; } = "";
}

public sealed class PlayerView
{
    public int Seat { get; set; }
    public string Name { get; set; } = "";
    public bool IsHuman { get; set; }
    public bool Connected { get; set; }
    public long Stack { get; set; }
    public long Bet { get; set; }
    public bool Folded { get; set; }
}

public sealed class LegalView
{
    public bool Fold { get; set; }
    public bool Check { get; set; }
    public bool Call { get; set; }
    public long CallAmount { get; set; }
    public bool Bet { get; set; }
    public bool Raise { get; set; }
    public bool AllIn { get; set; }
    public long Min { get; set; }
    public long Max { get; set; }

    public static LegalView From(LegalActions legal) => new()
    {
        Fold = legal.CanFold, Check = legal.CanCheck, Call = legal.CanCall,
        CallAmount = legal.CallAmount, Bet = legal.CanBet, Raise = legal.CanRaise,
        AllIn = legal.CanAllIn, Min = legal.MinBetOrRaiseTo, Max = legal.MaxBetOrRaiseTo
    };
}
