# Cinematic Poker — iOS wrapper

A [Capacitor](https://capacitorjs.com) shell that bundles the **Blazor
WebAssembly web version** (`Web/CinematicPoker.Web`) into a native iOS app.
No Unity involved: the exact game you can play in the browser runs fullscreen
and offline in a WKWebView, locked to landscape, with all models, audio and
the WASM engine packaged inside the app.

The native project (`ios/`) is committed; the staged web bundle (`www/`) and
`node_modules/` are generated and git-ignored.

## Prerequisites (on a Mac)

- Xcode (current version) with iOS SDK — the only step that truly needs macOS
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (`dotnet`)
- Node 20+ (`node`, `npm`)
- No CocoaPods needed — Capacitor 8 uses Swift Package Manager

## Build and run on your iPhone

```bash
cd Mobile/ios-wrapper
npm install
npm run ios        # publishes the Blazor app, stages www/, syncs, opens Xcode
```

Then in Xcode:

1. Select the **App** target → *Signing & Capabilities* → choose your
   **Team** and leave *Automatically manage signing* on.
   - A **free Apple ID** works for playing on your own phone (add it in
     Xcode → Settings → Accounts). Free provisioning expires after 7 days —
     just hit Run again to re-install. The paid Developer Program
     ($99/yr) is only needed for TestFlight / App Store distribution.
2. Plug in the iPhone, pick it as the run destination, press **Run**.
3. First run on a device: the phone will ask you to trust the developer
   certificate (Settings → General → VPN & Device Management).

## Testing on two phones before submitting

**Your own phone** needs nothing beyond the steps above — a free Apple ID
and a cable.

**Someone else's phone** (a remote friend) needs **TestFlight**, which
requires the paid Apple Developer Program ($99/yr). A free Apple ID cannot
distribute remotely — it only installs onto devices physically plugged into
your Mac, and those installs expire after 7 days. Ad hoc `.ipa` distribution
also requires the paid program and is more hassle than TestFlight. Since the
program is required for the App Store submission anyway, enrol as soon as
testing with others begins — enrolment is the long-lead item.

Once enrolled:

1. In Xcode: set the Team to the paid account, **Product → Archive**, then
   *Distribute App → App Store Connect* (create the app record first at
   [appstoreconnect.apple.com](https://appstoreconnect.apple.com) with
   bundle id `com.cinematicpoker.app` if it does not exist yet).
2. In App Store Connect → **TestFlight**: once the build finishes
   processing (minutes to an hour), add your friend as an **internal
   tester** (invite his Apple ID email; no review needed, available
   immediately) — or create an external-testing public link (first build
   goes through a light beta review, usually under 24 h).
3. Your friend installs the **TestFlight app** from the App Store, opens
   the invite, and taps Install. Every new build you upload reaches him
   automatically. Builds expire after 90 days.

**Before the paid account exists**, a remote friend can already playtest
the game itself in Safari via the GitHub Pages deployment ("Add to Home
Screen" runs it fullscreen). That is the same WebKit engine and the same
game code the wrapper ships — it just doesn't exercise the native shell.

## TestFlight builds from CI (no Mac in the loop)

The [`iOS TestFlight Build`](../../.github/workflows/ios-testflight.yml)
workflow builds, signs and uploads the wrapper to TestFlight on a GitHub
macOS runner — trigger it from the repo's **Actions** tab. One-time setup:

1. **Apple Developer Program** membership active.
2. **Create the app record** in
   [App Store Connect](https://appstoreconnect.apple.com) → My Apps → ＋ →
   New App, with bundle id `com.cinematicpoker.app` (register the bundle id
   at [developer.apple.com/account](https://developer.apple.com/account) →
   Identifiers if it is not offered in the dropdown).
3. **App Store Connect API key**: Users and Access → Integrations →
   App Store Connect API → ＋. Role: App Manager. Note the **Key ID** and
   **Issuer ID**, download the `.p8` file (one chance only).
4. **Apple Distribution certificate**: on any Mac, Xcode → Settings →
   Accounts → your team → Manage Certificates → ＋ → Apple Distribution,
   then export it from Keychain Access as a password-protected `.p12`
   (Keychain Access → My Certificates → right-click the certificate →
   Export). No Mac at all? `fastlane cert` on a borrowed machine or a
   one-off cloud Mac works too.
5. Add **repository secrets** (Settings → Secrets and variables → Actions):

   | Secret | Value |
   |---|---|
   | `APPLE_TEAM_ID` | Team ID from the developer account membership page |
   | `ASC_KEY_ID` | API key ID from step 3 |
   | `ASC_ISSUER_ID` | Issuer ID from step 3 |
   | `ASC_KEY_P8_BASE64` | `base64 -i AuthKey_XXXX.p8` |
   | `DIST_CERT_P12_BASE64` | `base64 -i certificate.p12` |
   | `DIST_CERT_PASSWORD` | The `.p12` export password |

6. Run the workflow. It publishes the Blazor app, syncs the iOS project,
   fetches/creates the App Store provisioning profile automatically, builds
   a signed `.ipa` (build number = run number), and uploads it to
   TestFlight. After Apple's processing (minutes to an hour), add testers
   in App Store Connect → TestFlight and install via the TestFlight app.

## Updating after game changes

Whenever the web game changes, re-stage and re-sync:

```bash
npm run build:www && npm run sync
```

(`npm run ios` does both and reopens Xcode.)

## What the wrapper configures

- **Landscape only** (the game's design; the web version shows a "rotate
  your phone" overlay in portrait — the native lock makes that unreachable).
- **Status bar hidden**, fullscreen, `viewport-fit=cover` safe-area handling
  comes from the web app itself.
- **`ITSAppUsesNonExemptEncryption = NO`** — removes the export-compliance
  question from every App Store Connect upload.
- Bundle id `com.cinematicpoker.app`, display name **Cinematic Poker**,
  iOS 15+ deployment target.
- Pre-compressed `.br`/`.gz` publish artifacts are stripped from the bundle
  (no server content-negotiation inside the app).

## Notes

- The Google Fonts stylesheet (Rye display face) is fetched over the network;
  offline it falls back to a system font. Everything else is bundled.
- App icon / splash are Capacitor defaults for now — replace
  `ios/App/App/Assets.xcassets` contents before any store submission
  (`npx @capacitor/assets generate` can do this from one 1024×1024 image).
- For the full App Store submission checklist see
  [`docs/ios-app-store-plan.md`](../../docs/ios-app-store-plan.md) — this
  wrapper is "Track B" in that plan.
