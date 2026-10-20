// Browser-local checkpoint storage for the active poker table. The .NET
// engine owns the snapshot format; this file only provides durable storage.
(function (w) {
    var KEY = 'dead-mans-hand-session-v1';
    w.pokerSession = {
        load: function () {
            try { return localStorage.getItem(KEY) || ''; } catch (e) { return ''; }
        },
        save: function (json) {
            try { localStorage.setItem(KEY, json); } catch (e) { /* private mode */ }
        },
        clear: function () {
            try { localStorage.removeItem(KEY); } catch (e) { /* private mode */ }
        }
    };
})(window);
