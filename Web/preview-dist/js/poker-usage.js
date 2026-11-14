// Fire-and-forget session beacons so Render logs can see solo play.
// The Blazor table runs in the browser; without this ping the server
// only hears multiplayer hub traffic.
(function (w) {
    function usageUrl() {
        var hub = w.pokerRealtimeEndpoint || (w.location.origin + '/gamehub');
        try {
            var u = new URL(hub, w.location.href);
            // Same host as the SignalR hub; path is /usage beside /gamehub.
            u.pathname = u.pathname.replace(/\/?gamehub\/?$/i, '/usage');
            if (!/\/usage$/i.test(u.pathname)) u.pathname = '/usage';
            u.search = '';
            u.hash = '';
            return u.toString();
        } catch (e) {
            return w.location.origin + '/usage';
        }
    }

    // mode: "solo" (default) | "solo_resume" | "host_table"
    function report(player, mode) {
        try {
            fetch(usageUrl(), {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    player: player || 'Player',
                    mode: mode || 'solo'
                }),
                keepalive: true,
                mode: 'cors',
                credentials: 'omit'
            }).catch(function () { /* offline / GH Pages preview — ignore */ });
        } catch (e) { }
    }

    w.pokerUsage = {
        report: report,
        // Older Blazor builds called reportSolo(name).
        reportSolo: function (player) { report(player, 'solo'); }
    };
})(window);
