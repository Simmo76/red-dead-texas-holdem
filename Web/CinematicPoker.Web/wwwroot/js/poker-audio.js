// Sound effects (Kenney CC0 casino pack), per-backdrop background music
// (owner-supplied saloon loop + OpenGameArt CC0 beach/western/disco beds),
// owner-supplied outlaw table voice packs (win / fold-or-lose / random banter),
// and a short dealer call for the player's winning hand (B. Patrick pack).
// Everything is MP3 (iOS Safari cannot decode Ogg Vorbis) and short
// clips play through WebAudio: once the context is unlocked by the first tap,
// timer-driven sounds (NPC chatter, opponent actions) keep
// working on iOS, where HTMLAudio.play() outside a user gesture is rejected.
// Failures (autoplay policy before first gesture, missing files) are silent
// by design.
window.pokerAudio = (function () {
    let ctx = null;
    const buffers = {};       // url -> Promise<AudioBuffer>
    let muted = false;
    let music = null;
    let voiceSrc = null;      // one table voice at a time so lines don't overlap
    let voicePlaying = false;
    let lastVoiceAt = 0;
    let aliveSeats = [];
    let awaitingHuman = false;
    let dealerSrc = null;
    let dealerPlaying = false;

    // Owner-supplied outlaw VO pools (renamed from the Dropbox zip).
    const WIN_CLIPS = 15;
    const LOSE_CLIPS = 14;
    const RANDOM_CLIPS = 22;

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

    function playBuffer(url, volume, rate, onEnded) {
        if (!ensureCtx() || ctx.state !== 'running') return null;
        const src = ctx.createBufferSource();
        loadBuffer(url).then(function (buf) {
            if (muted) { if (onEnded) onEnded(); return; }
            src.buffer = buf;
            if (rate) src.playbackRate.value = rate;
            if (onEnded) {
                src.onended = function () { onEnded(); };
            }
            const gain = ctx.createGain();
            gain.gain.value = volume;
            src.connect(gain).connect(ctx.destination);
            src.start();
        }).catch(function () { if (onEnded) onEnded(); });
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
    let musicUrl = 'audio/saloon-music.mp3?v=2';

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

    // Browsers block audio until the first user gesture, so (re)try on taps.
    // The same tap unlocks the WebAudio context used for SFX and voices.
    document.addEventListener('pointerdown', function () {
        const c = ensureCtx();
        if (c && c.state === 'suspended') {
            const p = c.resume();
            if (p) p.catch(function () { });
        }
        startMusic();
    });

    // One dealer call — used for the player's winning-hand category at pot award.
    // Ignored until the first tap unlocks audio, same as SFX.
    function dealer(name) {
        if (muted || !name) return;
        if (!ensureCtx() || ctx.state !== 'running') return;
        try { if (voiceSrc) voiceSrc.stop(); } catch (e) { }
        voiceSrc = null;
        voicePlaying = false;
        if (dealerSrc) {
            try { dealerSrc.onended = null; dealerSrc.stop(); } catch (e) { }
            dealerSrc = null;
        }
        dealerPlaying = true;
        lastVoiceAt = Date.now();
        const src = ctx.createBufferSource();
        dealerSrc = src;
        loadBuffer('audio/dealer/' + name + '.mp3').then(function (buf) {
            if (muted || dealerSrc !== src) {
                if (dealerSrc === src) {
                    dealerPlaying = false;
                    dealerSrc = null;
                }
                return;
            }
            src.buffer = buf;
            src.onended = function () {
                if (dealerSrc !== src) return;
                dealerPlaying = false;
                dealerSrc = null;
                lastVoiceAt = Date.now();
            };
            const gain = ctx.createGain();
            gain.gain.value = 1;
            src.connect(gain).connect(ctx.destination);
            src.start();
        }).catch(function () {
            if (dealerSrc !== src) return;
            dealerPlaying = false;
            dealerSrc = null;
        });
    }

    function poolUrl(kind, index) {
        const n = index < 10 ? '0' + index : String(index);
        if (kind === 'win') return 'audio/voices/win/win-' + n + '.mp3';
        if (kind === 'lose' || kind === 'fold') return 'audio/voices/lose/lose-' + n + '.mp3';
        return 'audio/voices/random/random-' + n + '.mp3';
    }

    function poolSize(kind) {
        if (kind === 'win') return WIN_CLIPS;
        if (kind === 'lose' || kind === 'fold') return LOSE_CLIPS;
        return RANDOM_CLIPS;
    }

    // kind: 'idle' | 'win' | 'lose' | 'fold'
    // Seat is only used so idle banter can trigger a matching talk gesture.
    function voice(seat, kind) {
        if (muted || dealerPlaying) return;
        if (!ensureCtx() || ctx.state !== 'running') return;
        const eventKind = (kind === 'win' || kind === 'lose' || kind === 'fold') ? kind : 'idle';
        // Don't stack banter over an event line still playing.
        if (voicePlaying && eventKind === 'idle') return;
        const now = Date.now();
        // Event lines can cut in sooner; banter waits for a clear table.
        const gap = eventKind === 'idle' ? 5500 : 1200;
        if (now - lastVoiceAt < gap && eventKind === 'idle') return;
        lastVoiceAt = now;
        const n = 1 + Math.floor(Math.random() * poolSize(eventKind));
        try { if (voiceSrc) voiceSrc.stop(); } catch (e) { }
        voicePlaying = true;
        voiceSrc = playBuffer(poolUrl(eventKind, n), 0.95, 1, function () {
            voicePlaying = false;
            voiceSrc = null;
            lastVoiceAt = Date.now();
        });
        // Idle chatter gets a matching talking gesture in the 3D scene.
        if (eventKind === 'idle' && seat > 0 && window.pokerScene && window.pokerScene.talk) {
            window.pokerScene.talk(seat);
        }
    }

    // Random table banter while waiting on the player's turn (and never over
    // an active win / fold / lose line).
    (function scheduleChatter() {
        setTimeout(function () {
            if (!muted && !document.hidden && awaitingHuman && !voicePlaying && !dealerPlaying) {
                const seat = aliveSeats.length
                    ? aliveSeats[Math.floor(Math.random() * aliveSeats.length)]
                    : 0;
                voice(seat, 'idle');
            }
            scheduleChatter();
        }, 9000 + Math.random() * 12000);
    })();

    return {
        play: play,
        voice: voice,
        dealer: dealer,
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
            const url = MUSIC_BY_SCENE[name] || 'audio/saloon-music.mp3?v=2';
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
        setAwaitingHuman: function (on) { awaitingHuman = !!on; },
        setMuted: function (m) {
            muted = m;
            if (music) { if (m) music.pause(); else startMusic(); }
            if (m && voiceSrc) { try { voiceSrc.stop(); } catch (e) { } voiceSrc = null; voicePlaying = false; }
            if (m && dealerSrc) {
                try { dealerSrc.onended = null; dealerSrc.stop(); } catch (e) { }
                dealerSrc = null;
                dealerPlaying = false;
            }
        }
    };
})();
