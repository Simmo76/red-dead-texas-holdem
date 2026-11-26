# Testing & regression plan

How Cinematic Poker stays stable as the web table, toolbar, and engine evolve.
Written for agents and humans picking up work with no prior context.

## Principles

1. **Pure logic gets unit tests** — anything that can run in Node without a
   browser or Unity belongs in a small module under
   `Web/CinematicPoker.Web/wwwroot/js/` plus a matching
   `tools/*.test.mjs` file.
2. **Engine rules stay in NUnit** — poker, AI, telemetry, coaching fixtures,
   and the 10,000-hand money-conservation soak live in
   `Assets/Tests/EditMode/` and compile through `DotNet/`.
3. **UI contracts get acceptance checks** — static assertions on Razor, CSS,
   assets, and handler names (`tools/toolbar-acceptance.test.mjs`) catch wiring
   regressions without Playwright.
4. **CI runs the full harness** — every pull request and every push to `main`
   or `cursor/**` runs `regression-tests.yml`. **Pages deploy workflows do not
   start until that workflow succeeds** (`workflow_run` gate). Merges to `main`
   should also require the **Regression harness** status check (see
   `.github/BRANCH_PROTECTION.md`).

## Layers

| Layer | What it guards | How to run |
|-------|----------------|------------|
| **Engine** | Cards, pots, side pots, AI, telemetry, coach CSV, 10k simulation | `dotnet test DotNet` |
| **Web logic** | Table lineup (women/skin rules), frame governor, pixel budget | `node tools/<name>.test.mjs` |
| **Web UI** | Mobile toolbar markup, tokens, breakpoints, icons | `node tools/toolbar-acceptance.test.mjs` |
| **Release tooling** | Preview artifact size budget | `bash tools/collect-previews.test.sh` |
| **Release tooling** | Branch preview includes `main` | `bash tools/branch-preview-ready.sh` |
| **Manual / smoke** | Full 3D hand, audio, coach overlay, iOS PWA | Local `dotnet run` in `Web/CinematicPoker.Web` |

## Regression harness (one command)

From the repo root:

```bash
tools/run-harness.sh
```

Order of execution:

1. `dotnet test DotNet/CinematicPoker.Engine.Tests/…`
2. Every `tools/*.test.mjs` (discovered in filename order)
3. `tools/collect-previews.test.sh`

| Workflow | When |
|----------|------|
| `regression-tests.yml` | Every PR + push to `main` and `cursor/**` (**gate**) |
| `deploy-web.yml` | Only after **successful** regression on a `main` push |
| `deploy-preview.yml` | Only after **successful** regression on a `cursor/**` push |
| `android-apk.yml` | Full `dotnet test DotNet` when Unity assets change |

**Merge policy:** enable branch protection on `main` requiring check
**Regression harness** (steps in `.github/BRANCH_PROTECTION.md`).

No preview URL is published and no live Pages deploy runs while regression is
red or still running.

### Branch preview policy (agents)

GitHub Pages branch builds live under
`https://simmo76.github.io/red-dead-texas-holdem/preview/<slug>/` where
`<slug>` is the branch name with `/` replaced by `-` (see
`.github/workflows/deploy-preview.yml`).

Before sharing that link for a `cursor/*` branch:

1. Rebase onto latest `origin/main` and push (`git fetch origin main &&
   git rebase origin/main && git push --force-with-lease`).
2. Run `bash tools/branch-preview-ready.sh` (included in `tools/run-harness.sh`).
   CI fails the harness if the branch tip is behind `origin/main`.

Rationale: preview deploys **only the branch tree**. A branch cut before HUD or
asset landings on `main` will look outdated (e.g. the old CSS stack pill instead
of `stack-plaque.png`) even though live `main` is current.

## When to add tests

| Change type | Required test |
|-------------|----------------|
| New betting / pot / showdown rule | NUnit in `Assets/Tests/EditMode/` |
| New table cast / lineup invariant | Extend `table-lineup.test.mjs` or module tests |
| Mobile GPU / render budget tweak | `frame-governor.test.mjs` and/or `pixel-budget.test.mjs` |
| Toolbar or top HUD layout | `toolbar-acceptance.test.mjs` + update `docs/toolbar/IMPLEMENTATION.md` |
| Bug fix for a past PR | **Regression test** named after the bug (comment in test file citing PR/issue) |

### Adding a web unit test module

1. Export pure functions from `wwwroot/js/<feature>.js` (no `window` / Three.js
   in the test surface — pass inputs as parameters).
2. Add `tools/<feature>.test.mjs` importing that module (relative path like
   existing lineup tests).
3. Run `tools/run-harness.sh` locally.
4. Mention the test file in the feature's plan doc acceptance section.

## Feature plans that reference this harness

- **Mobile GPU** — `docs/mobile-gpu-optimisation-plan.md` (frame governor +
  pixel budget unit tests)
- **Mobile toolbar** — `docs/toolbar/IMPLEMENTATION.md` (static acceptance +
  engine tests)
- **Live services foundation** — Step A in
  `docs/live-services-and-monetisation-plan.md` (telemetry NUnit)

## What is deliberately not automated (yet)

- Playwright / headless Three.js render tests (heavy; coach mode and GLB load
  need a real browser)
- Unity EditMode in CI (Android workflow runs `dotnet test` only — Unity
  licence required for play mode)
- Device thermal measurement (manual check after GPU changes)

Candidates for a later tier: export `textureAudit()` / `holdDebug()` hooks
from `poker-scene.js` into a thin module for DOM-less scene health checks once
a browser runner is justified.

## Local prerequisites

- **.NET 8 SDK** — engine tests and Blazor host
- **Node 18+** — ESM `import` in `tools/*.test.mjs`
- **bash** — `collect-previews.test.sh`

No Unity install is required for the harness.