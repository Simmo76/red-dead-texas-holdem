(function (w) {
    var connection = null;
    var callback = null;

    function endpoint() {
        // Deploy the API beside the WASM app in production, or set
        // window.pokerRealtimeEndpoint for local beta testing.
        return w.pokerRealtimeEndpoint || (w.location.origin + '/gamehub');
    }

    async function start(room, name, dotnetCallback) {
        if (!w.signalR) throw new Error('Realtime client is unavailable.');
        callback = dotnetCallback;
        connection = new signalR.HubConnectionBuilder()
            .withUrl(endpoint())
            .withAutomaticReconnect()
            .build();
        connection.on('state', function (state) {
            if (callback) callback.invokeMethodAsync('ReceiveState', state);
        });
        await connection.start();
        return await connection.invoke('Join', room, name || 'Friend');
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
