// Sound effects (Kenney CC0 casino pack), per-backdrop background music
// (bundled Wikimedia Commons saloon music + OpenGameArt CC0 beach/western/disco beds),
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
    let ambience = null;
    let musicTimer = null;
    let voiceSrc = null;      // one table voice at a time so lines don't overlap
    let voicePlaying = false;
    let lastVoiceAt = 0;
    let lastVoiceSeat = -1;
    const lastSeatVoiceAt = {};
    const recentClips = { idle: [], win: [], lose: [] };
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

    // Wikimedia Commons ragtime recordings, re-encoded to MP3 for iOS Safari.
    // Shuffle without repeats, then leave a five-second pause between beds.
    const MUSIC_TRACKS = [
        'audio/saloon/top-liner-rag.mp3',
        'audio/saloon/black-and-white-rag.mp3',
        'audio/saloon/wireless-rag.mp3',
        'audio/saloon/bachelors-button.mp3'
    ];
    const AMBIENCE_URL = 'audio/saloon-bar-ambience.mp3';
    let recentMusic = [];

    function pickMusic() {
        let candidates = MUSIC_TRACKS.filter(function (url) {
            return recentMusic.indexOf(url) === -1;
        });
        if (!candidates.length) {
            recentMusic = [];
            candidates = MUSIC_TRACKS.slice();
        }
        const url = candidates[Math.floor(Math.random() * candidates.length)];
        recentMusic.push(url);
        return url;
    }

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
        if (muted || introPlaying || musicTimer) return;
        try {
            if (!ambience) {
                ambience = new Audio(AMBIENCE_URL);
                ambience.loop = true;
                ambience.volume = 0.08;
                const ambiencePlay = ambience.play();
                if (ambiencePlay) ambiencePlay.catch(function () { });
            }
            if (!music) {
                music = new Audio(pickMusic());
                music.volume = 0.12;
                music.addEventListener('ended', function () {
                    music = null;
                    musicTimer = setTimeout(function () {
                        musicTimer = null;
                        startMusic();
                    }, 5000);
                });
            }
            const p = music.play();
            if (p) p.catch(function () { });
        } catch (e) { }
    }

    function stopMusic() {
        if (musicTimer) {
            clearTimeout(musicTimer);
            musicTimer = null;
        }
        if (music) { try { music.pause(); } catch (e) { } music = null; }
        if (ambience) { try { ambience.pause(); } catch (e) { } ambience = null; }
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

    function clipKey(kind) {
        return (kind === 'win') ? 'win' : (kind === 'lose' || kind === 'fold') ? 'lose' : 'idle';
    }

    // Avoid replaying a line until most of that pool has been heard.
    function pickClip(kind) {
        const size = poolSize(kind);
        const recent = recentClips[clipKey(kind)];
        const hold = Math.max(1, Math.floor(size * 0.7));
        const candidates = [];
        for (let i = 1; i <= size; i++) {
            if (recent.indexOf(i) === -1) candidates.push(i);
        }
        if (!candidates.length) {
            for (let i = 1; i <= size; i++) candidates.push(i);
        }
        const n = candidates[Math.floor(Math.random() * candidates.length)];
        recent.push(n);
        while (recent.length > hold) recent.shift();
        return n;
    }

    function pickIdleSeat() {
        const now = Date.now();
        const fresh = aliveSeats.filter(function (s) {
            return !lastSeatVoiceAt[s] || now - lastSeatVoiceAt[s] >= 22000;
        }).filter(function (s) { return s !== lastVoiceSeat; });
        const pool = fresh.length ? fresh : aliveSeats.filter(function (s) { return s !== lastVoiceSeat; });
        const seats = pool.length ? pool : aliveSeats;
        if (!seats.length) return 0;
        return seats[Math.floor(Math.random() * seats.length)];
    }

    // kind: 'idle' | 'win' | 'lose' | 'fold'
    // Seat is only used so idle banter can trigger a matching talk gesture.
    // James (seat 0) never uses the outlaw pack — that's NPC table talk.
    function voice(seat, kind) {
        if (muted || dealerPlaying || voicePlaying) return;
        if (!ensureCtx() || ctx.state !== 'running') return;
        const eventKind = (kind === 'win' || kind === 'lose' || kind === 'fold') ? kind : 'idle';
        if (seat === 0 && eventKind !== 'idle') return;
        const now = Date.now();
        // Table-wide gap for every line so folds/wins/banter cannot chorus.
        const gap = eventKind === 'idle' ? 18000 : 8000;
        if (now - lastVoiceAt < gap) return;
        if (seat > 0 && lastSeatVoiceAt[seat] && now - lastSeatVoiceAt[seat] < 24000) return;
        lastVoiceAt = now;
        lastVoiceSeat = seat;
        if (seat > 0) lastSeatVoiceAt[seat] = now;
        const n = pickClip(eventKind);
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
                voice(pickIdleSeat(), 'idle');
            }
            scheduleChatter();
        }, 20000 + Math.random() * 18000);
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
            stopMusic();
            playIntroPart(0);
        },
        // Skip (or finish): drop the intro audio and hand over to the
        // regular backdrop music bed.
        stopIntro: function () {
            stopIntroAudio();
            startMusic();
        },
        // Kept for HUD boot sync; only the saloon bed plays for now.
        setScene: function (/* name */) {
            // The saloon is the only available backdrop.
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
            if (m) stopMusic();
            else if (!m) startMusic();
            if (m && voiceSrc) { try { voiceSrc.stop(); } catch (e) { } voiceSrc = null; voicePlaying = false; }
            if (m && dealerSrc) {
                try { dealerSrc.onended = null; dealerSrc.stop(); } catch (e) { }
                dealerSrc = null;
                dealerPlaying = false;
            }
        }
    };
})();
