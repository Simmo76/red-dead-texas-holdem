# Apple App Store listing design — Dead Man's Hand

A complete App Store Connect listing design for the game's iOS/iPadOS release.
The positioning leans on the two things no other mobile poker game does well:
**you actually get better at poker while you play** (the table coach and the
live advisor), and **the places you play are worth looking at** (the Saloon
and the Octagon). Everything below fits App Store Connect's field limits.

All screenshots in `screenshots/` are real captures from the current build
(the Blazor web build, which renders the same tables, NPCs, coach and HUD the
mobile app ships). Regenerate them any time with `tools/appstore/` — see
[Asset production](#asset-production) at the end.

---

## 1. Positioning

**One-line pitch:** The poker game that teaches you poker — cinematic Texas
Hold'em against five characterful AI outlaws, with a coach in your corner,
entirely offline.

Three pillars, in the order the store assets present them:

1. **Learn as you play.** A tap-to-ask "What would AI do?" advisor on your
   turn (recommended action, equity, pot odds, a plain-English reason), and a
   post-hand **Poker Coach** — Maximilian in the Saloon, Tommy Gunn in the
   Octagon — who reviews why you won or lost and gives one thing to try.
   Rules and hand-rankings references are one tap away mid-hand.
2. **Extraordinary places.** Cinematic 3D tables with seated presence: the
   old-west **Saloon** and the cage-fighting **Octagon**, each with its own
   cast, coach, audio and atmosphere. More environments ship as packs.
3. **Honest offline poker.** Single player, no account, no server, no
   real-money wagering, chips are never for sale. Works on a plane.

**Audience:** people who want to learn or sharpen Texas Hold'em without the
sharks, stakes or spam of real-money/social casino apps; players who liked
poker minigames inside story games and want that as the whole game.

**Avoid in all copy:** anything implying real-money gambling, "win big",
"jackpot", "casino winnings". It is factually wrong for this game and drags
the listing into the wrong review category and the wrong search neighbourhood.

---

## 2. App Store Connect metadata

### App name (30 chars max)

> **Dead Man's Hand: Poker Coach** — 28 chars

The brand plus the differentiator. "Poker" and "Coach" in the name are the
two highest-value search terms we can own; generic "Texas Hold'em" names are
unwinnable search territory against the real-money giants.

Fallback if the name is taken in a storefront: `Dead Man's Hand — Learn Poker`
(29 chars).

### Subtitle (30 chars max)

> **Learn Texas Hold'em. Offline.** — 29 chars

Carries the learning promise and the offline hook, and adds "Texas Hold'em"
as indexed search text without spending keyword characters on it.

### Promotional text (170 chars max — editable without review)

> Sit down with five outlaws, let the table coach review every hand, and
> learn to think in equity and pot odds. Fully offline — no account, no
> real-money bets. — 157 chars

Use this slot for seasonal pushes later (new environment packs), since it can
change without a new build.

### Keywords (100 chars max, comma-separated)

> `holdem,cards,saloon,western,cowboy,octagon,odds,equity,strategy,trainer,bluff,tells,solo,ai,chips` — 97 chars

Notes: "poker", "coach", "learn", "texas", "hold'em" are already indexed from
the name/subtitle — never duplicate them. `holdem` (no apostrophe) is a
distinct, high-volume spelling. `tells` and `equity` are low-competition
terms that exactly match what the game teaches.

### Categories

- **Primary:** Games → Card
- **Secondary:** Games → Casino

(Education as secondary was considered and rejected: it buries the app among
flashcard apps, and Card/Casino is where poker intent lives. The learning
story is told by the name, subtitle and screenshots instead.)

### Age rating

- "Simulated Gambling" → **Frequent/Intense** → results in a **17+** rating
  unless offset; Apple's questionnaire currently yields **12+** when
  simulated gambling is marked Infrequent/Mild. Poker *is* the whole game, so
  answer **Frequent/Intense Simulated Gambling → 17+** and do not try to
  game the questionnaire. No other flags (no violence, no profanity in
  shipped dialogue, no user-generated content, no unrestricted web).
- Set "Made for Kids" to **No**.

### Pricing & IAP

- **Free**, with non-consumable **environment pack** IAPs (per the
  monetisation plan: base game free with the launch environments, packs at
  £2.99–£4.99, bundle later). No consumables, no chips for sale, no ads at
  launch.
- IAP display names are store-searchable; name them as destinations:
  - `The Octagon — Table & Coach` (if the Octagon ships as the first pack)
  - `Chicago 1926 — Table & Coach`
  - `Everything Bundle`
- Each IAP needs its own promo image (1024×1024): use that environment's
  splash art with the pack name.

### App Review notes (the "Notes" field for the reviewer)

> Dead Man's Hand is a single-player, offline Texas Hold'em game against AI
> opponents. There is no real-money wagering, no cash-out, no chip purchases,
> no multiplayer with strangers, and no account system. Chips are play money
> only and reset freely. IAPs are cosmetic environment packs (new table,
> cast and coach). The "coach" features are deterministic on-device analysis
> (Monte Carlo equity vs pot odds) — no server, no LLM, no data collection.
> The game is fully playable in Airplane Mode; we encourage testing it that
> way.

This pre-empts Guideline 5.3 (gambling) questions, which are the realistic
review risk for any poker app.

### Privacy nutrition label

- **Data Not Collected** across the board (offline-first, no account, no
  third-party SDKs at launch). This is rare for a card game and is itself a
  marketing asset — screenshot 9's caption ("no account") echoes it.
- If/when Unity Analytics ships (per the monetisation plan), update to
  "Data Not Linked to You: Product Interaction, Identifiers (pseudonymous)"
  before the build containing it goes for review.

---

## 3. Description (4000 chars max)

> **The poker game that teaches you poker.**
>
> Pull up a chair in a smoky frontier saloon — or at a felt table pitched in
> the middle of a fight cage — and play Texas Hold'em against five outlaws
> with real personalities. No accounts. No ads between hands. No real-money
> bets. Just you, the cards, and a coach in your corner.
>
> **A COACH IN YOUR CORNER**
> After any hand, ask the table coach for a review. Maximilian — the
> eye-patch bandit holding court at the saloon bar — walks you through why
> you won or lost, shows the key decision, and leaves you with one thing to
> try next time. Play the Octagon and Tommy Gunn does the same from inside
> the cage. Set the coach to review every hand, only notable hands, or stay
> quiet until you ask.
>
> **STUCK? ASK "WHAT WOULD AI DO?"**
> On your turn, one tap reveals what a card-counting pro would do with your
> exact cards: the recommended action, your estimated equity, the pot odds,
> and a plain-English reason. Use it as training wheels, check it after
> you've decided, or leave it alone and play your own game.
>
> **LEARN FROM SCRATCH**
> Never played Hold'em? The rules and the full hand-rankings chart are one
> tap away, even mid-hand. Start by tapping "What would AI do?" every turn;
> within a session you'll be reading the board on your own.
>
> **FIVE RIVALS WITH REAL PERSONALITIES**
> Nickey 2-guns, Tommy Gunn, Stevie Wanders, Jimmy two-hands and Simmo Says
> play like people, not slot machines: some tight, some reckless, some on
> tilt after a bad beat. They bluff, they have tells — and sometimes the
> tells are lies. The AI makes decisions with genuine card mathematics plus
> human-like noise. It never sees your cards.
>
> **EXTRAORDINARY PLACES**
> Every table is a seat in a living 3D scene — chips riffle, cards slide,
> characters watch you across the felt.
> • THE SALOON — brass taps, bottle-lined shelves, frontier hospitality.
> • THE OCTAGON — heavyweight bruisers playing cards where they usually
>   throw hands.
> More environments are on the way as one-time packs: new table, new cast,
> new coach.
>
> **HONEST OFFLINE POKER**
> • Single player, offline-first: plays on a plane, in a tunnel, anywhere.
> • No account, no sign-in, nothing to subscribe to.
> • Chips are play money and always will be. We sell places, never chips.
> • Real poker rules: blinds, side pots, split pots, min-raise tracking,
>   heads-up play — the engine is validated by a 10,000-hand simulation
>   every release.
>
> Shuffle up. The table's waiting, and the coach is buying.

(~2,340 chars — comfortably under the limit, leaving room for localisation
growth and a future environment list.)

**What's New template** (for updates):

> New at the table: [environment / feature]. The coach now [improvement].
> Plus smoother dealing and bug fixes — thanks for the reviews, they decide
> what we build next.

---

## 4. Screenshot set

Files: `screenshots/iphone-6.9/` (2868×1320 landscape, the required 6.9"
size — App Store Connect scales it to smaller iPhones) and
`screenshots/ipad-13/` (2752×2064, required for the iPad listing). Landscape
is correct for this game: it is landscape-only, and full-bleed table shots
read as gameplay, not marketing wallpaper.

Design language (matches the game UI): Rye display face for headlines in the
HUD gold `#e8c87a`, red kicker line in `#d87a6a`, Georgia italic sub-line,
scrim or solid band in the table's dark wood `#0c0a08`. iPhone panels are
full-bleed with a top scrim; panels whose subject fills the frame top (coach
card, rankings chart) use a solid caption band and gold-framed capture
instead, so marketing text never covers UI. iPad panels are framed captures
on a card-suit-watermarked background.

Apple shows the first three screenshots in search results — so the brand
panel, the advisor and the coach take those slots. Order and rationale:

| # | File | Scene | Caption (kicker / headline / sub) | Job |
|---|---|---|---|---|
| 1 | `01-cinematic-holdem` | Saloon splash art | SINGLE PLAYER • OFFLINE • NO REAL-MONEY BETS | Brand + instant genre read; the art carries the title so the kicker does the positioning |
| 2 | `02-what-would-ai-do` | Saloon, live advisor open (FOLD, 29% equity vs 38% pot odds) | LEARN AS YOU PLAY / STUCK? ASK "WHAT WOULD AI DO?" / Live equity, pot odds and a plain-English reason — on any turn | The #1 differentiator, shown not told |
| 3 | `03-poker-coach` | Saloon coach: Maximilian's hand review | YOUR PERSONAL POKER COACH / EVERY HAND REVIEWED, IF YOU WANT / Why you won, why you lost — and one thing to try next time | The coach pillar; completes the search-results trio |
| 4 | `04-key-decision` | Octagon coach: Tommy Gunn, key-decision breakdown | GO DEEPER / REPLAY THE KEY DECISION / Street, board and the numbers behind the coach's advice | Depth for the convinced scroller; quietly introduces the second coach |
| 5 | `05-octagon` | Octagon table, $549 pot, turn board | EXTRAORDINARY PLACES / STEP INTO THE OCTAGON / Heavyweights at the table — and Tommy Gunn in your corner | The second environment as a reveal |
| 6 | `06-saloon` | Saloon table, $757 pot, river decision | CINEMATIC TABLES / GO ALL-IN IN THE OLD SALOON / Five rivals with real personalities, tells and tilt | Core gameplay drama + NPC personality claim |
| 7 | `07-octagon-advice` | Octagon, advisor says RAISE TO $100 (45% vs 21%) | A COACH IN EVERY WORLD / KNOW WHEN TO RAISE FOR VALUE / The advisor counts the cards so you learn to think in odds | Learning works everywhere; shows an aggressive (fun) recommendation |
| 8 | `08-learn-from-scratch` | Hand-rankings chart over the table | NEW TO POKER? / RULES & HAND RANKINGS BUILT IN / From high card to royal flush — one tap away, mid-hand | Welcomes total beginners |
| 9 | `09-win-the-table` | Saloon win celebration, gold sparkles | NO ACCOUNT • NO ADS BETWEEN HANDS • PLAY ANYWHERE / OUTPLAY THE TABLE / Pure offline Texas Hold'em — your chips never cost a cent | Payoff shot + the trust/ethics close |

Screenshot hygiene notes for final production from the Unity build:
- Capture with the player stack **up**, mid-four-figure pots, and a board of
  4–5 cards — big numbers photograph better.
- Grey translucent characters are folded players (a real game feature);
  panels 2/5/6/7 keep a couple in frame deliberately — it reads as "a hand in
  progress" — but never let more than half the table be ghosts in a hero shot.
- Keep the player name "Jake" or similar-neutral in captures.

## 5. App preview video (storyboard, 15–30 s, landscape)

Autoplays muted in search — must work silently; captions carry the story.
One preview per device size, captured in-engine, no hands/UI overlays beyond
the game's own.

Two realised drafts (cut from real gameplay recordings, 24–29 s, silent)
ship with the player-facing mockup at
`Web/CinematicPoker.Web/wwwroot/appstore-design/store/`: `preview-saloon.mp4`
(title → deal → flop → "What would AI do?" → river → showdown → coach review)
and `preview-octagon.mp4` (title → cage deal → betting → advisor → two-pair
showdown → $2,104 pot award). They follow the storyboard below in miniature;
re-record on-device for the shipping versions.

| Time | Shot | On-screen text |
|---|---|---|
| 0–3 s | Saloon establishing orbit: table, chips, five characters | — |
| 3–7 s | Hole cards dealt, camera settles into the player's seat; bets fire around the table | "TEXAS HOLD'EM, SOMEWHERE WORTH SITTING" |
| 7–12 s | Tap "WHAT WOULD AI DO?" → advice card expands (equity, pot odds, reason); player raises | "LEARN AS YOU PLAY" |
| 12–17 s | Showdown win, chips slide over, sparkles | — |
| 17–23 s | Cut to the coach: Maximilian at the bar, hand-review card; tap SEE KEY DECISION | "EVERY HAND REVIEWED" |
| 23–27 s | Smash cut: the Octagon — cage, fighters, Tommy Gunn | "NEW WORLDS. SAME GAME. YOUR CORNER." |
| 27–30 s | Splash art with title | "FREE • OFFLINE • NO REAL-MONEY BETS" |

## 6. App icon direction

- **Subject:** the dead man's hand itself — aces and eights, fanned, on dark
  wood with one gold-rim poker chip. Zero text (the name renders below the
  icon anyway). High contrast at 60×60: two big card pips, one warm light.
- Avoid: character faces (too small), felt-green backgrounds (every poker
  app), and the word "poker" in the icon.
- Source material: crop of the splash art's foreground cards/chips already
  matches this exactly.

---

## Asset production

Everything regenerates from two scripts (kept in `tools/appstore/`):

1. **Capture** raw frames from the web build (same engine, tables, coach and
   HUD as mobile). Runs the real game headless, plays hands, opens the
   advisor/coach, and screenshots at 2× into `/tmp/appstore/shots/<scene>/`:

   ```bash
   cd Web/CinematicPoker.Web && dotnet run --urls http://localhost:5180 &
   cd tools/appstore && npm install playwright-core
   node capture.js saloon
   node capture.js street   # the Octagon
   ```

2. **Compose** the captioned store panels into `/tmp/appstore/final/`
   (iPhone 6.9" 2868×1320 JPEG + iPad 13" 2752×2064 JPEG — both accepted
   upload sizes):

   ```bash
   node compose.js
   ```

   Panel order, captions and layout (full-bleed vs band) live in the
   `PANELS` table at the top of `compose.js`. The scripts expect Chrome at
   `/usr/local/bin/google-chrome` and the Rye font cached next to them
   (`curl` command in the script header comments).

For the shipping build, re-capture from the Unity app on-device for final
fidelity (Metal rendering, device HUD scaling), keeping the same order,
captions and composition — the composer takes any same-aspect source PNGs.
