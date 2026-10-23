// Small, transport-agnostic invite layer for the first multiplayer slice.
// Room state is local until a server-backed realtime table is connected.
(function (w) {
    var ROOM_KEY = 'dead-mans-hand-room';

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

    function inviteUrl(room) {
        var url = new URL('/multiplayer', w.location.origin);
        url.searchParams.set('room', room);
        return url.toString();
    }

    function openTable(url) {
        w.location.href = url;
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

    w.pokerInvite = {
        ensureRoom: ensureRoom,
        url: inviteUrl,
        openTable: openTable,
        roomFromUrl: roomFromUrl,
        copy: copy,
        share: share,
        text: text
    };
})(window);
