// Both table scenes keep at least two women, including casts saved before
// that rule. Run: node tools/table-lineup.test.mjs
import {
  SEATS,
  MIN_WOMEN,
  GIRL_SKIN_TONES,
  buildSaloonLineup,
  buildStreetLineupFresh,
  deriveStreetFromSaloon,
  deriveSaloonFromStreet,
  ensureSaloonWomen,
  ensureStreetWomen,
  countWomen,
  femaleFighterModel,
} from '../Web/CinematicPoker.Web/wwwroot/js/table-lineup.js';

function assert(cond, msg) {
  if (!cond) throw new Error(msg);
}

function girlTones(lineup) {
  return lineup.filter((p) => p.kind === 'girl').map((p) => p.skinTone.id);
}

function checkSaloon(lineup, ultra, label) {
  assert(lineup.length === SEATS, `${label} seat count`);
  assert(lineup[0].kind === 'jacob', `${label} player is Jacob`);
  assert(countWomen(lineup) >= MIN_WOMEN, `${label} women ${countWomen(lineup)}`);
  const tones = girlTones(lineup);
  assert(tones.length >= MIN_WOMEN, `${label} girl count`);
  assert(new Set(tones).size === tones.length, `${label} shared skin tone ${tones}`);
  if (!ultra) {
    assert(lineup.filter((p) => p.kind === 'sheriff').length === 1, `${label} sheriff`);
    assert(lineup.filter((p) => p.kind === 'bandit').length === 1, `${label} bandit`);
  }
  const looks = lineup.filter((p) => p.kind === 'jacob').map((p) => JSON.stringify(p.look));
  assert(new Set(looks).size === looks.length, `${label} duplicate Jacob look`);
}

function checkStreet(lineup, label) {
  assert(lineup.length === SEATS, `${label} seat count`);
  assert(lineup[0].kind === 'fighter' && !femaleFighterModel(lineup[0].model), `${label} player is a man`);
  assert(countWomen(lineup) >= MIN_WOMEN, `${label} women ${countWomen(lineup)}`);
  const models = lineup.map((p) => p.model);
  assert(new Set(models).size === models.length, `${label} duplicate fighter ${models}`);
}

for (let n = 0; n < 200; n++) {
  for (const ultra of [false, true]) {
    const saloon = buildSaloonLineup(ultra);
    checkSaloon(saloon, ultra, `deal ${n} ultra=${ultra}`);
    const street = deriveStreetFromSaloon(saloon);
    checkStreet(street, `street-from-saloon ${n}`);
    const back = deriveSaloonFromStreet(street, ultra);
    checkSaloon(back, ultra, `saloon-from-street ${n}`);
  }
  checkStreet(buildStreetLineupFresh(), `fresh street ${n}`);
}

const thin = ensureSaloonWomen([
  { kind: 'jacob', look: { hat: 1, pants: 1 } },
  { kind: 'girl', look: null, skinTone: GIRL_SKIN_TONES[0] },
  { kind: 'sheriff', look: null },
  { kind: 'bandit', look: null },
  { kind: 'jacob', look: { hat: 2, pants: 2 } },
  { kind: 'jacob', look: { hat: 3, pants: 3 } },
]);
checkSaloon(thin, false, 'repaired one-woman saloon');
assert(thin.some((p) => p.kind === 'sheriff') && thin.some((p) => p.kind === 'bandit'), 'repair kept the men');

const boys = ensureStreetWomen([0, 2, 4, 0, 2, 4].map((model) => ({ kind: 'fighter', look: null, model })));
assert(countWomen(boys) >= MIN_WOMEN, 'repaired octagon women');
assert(!femaleFighterModel(boys[0].model), 'repaired octagon player');

console.log('table lineup: 200 deals, both scenes, repairs ok');
