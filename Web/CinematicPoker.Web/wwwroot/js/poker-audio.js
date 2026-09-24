// Tiny sound-effect player for the Kenney CC0 casino audio pack.
// Failures (autoplay policy before first user gesture, missing files) are silent.
window.pokerAudio = (function () {
    const cache = {};
    function play(name) {
        try {
            let audio = cache[name];
            if (!audio) {
                audio = new Audio("audio/" + name + ".ogg");
                audio.preload = "auto";
                cache[name] = audio;
            }
            audio.currentTime = 0;
            audio.volume = 0.55;
            const p = audio.play();
            if (p) p.catch(function () { });
        } catch (e) { }
    }
    return { play: play };
})();
