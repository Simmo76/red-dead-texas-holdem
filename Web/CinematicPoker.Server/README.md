# Multiplayer beta server

A tiny SignalR relay for playing Dead Man's Hand with friends. The host's
browser runs the real table (the same Blazor game as single player, NPCs
included). Friends open the host's invite link, take over an NPC seat, and
exchange table messages through this hub. The hub keeps rooms in memory and
never inspects the game state; restarting the process clears rooms.

The table survives the host leaving: every guest's browser already replays
the full game, so when a host stays away past a grace period (30 s, override
with `DMH_HOST_GRACE_MS`) the hub offers the room to the lowest-seated
connected friend, whose replica becomes the real table and play carries on.
An unanswered offer moves to the next friend every 10 s
(`DMH_TAKEOVER_RETRY_MS`). A host who returns too late keeps their own solo
game and can rejoin the live table through their old invite link.

Run it locally (it also serves the built web app when `DMH_WEB_ROOT` points
at a published `wwwroot`, so a single public URL is enough for phones):

```sh
dotnet run --project Web/CinematicPoker.Server --urls http://localhost:5080
```

When the Blazor app is served from another origin, set
`window.pokerRealtimeEndpoint = "http://localhost:5080/gamehub"` before
`poker-realtime.js` loads.
