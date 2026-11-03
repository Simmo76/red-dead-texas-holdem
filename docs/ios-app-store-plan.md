# iOS App Store release plan

A step-by-step plan to get Cinematic Poker onto the iOS App Store, written
against the current state of this repository (Unity 6000.0.32f1 project with an
Android-only build pipeline, a pure C# engine, and a polished Blazor
WebAssembly web version).

---

## Step 0 — Decide what actually ships

There are two viable builds, and the first real decision is which one goes to
the store first.

| | Track A — Unity native app | Track B — packaged web version |
|---|---|---|
| What it is | The Unity project in this repo, built for iOS (IL2CPP → Xcode project) | The Blazor WASM app in `Web/CinematicPoker.Web` packaged in a native shell (.NET MAUI `BlazorWebView` or Capacitor + WKWebView) |
| Visual state today | Phase 2 **primitive prototype** — runtime-generated scene, Kenney mini-characters / capsules | The full saloon experience: Jacob/Sheriff/Bandit characters, three.js scene, audio, dialogue |
| Work to reach shippable | Significant presentation work (effectively finishing Phase 3) | Days of packaging/integration work; the game itself is done |
| Risk | Schedule risk only; native Unity apps sail through review | Guideline 4.2 "minimum functionality" scrutiny for web-wrapped apps (low risk here — it is a complete, fully offline game, not a repackaged website, but Apple looks harder at WKWebView apps) |
| Long-term fit | This is the product the README describes | A stop-gap or a permanent choice if the web version *is* the product |

**Recommendation:** if the goal is "on the App Store as soon as possible",
ship **Track B** (specifically .NET MAUI Blazor Hybrid, because the Razor
components and the pure C# engine can be reused directly rather than wrapped
as a remote site — everything is bundled, offline, and genuinely native-shell).
If the goal is the cinematic Unity product, follow Track A and accept that the
store submission waits for the Phase 3 vertical slice to look shippable.

Every step below applies to both tracks except Step 3, which has an A and a B
variant.

---

## Step 1 — Accounts and hardware (can start today, has lead time)

1. **Enrol in the Apple Developer Program** ($99/year) at
   [developer.apple.com/programs](https://developer.apple.com/programs/).
   - *Individual* enrolment is fastest (hours–days) and the seller name shown
     on the store is your personal name.
   - *Organization* enrolment shows a company name but requires a registered
     legal entity and a **D-U-N-S number** (free, but can take 1–2 weeks to
     issue). Decide now — changing later is painful.
2. **Get access to a Mac** with the current Xcode (Apple requires recent
   Xcode/SDK versions for store submissions; Unity 6000.0.32f1 works with
   Xcode 16). Options: a physical Mac, a cloud Mac (MacStadium, AWS EC2 Mac),
   or GitHub Actions `macos-14`/`macos-15` runners for CI-only builds. There
   is no way to submit to the App Store without macOS somewhere in the loop.
3. **Get at least one physical iPhone** for testing (the oldest device you
   intend to support is the most valuable one). An iPad too, since the game
   targets iPadOS.
4. In **App Store Connect**, accept the latest developer agreements. The free
   app needs nothing more; the Paid Applications agreement (plus banking and
   tax forms) only becomes necessary when in-app purchases are added (see
   Step 8).
5. (Track A only) In Unity Hub, install the **iOS Build Support** module for
   6000.0.32f1.

## Step 2 — Legal, naming, and licensing pre-flight

These are the things most likely to cause a rejection or a takedown, and all
of them are cheap to fix now.

1. **Drop every trace of "Red Dead".** The repository is named
   `red-dead-texas-holdem`; the app, its bundle id, App Store name, subtitle,
   keywords, screenshots and marketing text must not contain "Red Dead" or
   anything else evoking Rockstar's trademark (App Review guideline 5.2,
   and Rockstar actively enforces). The product name "Cinematic Poker"
   already in `BuildScript.cs` is fine. Consider renaming the GitHub repo for
   hygiene, though Apple never sees it.
2. **Check the App Store name is available.** App names are unique per
   territory; reserve "Cinematic Poker" (or the final name) early by creating
   the app record (Step 5) — the name is claimed at that point, not at
   submission.
3. **Verify asset licences cover store distribution.** `ASSET_LICENSES.md` is
   in good shape, but several Unity Asset Store packs were "supplied privately
   via Dropbox". Confirm the Asset Store licences are held by the same legal
   entity that will publish the app (Asset Store licences are per-entity, and
   embedding converted derivatives in a shipped app is exactly the licensed
   use — it just has to be *your* licence). Do the same check for the
   owner-supplied music ("Epidemic Sound / AAAGCO style pack" voice lines,
   battle music, etc.): Epidemic Sound in particular licenses per-use and a
   personal subscription does not automatically cover in-app game audio.
   Resolve or replace anything uncertain before submission, not after.
4. **Write and host a privacy policy.** A privacy policy URL is mandatory for
   every App Store app, even a fully offline one that collects nothing. One
   page ("this game runs entirely on your device, collects nothing, transmits
   nothing") hosted anywhere public (GitHub Pages works) is sufficient. Also
   prepare a **support URL** (can be the same page or the repo's Pages site).
5. **Know your age rating before you fill in the questionnaire.** Texas
   hold'em with chips is **simulated gambling** in Apple's rating
   questionnaire. Answer honestly ("frequent/intense" — it is the whole
   game); under Apple's current global age-rating system expect the app to
   land at a high rating (17+/18+ tier). That is normal for poker apps and
   not a rejection risk by itself. Do **not** answer "none" to dodge the
   rating — that is a guaranteed rejection or later takedown. Note: no real
   money, no cash-out, no loot boxes keeps you out of guideline 5.3
   (real-money gaming) entirely, which is the dangerous one.
6. **Export compliance:** the app uses no custom cryptography, so declare the
   exemption up front by adding `ITSAppUsesNonExemptEncryption = NO` to the
   Info.plist (Step 3). This removes the export-compliance questions from
   every single build upload.

## Step 3 — Make the project build for iOS

### Track A — Unity

1. **Add an iOS build target to `Assets/Scripts/Editor/BuildScript.cs`**,
   mirroring `BuildAndroid()`:
   - `BuildTarget.iOS`, output `Builds/iOS` (Unity emits an **Xcode project**,
     not an .ipa).
   - `PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS,
     "com.<yourentity>.cinematicpoker")` — pick the final bundle id now; it
     can never change after the first App Store upload.
   - `PlayerSettings.iOS.targetOSVersionString` — iOS 15 or 16 is a sensible
     floor; Unity 6's own minimum is lower but older devices will struggle
     with URP anyway.
   - Scripting backend IL2CPP / ARM64 (iOS default — nothing to do),
     `targetDevice = iPhoneAndiPad`.
   - A menu item `Cinematic Poker → Build → iOS (Xcode project)` for editor
     use, and keep the method batchmode-callable for CI like `BuildAndroid`.
2. **App icon**: author a 1024×1024 icon (no alpha, no rounded corners) and
   assign it in Player Settings. This is also the App Store icon. Unity
   generates the smaller sizes.
3. **Launch screen**: configure Unity's iOS launch screen (storyboard) —
   a solid colour + logo is fine; a blank default looks broken in review.
4. **Orientation** is already handled (`ApplySharedPlayerSettings` sets
   landscape-only auto-rotation) and the UI already uses `SafeAreaFitter`,
   so the notch/Dynamic Island should be safe — verify on device.
5. **Privacy manifest**: Unity 6 embeds its engine `PrivacyInfo.xcprivacy`
   (required-reason API declarations) automatically. With no third-party
   SDKs in `Packages/manifest.json` there is nothing else to declare. When
   Unity IAP / Analytics are added later, re-check (those ship their own
   manifests).
6. **Info.plist additions** via a post-build processor
   (`OnPostprocessBuild`) or manually in Xcode:
   `ITSAppUsesNonExemptEncryption = NO`. No usage-description strings are
   needed — the game touches no camera/mic/location/photos. (If
   `WinPhotoCapture`-style screenshot saving ever comes to iOS, that changes:
   saving to Photos requires `NSPhotoLibraryAddUsageDescription`.)
7. **Commit `ProjectSettings/ProjectSettings.asset`.** Today only
   `ProjectVersion.txt` is committed and settings are applied in code at
   build time. That works, but icons, launch screens, the signing team id and
   splash settings live in `ProjectSettings.asset` — committing it makes
   builds reproducible on CI and other machines.
8. **Performance pass on device** (this is the gate for Track A): URP +
   Monte Carlo equity on a real iPhone, oldest supported model. The repo
   already has `PerformanceManager` tiers — validate Battery Saver 30 FPS
   actually holds. Apple rejects apps that hard-crash or visibly jank on
   review devices (guideline 2.1 app completeness).

### Track B — packaged web version

1. **Create a .NET MAUI Blazor Hybrid app** (`dotnet new maui-blazor`) in a
   new `Mobile/` folder. Reference the same engine sources the web project
   uses and move the Razor components into a shared Razor class library so
   `Web/` and `Mobile/` consume identical UI. The three.js scene runs inside
   the `BlazorWebView` (WKWebView) exactly as it does in mobile Safari — the
   web version already works there, which de-risks this heavily.
   - Alternative: **Capacitor** wrapping the published WASM output
     (`dotnet publish` → copy `wwwroot` into a Capacitor iOS shell). Less
     .NET plumbing, but you lose the shared-component story and debugging is
     clunkier. Pick one; MAUI is the better fit for this codebase.
2. All assets (models, audio, textures) must be **bundled, not fetched** —
   they already live in `wwwroot`, so the app is genuinely offline-first,
   which is both the product promise and your guideline 4.2 defence.
3. Bundle id, icon, launch screen, `ITSAppUsesNonExemptEncryption`,
   landscape-lock: same requirements as Track A, configured in the MAUI
   project (`Platforms/iOS/Info.plist`).
4. **Device performance pass**: three.js + WASM in WKWebView on the oldest
   target iPhone. Check load time (meshopt backdrop + character GLBs),
   memory (WKWebView gets killed around 1.5 GB on older devices), and audio
   unlock behaviour (already handled for iOS Safari per `ASSET_LICENSES.md`
   notes about MP3 re-encoding).

## Step 4 — Signing, first device build, first archive

1. In the Apple Developer portal (or just let Xcode do it), create an
   **App ID** matching the bundle id. No special capabilities are needed
   (no push, no Game Center, no iCloud — yet).
2. Open the generated Xcode project (Track A) or the MAUI iOS head (Track B),
   set the **Team**, and use **Automatically manage signing**. Manual
   certificates/profiles (fastlane match) only become worthwhile at the CI
   stage (Step 9).
3. Build to a **physical device** and play several full hands. Specifically
   test: app backgrounding mid-hand (save system should survive), aeroplane
   mode (must be a non-event), rotation between the two landscape
   orientations, mute switch vs. game audio, and a fresh-install first-run.
4. **Archive** (Product → Archive) and confirm the archive validates against
   App Store Connect (Xcode Organizer → Validate App). This catches icon,
   entitlement, SDK-version and privacy-manifest problems in minutes rather
   than at review time.

## Step 5 — Create the App Store Connect record

1. App Store Connect → My Apps → **＋ New App**: platform iOS, the reserved
   name, primary language, the bundle id from Step 3, and an SKU (any
   internal string, e.g. `cinematic-poker-ios`).
2. Fill in **App Information**: category **Games › Card** (secondary
   **Games › Casino**), content rights declaration (you have rights —
   backed by `ASSET_LICENSES.md`), and the **age rating questionnaire**
   (simulated gambling: frequent — see Step 2.5).
3. **App Privacy**: with the app fully offline and the repo's analytics
   local-only, the label is **"Data Not Collected"** — a genuine selling
   point. The moment Unity Analytics / Unity Auth / ads are switched on
   (per `docs/live-services-and-monetisation-plan.md`), this label must be
   redone, and ads would additionally trigger App Tracking Transparency.
   Ship v1 clean.
4. **Pricing**: Free, all territories (or trim territories if the licensing
   check in Step 2.3 found restrictions).
5. **Version metadata**:
   - Description (lead with: single-player, offline, no real money, no ads,
     no account).
   - Keywords (poker, hold'em, offline, card game, single player, western…
     — no competitor app names, no "Red Dead").
   - Support URL + privacy policy URL from Step 2.4.
   - **Screenshots**: required sets are 6.9″ iPhone and, because the app
     runs on iPad, 13″ iPad. Landscape screenshots of actual gameplay —
     the saloon table scene is the money shot. Up to 10 per device; the
     first 3 matter most. An optional 15–30 s app preview video is worth it
     for a visual game but not required for v1.

## Step 6 — TestFlight beta

1. Upload a build (Xcode Organizer → Distribute App → App Store Connect, or
   `xcrun altool`/Transporter). The build appears in TestFlight after
   automated processing (minutes to an hour).
2. **Internal testing** (up to 100 users on your team, no review needed):
   shake out crashes, save-system integrity, device coverage.
3. **External testing** (up to 10,000 testers via public link; requires a
   lightweight **beta app review**, usually < 24 h): this is also a free
   dress rehearsal of App Review — if the beta review flags something
   (e.g. the gambling questionnaire), you learn it before the real
   submission.
4. Iterate until crash-free sessions and the fun bar (the README's own
   gate: "entertaining for 15–30 minutes") are met. Watch TestFlight's
   crash reports and feedback screenshots.

## Step 7 — Submit for review

1. Select the final build on the version page, complete **export
   compliance** (auto-answered if `ITSAppUsesNonExemptEncryption` is set),
   and add **review notes**: explicitly state *"single-player poker versus
   AI opponents; no real-money wagering, no prizes or cash-out, no
   user-generated content, no account, fully offline"*. This pre-answers
   the questions a reviewer of any poker app will have. No demo account is
   needed since there is no login.
2. Choose release option: **manual release** after approval is recommended
   for a first launch (approve ≠ instantly live, so you control day one).
3. Submit. Typical review time is 24–48 hours currently.
4. **Likely rejection reasons and the pre-emption for each:**
   - *Guideline 2.1 (crashes/incomplete)* → the TestFlight soak in Step 6.
   - *Guideline 4.2 (minimum functionality, Track B only)* → emphasise in
     review notes that the game is fully bundled and offline (not a
     website wrapper); native shell features (proper launch screen, icon,
     safe-area handling, no browser chrome) help the impression.
   - *Age rating mismatch (simulated gambling)* → answered honestly in
     Step 5.2.
   - *Guideline 5.2 (IP)* → Step 2.1's trademark scrub.
   - If rejected: fix, reply in Resolution Center (a clear explanation
     sometimes reverses a misunderstanding without a new build), resubmit.
     Rejections are routine, not fatal.
5. On approval: release, then check the live store listing on a real device.

## Step 8 — Post-launch: monetisation (second release)

Per `docs/live-services-and-monetisation-plan.md`, v1 ships free with no IAP.
When environment packs are ready:

1. Sign the **Paid Applications agreement** in App Store Connect and complete
   banking/tax (allow a few days for processing).
2. Enrol in the **App Store Small Business Program** (15% commission instead
   of 30% under $1M/yr) — enrolment is not automatic.
3. Create **non-consumable IAP products** (one per environment pack, plus the
   bundle), integrate Unity IAP (Track A) or StoreKit 2 via MAUI bindings
   (Track B), and implement **Restore Purchases** (its absence is an
   automatic rejection for non-consumables).
4. IAPs are reviewed with the app version that introduces them; screenshots
   of the purchase flow are required per product.
5. Revisit the privacy label if any analytics/auth service ships in the same
   release.

## Step 9 — Automate (parallel track, optional but cheap)

Mirror the existing Android CI (`.github/workflows/android-apk.yml`):

1. GameCI's iOS flow on a `macos-*` runner: run the engine tests (already
   scripted), `unity -batchmode -executeMethod BuildScript.BuildiOS` to emit
   the Xcode project, then **fastlane** (`build_app` + `upload_to_testflight`)
   with **fastlane match** for signing certificates in a private repo.
   Needs the same Unity licence secrets the Android workflow uses, plus an
   **App Store Connect API key** (Users and Access → Integrations) for
   fastlane.
2. Result: every push to `main` can produce a TestFlight build, the same way
   pushes already deploy the web version to GitHub Pages.

---

## Condensed checklist

- [ ] Apple Developer Program enrolment (decide individual vs. organization/D-U-N-S)
- [ ] Mac + Xcode access secured; test iPhone/iPad in hand
- [ ] Track decision: Unity native (A) vs. packaged web (B)
- [ ] Trademark scrub ("Red Dead" nowhere near the store listing)
- [ ] Asset/music licences confirmed for the publishing entity
- [ ] Privacy policy + support URL live
- [ ] iOS build target added (`BuildScript.BuildiOS` or MAUI project); bundle id finalised
- [ ] Icon, launch screen, `ITSAppUsesNonExemptEncryption=NO`, landscape + safe area verified
- [ ] `ProjectSettings.asset` committed (Track A)
- [ ] Device performance pass on oldest supported iPhone
- [ ] First archive validates in Xcode Organizer
- [ ] App Store Connect record: name, category, age rating (simulated gambling — answered honestly), privacy label "Data Not Collected"
- [ ] 6.9″ iPhone + 13″ iPad landscape screenshots
- [ ] TestFlight internal → external beta, crash-free
- [ ] Review notes written (offline, no real money, no account)
- [ ] Submit, manual release on approval
- [ ] Later: Paid Apps agreement, Small Business Program, non-consumable IAPs + Restore Purchases
- [ ] Later: CI → TestFlight via GameCI + fastlane
