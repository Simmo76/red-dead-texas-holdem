// Battery benchmark harness — measures battery drain per hour so the web
// version (Safari / Home Screen) and the iPhone app (Capacitor WKWebView)
// can be compared on the same device. See docs/battery-comparison.md.
//
// Opening it:
//   - add ?battery=1 (or #battery) to the URL, or
//   - tap the top-left corner of the screen 5 times within 2.5 s
//     (works inside the iPhone app, which has no URL bar), or
//   - window.pokerBattery.open() from a console.
//
// Battery level sources, best available wins:
//   1. Capacitor Device plugin (the iPhone app) — UIDevice, 1% steps.
//   2. navigator.getBattery() (Chrome / Edge / Android browsers).
//   3. Manual — iOS Safari has no Battery API, so the tester types the
//      status-bar percentage at start and stop; the harness still times
//      the run, counts frames and computes %/hour.
//
// Completed runs are kept in localStorage (per browser/app — Safari and
// the native app do NOT share storage, so compare via the exported JSON).
(function (w, d) {
    'use strict';

    var LS_RUNS = 'dmh-battery-runs';
    var LS_ENABLED = 'dmh-battery-enabled';
    var LS_ACTIVE = 'dmh-battery-active-run';
    var SAMPLE_MS = 30000;
    var MAX_RUNS = 20;

    // ---------- platform / battery source detection ----------

    function isCapacitorNative() {
        try {
            if (w.location.protocol === 'capacitor:') return true;
            return !!(w.Capacitor && typeof w.Capacitor.isNativePlatform === 'function'
                && w.Capacitor.isNativePlatform());
        } catch (e) { return false; }
    }

    function capacitorDevice() {
        try {
            var p = w.Capacitor && w.Capacitor.Plugins && w.Capacitor.Plugins.Device;
            return (p && typeof p.getBatteryInfo === 'function') ? p : null;
        } catch (e) { return null; }
    }

    function platformId() {
        if (isCapacitorNative()) return 'iphone-app';
        if (navigator.standalone === true) return 'web-homescreen';
        if (w.matchMedia && w.matchMedia('(display-mode: standalone)').matches) return 'web-homescreen';
        return 'web-browser';
    }

    function platformLabel(id) {
        if (id === 'iphone-app') return 'iPhone app (native shell)';
        if (id === 'web-homescreen') return 'Web — Home Screen app';
        var ua = navigator.userAgent || '';
        if (/CriOS/.test(ua)) return 'Web — Chrome (iOS)';
        if (/iPhone|iPad/.test(ua)) return 'Web — Safari';
        if (/Chrome/.test(ua)) return 'Web — Chrome';
        if (/Firefox/.test(ua)) return 'Web — Firefox';
        if (/Safari/.test(ua)) return 'Web — Safari';
        return 'Web — browser';
    }

    var batteryManager = null; // navigator.getBattery() result, once resolved

    // cb(levelPct 0..100 or null, isCharging or null, sourceName)
    function readBattery(cb) {
        var dev = capacitorDevice();
        if (dev) {
            dev.getBatteryInfo().then(function (info) {
                var lvl = (info && typeof info.batteryLevel === 'number')
                    ? Math.round(info.batteryLevel * 100) : null;
                cb(lvl, info ? !!info.isCharging : null, 'capacitor');
            }).catch(function () { cb(null, null, 'manual'); });
            return;
        }
        if (batteryManager) {
            cb(Math.round(batteryManager.level * 100), !!batteryManager.charging, 'web-battery-api');
            return;
        }
        if (navigator.getBattery) {
            navigator.getBattery().then(function (bm) {
                batteryManager = bm;
                cb(Math.round(bm.level * 100), !!bm.charging, 'web-battery-api');
            }).catch(function () { cb(null, null, 'manual'); });
            return;
        }
        cb(null, null, 'manual');
    }

    // ---------- run state ----------

    var run = null;          // active run object
    var sampleTimer = null;
    var wakeLock = null;
    var frames = 0;          // rAF frames since last sample
    var rafId = null;
    var hiddenSince = null;

    function now() { return Date.now(); }

    function loadRuns() {
        try { return JSON.parse(localStorage.getItem(LS_RUNS)) || []; }
        catch (e) { return []; }
    }

    function saveRuns(runs) {
        try { localStorage.setItem(LS_RUNS, JSON.stringify(runs.slice(-MAX_RUNS))); }
        catch (e) { }
    }

    function persistActive() {
        try {
            if (run) localStorage.setItem(LS_ACTIVE, JSON.stringify(run));
            else localStorage.removeItem(LS_ACTIVE);
        } catch (e) { }
    }

    function countFrames() {
        frames++;
        rafId = w.requestAnimationFrame(countFrames);
    }

    function acquireWakeLock() {
        if (!navigator.wakeLock || !navigator.wakeLock.request) return;
        navigator.wakeLock.request('screen').then(function (lock) {
            wakeLock = lock;
        }).catch(function () { /* user denied / unsupported — Auto-Lock must be off */ });
    }

    function releaseWakeLock() {
        if (wakeLock) { try { wakeLock.release(); } catch (e) { } wakeLock = null; }
    }

    function takeSample() {
        if (!run) return;
        var sinceLast = run.samples.length
            ? now() - run.samples[run.samples.length - 1].t
            : now() - run.startedAt;
        var fps = sinceLast > 0 ? Math.round(frames * 1000 / sinceLast) : null;
        frames = 0;
        readBattery(function (lvl, charging, source) {
            if (!run) return;
            run.samples.push({
                t: now(),
                level: lvl,
                charging: charging,
                fps: fps,
                visible: d.visibilityState !== 'hidden'
            });
            if (charging === true) run.sawCharging = true;
            persistActive();
            if (!panelInputFocused()) render();
        });
    }

    function startRun(manualStartPct) {
        if (run) return;
        readBattery(function (lvl, charging, source) {
            run = {
                id: 'run-' + now(),
                platform: platformId(),
                platformLabel: platformLabel(platformId()),
                source: source,
                startedAt: now(),
                endedAt: null,
                startLevel: source === 'manual' ? manualStartPct : lvl,
                endLevel: null,
                sawCharging: charging === true,
                hiddenMs: 0,
                samples: [],
                userAgent: navigator.userAgent,
                screen: (w.screen ? w.screen.width + 'x' + w.screen.height : null),
                pixelRatio: w.devicePixelRatio || 1
            };
            frames = 0;
            if (!rafId) rafId = w.requestAnimationFrame(countFrames);
            sampleTimer = w.setInterval(takeSample, SAMPLE_MS);
            acquireWakeLock();
            persistActive();
            render();
        });
    }

    function finishRun(endedRun) {
        var runs = loadRuns();
        runs.push(endedRun);
        saveRuns(runs);
    }

    function stopRun(manualEndPct) {
        if (!run) return;
        w.clearInterval(sampleTimer);
        sampleTimer = null;
        if (rafId) { w.cancelAnimationFrame(rafId); rafId = null; }
        releaseWakeLock();
        var finish = function (lvl) {
            run.endedAt = now();
            run.endLevel = lvl;
            computeResults(run);
            finishRun(run);
            lastResult = run;
            run = null;
            persistActive();
            render();
        };
        if (run.source === 'manual') {
            finish(manualEndPct);
        } else {
            readBattery(function (lvl) { finish(lvl); });
        }
    }

    function computeResults(r) {
        var hours = (r.endedAt - r.startedAt) / 3600000;
        r.durationMin = Math.round((r.endedAt - r.startedAt) / 60000 * 10) / 10;
        if (typeof r.startLevel === 'number' && typeof r.endLevel === 'number' && hours > 0) {
            r.drainPct = r.startLevel - r.endLevel;
            r.drainPctPerHour = Math.round(r.drainPct / hours * 10) / 10;
        } else {
            r.drainPct = null;
            r.drainPctPerHour = null;
        }
        var fpsSamples = r.samples.filter(function (s) { return typeof s.fps === 'number' && s.visible; });
        r.avgFps = fpsSamples.length
            ? Math.round(fpsSamples.reduce(function (a, s) { return a + s.fps; }, 0) / fpsSamples.length)
            : null;
    }

    var lastResult = null;

    // Resume a run that survived a page reload.
    (function resumeActive() {
        try {
            var saved = JSON.parse(localStorage.getItem(LS_ACTIVE));
            if (saved && saved.startedAt && !saved.endedAt) {
                run = saved;
                frames = 0;
                rafId = w.requestAnimationFrame(countFrames);
                sampleTimer = w.setInterval(takeSample, SAMPLE_MS);
                acquireWakeLock();
            }
        } catch (e) { }
    })();

    d.addEventListener('visibilitychange', function () {
        if (d.visibilityState === 'hidden') {
            hiddenSince = now();
        } else {
            if (run && hiddenSince) run.hiddenMs += now() - hiddenSince;
            hiddenSince = null;
            if (run) acquireWakeLock(); // wake locks drop on backgrounding
        }
    });

    // ---------- export ----------

    function exportJson(r) {
        return JSON.stringify(r, null, 2);
    }

    function copyText(text, done) {
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(text).then(function () { done(true); },
                function () { done(false); });
            return;
        }
        try {
            var ta = d.createElement('textarea');
            ta.value = text;
            ta.style.position = 'fixed';
            ta.style.opacity = '0';
            d.body.appendChild(ta);
            ta.select();
            var ok = d.execCommand('copy');
            d.body.removeChild(ta);
            done(ok);
        } catch (e) { done(false); }
    }

    // ---------- UI ----------

    var root = null, panel = null, pill = null, panelOpen = false;

    var CSS = ''
        + '#dmh-batt-pill{position:fixed;top:max(8px,env(safe-area-inset-top));left:max(8px,env(safe-area-inset-left));'
        + 'z-index:9999;background:#232a3c;color:#f2e8d4;border:1px solid #8a93b0;border-radius:16px;'
        + 'padding:6px 12px;font:13px/1.4 Georgia,serif;cursor:pointer;user-select:none;'
        + 'box-shadow:0 2px 8px rgba(0,0,0,0.5);}'
        + '#dmh-batt-panel{position:fixed;top:max(8px,env(safe-area-inset-top));left:max(8px,env(safe-area-inset-left));'
        + 'z-index:9999;width:270px;max-height:80vh;overflow:auto;background:#10131c;color:#e8dcc8;'
        + 'border:1px solid #3a4154;border-radius:10px;padding:10px 12px;font:12px/1.5 Georgia,serif;'
        + 'box-shadow:0 4px 18px rgba(0,0,0,0.6);}'
        + '#dmh-batt-panel h3{margin:0 0 6px;font-size:13px;letter-spacing:0.06em;}'
        + '#dmh-batt-panel .row{display:flex;justify-content:space-between;gap:8px;margin:2px 0;}'
        + '#dmh-batt-panel .muted{opacity:0.65;}'
        + '#dmh-batt-panel .warn{color:#e8a84c;}'
        + '#dmh-batt-panel button{background:#2a3146;color:#e8dcc8;border:1px solid #4a5270;'
        + 'border-radius:6px;padding:5px 10px;font:12px Georgia,serif;cursor:pointer;margin:4px 4px 0 0;}'
        + '#dmh-batt-panel button:disabled{opacity:0.4;cursor:default;}'
        + '#dmh-batt-panel input[type=number]{width:58px;background:#1a1f2b;color:#e8dcc8;'
        + 'border:1px solid #4a5270;border-radius:4px;padding:3px 5px;font:12px Georgia,serif;}'
        + '#dmh-batt-panel .runs{margin-top:8px;border-top:1px solid #2a3146;padding-top:6px;}'
        + '#dmh-batt-panel .close{float:right;cursor:pointer;opacity:0.7;padding:0 4px;}';

    function ensureUi() {
        if (root) return;
        root = d.createElement('div');
        var style = d.createElement('style');
        style.textContent = CSS;
        root.appendChild(style);
        pill = d.createElement('div');
        pill.id = 'dmh-batt-pill';
        pill.textContent = '🔋 battery test';
        pill.addEventListener('click', function () { panelOpen = true; render(); });
        root.appendChild(pill);
        panel = d.createElement('div');
        panel.id = 'dmh-batt-panel';
        panel.style.display = 'none';
        root.appendChild(panel);
        d.body.appendChild(root);
        render();
    }

    function fmtClock(ms) {
        var s = Math.floor(ms / 1000);
        var m = Math.floor(s / 60);
        var h = Math.floor(m / 60);
        return (h ? h + 'h ' : '') + (m % 60) + 'm ' + (s % 60) + 's';
    }

    function esc(t) {
        return String(t).replace(/[&<>"]/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c];
        });
    }

    function panelInputFocused() {
        var a = d.activeElement;
        return !!(a && panel && panel.contains(a) && a.tagName === 'INPUT');
    }

    function render() {
        if (!root) return;
        pill.style.display = panelOpen ? 'none' : '';
        panel.style.display = panelOpen ? '' : 'none';
        if (!panelOpen) return;

        // Re-rendering replaces the inputs — keep anything already typed.
        var keepStart = d.getElementById('dmh-batt-startpct');
        var keepEnd = d.getElementById('dmh-batt-endpct');
        keepStart = keepStart ? keepStart.value : null;
        keepEnd = keepEnd ? keepEnd.value : null;

        var pid = platformId();
        var html = '<span class="close" id="dmh-batt-close">✕</span>'
            + '<h3>BATTERY TEST</h3>'
            + '<div class="row"><span class="muted">Running as</span><span>' + esc(platformLabel(pid)) + '</span></div>';

        if (run) {
            var last = run.samples.length ? run.samples[run.samples.length - 1] : null;
            var lvl = last && typeof last.level === 'number' ? last.level + '%'
                : (run.source === 'manual' ? 'manual' : '…');
            html += '<div class="row"><span class="muted">Elapsed</span><span>' + fmtClock(now() - run.startedAt) + '</span></div>'
                + '<div class="row"><span class="muted">Start level</span><span>'
                + (typeof run.startLevel === 'number' ? run.startLevel + '%' : '—') + '</span></div>'
                + '<div class="row"><span class="muted">Battery now</span><span>' + lvl + '</span></div>'
                + '<div class="row"><span class="muted">FPS (last sample)</span><span>'
                + (last && typeof last.fps === 'number' ? last.fps : '…') + '</span></div>';
            if (run.sawCharging)
                html += '<div class="warn">⚠ Charger detected — this run is not a valid drain measurement.</div>';
            if (run.hiddenMs > 5000)
                html += '<div class="warn">⚠ App was backgrounded for ' + fmtClock(run.hiddenMs) + '.</div>';
            if (run.source === 'manual') {
                html += '<div class="muted" style="margin-top:4px;">No Battery API here — type the status-bar % to stop:</div>'
                    + '<div class="row" style="margin-top:4px;"><span>End battery %</span>'
                    + '<input type="number" id="dmh-batt-endpct" min="1" max="100" inputmode="numeric"></div>';
            }
            html += '<button id="dmh-batt-stop">■ Stop &amp; save</button>';
        } else {
            var manual = !capacitorDevice() && !navigator.getBattery;
            if (manual) {
                html += '<div class="muted" style="margin-top:4px;">This browser has no Battery API (iOS Safari). '
                    + 'Type the status-bar battery % to start:</div>'
                    + '<div class="row" style="margin-top:4px;"><span>Start battery %</span>'
                    + '<input type="number" id="dmh-batt-startpct" min="1" max="100" inputmode="numeric"></div>';
            }
            html += '<button id="dmh-batt-start">▶ Start run</button>'
                + '<div class="muted" style="margin-top:4px;">Unplug the charger. Fix the brightness. '
                + 'Run at least 30 minutes at the table. See docs/battery-comparison.md.</div>';

            if (lastResult) html += renderResult('Last run', lastResult);

            var runs = loadRuns();
            if (runs.length) {
                html += '<div class="runs"><div class="muted">Saved runs (this app/browser only)</div>';
                for (var i = runs.length - 1; i >= 0; i--) {
                    var r = runs[i];
                    html += '<div class="row"><span>' + esc(r.platform) + ' · ' + r.durationMin + 'm</span><span>'
                        + (r.drainPctPerHour === null ? 'n/a' : r.drainPctPerHour + ' %/h')
                        + (r.sawCharging ? ' ⚠' : '') + '</span></div>';
                }
                html += '<button id="dmh-batt-copyall">Copy all as JSON</button>'
                    + '<button id="dmh-batt-clear">Clear</button></div>';
            }
        }

        panel.innerHTML = html;

        var el;
        if (keepStart && (el = d.getElementById('dmh-batt-startpct'))) el.value = keepStart;
        if (keepEnd && (el = d.getElementById('dmh-batt-endpct'))) el.value = keepEnd;
        if ((el = d.getElementById('dmh-batt-close')))
            el.addEventListener('click', function () { panelOpen = false; render(); });
        if ((el = d.getElementById('dmh-batt-start')))
            el.addEventListener('click', function () {
                var inp = d.getElementById('dmh-batt-startpct');
                var pct = inp ? parseInt(inp.value, 10) : null;
                if (inp && (!(pct >= 1) || pct > 100)) { inp.focus(); return; }
                startRun(inp ? pct : null);
            });
        if ((el = d.getElementById('dmh-batt-stop')))
            el.addEventListener('click', function () {
                var inp = d.getElementById('dmh-batt-endpct');
                var pct = inp ? parseInt(inp.value, 10) : null;
                if (inp && (!(pct >= 1) || pct > 100)) { inp.focus(); return; }
                stopRun(inp ? pct : null);
            });
        if ((el = d.getElementById('dmh-batt-copyall')))
            el.addEventListener('click', function () {
                copyText(exportJson({ exportedAt: new Date().toISOString(), runs: loadRuns() }), function (ok) {
                    el.textContent = ok ? 'Copied ✓' : 'Copy failed';
                });
            });
        if ((el = d.getElementById('dmh-batt-clear')))
            el.addEventListener('click', function () {
                saveRuns([]);
                lastResult = null;
                render();
            });
    }

    function renderResult(title, r) {
        return '<div class="runs"><div class="muted">' + esc(title) + ' — ' + esc(r.platformLabel) + '</div>'
            + '<div class="row"><span class="muted">Duration</span><span>' + r.durationMin + ' min</span></div>'
            + '<div class="row"><span class="muted">Battery</span><span>'
            + (typeof r.startLevel === 'number' ? r.startLevel + '% → ' + r.endLevel + '%' : 'n/a') + '</span></div>'
            + '<div class="row"><span class="muted">Drain</span><span><b>'
            + (r.drainPctPerHour === null ? 'n/a' : r.drainPctPerHour + ' %/hour') + '</b></span></div>'
            + '<div class="row"><span class="muted">Avg FPS</span><span>' + (r.avgFps === null ? 'n/a' : r.avgFps) + '</span></div>'
            + (r.sawCharging ? '<div class="warn">⚠ Charger was connected during this run.</div>' : '')
            + '</div>';
    }

    // Live clock while a run is in progress. Paused while the tester is
    // typing in a panel input, so the keystrokes are not wiped mid-entry.
    w.setInterval(function () {
        if (run && panelOpen && !panelInputFocused()) render();
    }, 1000);

    // ---------- activation ----------

    function enabled() {
        try {
            if (/[?&#]battery(=|&|$)/.test(w.location.search + w.location.hash)) return true;
            if (localStorage.getItem(LS_ENABLED) === '1') return true;
            if (localStorage.getItem(LS_ACTIVE)) return true;
        } catch (e) { }
        return false;
    }

    function setEnabled(on) {
        try {
            if (on) localStorage.setItem(LS_ENABLED, '1');
            else localStorage.removeItem(LS_ENABLED);
        } catch (e) { }
    }

    function open() {
        setEnabled(true);
        ensureUi();
        panelOpen = true;
        render();
    }

    // Hidden switch for the iPhone app (no URL bar there): 5 taps in the
    // top-left corner within 2.5 s toggles the harness.
    var taps = [];
    d.addEventListener('pointerdown', function (e) {
        if (e.clientX > 70 || e.clientY > 70) return;
        var t = now();
        taps = taps.filter(function (x) { return t - x < 2500; });
        taps.push(t);
        if (taps.length >= 5) {
            taps = [];
            if (root && enabled() && !run) {
                setEnabled(false);
                panelOpen = false;
                root.remove();
                root = panel = pill = null;
            } else {
                open();
            }
        }
    }, true);

    function boot() {
        if (enabled()) {
            setEnabled(true);
            ensureUi();
        }
    }

    if (d.readyState === 'loading') d.addEventListener('DOMContentLoaded', boot);
    else boot();

    w.pokerBattery = {
        open: open,
        start: startRun,
        stop: stopRun,
        runs: loadRuns,
        exportJson: function () { return exportJson({ exportedAt: new Date().toISOString(), runs: loadRuns() }); }
    };
})(window, document);
