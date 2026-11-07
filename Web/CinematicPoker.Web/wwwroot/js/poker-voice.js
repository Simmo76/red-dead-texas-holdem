// Voice-activated table actions (experimental): listens for the spoken
// keywords "check", "raise" and "fold" through the browser's Web Speech API
// and forwards them to Blazor, which validates them against the engine's
// legal actions exactly like a button tap.
//
// Browser support: Chrome/Edge/Safari expose SpeechRecognition (Chrome sends
// the audio to Google's recogniser, so it needs to be online); Firefox ships
// no implementation, so the mic button reports "unsupported" there.
// Recognition sessions end on their own after silence — onend restarts the
// session while the feature is switched on.
//
// The on/off choice is remembered in localStorage so a player who liked the
// feature gets it re-armed automatically on the next visit (started from the
// first tap on the title card, which doubles as the activation gesture).
window.pokerVoice = (function () {
    const SAVE_KEY = "cinematic-poker-voice-on";

    let recognition = null;
    let dotnetRef = null;
    let enabled = false;
    let lastFireAt = 0;
    let lastHeard = "";

    // Single-word commands are routinely misheard; accept the common
    // homophones so the test is about the idea, not the recogniser.
    const KEYWORDS = [
        { cmd: "check", words: ["check", "cheque", "czech", "chick", "checked"] },
        { cmd: "raise", words: ["raise", "rays", "raze", "raised", "race"] },
        { cmd: "fold", words: ["fold", "folds", "folded", "bold", "hold"] },
        { cmd: "call", words: ["call", "called", "paul"] },
        { cmd: "allin", words: ["all in", "all-in", "allin", "all win"] },
    ];

    function Recogniser() {
        return window.SpeechRecognition || window.webkitSpeechRecognition || null;
    }

    function save(on) {
        try { localStorage.setItem(SAVE_KEY, on ? "1" : "0"); } catch (e) { }
    }

    function savedOn() {
        try { return localStorage.getItem(SAVE_KEY) === "1"; } catch (e) { return false; }
    }

    function report(state) {
        if (!dotnetRef) return;
        try { dotnetRef.invokeMethodAsync("OnVoiceState", state); } catch (e) { }
    }

    // Silent cleanup: used inside start() where a stop() report("off") would
    // race the fresh session's "listening" and flip the Blazor toggle back.
    function teardown() {
        if (!recognition) return;
        try { recognition.onend = null; recognition.stop(); } catch (e) { }
        recognition = null;
    }

    function matchCommand(transcript) {
        const text = " " + transcript.toLowerCase().trim() + " ";
        for (const k of KEYWORDS) {
            for (const w of k.words) {
                if (text.includes(" " + w + " ")) return k.cmd;
            }
        }
        return null;
    }

    function handleResults(event) {
        if (!dotnetRef) return;
        // Scan everything new since the last event; interim transcripts are
        // included so a clearly heard keyword fires without waiting for the
        // recogniser to finalise the phrase.
        for (let i = event.resultIndex; i < event.results.length; i++) {
            const transcript = event.results[i][0].transcript || "";
            const cmd = matchCommand(transcript);
            if (!cmd) continue;
            const now = Date.now();
            // Interim and final results repeat the same words; a short
            // cooldown (and ignoring the exact same phrase) stops one
            // utterance acting twice.
            if (cmd === lastHeard && now - lastFireAt < 2000) return;
            if (now - lastFireAt < 800) return;
            lastFireAt = now;
            lastHeard = cmd;
            try { dotnetRef.invokeMethodAsync("OnVoiceCommand", cmd); } catch (e) { }
            return;
        }
    }

    function start(ref) {
        const R = Recogniser();
        if (!R) { dotnetRef = ref; save(false); report("unsupported"); return false; }
        teardown();
        dotnetRef = ref;
        enabled = true;

        recognition = new R();
        recognition.continuous = true;
        recognition.interimResults = true;
        recognition.lang = "en-US";
        recognition.maxAlternatives = 1;

        recognition.onstart = function () { report("listening"); };
        recognition.onresult = handleResults;
        recognition.onerror = function (e) {
            if (e.error === "not-allowed" || e.error === "service-not-allowed") {
                enabled = false;
                save(false);
                report("denied");
            }
            // "no-speech"/"aborted"/"network" fall through; onend restarts.
        };
        recognition.onend = function () {
            // The browser closes sessions after silence; keep listening
            // while the feature is on.
            if (!enabled) { report("off"); return; }
            try { recognition.start(); }
            catch (e) { enabled = false; report("off"); }
        };

        try { recognition.start(); }
        catch (e) { enabled = false; report("off"); return false; }
        save(true);
        return true;
    }

    function stop() {
        enabled = false;
        teardown();
        lastHeard = "";
        save(false);
        report("off");
    }

    return {
        start: start,
        stop: stop,
        savedOn: savedOn,
        supported: function () { return !!Recogniser(); },
    };
})();
