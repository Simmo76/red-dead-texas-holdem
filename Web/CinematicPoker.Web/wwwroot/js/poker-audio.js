// Sound effects (Kenney CC0 casino pack), background music (OpenGameArt CC0
// loop) and original character voice lines generated with Piper TTS for this
// project. Everything is MP3 (iOS Safari cannot decode Ogg Vorbis) and short
// clips play through WebAudio: once the context is unlocked by the first tap,
// timer-driven sounds (NPC chatter, opponent actions) keep working on iOS,
// where HTMLAudio.play() outside a user gesture is rejected. Failures
// (autoplay policy before first gesture, missing files) are silent by design.
window.pokerAudio = (function () {
    let ctx = null;
    const buffers = {};       // url -> Promise<AudioBuffer>
    let muted = false;
    let music = null;
    let voiceSrc = null;      // one voice at a time so lines don't overlap
    let lastVoiceAt = 0;
    let aliveSeats = [];

    // Seat order matches the fixed table roster in Home.razor.
    const SEAT_NAMES = [null, 'davo', 'mick', 'shazza', 'bluey', 'kev'];
    const IDLE_VARIANTS = 6;

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

    function playBuffer(url, volume, rate) {
        if (!ensureCtx() || ctx.state !== 'running') return null;
        const src = ctx.createBufferSource();
        loadBuffer(url).then(function (buf) {
            if (muted) return;
            src.buffer = buf;
            if (rate) src.playbackRate.value = rate;
            const gain = ctx.createGain();
            gain.gain.value = volume;
            src.connect(gain).connect(ctx.destination);
            src.start();
        }).catch(function () { });
        return src;
    }

    function play(name, delayMs) {
        if (muted) return;
        if (delayMs) { setTimeout(function () { play(name, 0); }, delayMs); return; }
        // Slight pitch variance so repeated chip/card sounds don't feel mechanical.
        playBuffer('audio/' + name + '.mp3', 0.55, 0.92 + Math.random() * 0.16);
    }

    function startMusic() {
        if (muted) return;
        try {
            if (!music) {
                music = new Audio('audio/saloon-music.mp3');
                music.loop = true;
                music.volume = 0.12;
            }
            const p = music.play();
            if (p) p.catch(function () { });
        } catch (e) { }
    }
    // Browsers block audio until the first user gesture, so (re)try on taps.
    // The same tap unlocks the WebAudio context used for SFX and voices.
    document.addEventListener('pointerdown', function () { ensureCtx(); startMusic(); });

    function voice(seat, kind) {   // kind: 'idle' | 'win' | 'lose'
        if (muted) return;
        const name = SEAT_NAMES[seat];
        if (!name) return;
        const now = Date.now();
        if (now - lastVoiceAt < 4500) return; // don't talk over each other
        lastVoiceAt = now;
        const n = kind === 'idle' ? 1 + Math.floor(Math.random() * IDLE_VARIANTS) : 1;
        try { if (voiceSrc) voiceSrc.stop(); } catch (e) { }
        voiceSrc = playBuffer('audio/voices/' + name + '-' + kind + '-' + n + '.mp3', 0.9);
        // Idle chatter gets a matching talking gesture in the 3D scene.
        // (Win/lose lines already come with their own victory/slump moves.)
        if (voiceSrc && kind === 'idle' && window.pokerScene && window.pokerScene.talk) {
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
        setAlive: function (seats) { aliveSeats = seats || []; },
        setMuted: function (m) {
            muted = m;
            if (music) { if (m) music.pause(); else startMusic(); }
            if (m && voiceSrc) { try { voiceSrc.stop(); } catch (e) { } voiceSrc = null; }
        }
    };
})();
