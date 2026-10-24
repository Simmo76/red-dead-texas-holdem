# Multiplayer beta server

A tiny SignalR relay for playing Dead Man's Hand with friends. The host's
browser runs the real table (the same Blazor game as single player, NPCs
included). Friends open the host's invite link, take over an NPC seat, and
exchange table messages through this hub. The hub keeps rooms in memory and
never inspects the game state; restarting the process clears rooms.

Run it locally (it also serves the built web app when `DMH_WEB_ROOT` points
at a published `wwwroot`, so a single public URL is enough for phones):

```sh
dotnet run --project Web/CinematicPoker.Server --urls http://localhost:5080
```

When the Blazor app is served from another origin, set
`window.pokerRealtimeEndpoint = "http://localhost:5080/gamehub"` before
`poker-realtime.js` loads.
