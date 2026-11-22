// Sound effects (Kenney CC0 casino pack), per-backdrop background music
// (bundled Wikimedia Commons saloon music + OpenGameArt CC0 beach/western/disco beds),
// owner-supplied outlaw table voice packs (win / fold-or-lose / random banter),
// and owner-supplied Octagon announcer one-liners,
// and a short dealer call for the player's winning hand (B. Patrick pack).
// Short clips are MP3 or PCM WAV (both are supported by iOS Safari) and
// play through WebAudio: once the context is unlocked by the first tap,
// timer-driven sounds (NPC chatter, opponent actions) keep
// working on iOS, where HTMLAudio.play() outside a user gesture is rejected.
// Failures (autoplay policy before first gesture, missing files) are silent
// by design.
window.pokerAudio = (function () {
    let ctx = null;
    const buffers = {};       // url -> Promise<AudioBuffer>
    // Independently switchable audio channels (settings SOUND combo):
    // music    — background music beds + intro score
    // voices   — table talk, dealer calls, announcer lines
    // effects  — chips, cards, buttons, win/loss cues
    // ambience — background crowd bed
    const channels = { music: true, voices: true, effects: true, ambience: true };
    // The channel prefs live in their own localStorage key, read synchronously
    // at script load. Blazor restores its copy seconds later (after the WASM
    // boot); without this, a tap on the loading screen started music that the
    // player had switched off — and on iOS a pause() racing a still-pending
    // play() could leave it playing with no reference left to stop.
    const CHANNELS_KEY = 'cinematic-poker-audio-channels';
    let channelsStored = false;
    (function loadStoredChannels() {
        try {
            const raw = localStorage.getItem(CHANNELS_KEY);
            if (!raw) return;
            const c = JSON.parse(raw);
            channels.music = c.music !== false;
            channels.voices = c.voices !== false;
            channels.effects = c.effects !== false;
            channels.ambience = c.ambience !== false;
            channelsStored = true;
        } catch (e) { /* first run or private mode */ }
    })();
    function storeChannels() {
        try {
            localStorage.setItem(CHANNELS_KEY, JSON.stringify(channels));
            channelsStored = true;
        } catch (e) { /* private mode */ }
    }
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

    // Owner-supplied outlaw VO pools (renamed from the Dropbox zip).
    const WIN_CLIPS = 15;
    const LOSE_CLIPS = 14;
    const RANDOM_CLIPS = 21;
    const STREET_VOICE_CLIPS = Array.from({ length: 16 }, function (_, i) {
        const n = i + 1 < 10 ? '0' + (i + 1) : String(i + 1);
        return 'audio/street/voices/street-voice-' + n + '.wav';
    });

    // Inside the Capacitor iOS wrapper the WKWebView is created with
    // mediaTypesRequiringUserActionForPlayback = [], so HTMLAudio may start
    // at any time without a user gesture. WebAudio, on the other hand, can
    // stay stuck in a non-running state in embedded web views even after
    // resume() (an in-app WebKit quirk Safari doesn't have). When that
    // happens, short clips fall back to HTMLAudio so SFX and voices are
    // never silently dropped in the native app. Plain browsers keep the
    // original behavior: WebAudio only, silent until unlocked.
    const NATIVE_WRAPPER = (function () {
        try {
            if (location.protocol === 'capacitor:') return true;
            return !!(window.Capacitor && typeof window.Capacitor.isNativePlatform === 'function'
                && window.Capacitor.isNativePlatform());
        } catch (e) { return false; }
    })();

    function ensureCtx() {
        try {
            if (!ctx) ctx = new (window.AudioContext || window.webkitAudioContext)();
            if (ctx.state !== 'running') { const p = ctx.resume(); if (p) p.catch(function () { }); }
        } catch (e) { }
        return ctx;
    }

    function webAudioReady() {
        const c = ensureCtx();
        return !!(c && c.state === 'running');
    }

    // True when some clip playback path is available right now.
    function audioPlayable() {
        return webAudioReady() || NATIVE_WRAPPER;
    }

    // HTMLAudio stand-in mimicking the bits of AudioBufferSourceNode the
    // callers use (stop(), assignable onended, _cancelled).
    function playHtmlAudio(url, volume, rate, onEnded, loop) {
        const a = new Audio(url);
        a.volume = Math.max(0, Math.min(1, volume || 1));
        if (rate && rate !== 1) {
            try { a.preservesPitch = false; } catch (e) { }
            try { a.webkitPreservesPitch = false; } catch (e) { }
            try { a.playbackRate = rate; } catch (e) { }
        }
        a.loop = !!loop;
        const handle = {
            _cancelled: false,
            onended: onEnded || null,
            stop: function () {
                handle._cancelled = true;
                try { a.pause(); } catch (e) { }
                fireEnded();
            }
        };
        function fireEnded() {
            const cb = handle.onended;
            handle.onended = null;
            if (cb) cb();
        }
        a.addEventListener('ended', fireEnded);
        try {
            const p = a.play();
            if (p) p.catch(fireEnded);
        } catch (e) { fireEnded(); }
        return handle;
    }

    // Clip entry point. In the native wrapper, ALWAYS HTMLAudio: on-device
    // testing showed the wrapper's WebAudio context reports "running" after
    // the first tap unlocks it yet produces no audible output (only the
    // first tap — played via fallback while still locked — was heard).
    // HTMLAudio is the path the background music uses, proven audible.
    // Plain browsers keep WebAudio with the original silent-until-unlocked
    // behavior.
    function playClip(url, volume, rate, onEnded, loop) {
        if (NATIVE_WRAPPER) {
            return playHtmlAudio(url, volume, rate, onEnded, loop);
        }
        if (webAudioReady()) return playBuffer(url, volume, rate, onEnded, loop);
        return null;
    }

    function flushPendingResultAudio() {
        if (!channels.effects || (!victoryPending && !playerLossPending)) return;
        if (!audioPlayable()) return;
        if (victoryPending) {
            victoryPending = false;
            playClip('audio/victory.mp3', 0.75, 1);
        }
        if (playerLossPending) {
            playerLossPending = false;
            playClip('audio/player-loss.wav', 2.25, 1);
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
            if (src._cancelled) { if (onEnded) onEnded(); return; }
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
        if (!channels.effects) return;
        stopSliderDrag();
        sliderDragActive = true;
        sliderAudioFinished = false;
        sliderDragSrc = playClip('audio/drop-chips.mp3', 0.7, 1, function () {
            if (sliderDragActive) sliderAudioFinished = true;
        });
    }

    function play(name, delayMs) {
        if (!channels.effects) return;
        if (delayMs) { setTimeout(function () { play(name, 0); }, delayMs); return; }
        // Slight pitch variance so repeated chip/card sounds don't feel mechanical.
        playClip('audio/' + name + '.mp3', 0.55, 0.92 + Math.random() * 0.16);
    }

    const BUTTON_SOUND_URL = 'audio/ui-click.wav';

    // Use the supplied click recording for every enabled button, including
    // buttons rendered by Blazor.
    function button() {
        if (!channels.effects) return;
        playClip(BUTTON_SOUND_URL, 1.6, 1);
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

    // pause() alone is not enough to silence an element that is still loading:
    // on iOS WebKit a pause() racing a still-pending play() can lose, and the
    // track starts anyway after we have dropped our reference to it. Mute and
    // detach the source so a late-resolving play() has nothing to sound.
    function silenceElement(a) {
        if (!a) return;
        try { a.muted = true; } catch (e) { }
        try { a.pause(); } catch (e) { }
        try { a.removeAttribute('src'); a.load(); } catch (e) { }
    }

    function disposeMusic(audio) {
        if (!audio) return;
        clearMusicFade(audio);
        try { audio.onended = null; } catch (e) { }
        try { audio.removeEventListener('timeupdate', audio._onTimeUpdate); } catch (e) { }
        silenceElement(audio);
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
            audio.volume = channels.music ? startVol + (targetVolume - startVol) * t : 0;
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
    let introPartIndex = 0;
    let introPartStartMs = 0;
    let introPausedElapsed = 0;

    function stopIntroAudio() {
        if (introAudio) {
            clearInterval(introAudio._fadeTimer);
            silenceElement(introAudio);
            introAudio = null;
        }
        introPlaying = false;
    }

    function tickIntroFade(a) {
        const fade = 0.9;
        const t = (Date.now() - introPartStartMs) / 1000;
        const vIn = Math.min(1, t / fade);
        const vOut = Math.max(0, Math.min(1, (INTRO_PART_SECONDS - t) / fade));
        a.volume = (channels.music ? 0.4 : 0) * Math.min(vIn, vOut);
        if (t >= INTRO_PART_SECONDS) {
            clearInterval(a._fadeTimer);
            a._fadeTimer = null;
            try { a.pause(); } catch (e) { }
            playIntroPart(introPartIndex + 1);
        }
    }

    function resumeIntroFade() {
        if (!introPlaying || !introAudio) return;
        const a = introAudio;
        if (a._fadeTimer) return;
        introPartStartMs = Date.now() - introPausedElapsed * 1000;
        a._fadeTimer = setInterval(function () {
            if (introAudio !== a) { clearInterval(a._fadeTimer); return; }
            tickIntroFade(a);
        }, 100);
    }

    function playIntroPart(index) {
        if (index >= INTRO_PARTS.length) { stopIntroAudio(); startMusic(); return; }
        introPartIndex = index;
        introPausedElapsed = 0;
        const a = new Audio(INTRO_PARTS[index]);
        introAudio = a;
        a.volume = 0;
        introPartStartMs = Date.now();
        const p = a.play();
        if (p) p.catch(function () { });
        a._fadeTimer = setInterval(function () {
            if (introAudio !== a) { clearInterval(a._fadeTimer); return; }
            tickIntroFade(a);
        }, 100);
    }

    function startMusic(opts) {
        if (introPlaying) return;
        const fadeIn = !!(opts && opts.fadeIn);
        try {
            if (!ambience && channels.ambience) {
                const ambienceUrl = sceneName === 'street' ? STREET_AMBIENCE_URL : SALOON_AMBIENCE_URL;
                ambience = new Audio(ambienceUrl);
                ambience.loop = true;
                ambience.volume = 0.08;
                const ambiencePlay = ambience.play();
                if (ambiencePlay) ambiencePlay.catch(function () { });
            }
            if (!music && channels.music) {
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
            if (music) {
                const p = music.play();
                if (p) p.catch(function () { });
            }
        } catch (e) { }
    }

    // Skip the current bed (or the intro score) and fade in another track
    // from the same backdrop. No-op while the MUSIC channel is off.
    function nextSong() {
        if (!channels.music) return;
        if (introPlaying) {
            stopIntroAudio();
            startMusic({ fadeIn: true });
            return;
        }
        if (music && !music._crossfading) {
            beginMusicCrossfade(music);
            return;
        }
        if (!music) startMusic({ fadeIn: true });
    }

    // Stops the music beds only — ambience (background crowd) keeps going.
    function stopMusicTracks() {
        disposeMusic(music);
        disposeMusic(musicFadingOut);
        music = null;
        musicFadingOut = null;
    }

    function stopMusic() {
        stopMusicTracks();
        if (ambience) { silenceElement(ambience); ambience = null; }
    }

    // Browsers block audio until the first user gesture, so (re)try on taps.
    // The same tap unlocks the WebAudio context used for SFX and voices.
    document.addEventListener('pointerdown', function (event) {
        const c = ensureCtx();
        // 'interrupted' is a WebKit-only state seen in embedded web views.
        if (c && c.state !== 'running') {
            const p = c.resume();
            if (p) p.then(flushPendingResultAudio).catch(function () { });
            // Classic iOS unlock: a one-sample silent buffer started inside
            // the gesture nudges WebKit to actually open the output stream.
            try {
                const unlock = c.createBufferSource();
                unlock.buffer = c.createBuffer(1, 1, 22050);
                unlock.connect(c.destination);
                unlock.start(0);
            } catch (e) { }
        }
        const target = event.target;
        const slider = target && target.closest
            ? target.closest('.slider-control input[type="range"]') : null;
        if (slider) startSliderDrag();
        const buttonEl = target && target.closest
            ? target.closest('button, [role="button"], .hud-board') : null;
        const playButton = buttonEl
            && !(buttonEl.tagName === 'BUTTON' && buttonEl.disabled);
        flushPendingResultAudio();
        startMusic();
        if (playButton) {
            if (!NATIVE_WRAPPER && c && c.state !== 'running') {
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

    // Table games (and most mobile games) pause audio when the app or tab is
    // not in the foreground. Music/podcast apps are the exception; this table
    // is interactive play, not passive listening.
    let audioPausedByPage = false;
    const bedsPausedByPage = { music: false, fading: false, ambience: false, intro: false };

    function pauseShortClipsForPage() {
        stopSliderDrag();
        if (voiceSrc) {
            try { voiceSrc.stop(); } catch (e) { }
            voiceSrc = null;
            voicePlaying = false;
        }
        if (dealerSrc) {
            try { dealerSrc.onended = null; dealerSrc.stop(); } catch (e) { }
            dealerSrc = null;
            dealerPlaying = false;
        }
    }

    function pauseBedsForPageHidden() {
        bedsPausedByPage.music = false;
        bedsPausedByPage.fading = false;
        bedsPausedByPage.ambience = false;
        bedsPausedByPage.intro = false;
        if (music && !music.paused) {
            try { music.pause(); bedsPausedByPage.music = true; } catch (e) { }
        }
        if (musicFadingOut && !musicFadingOut.paused) {
            try { musicFadingOut.pause(); bedsPausedByPage.fading = true; } catch (e) { }
        }
        if (ambience && !ambience.paused) {
            try { ambience.pause(); bedsPausedByPage.ambience = true; } catch (e) { }
        }
        if (introPlaying && introAudio) {
            if (introAudio._fadeTimer) {
                clearInterval(introAudio._fadeTimer);
                introAudio._fadeTimer = null;
            }
            introPausedElapsed = (Date.now() - introPartStartMs) / 1000;
            try { introAudio.pause(); bedsPausedByPage.intro = true; } catch (e) { }
        }
    }

    function resumeBedsForPageVisible() {
        if (bedsPausedByPage.intro && introPlaying && introAudio && channels.music) {
            const p = introAudio.play();
            if (p) p.catch(function () { });
            resumeIntroFade();
        }
        if (bedsPausedByPage.ambience && ambience && channels.ambience) {
            const p = ambience.play();
            if (p) p.catch(function () { });
        }
        if (bedsPausedByPage.fading && musicFadingOut && channels.music) {
            const p = musicFadingOut.play();
            if (p) p.catch(function () { });
        }
        if (bedsPausedByPage.music && music && channels.music) {
            const p = music.play();
            if (p) p.catch(function () { });
        }
        bedsPausedByPage.music = false;
        bedsPausedByPage.fading = false;
        bedsPausedByPage.ambience = false;
        bedsPausedByPage.intro = false;
    }

    function setPageAudioActive(active) {
        if (active) {
            if (!audioPausedByPage) return;
            audioPausedByPage = false;
            const c = ensureCtx();
            if (c && c.state === 'suspended') {
                const p = c.resume();
                if (p) p.catch(function () { });
            }
            resumeBedsForPageVisible();
            return;
        }
        if (audioPausedByPage) return;
        audioPausedByPage = true;
        pauseShortClipsForPage();
        pauseBedsForPageHidden();
        if (ctx && ctx.state === 'running') {
            try {
                const p = ctx.suspend();
                if (p) p.catch(function () { });
            } catch (e) { }
        }
    }

    document.addEventListener('visibilitychange', function () {
        setPageAudioActive(!document.hidden);
    });
    window.addEventListener('pagehide', function () { setPageAudioActive(false); });
    window.addEventListener('pageshow', function () {
        if (!document.hidden) setPageAudioActive(true);
    });

    // One dealer call — used for the player's winning-hand category at pot award.
    // Ignored until the first tap unlocks audio, same as SFX.
    function dealer(name) {
        if (!channels.voices || !name) return;
        if (!audioPlayable()) return;
        try { if (voiceSrc) voiceSrc.stop(); } catch (e) { }
        voiceSrc = null;
        voicePlaying = false;
        if (dealerSrc) {
            try { dealerSrc.onended = null; dealerSrc.stop(); } catch (e) { }
            dealerSrc = null;
        }
        dealerPlaying = true;
        lastVoiceAt = Date.now();
        const src = playClip('audio/dealer/' + name + '.mp3', 1, 1, function () {
            if (dealerSrc !== src) return;
            dealerPlaying = false;
            dealerSrc = null;
            lastVoiceAt = Date.now();
        });
        dealerSrc = src;
        if (!src) dealerPlaying = false;
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
        if (!channels.voices || dealerPlaying || voicePlaying) return;
        if (!audioPlayable()) return;
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
        voiceSrc = playClip(poolUrl(eventKind, n), 0.95, 1, function () {
            voicePlaying = false;
            voiceSrc = null;
            lastVoiceAt = Date.now();
        });
        if (!voiceSrc) voicePlaying = false;
        // Idle chatter gets a matching talking gesture in the 3D scene.
        if (eventKind === 'idle' && seat > 0 && window.pokerScene && window.pokerScene.talk) {
            window.pokerScene.talk(seat);
        }
    }

    // Random table banter while waiting on the player's turn (and never over
    // an active win / fold / lose line).
    (function scheduleChatter() {
        setTimeout(function () {
            if (channels.voices && !document.hidden && awaitingHuman && !voicePlaying && !dealerPlaying) {
                voice(pickIdleSeat(), 'idle');
            }
            scheduleChatter();
        }, 20000 + Math.random() * 18000);
    })();

    return {
        play: play,
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
        nextSong: nextSong,
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
            if (!channels.effects) return;
            // A result can be raised by Blazor just as the browser is
            // completing the resume initiated by the all-in tap. Keep the
            // one-shot pending instead of silently dropping the win cue.
            if (!audioPlayable()) {
                victoryPending = true;
                return;
            }
            playClip('audio/victory.mp3', 0.75, 1);
        },
        playerLoss: function () {
            if (!channels.effects) return;
            if (!audioPlayable()) {
                playerLossPending = true;
                return;
            }
            playClip('audio/player-loss.wav', 2.25, 1);
        },
        setAlive: function (seats) { aliveSeats = seats || []; },
        setAwaitingHuman: function (on) { awaitingHuman = !!on; },
        // Settings SOUND combo: each channel toggles independently.
        // Missing keys count as ON so a partial object can't silence the game.
        setChannels: function (c) {
            c = c || {};
            const wasMusic = channels.music, wasAmbience = channels.ambience;
            channels.music = c.music !== false;
            channels.voices = c.voices !== false;
            channels.effects = c.effects !== false;
            channels.ambience = c.ambience !== false;
            storeChannels();
            if (!channels.music) stopMusicTracks();
            if (!channels.ambience && ambience) {
                silenceElement(ambience);
                ambience = null;
            }
            if (!channels.effects) {
                stopSliderDrag();
                victoryPending = false;
                playerLossPending = false;
            }
            if (!channels.voices) {
                if (voiceSrc) { try { voiceSrc.stop(); } catch (e) { } voiceSrc = null; voicePlaying = false; }
                if (dealerSrc) {
                    try { dealerSrc.onended = null; dealerSrc.stop(); } catch (e) { }
                    dealerSrc = null;
                    dealerPlaying = false;
                }
            }
            if ((channels.music && !wasMusic) || (channels.ambience && !wasAmbience)) startMusic();
        },
        // Current channel state plus whether it came from this device's own
        // store. Blazor reads this on boot so the settings UI matches what
        // the audio engine is actually doing; `stored: false` means a fresh
        // device (or cleared storage), where the game-session snapshot is
        // still the only record of the player's choice.
        getChannels: function () {
            return {
                music: channels.music,
                voices: channels.voices,
                effects: channels.effects,
                ambience: channels.ambience,
                stored: channelsStored
            };
        }
    };
})();
