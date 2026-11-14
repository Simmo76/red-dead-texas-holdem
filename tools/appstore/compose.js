// Compose final App Store screenshot panels from the raw gameplay captures.
// iPhone 6.9" landscape (2868x1320) and iPad Pro 13" landscape (2752x2064).
const { chromium } = require('playwright-core');
const fs = require('fs');
const path = require('path');

const RAW = '/tmp/appstore/shots';
const OUT_IPHONE = '/tmp/appstore/final/iphone-6.9';
const OUT_IPAD = '/tmp/appstore/final/ipad-13';
fs.mkdirSync(OUT_IPHONE, { recursive: true });
fs.mkdirSync(OUT_IPAD, { recursive: true });

const FONT = fs.readFileSync('/tmp/appstore/rye.woff2').toString('base64');

// The ordered screenshot set. Learning + coach lead (slots 2-4 are visible
// in search alongside slot 1).
const PANELS = [
  {
    file: '01-cinematic-holdem',
    img: `${RAW}/saloon/00-splash.png`,
    kicker: 'SINGLE PLAYER \u2022 OFFLINE \u2022 NO REAL-MONEY BETS',
    headline: null, // splash art carries the title
    scrim: 0.55,
  },
  {
    file: '02-what-would-ai-do',
    img: `${RAW}/saloon/30-ai-advice-1-board0.png`,
    kicker: 'LEARN AS YOU PLAY',
    headline: 'STUCK? ASK \u201CWHAT WOULD AI DO?\u201D',
    sub: 'Live equity, pot odds and a plain-English reason \u2014 on any turn',
  },
  {
    file: '03-poker-coach',
    img: `${RAW}/saloon/20-coach-review.png`,
    kicker: 'YOUR PERSONAL POKER COACH',
    headline: 'EVERY HAND REVIEWED, IF YOU WANT',
    sub: 'Why you won, why you lost \u2014 and one thing to try next time',
    band: true,
  },
  {
    file: '04-key-decision',
    img: `${RAW}/street/21-coach-key-decision.png`,
    kicker: 'GO DEEPER',
    headline: 'REPLAY THE KEY DECISION',
    sub: 'Street, board and the numbers behind the coach\u2019s advice',
    band: true,
  },
  {
    file: '05-octagon',
    img: `${RAW}/street/40-turn-3-board4.png`,
    kicker: 'EXTRAORDINARY PLACES',
    headline: 'STEP INTO THE OCTAGON',
    sub: 'Heavyweights at the table \u2014 and Tommy Gunn in your corner',
  },
  {
    file: '06-saloon',
    img: `${RAW}/saloon/50-frame-018.png`,
    kicker: 'CINEMATIC TABLES',
    headline: 'GO ALL-IN IN THE OLD SALOON',
    sub: 'Five rivals with real personalities, tells and tilt',
  },
  {
    file: '07-octagon-advice',
    img: `${RAW}/street/30-ai-advice-1-board0.png`,
    kicker: 'A COACH IN EVERY WORLD',
    headline: 'KNOW WHEN TO RAISE FOR VALUE',
    sub: 'The advisor counts the cards so you learn to think in odds',
  },
  {
    file: '08-learn-from-scratch',
    img: `${RAW}/saloon/60-hand-rankings.png`,
    kicker: 'NEW TO POKER?',
    headline: 'RULES & HAND RANKINGS BUILT IN',
    sub: 'From high card to royal flush \u2014 one tap away, mid-hand',
    band: true,
  },
  {
    file: '09-win-the-table',
    img: `${RAW}/saloon/50-frame-012.png`,
    kicker: 'NO ACCOUNT \u2022 NO ADS BETWEEN HANDS \u2022 PLAY ANYWHERE',
    headline: 'OUTPLAY THE TABLE',
    sub: 'Pure offline Texas Hold\u2019em \u2014 your chips never cost a cent',
  },
];

function phoneBandHtml(p) {
  const img = fs.readFileSync(p.img).toString('base64');
  return `<!doctype html><html><head><style>
  @font-face { font-family:'Rye'; src:url(data:font/woff2;base64,${FONT}); }
  * { margin:0; padding:0; box-sizing:border-box; }
  body { width:1434px; height:660px; overflow:hidden; background:#0c0a08;
         font-family: Georgia, 'Times New Roman', serif; }
  .band { height:116px; text-align:center; padding-top:16px;
          background:linear-gradient(to bottom, #171007, #0c0a08); }
  .kicker { color:#d87a6a; font-size:16px; letter-spacing:6px; font-weight:bold; }
  .head { margin-top:6px; color:#e8c87a; font-family:'Rye', Georgia, serif;
          font-size:38px; letter-spacing:2px; }
  .sub { margin-top:4px; color:#e9ddc8; font-size:18px; letter-spacing:1px;
         font-style:italic; }
  .shotwrap { position:absolute; top:116px; bottom:0; left:0; right:0;
              display:flex; justify-content:center; align-items:flex-start; }
  .shot { height:544px; border-top:2px solid #a8853e;
          border-left:2px solid #a8853e; border-right:2px solid #a8853e;
          border-radius:14px 14px 0 0; }
  </style></head><body>
    <div class="band">
      <div class="kicker">${p.kicker}</div>
      <div class="head">${p.headline}</div>
      ${p.sub ? `<div class="sub">${p.sub}</div>` : ''}
    </div>
    <div class="shotwrap"><img class="shot" src="data:image/png;base64,${img}" /></div>
  </body></html>`;
}

function phoneHtml(p) {
  if (p.band) return phoneBandHtml(p);
  const img = fs.readFileSync(p.img).toString('base64');
  return `<!doctype html><html><head><style>
  @font-face { font-family:'Rye'; src:url(data:font/woff2;base64,${FONT}); }
  * { margin:0; padding:0; box-sizing:border-box; }
  body { width:1434px; height:660px; overflow:hidden; background:#0c0a08;
         font-family: Georgia, 'Times New Roman', serif; }
  .bg { position:absolute; inset:0; width:100%; height:100%;
        object-fit:cover; object-position:center; }
  .scrim { position:absolute; left:0; top:0; right:0; height:200px;
           background:linear-gradient(to bottom, rgba(5,3,2,${p.scrim ?? 0.86}) 0%,
             rgba(5,3,2,${(p.scrim ?? 0.86) * 0.65}) 55%, rgba(5,3,2,0) 100%); }
  .cap { position:absolute; left:0; right:0; top:26px; text-align:center; }
  .kicker { color:#d87a6a; font-size:17px; letter-spacing:6px; font-weight:bold; }
  .head { margin-top:10px; color:#e8c87a; font-family:'Rye', Georgia, serif;
          font-size:52px; letter-spacing:3px;
          text-shadow:0 3px 14px rgba(0,0,0,0.9); }
  .sub { margin-top:9px; color:#e9ddc8; font-size:21px; letter-spacing:1.5px;
         font-style:italic; text-shadow:0 2px 8px rgba(0,0,0,0.9); }
  .rule { width:120px; height:2px; margin:14px auto 0;
          background:linear-gradient(to right, transparent, #a8853e, transparent); }
  </style></head><body>
    <img class="bg" src="data:image/png;base64,${img}" />
    <div class="scrim"></div>
    <div class="cap">
      <div class="kicker">${p.kicker}</div>
      ${p.headline ? `<div class="head">${p.headline}</div>` : ''}
      ${p.sub ? `<div class="sub">${p.sub}</div>` : ''}
      ${p.headline ? '<div class="rule"></div>' : ''}
    </div>
  </body></html>`;
}

function ipadHtml(p) {
  const img = fs.readFileSync(p.img).toString('base64');
  return `<!doctype html><html><head><style>
  @font-face { font-family:'Rye'; src:url(data:font/woff2;base64,${FONT}); }
  * { margin:0; padding:0; box-sizing:border-box; }
  body { width:1376px; height:1032px; overflow:hidden;
         font-family: Georgia, 'Times New Roman', serif;
         background:
           radial-gradient(ellipse at 50% -20%, #3a2c1a 0%, rgba(58,44,26,0) 60%),
           radial-gradient(ellipse at 50% 120%, #241a10 0%, rgba(36,26,16,0) 55%),
           #0c0a08; }
  .suits { position:absolute; inset:0; opacity:0.05; font-size:64px; color:#e8c87a;
           line-height:120px; letter-spacing:52px; text-align:center;
           overflow:hidden; user-select:none; }
  .cap { position:absolute; left:0; right:0; top:74px; text-align:center; }
  .kicker { color:#d87a6a; font-size:21px; letter-spacing:7px; font-weight:bold; }
  .head { margin-top:14px; color:#e8c87a; font-family:'Rye', Georgia, serif;
          font-size:64px; letter-spacing:3px; }
  .sub { margin-top:12px; color:#e9ddc8; font-size:26px; letter-spacing:1.5px;
         font-style:italic; }
  .shot { position:absolute; left:50%; bottom:84px; transform:translateX(-50%);
          width:1216px; border:3px solid #a8853e; border-radius:18px;
          box-shadow:0 30px 80px rgba(0,0,0,0.85); }
  </style></head><body>
    <div class="suits">${'\u2660 \u2665 \u2666 \u2663 '.repeat(60)}</div>
    <div class="cap">
      <div class="kicker">${p.kicker}</div>
      ${p.headline ? `<div class="head">${p.headline}</div>` : ''}
      ${p.sub ? `<div class="sub">${p.sub}</div>` : ''}
    </div>
    <img class="shot" src="data:image/png;base64,${img}" />
  </body></html>`;
}

(async () => {
  const browser = await chromium.launch({
    executablePath: '/usr/local/bin/google-chrome',
    headless: true,
    args: ['--no-sandbox', '--disable-dev-shm-usage', '--hide-scrollbars'],
  });

  const phone = await browser.newPage({
    viewport: { width: 1434, height: 660 }, deviceScaleFactor: 2,
  });
  for (const p of PANELS) {
    await phone.setContent(phoneHtml(p), { waitUntil: 'networkidle' });
    await phone.waitForTimeout(300);
    await phone.screenshot({ path: `${OUT_IPHONE}/${p.file}.jpg`, type: 'jpeg', quality: 90 });
    console.log('iphone', p.file);
  }

  const ipad = await browser.newPage({
    viewport: { width: 1376, height: 1032 }, deviceScaleFactor: 2,
  });
  for (const p of PANELS) {
    await ipad.setContent(ipadHtml(p), { waitUntil: 'networkidle' });
    await ipad.waitForTimeout(300);
    await ipad.screenshot({ path: `${OUT_IPAD}/${p.file}.jpg`, type: 'jpeg', quality: 90 });
    console.log('ipad', p.file);
  }

  await browser.close();
  console.log('composed', PANELS.length, 'panels per device');
})().catch(e => { console.error(e); process.exit(1); });
