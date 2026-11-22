// Durable victory certificate gallery (one entry per pot the player won).
// Separate from the table checkpoint and stats archive keys.
(function (w) {
    var KEY = 'dead-mans-hand-wins-v1';
    w.pokerWins = {
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