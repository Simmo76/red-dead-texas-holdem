// Player name on this device. Survives reload, Restart, and closing the tab.
// localStorage is the main store; a long-lived cookie is the fallback for
// WebViews that drop localStorage between visits.
(function (w) {
    var KEY = 'cinematic-poker-player-name';
    var COOKIE = 'cp_player_name';

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

    w.pokerProfile = { loadName: loadName, saveName: saveName };
    w.__pokerPlayerName = loadName();
})(window);
