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

    // Durable hand-history archive behind the stats CSV export. Separate key
    // from the table checkpoint: RESTART clears the table but never the
    // history, so exports go back as far as this device has records.
    var STATS_KEY = 'dead-mans-hand-stats-v1';
    w.pokerStats = {
        load: function () {
            try { return localStorage.getItem(STATS_KEY) || ''; } catch (e) { return ''; }
        },
        save: function (json) {
            try { localStorage.setItem(STATS_KEY, json); } catch (e) { /* private mode */ }
        },
        clear: function () {
            try { localStorage.removeItem(STATS_KEY); } catch (e) { /* private mode */ }
        }
    };
})(window);
