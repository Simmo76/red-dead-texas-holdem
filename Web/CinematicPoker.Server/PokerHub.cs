using Microsoft.AspNetCore.SignalR;

namespace CinematicPoker.Server;

/// <summary>
/// Pure relay for the friends beta. The host's browser runs the real table
/// (engine, NPCs, dealing); this hub only introduces friends to the host and
/// forwards opaque messages between them. It never looks at cards or chips.
/// Lifecycle lines (start / invite / join) go to stdout so Render logs show
/// who opened a table, how they invited, and who sat down.
///
/// A table survives its host: every guest's browser already replays the full
/// game, so when the host stays away past a grace period the hub offers the
/// room to the longest-seated connected guest ("BecomeHost"). That guest
/// claims it with ClaimHost and their replica becomes the real table, so the
/// remaining friends keep playing.
/// </summary>
public sealed class PokerHub : Hub
{
    public const int MaxGuests = 4;

    // How long a vanished host can reclaim the table before a friend is asked
    // to take over, and how often an unanswered offer moves to the next
    // friend. Long enough that a host's page reload (changing the table scene
    // does one) never loses them their table. Env overrides exist so local
    // testing doesn't have to sit through the full grace period.
    private static readonly int HostGraceMs = EnvMs("DMH_HOST_GRACE_MS", 30_000);
    private static readonly int TakeoverRetryMs = EnvMs("DMH_TAKEOVER_RETRY_MS", 10_000);

    private static readonly RoomStore Rooms = new();
    private readonly ILogger<PokerHub> _log;
    private readonly IHubContext<PokerHub> _hubContext;

    public PokerHub(ILogger<PokerHub> log, IHubContext<PokerHub> hubContext)
    {
        _log = log;
        _hubContext = hubContext;
    }

    private static int EnvMs(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out int ms) && ms > 0 ? ms : fallback;

    public async Task<GuestInfo[]> HostRoom(string roomCode, string hostId, string hostName = null)
    {
        Room room = Rooms.GetOrCreate(roomCode);
        string cleanHost = CleanName(hostName);
        bool resumed;
        lock (room.Gate)
        {
            bool hostAlive = room.HostConnection != null && room.HostId != hostId;
            if (hostAlive)
                throw new HubException("Someone else is already hosting this table.");
            // Same host reclaiming after a reload/disconnect counts as a resume;
            // a new hostId (or first claim) is a fresh table open.
            resumed = !string.IsNullOrEmpty(room.HostId) && room.HostId == hostId;
            room.HostId = hostId;
            room.HostName = cleanHost;
            room.HostConnection = Context.ConnectionId;
            room.PromotionGeneration++; // the table has a host again: cancel any takeover watch
        }
        Rooms.Track(Context.ConnectionId, room);
        await Groups.AddToGroupAsync(Context.ConnectionId, room.Code + ":host");
        foreach (GuestInfo guest in room.Snapshot())
        {
            if (guest.Connected && guest.ConnectionId != null)
                await Clients.Client(guest.ConnectionId).SendAsync("HostBack");
        }

        if (resumed)
        {
            _log.LogInformation(
                "Game host resumed room={Room} host={Host} hostId={HostId}",
                room.Code, cleanHost, ShortId(hostId));
        }
        else
        {
            _log.LogInformation(
                "Game started at={At:o} room={Room} host={Host} hostId={HostId}",
                DateTimeOffset.UtcNow, room.Code, cleanHost, ShortId(hostId));
        }
        return room.Snapshot();
    }

    /// <summary>
    /// Host reports that they sent (or copied) an invite link. Invitees are
    /// not named by the share sheet / SMS / mailto composers, so we log the
    /// channel; joins below show who actually arrived.
    /// </summary>
    public Task ReportInvite(string roomCode, string channel)
    {
        Room room = Rooms.Find(roomCode);
        if (room == null || room.HostConnection != Context.ConnectionId)
            return Task.CompletedTask;

        string via = CleanChannel(channel);
        _log.LogInformation(
            "Game invite at={At:o} room={Room} host={Host} via={Via}",
            DateTimeOffset.UtcNow, room.Code, room.HostName ?? "Player", via);
        return Task.CompletedTask;
    }

    public async Task<JoinResult> JoinRoom(string roomCode, string playerId, string name)
    {
        Room room = Rooms.Find(roomCode);
        // A room whose host merely dropped (HostId set, connection gone) still
        // accepts joins: the seat is waiting when the host returns, or when a
        // friend takes the table over.
        if (room == null || (room.HostConnection == null && string.IsNullOrEmpty(room.HostId)))
            return new JoinResult(false, 0, "This table isn't open. Ask your friend to open the game and send a fresh link.");

        string cleanName = CleanName(name);
        int seat;
        lock (room.Gate)
        {
            seat = room.Seat(playerId, cleanName, Context.ConnectionId, MaxGuests);
        }
        if (seat == 0)
        {
            _log.LogInformation(
                "Game join rejected room={Room} guest={Guest} host={Host} reason=table_full",
                room.Code, cleanName, room.HostName ?? "Player");
            return new JoinResult(false, 0, "This table already has four friends seated.");
        }

        Rooms.Track(Context.ConnectionId, room);
        if (room.HostConnection != null)
            await Clients.Client(room.HostConnection).SendAsync("GuestJoined", seat, cleanName, playerId);
        else
            StartTakeoverWatch(room); // between hosts: make sure someone gets offered the table
        _log.LogInformation(
            "Game joined at={At:o} room={Room} guest={Guest} seat={Seat} host={Host} guestId={GuestId}",
            DateTimeOffset.UtcNow, room.Code, cleanName, seat, room.HostName ?? "Player", ShortId(playerId));
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

    /// <summary>
    /// A guest who was offered the table (via "BecomeHost") takes over as
    /// host. Their browser already replays the full game, so play continues
    /// with the same stacks and cards. Returns the claimer's old seat plus
    /// the remaining guest roster so the new host can re-map everyone.
    /// </summary>
    public async Task<ClaimResult> ClaimHost(string roomCode)
    {
        Room room = Rooms.Find(roomCode);
        if (room == null) return new ClaimResult(false, 0, Array.Empty<GuestInfo>());

        int seat;
        GuestInfo claimer;
        GuestInfo[] others;
        lock (room.Gate)
        {
            if (room.HostConnection != null)
                return new ClaimResult(false, 0, Array.Empty<GuestInfo>());
            seat = room.SeatOf(Context.ConnectionId);
            if (seat == 0)
                return new ClaimResult(false, 0, Array.Empty<GuestInfo>());
            claimer = room.GuestAt(seat);
            room.HostId = claimer.PlayerId;
            room.HostName = claimer.Name;
            room.HostConnection = Context.ConnectionId;
            room.Release(seat);
            room.PromotionGeneration++; // cancel the takeover watch
            others = room.Snapshot();
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, room.Code + ":host");
        foreach (GuestInfo guest in others)
        {
            if (guest.Connected && guest.ConnectionId != null)
                await Clients.Client(guest.ConnectionId).SendAsync("HostBack");
        }
        _log.LogInformation(
            "Game host takeover at={At:o} room={Room} newHost={Host} fromSeat={Seat}",
            DateTimeOffset.UtcNow, room.Code, claimer.Name ?? "Friend", seat);
        return new ClaimResult(true, seat, others);
    }

    private void StartTakeoverWatch(Room room)
    {
        int generation;
        lock (room.Gate) generation = ++room.PromotionGeneration;
        IHubContext<PokerHub> hub = _hubContext;
        ILogger log = _log;
        _ = Task.Run(() => OfferTakeoverWhileHostAway(hub, log, room, generation));
    }

    // Fire-and-forget per room (generation-guarded, so re-triggers and a
    // returning host simply orphan older watchers). After the grace period,
    // keep offering the table to the lowest-seat connected guest until a
    // host exists again or nobody is left.
    private static async Task OfferTakeoverWhileHostAway(
        IHubContext<PokerHub> hub, ILogger log, Room room, int generation)
    {
        await Task.Delay(HostGraceMs);
        while (true)
        {
            GuestInfo candidate;
            lock (room.Gate)
            {
                if (room.PromotionGeneration != generation || room.HostConnection != null) return;
                candidate = room.Snapshot()
                    .Where(g => g.Connected && g.ConnectionId != null)
                    .OrderBy(g => g.Seat)
                    .FirstOrDefault();
            }
            // Nobody left to promote; a guest who reconnects later restarts
            // the watch from JoinRoom.
            if (candidate == null) return;

            log.LogInformation(
                "Game host takeover offered room={Room} guest={Guest} seat={Seat}",
                room.Code, candidate.Name, candidate.Seat);
            try
            {
                await hub.Clients.Client(candidate.ConnectionId).SendAsync("BecomeHost");
            }
            catch (Exception)
            {
                // The candidate raced away mid-send; the next pass picks anew.
            }
            await Task.Delay(TakeoverRetryMs);
        }
    }

    public async Task LeaveRoom(string roomCode)
    {
        Room room = Rooms.Find(roomCode);
        if (room == null) return;
        int seat = room.SeatOf(Context.ConnectionId);
        if (seat == 0) return;
        string guestName = room.NameOf(seat);
        room.Release(seat);
        if (room.HostConnection != null)
            await Clients.Client(room.HostConnection).SendAsync("GuestLeft", seat, true);
        _log.LogInformation(
            "Game left room={Room} guest={Guest} seat={Seat} host={Host}",
            room.Code, guestName ?? "Friend", seat, room.HostName ?? "Player");
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        Room room = Rooms.Untrack(Context.ConnectionId);
        if (room != null)
        {
            if (room.HostConnection == Context.ConnectionId)
            {
                room.HostConnection = null;
                _log.LogInformation(
                    "Game host disconnected room={Room} host={Host}",
                    room.Code, room.HostName ?? "Player");
                foreach (GuestInfo guest in room.Snapshot())
                {
                    if (guest.Connected && guest.ConnectionId != null)
                        await Clients.Client(guest.ConnectionId).SendAsync("HostGone");
                }
                StartTakeoverWatch(room);
            }
            else
            {
                int seat = room.SeatOf(Context.ConnectionId);
                if (seat != 0)
                {
                    string guestName = room.NameOf(seat);
                    room.Disconnect(seat);
                    if (room.HostConnection != null)
                        await Clients.Client(room.HostConnection).SendAsync("GuestLeft", seat, false);
                    _log.LogInformation(
                        "Game guest disconnected room={Room} guest={Guest} seat={Seat} host={Host}",
                        room.Code, guestName ?? "Friend", seat, room.HostName ?? "Player");
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

    private static string CleanChannel(string channel)
    {
        string raw = (channel ?? "").Trim().ToLowerInvariant();
        return raw switch
        {
            "text" or "sms" => "text",
            "email" or "mail" => "email",
            "share" => "share",
            "copy" or "clipboard" => "copy",
            _ => string.IsNullOrEmpty(raw) ? "unknown" : new string(raw.Where(char.IsLetterOrDigit).Take(16).ToArray())
        };
    }

    private static string ShortId(string id)
    {
        if (string.IsNullOrEmpty(id)) return "?";
        return id.Length <= 8 ? id : id[..8];
    }
}

public sealed record JoinResult(bool Ok, int Seat, string Error);

public sealed record ClaimResult(bool Ok, int Seat, GuestInfo[] Guests);

public sealed record GuestInfo(int Seat, string PlayerId, string Name, bool Connected, string ConnectionId);

internal sealed class Room
{
    public Room(string code) => Code = code;

    public string Code { get; }
    public object Gate { get; } = new();
    public string HostId { get; set; }
    public string HostName { get; set; }
    public string HostConnection { get; set; }

    // Bumped (under Gate) whenever the room gains a host or a new takeover
    // watch starts, so stale watchers notice and stop.
    public int PromotionGeneration { get; set; }

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

    public GuestInfo GuestAt(int seat)
    {
        lock (Gate)
        {
            if (seat < 1 || seat >= _guests.Length) return null;
            return _guests[seat];
        }
    }

    public string NameOf(int seat)
    {
        lock (Gate)
        {
            if (seat < 1 || seat >= _guests.Length) return null;
            return _guests[seat]?.Name;
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
