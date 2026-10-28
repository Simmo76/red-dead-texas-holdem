// Friends-beta transport. The host's browser owns the table and the guests
// replay it; this file only moves opaque JSON strings between them via the
// SignalR relay and forwards events into the .NET page.
(function (w) {
    var connection = null;
    var dotnet = null;
    var role = '';
    var currentRoom = '';
    var currentName = '';
    var currentHostName = '';
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
        connection.on('BecomeHost', function () { notify('OnBecomeHost'); });
        connection.onreconnecting(function () { notify('OnLinkState', 'reconnecting'); });
        connection.onreconnected(async function () {
            try {
                if (role === 'host') {
                    var guests = await connection.invoke(
                        'HostRoom', currentRoom, playerId, currentHostName || 'Player');
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

    async function host(room, hostName, dotnetRef) {
        await stop();
        // Older Blazor builds called host(room, dotnetRef). Keep that working.
        if (dotnetRef == null && hostName && typeof hostName === 'object') {
            dotnetRef = hostName;
            hostName = 'Player';
        }
        dotnet = dotnetRef;
        role = 'host';
        currentRoom = room;
        currentHostName = hostName || 'Player';
        playerId = stableId('host', room);
        await connect();
        var guests = await connection.invoke('HostRoom', room, playerId, currentHostName);
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

    // The relay asked this guest to take over a table whose host left. On
    // success the hub frees our guest seat and routes friends' messages here,
    // and this connection behaves as the host from now on (including
    // automatic re-hosting after a reconnect).
    async function claimHost(hostName) {
        if (!connection || role !== 'guest') return JSON.stringify({ ok: false });
        var result;
        try {
            result = await connection.invoke('ClaimHost', currentRoom);
        } catch (e) {
            return JSON.stringify({ ok: false });
        }
        if (result && result.ok) {
            role = 'host';
            currentHostName = hostName || currentName || 'Player';
        }
        return JSON.stringify(result || { ok: false });
    }

    // Tell the relay (and Render logs) that the host sent an invite link.
    // channel: text | email | share | copy
    async function reportInvite(channel) {
        if (!connection || role !== 'host') return;
        try {
            await connection.invoke('ReportInvite', currentRoom, channel || 'unknown');
        } catch (e) { }
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

    // The table-chat log is a fixed-height box; keep the newest line in view
    // after Blazor renders it. No-op when the panel is collapsed.
    function scrollChat() {
        var el = document.getElementById('chat-log');
        if (!el) return;
        el.scrollTop = el.scrollHeight;
    }

    w.pokerRealtime = {
        host: host, join: join, claimHost: claimHost, reportInvite: reportInvite,
        toHost: toHost, toGuest: toGuest,
        leave: leave, stop: stop, isLive: isLive, scrollChat: scrollChat
    };
})(window);
