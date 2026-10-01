# Cinematic Poker — Go-to-Market, Monetisation & $1,000 Launch Plan (v2)

*Scope: maximise exposure and revenue for the iOS/Android launch with a US$1,000 paid budget. v2 centres the plan on the confirmed target market — **single players who want to play poker against NPCs the way they do in Red Dead Redemption 2** — and the hard constraint that **no real-money wagering and no bank-card handling** is ever involved.*

*A designed PDF edition with funnel, roadmap, budget and 12-month profit-projection charts lives at [`docs/marketing-plan.pdf`](marketing-plan.pdf) — regenerate it with `python3 tools/marketing-plan-pdf/generate_pdf.py` (needs Chrome; model assumptions are in the script).*

---

## 1. The niche: "RDR2 poker, as its own game" — viability assessment

### The thesis

RDR2's saloon poker is one of the most beloved *minigames* in modern gaming: players sink hours into it not because the poker is deep (it's actually mediocre poker — weak AI, no tells that matter) but because of **diegetic immersion**: you sit at a table *in a place*, with characters who talk, react, drink and exist. People routinely ask for exactly this as a standalone product and nobody has shipped it.

### Evidence the demand is real

- **Scale of the source audience.** RDR2 has sold 65M+ copies; even a tiny fraction who loved the poker specifically is a bigger audience than most indie games ever reach.
- **Explicit, repeated asks.** r/reddeadredemption and r/RDR2 regularly surface threads of the form "I just want RDR2's poker as a standalone game" / "what game scratches the RDR2 poker itch?" — and the answers are always unsatisfying (Prominence Poker: online/console; Governor of Poker 3: chip casino; Poker Night at the Inventory: delisted, PC-only). **An unanswered recurring question in a large community is the cheapest marketing signal that exists.**
- **Content performance.** "RDR2 poker" compilations, "winning big in the Blackwater game", and saloon-ambience videos pull six-to-seven-figure view counts. Creators already make this content; we give them a new thing to make it about.
- **The delisted-proof.** Poker Night at the Inventory (character-driven single-player poker) was successful enough for a sequel, then vanished with Telltale. The demand it validated has had no supply for a decade, and never on mobile.

### Honest risks of the angle, and mitigations

| Risk | Reality | Mitigation |
|---|---|---|
| **IP/trademark exposure** | We cannot use "Red Dead", Rockstar names, characters, music, or art anywhere in the product, store metadata, or our own ad creative. Apple also rejects metadata referencing third-party brands. | Comparisons must come from *other people's mouths*: creators, Reddit commenters, and press can say "scratches the RDR2 itch" freely (editorial speech). Our own copy says "saloon poker", "frontier poker", "the poker game where the table is a place". **The public-facing name stays Cinematic Poker (or similar); the `red-dead-texas-holdem` repo name never appears publicly.** |
| **Expectation gap** | RDR2 fans picture AAA fidelity; we're an indie with stylised art. | Build-in-public devlogs set expectations honestly; sell the *fantasy* (ambience, characters, tells) not graphics parity. Stylised-but-atmospheric is achievable and ages better. |
| **Niche conflation** | Some of these players want *RDR2*, not poker — they won't retain. | Fine. The wedge audience is "immersive poker" fans; the RDR2 framing is the *door*, not the ceiling. Retention metrics (§7) tell us quickly who stays. |
| **The product must deliver the fantasy on first launch** | If the free experience is a kitchen table with capsule mates, the RDR2-itch player churns in 90 seconds. | **Product decision, §3: the Western 1860s saloon moves up the roadmap to become the free flagship environment.** |

### Verdict

**Viable — in fact it is the strongest available wedge**, because it unifies everything the product already is (cinematic environments, characters with tells and memory, offline single-player) under one instantly-understood promise, and the architecture was explicitly built for it (environments are data; the Western saloon is already on the roadmap). It also *stacks* with, rather than replaces, the v1 counter-positioning: the chip-casino apps are precisely what the RDR2-itch player is fleeing.

> **Positioning line:** *"Sit down at a table that feels like a place. Six characters who bluff, tilt, talk and remember how you play. Offline, single-player, and we will never sell you chips."*

---

## 2. Money: how we get paid with no real money and no card details

**Constraint honoured by design, and turned into marketing:**

- **No real-money wagering, ever.** In-game chips are score. They cannot be bought, sold or cashed out. This keeps us out of gambling licensing entirely and into the stores' "simulated gambling" category (expect a 12+/Teen rating — acceptable).
- **We never see a card number.** All purchases are **one-time content unlocks through Apple App Store / Google Play IAP**. Apple/Google handle 100% of payment processing; the app collects no account, no personal data, no payment data. On iOS this earns the rare **"Data Not Collected" privacy label — put it in the screenshots**, it is a genuine differentiator in a category famous for data harvesting.
- **No loot boxes, no gacha, no randomised monetisation, no ads, no subscriptions.** Every purchase is a known thing at a known price.

**The catalogue (all one-time unlocks):**

| Item | Price | Notes |
|---|---|---|
| Base game: **Western Saloon (1860s)** + full engine, 5 NPCs, coaching, Mates' Kitchen as second free table | Free | The RDR2-itch fantasy must be free and immediate |
| Environment packs (Kalgoorlie Shed, Chicago 1926, Vegas, Disco 1978, Tokyo, Spaceport, …) — each a new place **and a new NPC cast with new tells/dialogue** | $3.99–4.99 | Packs are visitable (watch a hand, meet the cast) before buying |
| **"Everything Edition"** — all current *and future* environments | $14.99 launch / $19.99 later | Priced so two packs ≈ bundle: the obvious upgrade |
| Supporter tip ("buy the dealer a whisky") | $2.99 | Zero cost to add; loyal small audiences use these |
| **Steam/desktop premium edition** (same pure-C# engine; Unity desktop build is cheap to produce) | $9.99 one price, everything included | Second revenue stream aimed exactly where RDR2 PC players live; Steam wishlists + Next Fest demo are free marketing; the web demo already proves the engine runs everywhere |

**Revenue model sanity check (mobile):** 2–4% of free installs buying ~$8 average (one pack or part-bundle) → 10,000 installs ≈ $1,600–3,200; 100,000 installs ≈ $16K–32K. Steam at $9.99 with even 1,000 lifetime units adds ~$7K gross. The $1,000 budget's job is **not** profit — it is to find one repeatable channel with cost-per-install < $1.50 and D7 retention > 10%, which is the evidence that justifies real spend later.

---

## 3. Product decisions this plan requires (do these before spending a dollar)

1. **Promote the Western 1860s saloon to free flagship.** The vertical-slice fun bar ("five believable NPCs stay entertaining for 15–30 min") now applies to the saloon cast. Mates' Kitchen remains the free second table / tutorial. The environment-as-data architecture makes this a content re-ordering, not an engineering change.
2. **Saloon cast = five archetypes with loud, learnable personalities** (the drunk who overbets, the quiet pro, the tourist, the cheat-adjacent needler, the grandmother who limps everything). Learnable tells are the retention loop *and* the content creators will clip.
3. **Diegetic ambience is the product.** Piano, bottle clinks, muttered table talk, a bar fight that interrupts a hand once an hour. These moments are what make clips shareable.
4. **Web demo = saloon.** The shareable zero-install demo must show the flagship fantasy, instrumented with session analytics and store-link banners (UTM-tagged).
5. **Name/metadata hygiene.** No "Red Dead" anywhere public. Store title/subtitle targets: *"Cinematic Poker — Saloon Texas Hold'em"* style, keywords: `offline poker, single player poker, poker vs ai, saloon poker, western poker, texas holdem offline, poker trainer`.

---

## 4. Execution plan

### M0 — Product gate (no marketing spend yet)

- [ ] Build the Western Saloon environment + 5-NPC cast on the existing `PokerEnvironmentDefinition` pipeline.
- [ ] Pass the fun bar: 3+ external testers each play 15–30 minutes unprompted and can *name the NPCs' personalities afterwards* (that's the test that the characters read).
- [ ] Point the web demo at the saloon; add analytics (hands played, session length, store-banner clicks) and UTM plumbing.
- [ ] Record 60–90s of raw capture per NPC for the content library.

### M1 — Presence (runs parallel to late M0)

- [ ] App Store + Play Store listings: screenshots lead with **NPC faces and tells captions** ("Mick scratches his ear. Sometimes it means nothing."), not a top-down table. Fill simulated-gambling questionnaires correctly; claim the iOS "Data Not Collected" label.
- [ ] **$100 → trailer**: one 45-second character-led trailer (meet the cast → a bluff → the bar-fight moment → "Offline. No chips for sale. Free."). Reused everywhere.
- [ ] One-page press kit (presskit() format): fact sheet, GIFs of tells, the positioning line, web demo link.
- [ ] Put up a **Steam "Coming Soon" page** to start wishlist capture (free; the page itself is discoverable by exactly our audience).

### M2 — Audience build (4–6 weeks, $0)

- [ ] **Devlogs 2×/week** on TikTok/Shorts/Reels: "I'm building the saloon poker game", "my AI went on tilt and shoved three hands straight", "designing a false tell". Every clip ends on the web-demo link.
- [ ] **Reddit as a developer, not an advertiser**: genuine participation in r/poker, r/IndieGaming, r/AndroidGaming, r/iosgaming; when the recurring "standalone RDR2 poker?" thread appears in r/reddeadredemption — *that thread is ours to answer*, with the playable link.
- [ ] Build the **creator list (50 names, two columns)**: (a) poker TikTok/YouTube (strategy + entertainment, 5k–100k followers — nobody sends these people games, response rates are high); (b) western/RDR2-content and "games like RDR2" listicle creators. Log contact, size, fit, and the hook line per creator.
- [ ] Pitch 5–10 "best offline games / best poker games" listicle authors directly (those articles convert for years).

### M3 — Launch ($900 deployed)

- [ ] **Android soft launch first** (cheaper installs, faster review cycles). Gate: D1 > 35%, D7 > 10%, median session > 8 min. Fix retention before going loud.
- [ ] **Global launch week — coordinate the spike** (stores' ranking algorithms reward concentrated velocity):

| # | Spend | Amount | Detail | Kill/success gate |
|---|---|---|---|---|
| 1 | Trailer (M1) | $100 | Already produced | — |
| 2 | **Creator seeding** | $350 | 20–25 creators from the M2 list get keys + the web link; $25–50 paid placements for the 6–8 best-fit (split poker creators / RDR2-adjacent creators to A/B the two framings) | ≥4 videos live in launch week; track which framing (saloon fantasy vs anti-chip-casino) drives installs |
| 3 | **Apple Search Ads, exact match only** | $250 | `offline poker`, `single player poker`, `poker vs ai`, `texas holdem offline`, `western poker`, `saloon poker` — high intent, low competition, typically $0.50–1.50/tap | CPI < $1.50 **and** D1 > 35%, else pause the keyword |
| 4 | **Reddit promoted posts** | $150 | Target r/poker + mobile-gaming subs; interest-target western-gaming audiences **without using the trademark in our copy** — creative: 20s saloon clip + "No chips for sale. No internet needed. Free." → web demo | Median demo session > 2 min; demo→store CTR > 8% |
| 5 | **Reserve** | $150 | Weeks 3–4: double down on whichever line above produced the cheapest *retained* install | — |

- [ ] Deliberately $0: Facebook/Google broad UA (viable minimums are 10–50× our budget), press agencies, paid reviews, feature brokers.

### M4 — Live cadence (post-launch)

- One environment pack every 6–8 weeks; **every pack is a full marketing beat** (its own 30s trailer, creator wave, devlog arc, Reddit post). Packs chosen by audience vote in the community — participation is retention.
- Ship the **Steam version** 2–3 months post-mobile with the accumulated wishlist; enter the next **Steam Next Fest** with the demo (free, high-visibility, perfectly suited to a game with a browser-provable demo).
- The "Improve My Game" coaching feature gets its own beat aimed at the second audience: *"practise reading tells without risking a cent"* — poker-learning spaces (r/poker, training channels) where game marketing never appears.

---

## 5. Why free-to-download (unchanged from v1, restated)

Paid-up-front with a $1,000 budget and no publisher = a few hundred installs and ranking death. Free removes the one friction we can't argue against, maximises the web-demo→store funnel, and "free, and we will never sell you chips" is itself the hook. Subscriptions and ads are rejected because they destroy §1's positioning, which is worth more than any short-term revenue. (Steam is the exception: desktop audiences expect and reward premium pricing — $9.99, everything included.)

## 6. Competitive map (retained from v1)

| Competitor | Model | Weakness we exploit |
|---|---|---|
| Zynga Poker, WSOP, PokerStars Play | F2P chip casino, online | Chip sales, ads, connection required, zero table personality |
| Governor of Poker 3 | F2P chip casino, campaign veneer | Abandoned the beloved paid single-player formula of GoP 1–2 |
| Governor of Poker 1–2 | Paid single-player | Dated, simplistic AI, no tells/coaching; 10+ years old |
| Poker Night at the Inventory 1–2 | Premium character poker (PC) | **Delisted** — validated demand, zero supply, never on mobile |
| Prominence Poker | F2P console/PC, online | Not mobile, not offline, not single-player-first |
| RDR2 itself | AAA, poker is a minigame | 100GB install, can't play a quick hand on the bus; its poker AI is shallow — ours is the actual product |

## 7. Measurement & kill rules

- UTM-tag every link; the two funnels are **web demo → store** and **store direct**.
- Weekly dashboard: installs by source, CPI, D1/D7 retention, median session, % reaching hand 10, payer conversion, pack attach rate, Everything-Edition take rate, Steam wishlists.
- **Kill rule:** any paid channel that can't beat $1.50 CPI with D7 > 10% after $100 is stopped. The budget is ten experiments, not one campaign.
- **Framing A/B:** launch-week creator and ad data must answer one strategic question — does *saloon fantasy* or *anti-chip-casino* framing acquire better-retaining players? The winner leads all M4 beats.

## 8. Risks

- **Fun bar not met** → M0 gates all spend.
- **IP exposure** → no third-party names/assets anywhere we control (product, store metadata, our ad copy); comparisons live only in editorial/creator/community speech; repo name stays private.
- **Poker flagged as gambling by ad platforms** → creatives lead with "no real money, no chips for sale"; store simulated-gambling questionnaires completed correctly from day one.
- **Expectation gap vs AAA** → honest build-in-public devlogs; sell ambience and characters, never graphics parity.
- **Web-demo cannibalisation** → acceptable (it converts sceptics); keep one pack store-exclusive if data shows a problem.
