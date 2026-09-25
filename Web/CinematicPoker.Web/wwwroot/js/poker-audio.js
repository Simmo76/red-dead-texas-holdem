// Sound effects (Kenney CC0 casino pack), background music (OpenGameArt CC0
// loop) and original character voice lines generated with Piper TTS for this
// project. Failures (autoplay policy before first gesture, missing files) are
// silent by design.
window.pokerAudio = (function () {
    const cache = {};
    let muted = false;
    let music = null;
    let voiceEl = null;       // one voice at a time so lines don't overlap
    let lastVoiceAt = 0;
    let aliveSeats = [];

    // Seat order matches the fixed table roster in Home.razor.
    const SEAT_NAMES = [null, 'davo', 'mick', 'shazza', 'bluey', 'kev'];
    const IDLE_VARIANTS = 3;

    function play(name, delayMs) {
        if (muted) return;
        if (delayMs) { setTimeout(function () { play(name, 0); }, delayMs); return; }
        try {
            let base = cache[name];
            if (!base) {
                base = new Audio("audio/" + name + ".ogg");
                base.preload = "auto";
                cache[name] = base;
            }
            // Clone so overlapping sounds don't cut each other off, and vary the
            // pitch slightly so repeated chip/card sounds don't feel mechanical.
            const audio = base.cloneNode();
            audio.volume = 0.55;
            audio.playbackRate = 0.92 + Math.random() * 0.16;
            const p = audio.play();
            if (p) p.catch(function () { });
        } catch (e) { }
    }

    function startMusic() {
        if (muted) return;
        try {
            if (!music) {
                music = new Audio('audio/saloon-music.ogg');
                music.loop = true;
                music.volume = 0.12;
            }
            const p = music.play();
            if (p) p.catch(function () { });
        } catch (e) { }
    }
    // Browsers block audio until the first user gesture, so (re)try on taps.
    document.addEventListener('pointerdown', startMusic);

    function voice(seat, kind) {   // kind: 'idle' | 'win' | 'lose'
        if (muted) return;
        const name = SEAT_NAMES[seat];
        if (!name) return;
        const now = Date.now();
        if (now - lastVoiceAt < 4500) return; // don't talk over each other
        lastVoiceAt = now;
        const n = kind === 'idle' ? 1 + Math.floor(Math.random() * IDLE_VARIANTS) : 1;
        try {
            if (voiceEl) voiceEl.pause();
            voiceEl = new Audio('audio/voices/' + name + '-' + kind + '-' + n + '.ogg');
            voiceEl.volume = 0.9;
            const p = voiceEl.play();
            if (p) p.catch(function () { });
            // Idle chatter gets a matching talking gesture in the 3D scene.
            // (Win/lose lines already come with their own victory/slump moves.)
            if (kind === 'idle' && window.pokerScene && window.pokerScene.talk) {
                window.pokerScene.talk(seat);
            }
        } catch (e) { }
    }

    // Random table chatter from a random surviving opponent.
    (function scheduleChatter() {
        setTimeout(function () {
            if (!muted && aliveSeats.length) {
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
            if (m && voiceEl) voiceEl.pause();
        }
    };
})();
