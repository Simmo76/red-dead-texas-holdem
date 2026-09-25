// Tiny sound-effect player for the Kenney CC0 casino audio pack.
// Failures (autoplay policy before first user gesture, missing files) are silent.
window.pokerAudio = (function () {
    const cache = {};
    function play(name, delayMs) {
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
    return { play: play };
})();
