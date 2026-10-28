// Invite links for the friends beta: the host shares /?room=CODE by text,
// email, the native share sheet, or the clipboard.
(function (w) {
    var ROOM_KEY = 'dead-mans-hand-room';
    var HOSTING_KEY = 'dead-mans-hand-hosting';

    // A table lives only as long as a reload cycle (a scene change). Opening
    // the game afresh starts a new table with a new link, so a friend who left
    // an earlier table can never drift back into this one.
    (function startFreshTableUnlessReload() {
        var type = '';
        try {
            var nav = w.performance && w.performance.getEntriesByType('navigation')[0];
            type = nav ? nav.type : '';
        } catch (e) { }
        if (type === 'reload') return;
        try {
            sessionStorage.removeItem(ROOM_KEY);
            sessionStorage.removeItem(HOSTING_KEY);
        } catch (e) { }
    })();

    function randomRoom() {
        if (w.crypto && w.crypto.getRandomValues) {
            var bytes = new Uint8Array(5);
            w.crypto.getRandomValues(bytes);
            return Array.from(bytes, function (b) {
                return b.toString(36).padStart(2, '0');
            }).join('').slice(0, 8).toUpperCase();
        }
        return Math.random().toString(36).slice(2, 10).toUpperCase();
    }

    function ensureRoom() {
        var room = '';
        try { room = sessionStorage.getItem(ROOM_KEY) || ''; } catch (e) { }
        if (!room) {
            room = randomRoom();
            try { sessionStorage.setItem(ROOM_KEY, room); } catch (e) { }
        }
        return room;
    }

    // The host page can reload mid-game (changing the table scene does it).
    // Remember that this tab was hosting so the table re-opens by itself.
    function markHosting() {
        try { sessionStorage.setItem(HOSTING_KEY, '1'); } catch (e) { }
    }

    function hostedRoom() {
        try {
            if (sessionStorage.getItem(HOSTING_KEY) === '1') return sessionStorage.getItem(ROOM_KEY) || '';
        } catch (e) { }
        return '';
    }

    // A guest promoted to host owns the table now: remember the room like a
    // host would (so a reload re-opens it) and drop ?room= from the address
    // bar so this tab stops booting as a guest of its own table.
    function adoptRoom(room) {
        try {
            sessionStorage.setItem(ROOM_KEY, room);
            sessionStorage.setItem(HOSTING_KEY, '1');
        } catch (e) { }
        try {
            var url = new URL(w.location.href);
            if (url.searchParams.has('room')) {
                url.searchParams.delete('room');
                w.history.replaceState(null, '', url.toString());
            }
        } catch (e) { }
    }

    var SCENE_KEY = 'cinematic-poker-table-scene';

    function scene() {
        try { return localStorage.getItem(SCENE_KEY) === 'street' ? 'street' : 'saloon'; } catch (e) { return 'saloon'; }
    }

    // A friend always plays in the host's scene: switch and reload if it differs.
    function followScene(id) {
        var next = id === 'street' ? 'street' : 'saloon';
        if (next === scene()) return false;
        try { localStorage.setItem(SCENE_KEY, next); } catch (e) { return false; }
        w.location.reload();
        return true;
    }

    function inviteUrl(room) {
        var url = new URL('/', w.location.origin);
        url.searchParams.set('room', room);
        return url.toString();
    }

    function roomFromUrl() {
        return new URL(w.location.href).searchParams.get('room') || '';
    }

    async function copy(text) {
        try {
            if (navigator.clipboard && navigator.clipboard.writeText) {
                await navigator.clipboard.writeText(text);
                return 'copied';
            }
        } catch (e) { }
        return 'failed';
    }

    async function share(url, title) {
        if (navigator.share) {
            try {
                await navigator.share({ title: title, text: title, url: url });
                return 'shared';
            } catch (e) {
                if (e && e.name === 'AbortError') return 'cancelled';
            }
        }
        return (await copy(url)) === 'copied' ? 'copied' : 'failed';
    }

    function text(url, name) {
        var body = encodeURIComponent((name || 'Your friend') +
            ' invited you to play Dead Man\'s Hand. Join the table: ' + url);
        w.location.href = 'sms:?&body=' + body;
        return 'opened';
    }

    function email(url, name) {
        var subject = encodeURIComponent((name || 'Your friend') +
            ' invited you to play Dead Man\'s Hand');
        var body = encodeURIComponent((name || 'Your friend') +
            ' invited you to play Dead Man\'s Hand.\n\nJoin the table: ' + url +
            '\n\nEnter your name and take the next open seat.');
        w.location.href = 'mailto:?subject=' + subject + '&body=' + body;
        return 'opened';
    }

    function goHome() {
        w.location.href = w.location.origin + '/';
    }

    w.pokerInvite = {
        goHome: goHome,
        markHosting: markHosting,
        adoptRoom: adoptRoom,
        hostedRoom: hostedRoom,
        scene: scene,
        followScene: followScene,
        ensureRoom: ensureRoom,
        url: inviteUrl,
        roomFromUrl: roomFromUrl,
        copy: copy,
        share: share,
        text: text,
        email: email
    };
})(window);
