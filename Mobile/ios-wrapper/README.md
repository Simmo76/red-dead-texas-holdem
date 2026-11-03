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
