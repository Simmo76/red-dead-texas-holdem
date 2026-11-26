# Comparing battery usage: web app vs iPhone app

Both versions run the **same code** — the Blazor WebAssembly app in
`Web/CinematicPoker.Web`. The "web app" runs it in Safari (or as a Home
Screen web app) and the "iPhone app" runs it in a Capacitor `WKWebView`
shell. Both use the same WebKit engine on iOS, so any battery difference
comes from what surrounds the page: browser chrome, tab/process management,
the PWA shell vs the native shell, and process priorities — not from the
game logic or renderer. Expect the numbers to be close; the point of
measuring is to confirm that (or catch a shell-specific regression such as
the WKWebView not pausing `requestAnimationFrame` correctly).

There are two complementary ways to compare, described below:

1. the **in-app battery test harness** (built into the game, works in both
   versions, gives a %-per-hour number), and
2. **iOS's own per-app battery attribution** (Settings → Battery), backed
   up by Xcode Instruments for the native app if deeper analysis is needed.

---

## 1. The in-app battery test harness

A benchmark harness ships inside the game (`wwwroot/js/poker-battery.js`).
It records battery level over a timed session and reports **drain in % per
hour**, plus average FPS so you can confirm both runs did comparable work.
Normal players never see it; it only appears when explicitly opened.

### Opening the harness

| Where | How |
|---|---|
| Browser / Home Screen app | add `?battery=1` to the URL, e.g. `https://simmo76.github.io/red-dead-texas-holdem/?battery=1` |
| iPhone app (no URL bar) | tap the **top-left corner** of the screen **5 times within 2.5 s** |
| Any devtools console | `pokerBattery.open()` |

The same 5-tap gesture also works on the web, and toggles the harness off
again when no run is active.

### Where the battery reading comes from

| Version | Source | Granularity |
|---|---|---|
| iPhone app | Capacitor Device plugin → `UIDevice` | 1% |
| Chrome / Edge / Android browsers | `navigator.getBattery()` | ~1% |
| **iOS Safari / Home Screen app** | **manual** — Safari has no Battery API. The harness asks you to type the status-bar percentage at start and at stop; it still times the run, counts frames and computes %/hour | 1% (status bar) |

### Test protocol (do this identically for both runs)

A fair comparison needs the *same device, same conditions, same workload*:

1. **Same iPhone** for both runs, charged to a similar level (ideally
   50–90% — avoid the top and bottom of the charge curve).
2. **Unplug the charger.** The harness flags the run as invalid if it ever
   sees charging (it can only detect that in the iPhone app and on
   Android/Chrome — on iOS Safari, just don't plug in).
3. **Fix the screen brightness** (e.g. 50%) and turn off auto-brightness
   (Settings → Accessibility → Display & Text Size → Auto-Brightness).
4. **Turn off Low Power Mode** and enable Do Not Disturb.
5. Set **Auto-Lock to Never** (Settings → Display & Brightness) for the
   duration of the test. The harness also requests a screen wake lock
   where supported (iOS 16.4+), but don't rely on it.
6. Load the game, sit at the **same table/scene** with the same audio
   setting, open the harness, press **Start run**.
7. Leave the phone **idle at the table** (the 3D render loop is the
   dominant battery cost and idling is perfectly reproducible), or play
   normally — but then play the same way in both runs.
8. Run for **at least 30 minutes** (battery reports in 1% steps; a short
   run drowns in quantisation noise — 30 min at a typical 10–20 %/h drain
   gives 5–10 percentage points of signal).
9. Press **Stop & save**. Note the **%/hour** figure and the **avg FPS**.
10. Repeat for the other version, then ideally do a second pair in the
    opposite order (battery drain is slightly nonlinear and the device
    warms up).

> Safari and the iPhone app do **not** share storage, so each version only
> lists its own saved runs. Use **Copy all as JSON** in each version and
> paste the results somewhere central to compare.

### Recording template

| Run | Version | Duration | Start % | End % | Drain %/h | Avg FPS | Notes |
|---|---|---|---|---|---|---|---|
| 1 | Web (Safari) | 30 min | 82 | 76 | | | |
| 2 | iPhone app | 30 min | 76 | 70 | | | |
| 3 | iPhone app | 30 min | | | | | order swapped |
| 4 | Web (Safari) | 30 min | | | | | order swapped |

If the two versions are within ~2 %/h of each other, they are effectively
identical — that difference is within the noise of this method.

### Validity warnings the harness shows

- **Charger detected** — the run is not a valid drain measurement.
- **App was backgrounded** — iOS suspends the page; the elapsed time then
  includes near-zero-drain periods and understates the true rate.

---

## 2. iOS's own per-app attribution (cross-check)

After a day that included both test sessions, open
**Settings → Battery → last 24 hours**. iOS attributes battery use per app:

- the Capacitor wrapper shows up as **Dead Man's Hand**;
- Safari browsing shows up under **Safari**;
- a Home Screen web app shows up as its own entry.

Tap an entry to see **screen-on time**; divide battery share by screen-on
time for a rough per-hour comparison. This is coarser than the harness but
it is Apple's own accounting and a good sanity check that the harness
numbers point the same way.

### Deeper analysis (native app only, needs a Mac)

For the Capacitor app, Xcode gives proper instrumentation:

- **Debug gauges**: run the app from Xcode, open the Debug navigator →
  **Energy Impact** — live CPU/GPU/network energy ratings.
- **Instruments → Energy Log**: Product → Profile → Energy Log, or record
  on-device without a cable (Settings → Developer → Logging) and import.

There is no equivalent for the Safari version — Safari Web Inspector's
Timelines (connect the iPhone to a Mac, Develop menu) shows CPU usage and
frames, which is the main battery proxy, but not energy as such.

---

## Interpreting differences

If one version drains meaningfully faster, the usual suspects are:

- **FPS mismatch** — check the harness's avg FPS for both runs. The scene
  caps its pixel ratio for battery reasons (`poker-scene.js`); a different
  reported `devicePixelRatio` or viewport between Safari and the WKWebView
  changes GPU load.
- **Browser chrome** — Safari's toolbars shrink the canvas (less GPU work)
  but Safari itself adds overhead; a Home Screen install removes toolbars.
- **Background throttling differences** — WKWebView in a foreground native
  app and a foreground Safari tab should both run rAF at full rate, but a
  misconfigured shell can keep rendering while covered.
- **Network** — the web version talks to the network on load and sends
  usage beacons; the app is fully bundled. Over a 30-minute run this is
  negligible, but airplane mode levels it if in doubt.
