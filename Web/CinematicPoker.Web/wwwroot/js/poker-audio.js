// Sound effects (Kenney CC0 casino pack), per-backdrop background music
// (owner-supplied saloon loop + OpenGameArt CC0 beach/western/disco beds),
// original character voice lines synthesised with Kokoro TTS, and the
// dealer calls from B. Patrick's Poker Dealer voice pack (dry takes).
// Everything is MP3 (iOS Safari cannot decode Ogg Vorbis) and short
// clips play through WebAudio: once the context is unlocked by the first tap,
// timer-driven sounds (NPC chatter, opponent actions, dealer calls) keep
// working on iOS, where HTMLAudio.play() outside a user gesture is rejected.
// Failures (autoplay policy before first gesture, missing files) are silent
// by design.
window.pokerAudio = (function () {
    let ctx = null;
    const buffers = {};       // url -> Promise<AudioBuffer>
    let muted = false;
    let music = null;
    let voiceSrc = null;      // one character voice at a time so lines don't overlap
    let lastVoiceAt = 0;
    let aliveSeats = [];
    // Dealer calls queue so a burst (blinds, then a hand name) plays in order
    // instead of stacking. Character chatter waits until the dealer is done.
    let dealerQueue = [];
    let dealerSrc = null;
    let dealerPlaying = false;
    let dealerToken = 0;

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

    // Background music follows the backdrop: tropical on the beach, western
    // in the desert, disco in the club, the saloon loop everywhere else.
    const MUSIC_BY_SCENE = {
        BEACH: 'audio/music-beach.mp3',
        DESERT: 'audio/music-desert.mp3',
        CLUB: 'audio/music-club.mp3'
    };
    let musicUrl = 'audio/saloon-music.mp3';

    // Intro movie audio: the two owner-supplied parts played back to back
    // (desert sweep, then club sweep), each faded in and out. Regular music
    // stays out of the way until the intro finishes or is skipped.
    const INTRO_PARTS = ['audio/music-desert.mp3', 'audio/music-club.mp3'];
    const INTRO_PART_SECONDS = 8;
    let introAudio = null;
    let introPlaying = false;

    function stopIntroAudio() {
        if (introAudio) {
            clearInterval(introAudio._fadeTimer);
            try { introAudio.pause(); } catch (e) { }
            introAudio = null;
        }
        introPlaying = false;
    }

    function playIntroPart(index) {
        if (index >= INTRO_PARTS.length) { stopIntroAudio(); startMusic(); return; }
        const a = new Audio(INTRO_PARTS[index]);
        introAudio = a;
        a.volume = 0;
        const p = a.play();
        if (p) p.catch(function () { });
        const fade = 0.9, t0 = Date.now();
        a._fadeTimer = setInterval(function () {
            if (introAudio !== a) { clearInterval(a._fadeTimer); return; }
            const t = (Date.now() - t0) / 1000;
            const vIn = Math.min(1, t / fade);
            const vOut = Math.max(0, Math.min(1, (INTRO_PART_SECONDS - t) / fade));
            a.volume = (muted ? 0 : 0.4) * Math.min(vIn, vOut);
            if (t >= INTRO_PART_SECONDS) {
                clearInterval(a._fadeTimer);
                try { a.pause(); } catch (e) { }
                playIntroPart(index + 1);
            }
        }, 100);
    }

    function startMusic() {
        if (muted || introPlaying) return;
        try {
            if (music && music._url !== musicUrl) {
                music.pause();
                music = null;
            }
            if (!music) {
                music = new Audio(musicUrl);
                music._url = musicUrl;
                music.loop = true;
                music.volume = 0.12;
            }
            const p = music.play();
            if (p) p.catch(function () { });
        } catch (e) { }
    }
    function stopDealer() {
        dealerToken++;
        dealerQueue = [];
        dealerPlaying = false;
        if (dealerSrc) {
            try { dealerSrc.onended = null; dealerSrc.stop(); } catch (e) { }
            dealerSrc = null;
        }
    }

    function pumpDealer() {
        if (dealerPlaying || muted || !dealerQueue.length) return;
        if (!ensureCtx() || ctx.state !== 'running') return;
        const name = dealerQueue.shift();
        const token = dealerToken;
        dealerPlaying = true;
        try { if (voiceSrc) voiceSrc.stop(); } catch (e) { }
        voiceSrc = null;
        lastVoiceAt = Date.now();
        const src = ctx.createBufferSource();
        dealerSrc = src;
        loadBuffer('audio/dealer/' + name + '.mp3').then(function (buf) {
            if (token !== dealerToken || muted) {
                // This call was cleared (restart, mute). Don't leave the queue
                // stuck if the token is still ours.
                if (token === dealerToken) {
                    dealerPlaying = false;
                    dealerSrc = null;
                }
                return;
            }
            src.buffer = buf;
            src.onended = function () {
                if (token !== dealerToken) return;
                dealerPlaying = false;
                dealerSrc = null;
                lastVoiceAt = Date.now();
                pumpDealer();
            };
            const gain = ctx.createGain();
            gain.gain.value = 1;
            src.connect(gain).connect(ctx.destination);
            src.start();
        }).catch(function () {
            if (token !== dealerToken) return;
            dealerPlaying = false;
            dealerSrc = null;
            pumpDealer();
        });
    }

    // One dealer call. Ignored until the first tap unlocks audio, same as SFX,
    // so a hand that started under the title card does not dump a backlog.
    function dealer(name) {
        if (muted || !name) return;
        if (!ensureCtx() || ctx.state !== 'running') return;
        dealerQueue.push(name);
        pumpDealer();
    }

    // Browsers block audio until the first user gesture, so (re)try on taps.
    // The same tap unlocks the WebAudio context used for SFX and voices.
    document.addEventListener('pointerdown', function () {
        const c = ensureCtx();
        if (c && c.state === 'suspended') {
            const p = c.resume();
            if (p) p.then(function () { pumpDealer(); }).catch(function () { });
        } else {
            pumpDealer();
        }
        startMusic();
    });

    function voice(seat, kind) {   // kind: 'idle' | 'win' | 'lose'
        if (muted || dealerPlaying || dealerQueue.length) return;
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
        dealer: dealer,
        clearDealer: stopDealer,
        // Start the intro movie audio (called on the same tap that starts
        // the camera sweep, so autoplay is already unlocked). The tap's own
        // pointerdown may have started the saloon loop a moment earlier —
        // silence it for the duration.
        playIntro: function () {
            stopIntroAudio();
            introPlaying = true;
            if (music) { music.pause(); music = null; }
            playIntroPart(0);
        },
        // Skip (or finish): drop the intro audio and hand over to the
        // regular backdrop music bed.
        stopIntro: function () {
            stopIntroAudio();
            startMusic();
        },
        // Switch the music bed to match the current backdrop. Called from a
        // click handler, so play() is allowed even before other audio ran.
        setScene: function (name) {
            const url = MUSIC_BY_SCENE[name] || 'audio/saloon-music.mp3';
            if (url === musicUrl) return;
            musicUrl = url;
            const wasPlaying = music && !music.paused;
            if (music) { music.pause(); music = null; }
            if (wasPlaying) startMusic();
        },
        // Short fanfare when a heads-up showdown crowns its winner (steady
        // pitch: unlike SFX, a detuned jingle is instantly noticeable).
        victory: function () {
            if (!muted) playBuffer('audio/victory.mp3', 0.75, 1);
        },
        setAlive: function (seats) { aliveSeats = seats || []; },
        setMuted: function (m) {
            muted = m;
            if (music) { if (m) music.pause(); else startMusic(); }
            if (m && voiceSrc) { try { voiceSrc.stop(); } catch (e) { } voiceSrc = null; }
            if (m) stopDealer();
        }
    };
})();
