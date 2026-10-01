#!/usr/bin/env python3
"""Generate docs/marketing-plan.pdf — a designed, chart-driven PDF of the
go-to-market plan in docs/marketing-plan.md.

Usage:  python3 tools/marketing-plan-pdf/generate_pdf.py
Needs:  google-chrome (headless) on PATH. No other dependencies.
"""

import os
import subprocess
import sys
import tempfile

# ---------------------------------------------------------------- palette ---
INK = "#1b2420"          # near-black green
FELT = "#0e3b2e"         # deep felt green
FELT_LIGHT = "#17584a"
GOLD = "#d4a94e"
GOLD_DEEP = "#b8860b"
CREAM = "#faf6ee"
PAPER = "#ffffff"
GREY = "#8a938e"
RED = "#b3472f"
BLUE = "#3d6b8e"

# ------------------------------------------------------- financial model ---
MONTHS = 12
MOBILE_NET = 0.85        # App Store / Play small-business tier (15% cut)
STEAM_NET = 0.70         # Steam 30% cut
STEAM_PRICE = 9.99
UPFRONT_COSTS = 1250.0   # $1,000 ads + dev accounts ($99+$25) + Steam Direct ($100) + misc
MONTHLY_COSTS = 50.0     # tools, music licences, odd costs
# Steam launches month 4; Next Fest bump month 6. Fractions of lifetime units per month.
STEAM_SPREAD = [0, 0, 0, 0.35, 0.15, 0.20, 0.10, 0.04, 0.04, 0.04, 0.04, 0.04]

SCENARIOS = {
    "Conservative": dict(
        installs=[3000, 1200, 900, 800, 700, 750, 650, 700, 600, 650, 550, 600],
        conv=0.02, arppu=7.0, steam_units=300, color=GREY,
        note="Launch lands quietly; no creator video takes off; organic floor only.",
    ),
    "Base": dict(
        installs=[10000, 4000, 3000, 2800, 2500, 2600, 2200, 2300, 2000, 2100, 1900, 2000],
        conv=0.03, arppu=8.0, steam_units=1000, color=GOLD_DEEP,
        note="Creator seeding produces a handful of mid-size videos; ASO cluster ranks; pack beats sustain a floor.",
    ),
    "Optimistic": dict(
        installs=[25000, 9000, 7000, 6500, 6000, 6500, 5500, 5500, 5000, 5200, 4800, 5000],
        conv=0.04, arppu=9.0, steam_units=2500, color=FELT_LIGHT,
        note="One creator video breaks out; the RDR2-itch framing spreads; Everything Edition attach rises.",
    ),
}


def run_model():
    out = {}
    for name, s in SCENARIOS.items():
        mobile_net, steam_net, monthly_net, cum = [], [], [], []
        total = 0.0
        for m in range(MONTHS):
            mn = s["installs"][m] * s["conv"] * s["arppu"] * MOBILE_NET
            sn = s["steam_units"] * STEAM_SPREAD[m] * STEAM_PRICE * STEAM_NET
            cost = MONTHLY_COSTS + (UPFRONT_COSTS if m == 0 else 0.0)
            net = mn + sn - cost
            total += net
            mobile_net.append(mn)
            steam_net.append(sn)
            monthly_net.append(net)
            cum.append(total)
        out[name] = dict(s, mobile_net=mobile_net, steam_net=steam_net,
                         monthly=monthly_net, cumulative=cum,
                         installs_total=sum(s["installs"]))
    return out


def money(v):
    sign = "\u2212" if v < 0 else ""
    v = abs(v)
    if v >= 1000:
        return f"{sign}${v/1000:,.1f}k".replace(".0k", "k")
    return f"{sign}${v:,.0f}"


# ------------------------------------------------------------ svg helpers ---

def svg_line_chart(model, w=1040, h=430):
    ml, mr, mt, mb = 86, 150, 24, 46
    pw, ph = w - ml - mr, h - mt - mb
    all_vals = [v for s in model.values() for v in s["cumulative"]]
    lo = min(min(all_vals), 0)
    hi = max(all_vals)
    lo = (int(lo // 5000) - 0) * 5000 if lo < 0 else 0
    lo = min(lo, -2500)
    hi = (int(hi // 5000) + 1) * 5000

    def x(m):  # m: 0..11
        return ml + pw * m / (MONTHS - 1)

    def y(v):
        return mt + ph * (1 - (v - lo) / (hi - lo))

    parts = [f'<svg viewBox="0 0 {w} {h}" xmlns="http://www.w3.org/2000/svg" '
             f'font-family="Helvetica,Arial,sans-serif">']
    # gridlines
    step = 10000 if hi > 25000 else 5000
    v = lo - (lo % step)
    while v <= hi:
        yy = y(v)
        strong = abs(v) < 1e-9
        parts.append(f'<line x1="{ml}" y1="{yy:.1f}" x2="{ml+pw}" y2="{yy:.1f}" '
                     f'stroke="{INK if strong else "#e2e0d8"}" stroke-width="{1.6 if strong else 1}"/>')
        parts.append(f'<text x="{ml-10}" y="{yy+4:.1f}" text-anchor="end" font-size="13" '
                     f'fill="{GREY}">{money(v)}</text>')
        v += step
    # x labels
    for m in range(MONTHS):
        parts.append(f'<text x="{x(m):.1f}" y="{h-18}" text-anchor="middle" font-size="13" '
                     f'fill="{GREY}">M{m+1}</text>')
    parts.append(f'<text x="{ml+pw/2}" y="{h-2}" text-anchor="middle" font-size="12" '
                 f'fill="{GREY}">Months after global launch (Steam edition ships M4, Next Fest M6)</text>')
    # steam launch marker
    sx = x(3)
    parts.append(f'<line x1="{sx:.1f}" y1="{mt}" x2="{sx:.1f}" y2="{mt+ph}" stroke="{BLUE}" '
                 f'stroke-width="1" stroke-dasharray="5 4" opacity="0.6"/>')
    parts.append(f'<text x="{sx+6:.1f}" y="{mt+14}" font-size="12" fill="{BLUE}">Steam launch</text>')
    # lines
    for name, s in model.items():
        pts = " ".join(f"{x(m):.1f},{y(s['cumulative'][m]):.1f}" for m in range(MONTHS))
        parts.append(f'<polyline points="{pts}" fill="none" stroke="{s["color"]}" '
                     f'stroke-width="3.5" stroke-linejoin="round" stroke-linecap="round"/>')
        for m in (0, 3, 5, 11):
            parts.append(f'<circle cx="{x(m):.1f}" cy="{y(s["cumulative"][m]):.1f}" r="4" '
                         f'fill="{s["color"]}"/>')
        endv = s["cumulative"][-1]
        parts.append(f'<text x="{ml+pw+10}" y="{y(endv)+5:.1f}" font-size="14" font-weight="700" '
                     f'fill="{s["color"]}">{name} {money(endv)}</text>')
    parts.append("</svg>")
    return "".join(parts)


def svg_stacked_bars(model, w=1040, h=330):
    s = model["Base"]
    ml, mr, mt, mb = 86, 30, 20, 46
    pw, ph = w - ml - mr, h - mt - mb
    hi = max(m + st for m, st in zip(s["mobile_net"], s["steam_net"]))
    hi = (int(hi // 500) + 1) * 500
    bw = pw / MONTHS * 0.62
    parts = [f'<svg viewBox="0 0 {w} {h}" xmlns="http://www.w3.org/2000/svg" '
             f'font-family="Helvetica,Arial,sans-serif">']
    for gv in range(0, int(hi) + 1, 1000):
        yy = mt + ph * (1 - gv / hi)
        parts.append(f'<line x1="{ml}" y1="{yy:.1f}" x2="{ml+pw}" y2="{yy:.1f}" stroke="#e2e0d8"/>')
        parts.append(f'<text x="{ml-10}" y="{yy+4:.1f}" text-anchor="end" font-size="13" '
                     f'fill="{GREY}">{money(gv)}</text>')
    for m in range(MONTHS):
        cx = ml + pw * (m + 0.5) / MONTHS
        mh = ph * s["mobile_net"][m] / hi
        sh = ph * s["steam_net"][m] / hi
        base_y = mt + ph
        parts.append(f'<rect x="{cx-bw/2:.1f}" y="{base_y-mh:.1f}" width="{bw:.1f}" '
                     f'height="{mh:.1f}" rx="3" fill="{GOLD}"/>')
        if sh > 0.5:
            parts.append(f'<rect x="{cx-bw/2:.1f}" y="{base_y-mh-sh:.1f}" width="{bw:.1f}" '
                         f'height="{sh:.1f}" rx="3" fill="{BLUE}"/>')
        parts.append(f'<text x="{cx:.1f}" y="{h-26}" text-anchor="middle" font-size="13" '
                     f'fill="{GREY}">M{m+1}</text>')
    parts.append(f'<rect x="{ml}" y="{h-16}" width="14" height="14" rx="3" fill="{GOLD}"/>')
    parts.append(f'<text x="{ml+20}" y="{h-4}" font-size="13" fill="{INK}">Mobile IAP net (after 15% store cut)</text>')
    parts.append(f'<rect x="{ml+300}" y="{h-16}" width="14" height="14" rx="3" fill="{BLUE}"/>')
    parts.append(f'<text x="{ml+320}" y="{h-4}" font-size="13" fill="{INK}">Steam premium net (after 30% cut)</text>')
    parts.append("</svg>")
    return "".join(parts)


def svg_donut(w=400, h=400):
    import math
    items = [("Creator seeding", 350, GOLD_DEEP), ("Apple Search Ads", 250, FELT_LIGHT),
             ("Reddit ads test", 150, BLUE), ("Reserve (double down)", 150, GREY),
             ("Trailer & assets", 100, RED)]
    total = sum(v for _, v, _ in items)
    cx, cy, r, ir = w / 2, h / 2, 150, 92
    a = -90.0
    parts = [f'<svg viewBox="0 0 {w} {h}" xmlns="http://www.w3.org/2000/svg" '
             f'font-family="Helvetica,Arial,sans-serif">']
    for label, v, color in items:
        frac = v / total
        a2 = a + frac * 360
        large = 1 if frac > 0.5 else 0

        def pt(ang, rad):
            return (cx + rad * math.cos(math.radians(ang)),
                    cy + rad * math.sin(math.radians(ang)))
        x1, y1 = pt(a, r)
        x2, y2 = pt(a2, r)
        x3, y3 = pt(a2, ir)
        x4, y4 = pt(a, ir)
        parts.append(
            f'<path d="M {x1:.1f} {y1:.1f} A {r} {r} 0 {large} 1 {x2:.1f} {y2:.1f} '
            f'L {x3:.1f} {y3:.1f} A {ir} {ir} 0 {large} 0 {x4:.1f} {y4:.1f} Z" '
            f'fill="{color}" stroke="{PAPER}" stroke-width="3"/>')
        mid = (a + a2) / 2
        lx, ly = pt(mid, (r + ir) / 2)
        parts.append(f'<text x="{lx:.1f}" y="{ly+5:.1f}" text-anchor="middle" font-size="17" '
                     f'font-weight="700" fill="#fff">${v}</text>')
        a = a2
    parts.append(f'<text x="{cx}" y="{cy-6}" text-anchor="middle" font-size="34" '
                 f'font-weight="800" fill="{INK}">$1,000</text>')
    parts.append(f'<text x="{cx}" y="{cy+20}" text-anchor="middle" font-size="14" '
                 f'fill="{GREY}">10 experiments,</text>')
    parts.append(f'<text x="{cx}" y="{cy+38}" text-anchor="middle" font-size="14" '
                 f'fill="{GREY}">not one campaign</text>')
    parts.append("</svg>")
    return "".join(parts)


def svg_funnel(w=1040, h=460):
    stages = [
        ("Reach", "100,000", "devlogs · creator videos · Reddit · ads", 1.00, FELT),
        ("Web demo sessions", "12,000", "zero-install playable link · 12% of reach", 0.80, FELT_LIGHT),
        ("Store installs", "10,000", "demo converts + direct ASO/ads installs", 0.62, GOLD_DEEP),
        ("D7 retained players", "1,200", "12% — the kill-rule threshold is 10%", 0.44, GOLD),
        ("Paying players", "300", "3% of installs · ~$8 avg → $2,040 net", 0.28, RED),
    ]
    parts = [f'<svg viewBox="0 0 {w} {h}" xmlns="http://www.w3.org/2000/svg" '
             f'font-family="Helvetica,Arial,sans-serif">']
    cx = 390
    top, bh, gap = 10, 78, 10
    for i, (label, num, sub, width, color) in enumerate(stages):
        y0 = top + i * (bh + gap)
        w0 = 700 * width
        w1 = 700 * (stages[i + 1][3] if i + 1 < len(stages) else width * 0.82)
        x0, x1 = cx - w0 / 2, cx + w0 / 2
        x2, x3 = cx + w1 / 2, cx - w1 / 2
        parts.append(f'<path d="M {x0:.0f} {y0} L {x1:.0f} {y0} L {x2:.0f} {y0+bh} '
                     f'L {x3:.0f} {y0+bh} Z" fill="{color}" rx="6"/>')
        parts.append(f'<text x="{cx}" y="{y0+33}" text-anchor="middle" font-size="19" '
                     f'font-weight="800" fill="#fff">{label}</text>')
        parts.append(f'<text x="{cx}" y="{y0+57}" text-anchor="middle" font-size="15" '
                     f'fill="#ffffffcc">{num}</text>')
        parts.append(f'<text x="{cx+370}" y="{y0+bh/2+5}" font-size="13.5" '
                     f'fill="{GREY}">{sub}</text>')
    parts.append("</svg>")
    return "".join(parts)


# ----------------------------------------------------------------- html ----

CSS = """
* { margin: 0; padding: 0; box-sizing: border-box; }
html { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
body { font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif; color: INK;
       font-size: 10.5pt; line-height: 1.52; }
@page { size: A4; margin: 0; }
.page { width: 210mm; height: 297mm; padding: 16mm 15mm 13mm 15mm; position: relative;
        page-break-after: always; background: PAPER; overflow: hidden; }
.page:last-child { page-break-after: auto; }

.cover { background: linear-gradient(160deg, #0b2f24 0%, FELT 55%, #0a241c 100%);
         color: CREAM; display: flex; flex-direction: column; padding: 22mm 18mm; }
.cover .kicker { letter-spacing: 3.5px; text-transform: uppercase; font-size: 10pt;
                 color: GOLD; font-weight: 700; }
.cover h1 { font-size: 37pt; line-height: 1.08; margin: 10mm 0 6mm; font-weight: 800; }
.cover .sub { font-size: 13.5pt; color: #e9e2d2; max-width: 150mm; }
.cover .rule { height: 2px; background: GOLD; width: 42mm; margin: 10mm 0; }
.statgrid { display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 6mm; margin-top: auto; }
.stat { border: 1px solid #ffffff2b; border-radius: 10px; padding: 6mm 6mm;
        background: #ffffff0d; }
.stat .v { font-size: 19pt; font-weight: 800; color: GOLD; }
.stat .l { font-size: 9pt; color: #d9d2c0; margin-top: 1.5mm; }
.cover .foot { margin-top: 10mm; font-size: 9pt; color: #bcb49e;
               display: flex; justify-content: space-between; }

.kicker2 { letter-spacing: 3px; text-transform: uppercase; font-size: 8.5pt;
           color: GOLD_DEEP; font-weight: 800; }
h2 { font-size: 19pt; font-weight: 800; color: FELT; margin: 2mm 0 4mm; }
h3 { font-size: 12pt; font-weight: 800; color: FELT; margin: 5mm 0 2mm; }
p  { margin-bottom: 2.6mm; }
.lead { font-size: 11.5pt; color: #3c463f; }
.quote { background: CREAM; border-left: 4px solid GOLD; padding: 4mm 5mm;
         border-radius: 0 8px 8px 0; font-size: 11.5pt; font-style: italic;
         color: FELT; margin: 4mm 0; }
.cols2 { display: grid; grid-template-columns: 1fr 1fr; gap: 7mm; }
.cols2w { display: grid; grid-template-columns: 1.25fr 1fr; gap: 7mm; }

table { width: 100%; border-collapse: collapse; font-size: 9.3pt; margin: 2.5mm 0; }
th { background: FELT; color: #fff; text-align: left; padding: 2.4mm 2.8mm;
     font-size: 8.6pt; letter-spacing: 0.4px; text-transform: uppercase; }
td { padding: 2.2mm 2.8mm; border-bottom: 1px solid #e7e4da; vertical-align: top; }
tr:nth-child(even) td { background: #faf8f2; }
td.r, th.r { text-align: right; }
.gold { color: GOLD_DEEP; font-weight: 700; }

.card { border: 1px solid #e4e0d4; border-radius: 10px; padding: 4.5mm 5mm;
        background: #fffdf8; margin-bottom: 4mm; }
.card h4 { font-size: 10.5pt; color: FELT; margin-bottom: 1.5mm; }
.badge { display: inline-block; background: FELT; color: GOLD; font-weight: 800;
         font-size: 8pt; padding: 1mm 2.6mm; border-radius: 99px;
         letter-spacing: 1px; text-transform: uppercase; }
.badge.red { background: RED; color: #fff; }
.badge.blue { background: BLUE; color: #fff; }

.flow { display: flex; align-items: stretch; gap: 0; margin: 4mm 0; }
.fbox { flex: 1; background: CREAM; border: 1.5px solid #ddd3bb; border-radius: 10px;
        padding: 3.5mm 3.5mm; font-size: 8.6pt; }
.fbox .t { font-weight: 800; color: FELT; font-size: 9.6pt; margin-bottom: 1mm; }
.fbox ul { padding-left: 4mm; } .fbox li { margin-bottom: 0.8mm; }
.farr { align-self: center; padding: 0 1.6mm; color: GOLD_DEEP; font-size: 15pt;
        font-weight: 800; }
.gate { background: FELT; color: #fff; border-radius: 8px; padding: 2.6mm 3.5mm;
        font-size: 8.8pt; margin-top: 2.5mm; }
.gate b { color: GOLD; }

.kpis { display: grid; grid-template-columns: repeat(4, 1fr); gap: 4mm; margin: 3mm 0; }
.kpi { background: CREAM; border-radius: 10px; padding: 4mm; text-align: center; }
.kpi .v { font-size: 16pt; font-weight: 800; color: FELT; }
.kpi .l { font-size: 8.2pt; color: GREY; margin-top: 1mm; }

.pagefoot { position: absolute; bottom: 7mm; left: 15mm; right: 15mm;
            display: flex; justify-content: space-between; font-size: 8pt;
            color: #a9a393; border-top: 1px solid #e7e4da; padding-top: 2mm; }
.chartwrap { margin: 3mm 0; }
svg { width: 100%; height: auto; display: block; }
.small { font-size: 8.8pt; color: GREY; }
"""

for k, v in [("FELT_LIGHT", FELT_LIGHT), ("GOLD_DEEP", GOLD_DEEP), ("FELT", FELT),
             ("GOLD", GOLD), ("CREAM", CREAM), ("PAPER", PAPER), ("GREY", GREY),
             ("INK", INK), ("RED", RED), ("BLUE", BLUE)]:
    CSS = CSS.replace(k, v)


def foot(n, total=8):
    return (f'<div class="pagefoot"><span>Cinematic Poker — Go-to-Market &amp; '
            f'Monetisation Plan</span><span>Confidential draft · page {n} / {total}</span></div>')


def build_html(model):
    base = model["Base"]
    y1_profit = {k: v["cumulative"][-1] for k, v in model.items()}

    pages = []

    # ---- 1 · cover ----
    pages.append(f"""
<div class="page cover">
  <div class="kicker">Go-to-market · monetisation · $1,000 launch budget</div>
  <h1>Cinematic Poker</h1>
  <div class="sub">Sit down at a table that feels like a place. Six characters who bluff,
  tilt, talk and remember how you play. Offline, single-player —
  and we will never sell you chips.</div>
  <div class="rule"></div>
  <div class="sub" style="font-size:11pt">The plan: own the niche of single players who want
  RDR2-style NPC poker as its own game, monetise with one-time content unlocks only
  (no real money, no card details, no ads), and deploy $1,000 as ten measured experiments
  to find one repeatable acquisition channel.</div>
  <div class="statgrid">
    <div class="stat"><div class="v">$1,000</div><div class="l">Paid launch budget, deployed as gated experiments</div></div>
    <div class="stat"><div class="v">{money(y1_profit['Base'])}</div><div class="l">Base-case cumulative profit, 12 months post-launch</div></div>
    <div class="stat"><div class="v">0</div><div class="l">Chips sold · ads shown · card numbers touched</div></div>
    <div class="stat"><div class="v">65M+</div><div class="l">RDR2 copies sold — the wedge audience's home</div></div>
    <div class="stat"><div class="v">{base['installs_total']//1000}k</div><div class="l">Base-case year-one installs (mobile)</div></div>
    <div class="stat"><div class="v">M{next(i+1 for i,v in enumerate(base['cumulative']) if v>0)}</div><div class="l">Base-case breakeven month</div></div>
  </div>
  <div class="foot"><span>Prepared October 2026</span><span>docs/marketing-plan.md · v2</span></div>
</div>""")

    # ---- 2 · niche ----
    pages.append(f"""
<div class="page">
  <div class="kicker2">01 · The niche</div>
  <h2>"RDR2 poker, as its own game" — a viable wedge</h2>
  <p class="lead">RDR2's saloon poker is one of gaming's most beloved minigames — not because
  the poker is deep (it isn't), but because of <b>diegetic immersion</b>: a table in a place,
  with characters who talk, react and exist. Players repeatedly ask for this as a standalone
  product. Nobody has shipped it.</p>
  <div class="cols2">
    <div>
      <h3>Why the demand is real</h3>
      <div class="card"><h4>Scale</h4>RDR2 sold 65M+ copies. A sliver who loved the poker
      specifically outnumbers most indie audiences.</div>
      <div class="card"><h4>Explicit, repeated asks</h4>r/reddeadredemption recurrently asks
      "what scratches the RDR2 poker itch?" — every answer today is unsatisfying. An unanswered
      recurring question in a large community is the cheapest marketing signal that exists.</div>
      <div class="card"><h4>Proven, then abandoned</h4>Poker Night at the Inventory validated
      character-driven single-player poker, earned a sequel, then was delisted. A decade of
      demand, zero supply — never on mobile.</div>
    </div>
    <div>
      <h3>Risks &amp; mitigations</h3>
      <div class="card"><h4><span class="badge red">IP</span> Trademark exposure</h4>
      "Red Dead" never appears in anything we control — product, store metadata, our ad copy.
      Comparisons live only in creator / community / press speech (legal editorial use).
      The public name stays <b>Cinematic Poker</b>.</div>
      <div class="card"><h4><span class="badge blue">Expectations</span> AAA fidelity gap</h4>
      Build-in-public devlogs set honest expectations; we sell ambience, characters and tells —
      never graphics parity.</div>
      <div class="card"><h4><span class="badge">Product</span> Deliver the fantasy at minute one</h4>
      The Western 1860s saloon is promoted to <b>free flagship environment</b>. With
      environments-as-data this is a content re-ordering, not an engineering change.</div>
    </div>
  </div>
  <div class="quote">Verdict: viable — the strongest available wedge. It unifies what the product
  already is (cinematic places, characters with tells and memory, offline single-player) under one
  instantly-understood promise, and it stacks with the anti-chip-casino counter-position:
  those apps are exactly what this player is fleeing.</div>
  {foot(2)}
</div>""")

    # ---- 3 · competitors ----
    pages.append(f"""
<div class="page">
  <div class="kicker2">02 · Market</div>
  <h2>Competitive landscape &amp; our edge</h2>
  <table>
    <tr><th style="width:30%">Competitor</th><th style="width:26%">Model</th><th>Weakness we exploit</th></tr>
    <tr><td><b>Zynga Poker · WSOP · PokerStars Play</b></td><td>F2P chip casino, online</td>
        <td>Chip sales, ads, connection required, zero table personality — loudly resented in their own reviews</td></tr>
    <tr><td><b>Governor of Poker 3</b></td><td>F2P chip casino, campaign veneer</td>
        <td>Abandoned the beloved paid single-player formula of GoP 1–2</td></tr>
    <tr><td><b>Governor of Poker 1–2</b></td><td>Paid single-player</td>
        <td>10+ years old, dated presentation, simplistic AI, no tells or coaching</td></tr>
    <tr><td><b>Poker Night at the Inventory 1–2</b></td><td>Premium character poker (PC)</td>
        <td><b>Delisted</b> — validated demand, zero supply, never on mobile</td></tr>
    <tr><td><b>Prominence Poker</b></td><td>F2P console/PC, online</td>
        <td>Not mobile, not offline, not single-player-first</td></tr>
    <tr><td><b>RDR2 itself</b></td><td>AAA; poker is a minigame</td>
        <td>100GB install, no quick hand on the bus; shallow poker AI — ours is the actual product</td></tr>
  </table>
  <h3>Five defensible edges (features, not claims)</h3>
  <div class="cols2">
    <div>
      <div class="card"><h4>1 · Opponents with character</h4>12-parameter AI personalities, tilt model,
      opponent memory (VPIP / PFR / 3-bet), probabilistic tells with false tells. Nobody on mobile has this.</div>
      <div class="card"><h4>2 · Offline-first</h4>Flights, commutes, no account, no server —
      and "offline poker" is a high-intent, low-competition search cluster.</div>
      <div class="card"><h4>3 · Ethical monetisation</h4>We sell <i>places</i>, never chips.
      A moral stance that doubles as the marketing hook.</div>
    </div>
    <div>
      <div class="card"><h4>4 · "Improve My Game" coaching</h4>Hand-history review makes the game a
      poker trainer — a second audience and keyword cluster no competitor can copy.</div>
      <div class="card"><h4>5 · The web demo</h4>A shareable, zero-install playable link running the
      identical engine. Every post, comment and email ends in "play it right now."</div>
      <div class="quote" style="margin-top:3mm">Counter-positioning is the only marketing strategy
      that works on $1,000 — and we counter the entire category at once.</div>
    </div>
  </div>
  {foot(3)}
</div>""")

    # ---- 4 · money ----
    pages.append(f"""
<div class="page">
  <div class="kicker2">03 · Monetisation</div>
  <h2>How we get paid — no real money, no card details</h2>
  <div class="cols2">
    <div>
      <p><b>The constraint, honoured by design:</b> in-game chips are score — they can never be
      bought, sold or cashed out, keeping us outside gambling licensing entirely (stores'
      "simulated gambling" category, 12+/Teen rating). All purchases are <b>one-time content
      unlocks via Apple / Google IAP</b>: the platforms process 100% of payments, and the app
      collects no account, no personal data, no payment data.</p>
      <p>On iOS that earns the rare <b>"Data Not Collected"</b> privacy label — a genuine
      differentiator in a category famous for data harvesting. Put it in the screenshots.</p>
      <p><b>Never:</b> loot boxes, gacha, randomised monetisation, ads, subscriptions.
      Every purchase is a known thing at a known price.</p>
      <h3>Why free-to-download</h3>
      <p class="small">Paid-up-front on a $1,000 budget with no publisher means a few hundred
      installs and ranking death. Free removes the one friction we can't argue against, maximises
      the web-demo → store funnel, and "free, and we will never sell you chips" is itself the hook.
      Steam is the exception: desktop audiences reward premium pricing.</p>
    </div>
    <div>
      <table>
        <tr><th>Item</th><th class="r">Price</th></tr>
        <tr><td><b>Base game</b> — Western Saloon flagship, full engine, 5 NPCs, coaching,
            Mates' Kitchen second table</td><td class="r gold">Free</td></tr>
        <tr><td><b>Environment packs</b> — a new place <i>and</i> a new NPC cast with new tells
            &amp; dialogue (Kalgoorlie, Chicago 1926, Vegas, Disco 1978, Tokyo, Spaceport…);
            visitable before buying</td><td class="r">$3.99–4.99</td></tr>
        <tr><td><b>Everything Edition</b> — all current and future environments; priced so two
            packs ≈ bundle</td><td class="r">$14.99 → $19.99</td></tr>
        <tr><td><b>Supporter tip</b> — "buy the dealer a whisky"</td><td class="r">$2.99</td></tr>
        <tr><td><b>Steam / desktop premium</b> — everything included, ships M4; wishlists and
            Next Fest are free marketing</td><td class="r">$9.99</td></tr>
      </table>
      <div class="kpis" style="grid-template-columns:1fr 1fr 1fr">
        <div class="kpi"><div class="v">2–4%</div><div class="l">of installs become payers</div></div>
        <div class="kpi"><div class="v">~$8</div><div class="l">avg revenue per payer (mobile)</div></div>
        <div class="kpi"><div class="v">85% / 70%</div><div class="l">net kept — mobile / Steam</div></div>
      </div>
    </div>
  </div>
  <h3>The acquisition funnel — base case, launch quarter (illustrative)</h3>
  <div class="chartwrap">{svg_funnel()}</div>
  {foot(4)}
</div>""")

    # ---- 5 · budget ----
    pages.append(f"""
<div class="page">
  <div class="kicker2">04 · The $1,000</div>
  <h2>Budget allocation — ten experiments, hard kill rules</h2>
  <div class="cols2w">
    <div>
      <table>
        <tr><th>Line</th><th class="r">$</th><th>Kill / success gate</th></tr>
        <tr><td><b>Creator seeding</b> — 20–25 poker + RDR2-adjacent micro-creators (5k–100k);
            $25–50 paid for the 6–8 best fits; A/B the two framings</td>
            <td class="r">350</td><td>≥4 videos live in launch week; track which framing drives installs</td></tr>
        <tr><td><b>Apple Search Ads</b> — exact match only: offline poker, single player poker,
            poker vs ai, texas holdem offline, western / saloon poker</td>
            <td class="r">250</td><td>CPI &lt; $1.50 <b>and</b> D1 &gt; 35%, else pause keyword</td></tr>
        <tr><td><b>Reddit promoted posts</b> — r/poker + mobile-gaming subs; western-gaming
            interest targeting, trademark never in our copy; destination = web demo</td>
            <td class="r">150</td><td>Median demo session &gt; 2 min; demo→store CTR &gt; 8%</td></tr>
        <tr><td><b>Reserve</b> — weeks 3–4, doubles down on the cheapest <i>retained</i> install</td>
            <td class="r">150</td><td>—</td></tr>
        <tr><td><b>Trailer &amp; assets</b> — one 45s character-led trailer, reused everywhere</td>
            <td class="r">100</td><td>Exists before any spend</td></tr>
      </table>
      <p class="small"><b>Deliberately $0:</b> Facebook/Google broad UA (viable minimums are
      10–50× this budget), press agencies, paid reviews, feature brokers.
      <b>Free actions worth more than any paid line:</b> twice-weekly devlogs, Reddit participation
      as a developer, press kit, pitching "best offline games" listicle authors, Steam wishlist page.</p>
      <div class="gate"><b>Global kill rule:</b> any paid channel that cannot beat $1.50 CPI with
      D7 &gt; 10% after $100 spent is stopped.</div>
    </div>
    <div>
      <div class="chartwrap">{svg_donut()}</div>
    </div>
  </div>
  {foot(5)}
</div>""")

    # ---- 6 · roadmap ----
    pages.append(f"""
<div class="page">
  <div class="kicker2">05 · Execution</div>
  <h2>Roadmap — M0 to M4</h2>
  <div class="flow">
    <div class="fbox"><div class="t">M0 · Product gate</div><ul>
      <li>Western Saloon built as free flagship (5-NPC cast)</li>
      <li>Web demo = saloon, with analytics + UTM store banners</li>
      <li>60–90s raw capture per NPC for the content library</li></ul></div>
    <div class="farr">→</div>
    <div class="fbox"><div class="t">M1 · Presence</div><ul>
      <li>Store listings: NPC faces + tell captions, not a table</li>
      <li>$100 trailer (45s, character-led)</li>
      <li>Press kit · Steam "Coming Soon" wishlist page</li></ul></div>
    <div class="farr">→</div>
    <div class="fbox"><div class="t">M2 · Audience (4–6 wks, $0)</div><ul>
      <li>Devlogs 2×/week on TikTok / Shorts / Reels</li>
      <li>Reddit as a developer — own the recurring "standalone RDR2 poker?" thread</li>
      <li>50-creator outreach list (poker + RDR2-adjacent)</li></ul></div>
    <div class="farr">→</div>
    <div class="fbox"><div class="t">M3 · Launch ($900)</div><ul>
      <li>Android soft launch → fix retention</li>
      <li>Coordinated global launch week spike</li>
      <li>Deploy budget per §04 with kill rules</li></ul></div>
    <div class="farr">→</div>
    <div class="fbox"><div class="t">M4 · Live cadence</div><ul>
      <li>One pack / 6–8 wks — each a full marketing beat</li>
      <li>Steam premium M4 + Next Fest M6</li>
      <li>Coaching beat into poker-learning spaces</li></ul></div>
  </div>
  <div class="cols2">
    <div class="gate"><b>Gate A (blocks all spend):</b> 3+ external testers each play the saloon
    15–30 minutes unprompted <b>and can name the NPCs' personalities afterwards</b>.</div>
    <div class="gate"><b>Gate B (blocks global launch):</b> soft-launch metrics — D1 &gt; 35%,
    D7 &gt; 10%, median session &gt; 8 minutes.</div>
  </div>
  <h3>The strategic A/B every launch asset answers</h3>
  <p>Does the <b>saloon-fantasy</b> framing or the <b>anti-chip-casino</b> framing acquire
  better-retaining players? Creator briefs and ad sets are split between the two; the winner
  leads every M4 beat.</p>
  <h3>Launch-week KPI targets</h3>
  <div class="kpis">
    <div class="kpi"><div class="v">&lt; $1.50</div><div class="l">cost per install (paid)</div></div>
    <div class="kpi"><div class="v">&gt; 35%</div><div class="l">day-1 retention</div></div>
    <div class="kpi"><div class="v">&gt; 10%</div><div class="l">day-7 retention</div></div>
    <div class="kpi"><div class="v">&gt; 8 min</div><div class="l">median session</div></div>
    <div class="kpi"><div class="v">&gt; 2 min</div><div class="l">median web-demo session</div></div>
    <div class="kpi"><div class="v">&gt; 8%</div><div class="l">demo → store click-through</div></div>
    <div class="kpi"><div class="v">2–4%</div><div class="l">payer conversion</div></div>
    <div class="kpi"><div class="v">≥ 4</div><div class="l">creator videos live in week 1</div></div>
  </div>
  {foot(6)}
</div>""")

    # ---- 7 · projections ----
    rows = "".join(
        f'<tr><td><b>{name}</b><br/><span class="small">{s["note"]}</span></td>'
        f'<td class="r">{s["installs_total"]:,}</td>'
        f'<td class="r">{s["conv"]*100:.0f}% · ${s["arppu"]:.0f}</td>'
        f'<td class="r">{s["steam_units"]:,}</td>'
        f'<td class="r"><b>{money(s["cumulative"][-1])}</b></td>'
        f'<td class="r">{"M"+str(next((i+1 for i,v in enumerate(s["cumulative"]) if v>0), "—"))}</td></tr>'
        for name, s in model.items())
    pages.append(f"""
<div class="page">
  <div class="kicker2">06 · Projections</div>
  <h2>Projected profit over 12 months</h2>
  <p class="small">Cumulative profit = mobile IAP net (85% after store small-business cut)
  + Steam net (70%) − $1,250 upfront (ads, dev accounts, Steam Direct) − $50/month running costs.
  Scenarios differ on install volume, payer conversion, average revenue per payer and Steam units.</p>
  <div class="chartwrap">{svg_line_chart(model)}</div>
  <table>
    <tr><th>Scenario</th><th class="r">Installs (yr 1)</th><th class="r">Payer % · ARPPU</th>
        <th class="r">Steam units</th><th class="r">Cumulative profit (M12)</th><th class="r">Breakeven</th></tr>
    {rows}
  </table>
  <div class="quote">Because total cost is tiny, even the conservative case reaches breakeven —
  the real risk is opportunity cost, not capital. The budget's job is to find <b>one repeatable
  channel</b> (CPI &lt; $1.50, D7 &gt; 10%); that evidence, not launch revenue, is what justifies
  real spend afterwards.</div>
  {foot(7)}
</div>""")

    # ---- 8 · base case detail + risks ----
    pages.append(f"""
<div class="page">
  <div class="kicker2">07 · Base case &amp; risks</div>
  <h2>Base-case monthly net revenue mix</h2>
  <p class="small">Mobile launch spike decays to an ASO + devlog floor sustained by pack beats
  every 6–8 weeks; the $9.99 Steam edition lands month 4 with a Next Fest bump in month 6.</p>
  <div class="chartwrap">{svg_stacked_bars(model)}</div>
  <h3>Risk register</h3>
  <table>
    <tr><th style="width:26%">Risk</th><th>Mitigation</th></tr>
    <tr><td>Fun bar not met</td><td>Gate A blocks all marketing spend until the saloon slice passes
        the 15–30 minute test with nameable NPC personalities</td></tr>
    <tr><td>IP exposure</td><td>No third-party names or assets anywhere we control; comparisons live
        only in editorial / creator / community speech; repo name stays private</td></tr>
    <tr><td>Poker flagged as gambling by ad platforms</td><td>Creatives lead with "no real money,
        no chips for sale"; simulated-gambling store questionnaires completed correctly from day one</td></tr>
    <tr><td>Expectation gap vs AAA</td><td>Honest build-in-public devlogs; sell ambience and
        characters, never graphics parity</td></tr>
    <tr><td>Web-demo cannibalisation</td><td>Acceptable — it converts sceptics; keep one pack
        store-exclusive if the data shows a problem</td></tr>
  </table>
  <h3>Measurement</h3>
  <p class="small">UTM-tag every link; two funnels (web demo → store, store direct). Weekly
  dashboard: installs by source, CPI, D1/D7, median session, % reaching hand 10, payer conversion,
  pack attach rate, Everything-Edition take rate, Steam wishlists. The strategic A/B — saloon
  fantasy vs anti-chip-casino — is settled with launch-week data and leads all future beats.</p>
  {foot(8)}
</div>""")

    return ("<!doctype html><html><head><meta charset='utf-8'>"
            f"<style>{CSS}</style></head><body>{''.join(pages)}</body></html>")


def main():
    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    model = run_model()
    html = build_html(model)
    out_pdf = os.path.join(root, "docs", "marketing-plan.pdf")
    chrome = next((c for c in ("/usr/bin/google-chrome-stable", "/opt/google/chrome/chrome",
                               "google-chrome") if c.startswith("g") or os.path.exists(c)),
                  "google-chrome")
    with tempfile.NamedTemporaryFile("w", suffix=".html", delete=False) as f:
        f.write(html)
        html_path = f.name
    with tempfile.TemporaryDirectory() as profile:
        try:
            subprocess.run(
                [chrome, "--headless=new", "--disable-gpu", "--no-sandbox",
                 f"--user-data-dir={profile}", "--no-pdf-header-footer",
                 f"--print-to-pdf={out_pdf}", f"file://{html_path}"],
                check=True, capture_output=True, text=True, timeout=120)
        finally:
            os.unlink(html_path)
    for name, s in model.items():
        be = next((i + 1 for i, v in enumerate(s["cumulative"]) if v > 0), None)
        print(f"{name:>12}: installs {s['installs_total']:>7,} | M12 profit "
              f"{s['cumulative'][-1]:>10,.0f} | breakeven {'M'+str(be) if be else 'none'}")
    print(f"PDF written: {out_pdf}")


if __name__ == "__main__":
    main()
