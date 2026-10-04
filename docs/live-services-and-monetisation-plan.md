# Live services & monetisation plan

How Cinematic Poker makes money, signs players in, tracks them, lets them
manage their account for free, and collects the analytics needed to make
business decisions — **without breaking the game's two founding rules**:

1. **Offline-first.** The game must stay fully playable with no network, no
   account and no server. Online services are additive, never required.
2. **Sell environment packs, never chips.** No real-money wagering, no
   cash-out, no pay-to-win (see README "Content & licensing rules").

## TL;DR — the recommended stack

| Need | Solution | Cost |
|---|---|---|
| Make money | In-app purchases of **environment packs** via Unity IAP (+ optional rewarded ads later) | Free SDK; stores take 15% (likely) or 30% of revenue |
| Players log in | **Unity Authentication**: anonymous sign-in by default, optional Apple/Google account linking | Free, unlimited players |
| Track players | Pseudonymous **player id** on every analytics event (shipped in this repo, works offline today) + Unity Analytics once online | Free |
| Change account details | In-game profile screen writing locally (today) / to Unity Cloud Save (later); passwords & email stay with Apple/Google | Free |
| Data analytics for business decisions | Event taxonomy in `AnalyticsSchema` (shipped) → **Unity Analytics** dashboards, funnels, retention & revenue KPIs | Free tier covers far beyond launch scale |

Everything above costs **£0 in service fees until the game has significant
traction**; the only unavoidable costs are the developer accounts
(Apple $99/yr, Google Play $25 once) and the stores' revenue cut.

Why Unity Gaming Services (UGS) rather than Firebase or a custom backend: the
game is Unity, so it is one SDK family, one dashboard, one privacy policy
integration; Authentication/Cloud Save/Analytics free tiers comfortably cover
an indie launch; and nothing below prevents swapping vendors later because all
game code goes through our own interfaces (`IAnalyticsSink`, service classes),
never vendor APIs directly. Firebase is the strongest alternative (also ~free
at this scale) if cross-engine reuse ever matters. A self-hosted backend is
explicitly rejected for now: a VPS + database + auth + GDPR compliance is
monthly cost and ops burden with zero player-visible benefit at this stage.

---

## 1. Making money

### Primary: premium environment packs (IAP)

The product already points here: environments are self-contained data
(`PokerEnvironmentDefinition` carries NPCs, difficulty, dialogue, tells,
cameras, audio). The roadmap's premium packs (Chicago 1926, Vegas, Disco 1978,
Velvet Mansion, Tokyo, Spaceport…) are the inventory.

- **Base game free** with 2–3 environments (Mates' Kitchen, Western 1860s,
  Kalgoorlie Shed) — free is non-negotiable for discovery on mobile stores.
- **Packs at £2.99–£4.99** (non-consumable, one-time purchase, restorable).
  Each pack = new table, new NPC cast, new dialogue/tells, new music.
- **"Everything" bundle** at a discount once ≥4 packs exist; it typically
  becomes the best seller.
- Implementation: **Unity IAP** package (free) over StoreKit/Play Billing.
  Non-consumables need no server; receipts are validated on-device at first,
  server-side receipt validation can be added later if piracy proves material.
- Store fees: 30% standard, **15% via Apple's Small Business Program and
  Google's equivalent** (both apply under $1M/yr — enrol in both).

### Secondary (optional, test later): rewarded ads

One tasteful placement only — e.g. after busting out, "watch an ad to rebuy".
Rewarded ads don't interrupt and players opt in; interstitials/banners are
ruled out as they would destroy the premium cinematic feel. Use Unity
LevelPlay (free SDK). **Decision rule:** only enable if analytics show a large
cohort that plays many sessions but never buys a pack; then A/B it and watch
D7 retention — if retention drops, remove ads permanently.

### Ruled out

Chips/currency sales (violates the founding rule and gambling-adjacent
regulation), loot boxes, energy timers, subscriptions (nothing recurring to
offer yet — revisit if LLM-driven dialogue ever ships as a live feature).

## 2. Players logging in

**Anonymous-first.** On first launch the game creates an account silently —
no signup wall ever appears before the player reaches a table:

1. **Today (shipped in this repo):** `PlayerAccountService` creates a local
   pseudonymous player id (random GUID) on first launch. Works offline, costs
   nothing, and already gives analytics a stable identity.
2. **Next:** Unity Authentication **anonymous sign-in** — the device gets a
   UGS player id with zero UI. The local GUID is attached as the analytics
   external id so history stays continuous.
3. **Then:** optional **account linking** — "Save your progress" prompt offers
   *Sign in with Apple* (iOS) and *Google Play Games* (Android). Linking
   upgrades the same anonymous account, so nothing is lost. This is what
   enables cross-device restore of purchases and saves.

Rules that keep this cheap and compliant:

- Login is **never required to play** — only to sync across devices.
- No email/password system of our own: credentials, password resets, 2FA and
  breach liability all stay with Apple/Google. That is the single biggest
  cost- and support-avoidance decision in this plan.
- Apple requires *Sign in with Apple* whenever third-party login is offered,
  so it ships in the first login build.
- Unity Authentication is free with no MAU cap.

## 3. Tracking players

Every analytics event carries `player_id` (pseudonymous GUID) and
`session_id` (per app-launch GUID) — implemented in
`Assets/Scripts/Engine/Telemetry/` and recorded on-device as JSON Lines in
`persistentDataPath/analytics/*.jsonl` from day one, even fully offline.

What this enables immediately: sessions per player, session length, hands per
session, environments played, bust-out rates — collectable in playtests today
by pulling the `.jsonl` files off a device.

Once Unity Analytics is wired in (one extra `IAnalyticsSink` adapter in
`TelemetryService` — no gameplay code changes), the same events flow to the
UGS dashboard automatically and per-player tracking becomes fleet-wide:
DAU/MAU, retention cohorts, funnels, revenue per player.

Player *state* (stack, unlocked packs, coaching history) stays in the
existing offline `SaveSystem`; **Unity Cloud Save** later mirrors it per
player id for cross-device continuity (free tier, and our save file is tiny).

## 4. Players changing their account details — free

The game deliberately holds very little personal data, which makes "account
management" small, free and low-risk:

| Detail | Where it lives | How the player changes it | Cost |
|---|---|---|---|
| Display name | Local `player_account.json` (today) → Cloud Save (later) | In-game **Profile** screen → `PlayerAccountService.TryChangeDisplayName` (validation + UI-ready error reasons already implemented) | Free |
| Avatar/character choice | Same | Same profile screen | Free |
| Email / password / 2FA | **Apple or Google — never us** | Their account settings; nothing to build or support | Free |
| Linked sign-in providers | Unity Authentication | Profile screen link/unlink buttons (SDK calls) | Free |
| Delete account & data | Local wipe (today); UGS deletion API (later) | Profile screen → "Delete my data" — wipes save, account file and analytics buffer, calls UGS delete once online accounts exist. **Apple has required in-app account deletion since 2022**, so this ships with the first login build. | Free |

No support inbox, no password-reset emails, no PII database. The profile
screen is a plain uGUI panel — a text field, a save button, and the
link/delete buttons.

## 5. Analytics → business decisions

### Event taxonomy (single source of truth: `AnalyticsSchema`)

Code never tracks ad-hoc strings — only constants from
`Assets/Scripts/Engine/Telemetry/AnalyticsSchema.cs`, so dashboards and game
can't drift apart. The schema ships with:

| Event | Business question it answers |
|---|---|
| `session_start` / `session_end` (duration, hands played) | Retention, session length, engagement — the health metrics everything else hangs off |
| `hand_started` / `hand_completed` / `player_busted` / `game_over` | Is the core loop fun? Are tables too hard/easy (bust rate per environment)? |
| `environment_selected` | Which tables players love → **which premium pack to build next** |
| `coaching_report_viewed` | Is "Improve My Game" a retention driver worth investing in? |
| `pack_store_viewed` → `pack_viewed` → `pack_purchase_started` → `pack_purchase_completed`/`failed` | The full purchase funnel: where buyers drop off, which packs and price tiers convert |
| `ad_offered` / `ad_watched` | Rewarded-ad engagement, if ever enabled |
| `account_created` / `account_details_changed` / `account_linked` | Login & profile adoption — is cross-device sync worth its surface area? |

### KPIs to put on the dashboard

- **Retention D1 / D7 / D30** — the single most important signal; mobile
  benchmarks ≈ 25–30% / 10% / 4% for casual card games.
- **Average session length** and **hands per session** — the Phase 3 bar
  ("15–30 minutes entertaining") becomes measurable instead of anecdotal.
- **Conversion rate** (% of players who ever purchase) — 1–3% is normal for
  freemium; packs-not-chips may skew higher among retained players.
- **ARPDAU** and revenue per pack.
- **Funnel: store viewed → pack viewed → purchase started → completed.**

### Decision playbook (examples)

- D1 retention < 20% → the core loop needs work before any monetisation
  effort; look at `game_over`/`player_busted` timing for difficulty spikes.
- Store viewed → purchase < 1.5% → test price tiers, improve pack previews
  (cinematic flythrough), or reposition the store entry point.
- One environment dominates `environment_selected` → its theme guides the
  next paid pack; a dud environment is a warning before building its sequel.
- Many high-session players with zero purchases → that's the cohort for the
  rewarded-ad experiment (§1), never for nagging popups.
- `pack_purchase_failed` spikes → store integration bug; treat as an outage.

### Privacy & compliance (cheap because the data is minimal)

- Events are pseudonymous (random GUID) — no name, email, device id or IP
  stored by the game itself. Display name is never attached to events.
- GDPR: consent toggle in settings before any *vendor* upload (local-only
  JSONL needs no consent, but ask anyway for goodwill); "Delete my data" in
  the profile screen covers erasure.
- Apple ATT prompt is **not** needed for first-party analytics of this kind —
  only if ad SDKs that track across apps are added (another reason to be slow
  about ads).
- Age gating: the game has simulated gambling → expect 12+/17+ style ratings;
  no COPPA-directed audience.

### The web version

The Blazor WebAssembly build is a shareable demo, not a revenue surface.
Keep it as marketing; if traffic data is wanted, use a cookieless counter
(e.g. Cloudflare Web Analytics, free) rather than porting the game taxonomy.

## 6. Implementation roadmap

| Step | What ships | New dependencies |
|---|---|---|
| **A — this PR** | Pseudonymous identity, display-name changes with validation, event taxonomy, offline JSONL telemetry, tests | None — pure C# + two Unity-layer services |
| **B** | Wire `Track` calls into gameplay (`PokerTableController`/bootstraps: hand & session events) and build the Profile UI panel | None |
| **C** | Unity Analytics adapter sink + consent toggle; dashboards & funnels | `com.unity.services.analytics` |
| **D** | Unity Authentication: anonymous sign-in, then Apple/Google linking; account deletion flow | `com.unity.services.authentication` |
| **E** | Unity IAP + first two premium packs; purchase funnel live | `com.unity.purchasing` |
| **F** | Cloud Save mirror of `SaveData` for cross-device | `com.unity.services.cloudsave` |

Steps B onward each need the Unity Editor and a UGS project id (free,
dashboard.unity3d.com); A is fully covered by the `dotnet test` suite in CI.

## What's already in this repository

- `Assets/Scripts/Engine/Telemetry/` — `PlayerIdentity`, `AnalyticsSchema`,
  `AnalyticsEvent`, `AnalyticsTracker`, `BufferedAnalyticsSink`,
  `AnalyticsJson` (pure C#, no Unity dependency, compiled by the DotNet
  solution like the rest of the engine).
- `Assets/Scripts/Game/Services/` — `PlayerAccountService` (local account
  file, atomic writes, free display-name changes) and `TelemetryService`
  (auto session tracking, periodic + on-pause/quit flush to
  `persistentDataPath/analytics/*.jsonl`).
- `Assets/Tests/EditMode/TelemetryTests.cs` — 10 tests covering identity,
  validation, session accounting, JSON output and buffer bounds; they run in
  Unity's Test Runner and in `dotnet test`.
