(function (w) {
    var connection = null;
    var callback = null;
    var currentRoom = '';
    var currentName = '';
    var playerId = '';

    function stablePlayerId(room) {
        var key = 'dead-mans-hand-player-' + room;
        try {
            var saved = sessionStorage.getItem(key);
            if (saved) return saved;
            var bytes = new Uint8Array(12);
            crypto.getRandomValues(bytes);
            saved = Array.from(bytes, function (b) {
                return b.toString(16).padStart(2, '0');
            }).join('');
            sessionStorage.setItem(key, saved);
            return saved;
        } catch (e) {
            return Math.random().toString(36).slice(2);
        }
    }

    function endpoint() {
        // Deploy the API beside the WASM app in production, or set
        // window.pokerRealtimeEndpoint for local beta testing.
        return w.pokerRealtimeEndpoint || (w.location.origin + '/gamehub');
    }

    async function start(room, name, dotnetCallback) {
        if (!w.signalR) throw new Error('Realtime client is unavailable.');
        callback = dotnetCallback;
        currentRoom = room;
        currentName = name || 'Friend';
        playerId = stablePlayerId(room);
        connection = new signalR.HubConnectionBuilder()
            // The temporary localtunnel proxy drops long-lived SSE/WebSocket
            // connections. Long Polling keeps beta testing reliable over
            // ordinary HTTPS; production can use the default transport.
            .withUrl(endpoint(), { transport: signalR.HttpTransportType.LongPolling })
            .withAutomaticReconnect()
            .build();
        connection.onreconnected(async function () {
            await connection.invoke('Join', currentRoom, currentName, playerId);
        });
        connection.on('state', function (state) {
            if (callback) callback.invokeMethodAsync('ReceiveState', state);
        });
        await connection.start();
        return await connection.invoke('Join', room, currentName, playerId);
    }

    async function act(room, action, amount) {
        if (!connection) throw new Error('The table is not connected.');
        await connection.invoke('Act', room, action, amount || 0);
    }

    async function stop() {
        if (connection) {
            await connection.stop();
            connection = null;
            callback = null;
        }
    }

    w.pokerRealtime = { start: start, act: act, stop: stop };
})(window);
