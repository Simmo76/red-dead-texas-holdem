// Player name on this device. Survives reload, Restart, and closing the tab.
// localStorage is the main store; a long-lived cookie is the fallback for
// WebViews that drop localStorage between visits.
(function (w) {
    var KEY = 'cinematic-poker-player-name';
    var COOKIE = 'cp_player_name';
    var HINT_KEY = 'cinematic-poker-board-hint-seen';
    var HINT_COOKIE = 'cp_board_hint_seen';

    function readCookie() {
        var parts = ('; ' + document.cookie).split('; ' + COOKIE + '=');
        if (parts.length < 2) return '';
        try { return decodeURIComponent(parts.pop().split(';').shift() || ''); }
        catch (e) { return ''; }
    }

    function writeCookie(name) {
        document.cookie = COOKIE + '=' + encodeURIComponent(name)
            + '; Max-Age=315360000; Path=/; SameSite=Lax';
    }

    function loadName() {
        var name = '';
        try { name = localStorage.getItem(KEY) || ''; }
        catch (e) { name = ''; }
        if (!name) name = readCookie();
        if (name) {
            try { localStorage.setItem(KEY, name); } catch (e) { /* private mode */ }
            try { writeCookie(name); } catch (e) { /* ignore */ }
        }
        return name;
    }

    function saveName(name) {
        name = (name || '').trim();
        if (!name) return;
        try { localStorage.setItem(KEY, name); } catch (e) { /* private mode */ }
        try { writeCookie(name); } catch (e) { /* ignore */ }
    }

    // One-shot "tap the community cards for a close-up" tutorial hint:
    // remembered on this device the same way the name is, so it only ever
    // shows the first time someone plays here.
    function boardHintSeen() {
        var seen = '';
        try { seen = localStorage.getItem(HINT_KEY) || ''; } catch (e) { seen = ''; }
        if (!seen && ('; ' + document.cookie).indexOf('; ' + HINT_COOKIE + '=1') >= 0) seen = '1';
        return seen === '1';
    }

    function markBoardHintSeen() {
        try { localStorage.setItem(HINT_KEY, '1'); } catch (e) { /* private mode */ }
        try {
            document.cookie = HINT_COOKIE + '=1'
                + '; Max-Age=315360000; Path=/; SameSite=Lax';
        } catch (e) { /* ignore */ }
    }

    w.pokerProfile = {
        loadName: loadName,
        saveName: saveName,
        boardHintSeen: boardHintSeen,
        markBoardHintSeen: markBoardHintSeen
    };
    w.__pokerPlayerName = loadName();
})(window);
