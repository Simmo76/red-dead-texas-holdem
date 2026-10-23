using Microsoft.AspNetCore.SignalR;

namespace CinematicPoker.Server;

/// <summary>
/// Pure relay for the friends beta. The host's browser runs the real table
/// (engine, NPCs, dealing); this hub only introduces friends to the host and
/// forwards opaque messages between them. It never looks at cards or chips.
/// </summary>
public sealed class PokerHub : Hub
{
    public const int MaxGuests = 4;
    private static readonly RoomStore Rooms = new();

    public async Task<GuestInfo[]> HostRoom(string roomCode, string hostId)
    {
        Room room = Rooms.GetOrCreate(roomCode);
        lock (room.Gate)
        {
            bool hostAlive = room.HostConnection != null && room.HostId != hostId;
            if (hostAlive)
                throw new HubException("Someone else is already hosting this table.");
            room.HostId = hostId;
            room.HostConnection = Context.ConnectionId;
        }
        Rooms.Track(Context.ConnectionId, room);
        await Groups.AddToGroupAsync(Context.ConnectionId, room.Code + ":host");
        foreach (GuestInfo guest in room.Snapshot())
        {
            if (guest.Connected && guest.ConnectionId != null)
                await Clients.Client(guest.ConnectionId).SendAsync("HostBack");
        }
        return room.Snapshot();
    }

    public async Task<JoinResult> JoinRoom(string roomCode, string playerId, string name)
    {
        Room room = Rooms.Find(roomCode);
        if (room == null || room.HostConnection == null)
            return new JoinResult(false, 0, "This table isn't open. Ask your friend to open the game and send a fresh link.");

        string cleanName = CleanName(name);
        int seat;
        lock (room.Gate)
        {
            seat = room.Seat(playerId, cleanName, Context.ConnectionId, MaxGuests);
        }
        if (seat == 0)
            return new JoinResult(false, 0, "This table already has four friends seated.");

        Rooms.Track(Context.ConnectionId, room);
        await Clients.Client(room.HostConnection).SendAsync("GuestJoined", seat, cleanName, playerId);
        return new JoinResult(true, seat, "");
    }

    public async Task ToHost(string roomCode, string payload)
    {
        Room room = Rooms.Find(roomCode);
        if (room?.HostConnection == null) return;
        int seat = room.SeatOf(Context.ConnectionId);
        if (seat == 0) return;
        await Clients.Client(room.HostConnection).SendAsync("GuestMsg", seat, payload);
    }

    public async Task ToGuest(string roomCode, int seat, string payload)
    {
        Room room = Rooms.Find(roomCode);
        if (room == null || room.HostConnection != Context.ConnectionId) return;
        string connection = room.ConnectionOf(seat);
        if (connection == null) return;
        await Clients.Client(connection).SendAsync("HostMsg", payload);
    }

    public async Task LeaveRoom(string roomCode)
    {
        Room room = Rooms.Find(roomCode);
        if (room == null) return;
        int seat = room.SeatOf(Context.ConnectionId);
        if (seat == 0) return;
        room.Release(seat);
        if (room.HostConnection != null)
            await Clients.Client(room.HostConnection).SendAsync("GuestLeft", seat, true);
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        Room room = Rooms.Untrack(Context.ConnectionId);
        if (room != null)
        {
            if (room.HostConnection == Context.ConnectionId)
            {
                room.HostConnection = null;
                foreach (GuestInfo guest in room.Snapshot())
                {
                    if (guest.Connected && guest.ConnectionId != null)
                        await Clients.Client(guest.ConnectionId).SendAsync("HostGone");
                }
            }
            else
            {
                int seat = room.SeatOf(Context.ConnectionId);
                if (seat != 0)
                {
                    room.Disconnect(seat);
                    if (room.HostConnection != null)
                        await Clients.Client(room.HostConnection).SendAsync("GuestLeft", seat, false);
                }
            }
        }
        await base.OnDisconnectedAsync(exception);
    }

    private static string CleanName(string name)
    {
        string cleaned = string.Join(" ", (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
        cleaned = new string(cleaned.Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_').Take(16).ToArray());
        return cleaned.Length == 0 ? "Friend" : cleaned;
    }
}

public sealed record JoinResult(bool Ok, int Seat, string Error);

public sealed record GuestInfo(int Seat, string PlayerId, string Name, bool Connected, string ConnectionId);

internal sealed class Room
{
    public Room(string code) => Code = code;

    public string Code { get; }
    public object Gate { get; } = new();
    public string HostId { get; set; }
    public string HostConnection { get; set; }
    private readonly GuestInfo[] _guests = new GuestInfo[PokerHub.MaxGuests + 1];

    public int Seat(string playerId, string name, string connectionId, int max)
    {
        for (int seat = 1; seat <= max; seat++)
        {
            if (_guests[seat]?.PlayerId == playerId)
            {
                _guests[seat] = new GuestInfo(seat, playerId, name, true, connectionId);
                return seat;
            }
        }
        for (int seat = 1; seat <= max; seat++)
        {
            if (_guests[seat] == null)
            {
                _guests[seat] = new GuestInfo(seat, playerId, name, true, connectionId);
                return seat;
            }
        }
        for (int seat = 1; seat <= max; seat++)
        {
            if (!_guests[seat].Connected)
            {
                _guests[seat] = new GuestInfo(seat, playerId, name, true, connectionId);
                return seat;
            }
        }
        return 0;
    }

    public int SeatOf(string connectionId)
    {
        lock (Gate)
        {
            for (int seat = 1; seat < _guests.Length; seat++)
                if (_guests[seat]?.ConnectionId == connectionId) return seat;
            return 0;
        }
    }

    public string ConnectionOf(int seat)
    {
        lock (Gate)
        {
            if (seat < 1 || seat >= _guests.Length) return null;
            GuestInfo guest = _guests[seat];
            return guest is { Connected: true } ? guest.ConnectionId : null;
        }
    }

    public void Disconnect(int seat)
    {
        lock (Gate)
        {
            GuestInfo guest = _guests[seat];
            if (guest != null) _guests[seat] = guest with { Connected = false, ConnectionId = null };
        }
    }

    public void Release(int seat)
    {
        lock (Gate) _guests[seat] = null;
    }

    public GuestInfo[] Snapshot()
    {
        lock (Gate) return _guests.Where(g => g != null).ToArray();
    }
}

internal sealed class RoomStore
{
    private readonly Dictionary<string, Room> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Room> _byConnection = new();
    private readonly object _gate = new();

    private static string Normalize(string rawCode)
    {
        string code = new string((rawCode ?? "").Where(char.IsLetterOrDigit).Take(12).ToArray()).ToUpperInvariant();
        if (code.Length < 4) throw new HubException("A valid room code is required.");
        return code;
    }

    public Room GetOrCreate(string rawCode)
    {
        string code = Normalize(rawCode);
        lock (_gate)
        {
            if (!_rooms.TryGetValue(code, out Room room))
                _rooms[code] = room = new Room(code);
            return room;
        }
    }

    public Room Find(string rawCode)
    {
        string code = Normalize(rawCode);
        lock (_gate) return _rooms.TryGetValue(code, out Room room) ? room : null;
    }

    public void Track(string connectionId, Room room)
    {
        lock (_gate) _byConnection[connectionId] = room;
    }

    public Room Untrack(string connectionId)
    {
        lock (_gate)
        {
            if (!_byConnection.Remove(connectionId, out Room room)) return null;
            return room;
        }
    }
}
