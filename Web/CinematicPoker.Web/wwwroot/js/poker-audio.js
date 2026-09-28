// Dealer voice audio: every sound in the web game is a spoken line from the
// owner-supplied POKER DEALER VOICE PACK (see ASSET_LICENSES.md) — hand
// openers, street and action calls, pot awards and the NPCs' poker-slang
// chatter. The old casino foley, music beds and TTS character voices were
// removed with the switch to this pack. Clips are MP3 (iOS Safari cannot
// decode Ogg Vorbis) and play through WebAudio: once the context is unlocked
// by the first tap, timer-driven lines keep working on iOS, where
// HTMLAudio.play() outside a user gesture is rejected. One dealer channel:
// lines queue (cap 3, extras dropped — a late call is worse than a missed
// one) and play back to back so the dealer never talks over himself.
// Failures (autoplay policy before first gesture, missing files) are silent
// by design.
window.pokerAudio = (function () {
    let ctx = null;
    const buffers = {};       // url -> Promise<AudioBuffer>
    let muted = false;
    let aliveSeats = [];
    let lastChatterAt = 0;

    // Reaction and chatter lines, all from the dealer pack. Idle chatter is
    // poker slang the table might mumble; win/lose lines comment the pot.
    const WIN_LINES = ['nice-hand', 'monster', 'big-hand'];
    const LOSE_LINES = ['bad-beat', 'youre-stuck', 'worst-hand'];
    const IDLE_LINES = ['big-slick', 'cowboys', 'pocket-rockets', 'bullets',
        'ladies', 'nuts', 'deadmans-hand', 'broadway'];

    function ensureCtx() {
        try {
            if (!ctx) ctx = new (window.AudioContext || window.webkitAudioContext)();
            if (ctx.state === 'suspended') { const p = ctx.resume(); if (p) p.catch(function () { }); }
        } catch (e) { }
        return ctx;
    }

    function loadBuffer(url) {
        if (!buffers[url]) {
            buffers[url] = fetch(url)
                .then(function (r) { if (!r.ok) throw new Error(url); return r.arrayBuffer(); })
                .then(function (ab) {
                    // Callback form: Safari's decodeAudioData promise support is patchy.
                    return new Promise(function (res, rej) { ctx.decodeAudioData(ab, res, rej); });
                });
            buffers[url].catch(function () { delete buffers[url]; });
        }
        return buffers[url];
    }

    // ------------------------------------------------ single dealer channel
    const queue = [];
    let current = null;       // { src, stopped } while a line plays or loads

    function finish(entry) {
        if (current !== entry) return;
        current = null;
        setTimeout(pump, 220); // a short breath between lines
    }

    function pump() {
        if (current || !queue.length) return;
        if (!ensureCtx() || ctx.state !== 'running') { queue.length = 0; return; }
        const name = queue.shift();
        const entry = { src: null, stopped: false };
        current = entry;
        loadBuffer('audio/dealer/' + name + '.mp3').then(function (buf) {
            if (muted || entry.stopped || current !== entry) { finish(entry); return; }
            const src = ctx.createBufferSource();
            entry.src = src;
            src.buffer = buf;
            const gain = ctx.createGain();
            gain.gain.value = 0.85;
            src.connect(gain).connect(ctx.destination);
            src.onended = function () { finish(entry); };
            src.start();
        }).catch(function () { finish(entry); });
    }

    function say(name) {
        if (muted || queue.length >= 3) return;
        queue.push(name);
        pump();
    }

    function stopSpeech() {
        queue.length = 0;
        if (current) {
            current.stopped = true;
            try { if (current.src) current.src.stop(); } catch (e) { }
            current = null;
        }
    }

    function play(name, delayMs) {
        if (muted) return;
        if (delayMs) { setTimeout(function () { play(name, 0); }, delayMs); return; }
        say(name);
    }

    // Browsers block audio until the first user gesture, so unlock the
    // WebAudio context on taps.
    document.addEventListener('pointerdown', function () { ensureCtx(); });

    function voice(seat, kind) {   // kind: 'idle' | 'win' | 'lose'
        if (muted) return;
        const lines = kind === 'win' ? WIN_LINES : kind === 'lose' ? LOSE_LINES : IDLE_LINES;
        if (kind === 'idle') {
            // Chatter waits for a quiet dealer and doesn't pile up.
            const now = Date.now();
            if (current || queue.length || now - lastChatterAt < 4500) return;
            lastChatterAt = now;
        }
        say(lines[Math.floor(Math.random() * lines.length)]);
        // Idle chatter gets a matching talking gesture in the 3D scene.
        // (Win/lose lines already come with their own victory/slump moves.)
        if (kind === 'idle' && window.pokerScene && window.pokerScene.talk) {
            window.pokerScene.talk(seat);
        }
    }

    // Random table chatter from a random surviving opponent.
    (function scheduleChatter() {
        setTimeout(function () {
            if (!muted && !document.hidden && aliveSeats.length) {
                voice(aliveSeats[Math.floor(Math.random() * aliveSeats.length)], 'idle');
            }
            scheduleChatter();
        }, 14000 + Math.random() * 16000);
    })();

    return {
        play: play,
        voice: voice,
        // The intro movie audio and per-backdrop music beds went out with the
        // old audio set; these stay as no-ops so existing callers keep working.
        playIntro: function () { },
        stopIntro: function () { },
        setScene: function () { },
        // Heads-up showdown winner gets the dealer's jackpot call.
        victory: function () { say('jackpot'); },
        setAlive: function (seats) { aliveSeats = seats || []; },
        setMuted: function (m) {
            muted = m;
            if (m) stopSpeech();
        }
    };
})();
