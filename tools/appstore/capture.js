// Capture high-res gameplay screenshots from the Cinematic Poker web build
// for App Store design work. One run per scene: `node capture.js saloon|street`.
const { chromium } = require('playwright-core');
const fs = require('fs');

const SCENE = process.argv[2] === 'street' ? 'street' : 'saloon';
const OUT = `/tmp/appstore/shots/${SCENE}`;
fs.mkdirSync(OUT, { recursive: true });

const APP = 'http://localhost:5180/';

(async () => {
  const browser = await chromium.launch({
    executablePath: '/usr/local/bin/google-chrome',
    headless: true,
    args: [
      '--use-gl=angle', '--use-angle=swiftshader',
      '--enable-unsafe-swiftshader', '--no-sandbox',
      '--disable-dev-shm-usage', '--hide-scrollbars',
    ],
  });
  const ctx = await browser.newContext({
    viewport: { width: 1434, height: 660 },
    deviceScaleFactor: 2,
  });
  const page = await ctx.newPage();

  await ctx.addInitScript(([scene]) => {
    localStorage.setItem('cinematic-poker-player-name', 'Jake');
    localStorage.setItem('cinematic-poker-table-scene', scene);
    localStorage.setItem('cinematic-poker-board-hint-seen', '1');
  }, [SCENE]);

  await page.goto(APP, { waitUntil: 'networkidle' });

  const shot = async (name) => {
    await page.screenshot({ path: `${OUT}/${name}.png` });
    console.log('shot', name);
  };

  await page.waitForSelector('.intro-overlay', { timeout: 60000 });
  await page.waitForTimeout(3000);
  await shot('00-splash');
  await page.click('.intro-overlay');
  await page.waitForSelector('.intro-overlay', { state: 'detached', timeout: 180000 });
  await page.waitForTimeout(3000);

  const clickIf = async (sel) => {
    const el = await page.$(sel);
    if (el && await el.isVisible()) { await el.click().catch(() => {}); return true; }
    return false;
  };

  let frame = 0;
  let adviceShot = 0;
  let tableShot = 0;
  let coachDone = false;
  let keyShot = false;
  let hands = 0;
  const deadline = Date.now() + 900000;

  while (Date.now() < deadline && !(coachDone && hands >= 2)) {
    // ---- coach open?
    if (await page.$('.coach-panel')) {
      if (!coachDone) {
        await page.waitForTimeout(9000); // let the 3D coach scene settle
        await shot('20-coach-review');
        // Key decision breakdown, if offered.
        const key = await page.$('.coach-panel button:has-text("SEE KEY DECISION")');
        if (key && !keyShot) {
          await key.click();
          await page.waitForTimeout(2500);
          await shot('21-coach-key-decision');
          keyShot = true;
        }
        coachDone = true;
      }
      // Advance: NEXT HAND inside the coach, else close.
      if (!(await clickIf('.coach-panel button:has-text("NEXT HAND")'))) {
        await page.keyboard.press('Escape');
      }
      await page.waitForTimeout(2500);
      hands++;
      continue;
    }

    // ---- hand result on screen with the coach button in the topbar
    if (await page.$('.chip-btn.review-hand')) {
      await page.waitForTimeout(1200);
      await shot(`10-hand-result-${hands}`);
      if (!coachDone) {
        await page.click('.chip-btn.review-hand');
        await page.waitForSelector('.coach-panel', { timeout: 30000 });
        continue;
      }
      await clickIf('.actionbar .act.ff-next'); // NEXT HAND
      await page.waitForTimeout(2000);
      hands++;
      continue;
    }

    // ---- our turn
    const canAct = await page.$('.actionbar .act.check, .actionbar .act.call, .actionbar .act.fold');
    if (canAct) {
      const boardCards = await page.$$eval('.hud-board .hud-row:first-child .hud-card:not(.facedown)', els => els.length).catch(() => 0);
      // Expand the AI advisor once per street band for the learning shot.
      if (adviceShot < 2 && await page.$('.ai-advice')) {
        await page.click('.ai-advice');
        await page.waitForTimeout(2500); // Monte Carlo advice computes
        await shot(`30-ai-advice-${adviceShot}-board${boardCards}`);
        adviceShot++;
      }
      if (tableShot < 6) {
        await shot(`40-turn-${tableShot}-board${boardCards}`);
        tableShot++;
      }
      if (!(await clickIf('.actionbar .act.check'))) {
        if (!(await clickIf('.actionbar .act.call'))) await clickIf('.actionbar .act.fold');
      }
      await page.waitForTimeout(900);
      continue;
    }

    // ---- NPCs acting: periodic frames to catch all-in-colour table moments
    if (frame % 3 === 0) await shot(`50-frame-${String(frame).padStart(3, '0')}`);
    frame++;
    // Skip button fast-forwards NPC pauses, but never NEXT HAND (keep result up).
    await page.waitForTimeout(1500);
  }

  // Learning overlays (one scene is enough, but cheap to take in both).
  await page.keyboard.press('Escape');
  await page.waitForTimeout(1500);
  if (await clickIf('button:has-text("HANDS")')) {
    await page.waitForTimeout(2500);
    await shot('60-hand-rankings');
    await page.keyboard.press('Escape');
    await page.waitForTimeout(800);
  }
  if (await clickIf('button:has-text("RULES")')) {
    await page.waitForTimeout(2000);
    await shot('61-rules');
    await page.keyboard.press('Escape');
    await page.waitForTimeout(800);
  }
  if (await clickIf('button:has-text("SETTINGS")')) {
    await page.waitForTimeout(1500);
    await shot('62-settings');
    await page.keyboard.press('Escape');
  }

  await browser.close();
  console.log('done', SCENE);
})().catch(e => { console.error(e); process.exit(1); });
