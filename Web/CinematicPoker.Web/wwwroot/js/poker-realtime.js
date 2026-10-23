// Friends-beta transport. The host's browser owns the table and the guests
// replay it; this file only moves opaque JSON strings between them via the
// SignalR relay and forwards events into the .NET page.
(function (w) {
    var connection = null;
    var dotnet = null;
    var role = '';
    var currentRoom = '';
    var currentName = '';
    var playerId = '';

    function randomId() {
        try {
            var bytes = new Uint8Array(12);
            crypto.getRandomValues(bytes);
            return Array.from(bytes, function (b) {
                return b.toString(16).padStart(2, '0');
            }).join('');
        } catch (e) {
            return Math.random().toString(36).slice(2);
        }
    }

    function stableId(kind, room) {
        var key = 'dead-mans-hand-' + kind + '-' + room;
        try {
            var saved = sessionStorage.getItem(key);
            if (saved) return saved;
            saved = randomId();
            sessionStorage.setItem(key, saved);
            return saved;
        } catch (e) {
            return randomId();
        }
    }

    function endpoint() {
        return w.pokerRealtimeEndpoint || (w.location.origin + '/gamehub');
    }

    function notify(method) {
        var args = Array.prototype.slice.call(arguments, 1);
        if (!dotnet) return;
        dotnet.invokeMethodAsync.apply(dotnet, [method].concat(args)).catch(function (e) {
            console.error('Multiplayer callback failed', method, e);
        });
    }

    async function connect() {
        if (!w.signalR) throw new Error('Realtime client is unavailable.');
        connection = new signalR.HubConnectionBuilder()
            // Temporary HTTPS tunnels drop long-lived SSE/WebSocket
            // connections; long polling is slower but dependable. A real
            // deployment can switch back to the default transports.
            .withUrl(endpoint(), { transport: signalR.HttpTransportType.LongPolling })
            .withAutomaticReconnect([0, 1000, 2000, 4000, 8000, 15000])
            .build();
        connection.on('GuestJoined', function (seat, name, id) { notify('OnGuestJoined', seat, name, id); });
        connection.on('GuestLeft', function (seat, permanent) { notify('OnGuestLeft', seat, permanent); });
        connection.on('GuestMsg', function (seat, payload) { notify('OnGuestMessage', seat, payload); });
        connection.on('HostMsg', function (payload) { notify('OnHostMessage', payload); });
        connection.on('HostGone', function () { notify('OnHostGone'); });
        connection.on('HostBack', function () { notify('OnHostBack'); });
        connection.onreconnecting(function () { notify('OnLinkState', 'reconnecting'); });
        connection.onreconnected(async function () {
            try {
                if (role === 'host') {
                    var guests = await connection.invoke('HostRoom', currentRoom, playerId);
                    notify('OnHostReattached', JSON.stringify(guests));
                } else {
                    var result = await connection.invoke('JoinRoom', currentRoom, playerId, currentName);
                    if (result.ok) notify('OnLinkState', 'connected');
                    else notify('OnLinkState', 'rejected');
                }
            } catch (e) {
                notify('OnLinkState', 'rejected');
            }
        });
        connection.onclose(function () { notify('OnLinkState', 'closed'); });
        await connection.start();
    }

    async function host(room, dotnetRef) {
        await stop();
        dotnet = dotnetRef;
        role = 'host';
        currentRoom = room;
        playerId = stableId('host', room);
        await connect();
        var guests = await connection.invoke('HostRoom', room, playerId);
        return JSON.stringify(guests);
    }

    async function join(room, name, dotnetRef) {
        await stop();
        dotnet = dotnetRef;
        role = 'guest';
        currentRoom = room;
        currentName = name || 'Friend';
        playerId = stableId('guest', room);
        await connect();
        var result = await connection.invoke('JoinRoom', room, playerId, currentName);
        return JSON.stringify(result);
    }

    async function toHost(payload) {
        if (!connection || role !== 'guest') return;
        await connection.invoke('ToHost', currentRoom, payload);
    }

    async function toGuest(seat, payload) {
        if (!connection || role !== 'host') return;
        await connection.invoke('ToGuest', currentRoom, seat, payload);
    }

    async function leave() {
        if (connection && role === 'guest') {
            try { await connection.invoke('LeaveRoom', currentRoom); } catch (e) { }
        }
        await stop();
    }

    async function stop() {
        var c = connection;
        connection = null;
        if (c) {
            try { await c.stop(); } catch (e) { }
        }
        dotnet = null;
        role = '';
    }

    function isLive() {
        return !!connection && connection.state === 'Connected';
    }

    w.pokerRealtime = { host: host, join: join, toHost: toHost, toGuest: toGuest, leave: leave, stop: stop, isLive: isLive };
})(window);
