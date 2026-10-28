// Sound effects (Kenney CC0 casino pack), per-backdrop background music
// (bundled Wikimedia Commons saloon music + OpenGameArt CC0 beach/western/disco beds),
// owner-supplied outlaw table voice packs (win / fold-or-lose / random banter),
// and owner-supplied Octagon announcer one-liners,
// a short dealer call for the player's winning hand (B. Patrick pack),
// and an owner-supplied tap played whenever any seat checks.
// Short clips are MP3 or PCM WAV (both are supported by iOS Safari) and
// play through WebAudio: once the context is unlocked by the first tap,
// timer-driven sounds (NPC chatter, opponent actions) keep
// working on iOS, where HTMLAudio.play() outside a user gesture is rejected.
// Failures (autoplay policy before first gesture, missing files) are silent
// by design.
window.pokerAudio = (function () {
    let ctx = null;
    const buffers = {};       // url -> Promise<AudioBuffer>
    let muted = false;
    let music = null;
    let musicFadingOut = null;
    let ambience = null;
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
    let sliderDragSrc = null;
    let sliderDragActive = false;
    let sliderAudioFinished = false;
    let victoryPending = false;
    let playerLossPending = false;
    let checkPending = false;
    let lastCheckAt = 0;
    // 16-bit PCM, same family as the other owner one-shots. The supplied file
    // was a quiet m4a-derived MP3; this copy is level-matched so it reads
    // over the table bed and the CHECK button click.
    const CHECK_URL = 'audio/poker-check-sfx.wav';
    const CHECK_VOLUME = 1.1;

    // Owner-supplied outlaw VO pools (renamed from the Dropbox zip).
    const WIN_CLIPS = 15;
    const LOSE_CLIPS = 14;
    const RANDOM_CLIPS = 22;
    const STREET_VOICE_CLIPS = Array.from({ length: 16 }, function (_, i) {
        const n = i + 1 < 10 ? '0' + (i + 1) : String(i + 1);
        return 'audio/street/voices/street-voice-' + n + '.wav';
    });

    function ensureCtx() {
        try {
            if (!ctx) ctx = new (window.AudioContext || window.webkitAudioContext)();
            if (ctx.state === 'suspended') { const p = ctx.resume(); if (p) p.catch(function () { }); }
        } catch (e) { }
        return ctx;
    }

    function flushPendingResultAudio() {
        if (muted || (!victoryPending && !playerLossPending && !checkPending)) return;
        const c = ensureCtx();
        if (!c || c.state !== 'running') return;
        if (victoryPending) {
            victoryPending = false;
            playBuffer('audio/victory.mp3', 0.75, 1);
        }
        if (playerLossPending) {
            playerLossPending = false;
            playBuffer('audio/player-loss.wav', 2.25, 1);
        }
        if (checkPending) {
            checkPending = false;
            playBuffer(CHECK_URL, CHECK_VOLUME, 1);
        }
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

    function playBuffer(url, volume, rate, onEnded, loop) {
        if (!ensureCtx() || ctx.state !== 'running') return null;
        const src = ctx.createBufferSource();
        loadBuffer(url).then(function (buf) {
            if (src._cancelled || muted) { if (onEnded) onEnded(); return; }
            src.buffer = buf;
            if (rate) src.playbackRate.value = rate;
            src.loop = !!loop;
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

    function stopSliderDrag() {
        sliderDragActive = false;
        sliderAudioFinished = false;
        if (!sliderDragSrc) return;
        sliderDragSrc._cancelled = true;
        try { sliderDragSrc.onended = null; sliderDragSrc.stop(); } catch (e) { }
        sliderDragSrc = null;
    }

    function startSliderDrag() {
        if (muted) return;
        stopSliderDrag();
        sliderDragActive = true;
        sliderAudioFinished = false;
        sliderDragSrc = playBuffer('audio/drop-chips.mp3', 0.7, 1, function () {
            if (sliderDragActive) sliderAudioFinished = true;
        });
    }

    function play(name, delayMs) {
        if (muted) return;
        if (delayMs) { setTimeout(function () { play(name, 0); }, delayMs); return; }
        // Slight pitch variance so repeated chip/card sounds don't feel mechanical.
        playBuffer('audio/' + name + '.mp3', 0.55, 0.92 + Math.random() * 0.16);
    }

    // Owner-supplied check tap. Steady pitch. A check on the unlocking tap
    // (the CHECK button itself) can arrive while resume() is still in flight;
    // hold it and play on the same gesture instead of dropping it.
    function check() {
        if (muted) return;
        const now = Date.now();
        // The CHECK button starts the clip on the tap; the engine event
        // follows a moment later. One play per check.
        if (now - lastCheckAt < 500) return;
        lastCheckAt = now;
        const c = ensureCtx();
        if (!c || c.state !== 'running') {
            checkPending = true;
            return;
        }
        playBuffer(CHECK_URL, CHECK_VOLUME, 1);
    }

    const BUTTON_SOUND_URL = 'audio/ui-click.wav';

    // Use the supplied click recording for every enabled button, including
    // buttons rendered by Blazor.
    function button() {
        if (muted) return;
        playBuffer(BUTTON_SOUND_URL, 1.6, 1);
    }

    // Wikimedia Commons ragtime recordings, re-encoded to MP3 for iOS Safari.
    // Shuffle without repeats; beds crossfade so there is no hard gap.
    const SALOON_MUSIC_TRACKS = [
        'audio/saloon/top-liner-rag.mp3',
        'audio/saloon/black-and-white-rag.mp3',
        'audio/saloon/wireless-rag.mp3',
        'audio/saloon/bachelors-button.mp3'
    ];
    const STREET_MUSIC_TRACKS = [
        'audio/street/urban-battle-bpm135.mp3',
        'audio/street/fight-looped.wav',
        'audio/street/crushing-enemies-dos.mp3',
        // Owner-supplied "BATTLE MUSIC Volume 162 / CLASSIC ARCADE GAME" beds
        // (Dropbox pack; filenames shortened for the web build).
        'audio/street/battle-music-vol162-01.mp3',
        'audio/street/battle-music-vol162-02.mp3',
        'audio/street/battle-music-vol162-03.mp3',
        'audio/street/battle-music-vol162-04.mp3',
        'audio/street/battle-music-vol162-05.mp3',
        'audio/street/battle-music-vol162-07.mp3',
        'audio/street/battle-music-vol162-09.mp3',
        'audio/street/battle-music-vol162-10.mp3',
        'audio/street/battle-music-vol162-11.mp3'
    ];
    const SALOON_AMBIENCE_URL = 'audio/saloon-bar-ambience.mp3';
    const STREET_AMBIENCE_URL = 'audio/street/crowd-shouting.ogg';
    const MUSIC_VOLUME = 0.12;
    const MUSIC_FADE_SECONDS = 2.5;
    let sceneName = 'saloon';
    let recentMusic = [];

    function pickMusic() {
        const tracks = sceneName === 'street' ? STREET_MUSIC_TRACKS : SALOON_MUSIC_TRACKS;
        let candidates = tracks.filter(function (url) {
            return recentMusic.indexOf(url) === -1;
        });
        if (!candidates.length) {
            recentMusic = [];
            candidates = tracks.slice();
        }
        const url = candidates[Math.floor(Math.random() * candidates.length)];
        recentMusic.push(url);
        return url;
    }

    function clearMusicFade(audio) {
        if (audio && audio._fadeTimer) {
            clearInterval(audio._fadeTimer);
            audio._fadeTimer = null;
        }
    }

    function disposeMusic(audio) {
        if (!audio) return;
        clearMusicFade(audio);
        try { audio.onended = null; } catch (e) { }
        try { audio.removeEventListener('timeupdate', audio._onTimeUpdate); } catch (e) { }
        try { audio.pause(); } catch (e) { }
        if (music === audio) music = null;
        if (musicFadingOut === audio) musicFadingOut = null;
    }

    function fadeMusicTo(audio, targetVolume, seconds, onDone) {
        if (!audio) { if (onDone) onDone(); return; }
        clearMusicFade(audio);
        const startVol = audio.volume;
        const t0 = Date.now();
        const ms = Math.max(50, (seconds || MUSIC_FADE_SECONDS) * 1000);
        audio._fadeTimer = setInterval(function () {
            const t = Math.min(1, (Date.now() - t0) / ms);
            audio.volume = muted ? 0 : startVol + (targetVolume - startVol) * t;
            if (t >= 1) {
                clearMusicFade(audio);
                if (onDone) onDone();
            }
        }, 50);
    }

    function musicFadeSecondsFor(audio) {
        const dur = audio && isFinite(audio.duration) ? audio.duration : 0;
        if (dur > 0) return Math.min(MUSIC_FADE_SECONDS, Math.max(0.4, dur * 0.2));
        return MUSIC_FADE_SECONDS;
    }

    function beginMusicCrossfade(outgoing) {
        if (!outgoing || outgoing._crossfading) return;
        outgoing._crossfading = true;
        if (musicFadingOut && musicFadingOut !== outgoing) disposeMusic(musicFadingOut);
        musicFadingOut = outgoing;
        if (music === outgoing) music = null;
        const fadeSec = musicFadeSecondsFor(outgoing);
        fadeMusicTo(outgoing, 0, fadeSec, function () {
            disposeMusic(outgoing);
        });
        startMusic({ fadeIn: true, fadeSeconds: fadeSec });
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

    function startMusic(opts) {
        if (muted || introPlaying) return;
        const fadeIn = !!(opts && opts.fadeIn);
        try {
            if (!ambience) {
                const ambienceUrl = sceneName === 'street' ? STREET_AMBIENCE_URL : SALOON_AMBIENCE_URL;
                ambience = new Audio(ambienceUrl);
                ambience.loop = true;
                ambience.volume = 0.08;
                const ambiencePlay = ambience.play();
                if (ambiencePlay) ambiencePlay.catch(function () { });
            }
            if (!music) {
                const track = new Audio(pickMusic());
                music = track;
                track.volume = fadeIn ? 0 : MUSIC_VOLUME;
                track._onTimeUpdate = function () {
                    if (music !== track || track._crossfading) return;
                    if (!isFinite(track.duration) || track.duration <= 0) return;
                    const remaining = track.duration - track.currentTime;
                    if (remaining <= musicFadeSecondsFor(track)) {
                        beginMusicCrossfade(track);
                    }
                };
                track.addEventListener('timeupdate', track._onTimeUpdate);
                track.addEventListener('ended', function () {
                    if (track._crossfading) {
                        disposeMusic(track);
                        return;
                    }
                    // Duration/metadata missing (some mobile browsers): swap
                    // immediately with a fade-in instead of a hard gap.
                    disposeMusic(track);
                    startMusic({ fadeIn: true });
                });
                if (fadeIn) {
                    const fadeSec = (opts && opts.fadeSeconds) || MUSIC_FADE_SECONDS;
                    fadeMusicTo(track, MUSIC_VOLUME, fadeSec);
                }
            }
            const p = music.play();
            if (p) p.catch(function () { });
        } catch (e) { }
    }

    function stopMusic() {
        disposeMusic(music);
        disposeMusic(musicFadingOut);
        music = null;
        musicFadingOut = null;
        if (ambience) { try { ambience.pause(); } catch (e) { } ambience = null; }
    }

    // Browsers block audio until the first user gesture, so (re)try on taps.
    // The same tap unlocks the WebAudio context used for SFX and voices.
    document.addEventListener('pointerdown', function (event) {
        const c = ensureCtx();
        if (c && c.state === 'suspended') {
            const p = c.resume();
            if (p) p.then(flushPendingResultAudio).catch(function () { });
        }
        const target = event.target;
        const slider = target && target.closest
            ? target.closest('.slider-control input[type="range"]') : null;
        if (slider) startSliderDrag();
        const buttonEl = target && target.closest
            ? target.closest('button, [role="button"], .hud-board') : null;
        const playButton = buttonEl
            && !(buttonEl.tagName === 'BUTTON' && buttonEl.disabled);
        const checkButton = playButton && buttonEl.classList
            && buttonEl.classList.contains('act')
            && buttonEl.classList.contains('check');
        flushPendingResultAudio();
        startMusic();
        // Player check: start the clip inside the tap. NPC checks arrive
        // later through pokerAudio.check, once the context is already running.
        if (checkButton) check();
        if (playButton) {
            if (c && c.state === 'suspended') {
                const p = c.resume();
                if (p) p.then(function () { button(); }).catch(function () { });
            } else {
                button();
            }
        }
    });
    document.addEventListener('pointerup', function (event) {
        if (sliderDragSrc) stopSliderDrag();
    });
    document.addEventListener('pointermove', function (event) {
        const target = event.target;
        if (!sliderDragActive || !sliderAudioFinished
            || !target || !target.closest
            || !target.closest('.slider-control input[type="range"]')) return;
        startSliderDrag();
    });
    document.addEventListener('pointercancel', stopSliderDrag);

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
        if (sceneName === 'street') return STREET_VOICE_CLIPS[index - 1];
        const n = index < 10 ? '0' + index : String(index);
        if (kind === 'win') return 'audio/voices/win/win-' + n + '.mp3';
        if (kind === 'lose' || kind === 'fold') return 'audio/voices/lose/lose-' + n + '.mp3';
        return 'audio/voices/random/random-' + n + '.mp3';
    }

    function poolSize(kind) {
        if (sceneName === 'street') return STREET_VOICE_CLIPS.length;
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
    // James (seat 0) never uses the table voice pack — that's NPC table talk.
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
        check: check,
        button: button,
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
        // Keep the audio bed in sync with the selected 3D backdrop. The
        // Blazor HUD calls this after reading the persisted scene choice.
        setScene: function (name) {
            const next = String(name || '').toLowerCase().indexOf('street') !== -1
                ? 'street' : 'saloon';
            if (next === sceneName) return;
            sceneName = next;
            recentMusic = [];
            stopMusic();
            startMusic();
        },
        // Short fanfare when the player wins a pot (steady pitch: unlike SFX,
        // a detuned jingle is instantly noticeable).
        victory: function () {
            if (muted) return;
            const c = ensureCtx();
            // A result can be raised by Blazor just as the browser is
            // completing the resume initiated by the all-in tap. Keep the
            // one-shot pending instead of silently dropping the win cue.
            if (!c || c.state !== 'running') {
                victoryPending = true;
                return;
            }
            playBuffer('audio/victory.mp3', 0.75, 1);
        },
        playerLoss: function () {
            if (muted) return;
            const c = ensureCtx();
            if (!c || c.state !== 'running') {
                playerLossPending = true;
                return;
            }
            playBuffer('audio/player-loss.wav', 2.25, 1);
        },
        setAlive: function (seats) { aliveSeats = seats || []; },
        setAwaitingHuman: function (on) { awaitingHuman = !!on; },
        setMuted: function (m) {
            muted = m;
            if (m) stopSliderDrag();
            if (m) {
                victoryPending = false;
                playerLossPending = false;
                checkPending = false;
            }
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
