const { chromium } = require('playwright-core');

const SHOTS = [
  ['Fighter1', 0, 'front'],
  ['Fighter1', 35, 'quarter'],
  ['Fighter2', 0, 'front'],
  ['Fighter2', 35, 'quarter'],
];

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
  const page = await browser.newPage({ viewport: { width: 1024, height: 1024 } });
  for (const [model, yaw, label] of SHOTS) {
    await page.goto(`http://127.0.0.1:8123/__viewer_tmp.html?m=/models/characters/street/${model}.glb&yaw=${yaw}`);
    await page.waitForFunction(() => document.title === 'READY', null, { timeout: 60000 });
    await page.waitForTimeout(300);
    await page.screenshot({ path: `/tmp/fighter-renders/${model}-${label}.png` });
    console.log('rendered', model, label);
  }
  await browser.close();
})();
