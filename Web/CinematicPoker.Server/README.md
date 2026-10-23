# Multiplayer beta server

This is the first server-authoritative multiplayer slice. It keeps rooms in
memory, seats up to five human connections, runs NPCs in the remaining seats, and
publishes each player's private hole cards only to that connection.

Run it locally:

```sh
dotnet run --project Web/CinematicPoker.Server --urls http://localhost:5080
```

When the Blazor app is served from another origin, set
`window.pokerRealtimeEndpoint = "http://localhost:5080/gamehub"` before
`poker-realtime.js` loads. Production should serve both pieces behind the
same origin. Restarting the process clears beta rooms.
