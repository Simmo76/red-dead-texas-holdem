// Who sits where. Both table scenes (saloon and octagon) keep at least
// MIN_WOMEN women in the chairs for the whole session. The player stays a
// man: Jacob in the saloon, a male fighter in the octagon.
//
// The western pack has one female body (Cowboy Girl). The saloon seats her
// twice, each time with a different skin tone, so the two women read as
// different people. Every other body is unique. The octagon has two distinct
// female fighter GLBs and always uses both.

export const SEATS = 6;
export const MIN_WOMEN = 2;

export const FIGHTER_URLS = [
  'models/characters/street/Fighter1.glb?v=3',
  'models/characters/street/FemaleFighter1.glb?v=1',
  'models/characters/street/Fighter2.glb?v=3',
  'models/characters/street/FemaleFighter2.glb?v=1',
  'models/characters/street/Fighter1-variation.glb?v=3',
  'models/characters/street/Fighter2-variation.glb?v=3',
];

// Outfit and head presets from the Cowboy 1 pack (SK_Jacob). Each game deals
// distinct looks. `shirt` / `shirtOpt` are the two shirt meshes (only one is
// worn). Zero means that piece is off.
export const WESTERN_LOOKS = [
  { pants: 1, jacket: 1, shirt: 0, shirtOpt: 1, scarf: 1, hat: 1, hair: 'cut', gun: true, holster: true, belt: true, head: 'clean' },
  { pants: 2, jacket: 2, shirt: 0, shirtOpt: 2, scarf: 2, hat: 2, hair: 'cut', gun: true, holster: true, belt: true, head: 'dirt' },
  { pants: 3, jacket: 3, shirt: 0, shirtOpt: 3, scarf: 3, hat: 0, hair: 'full', gun: true, holster: true, belt: true, head: 'blood2' },
  { pants: 2, jacket: 0, shirt: 1, shirtOpt: 0, scarf: 3, hat: 2, hair: 'cut', gun: false, holster: false, belt: true, head: 'dirtblood' },
  { pants: 1, jacket: 1, shirt: 0, shirtOpt: 2, scarf: 2, hat: 0, hair: 'full', gun: false, holster: false, belt: false, head: 'blood' },
  { pants: 2, jacket: 0, shirt: 3, shirtOpt: 0, scarf: 3, hat: 2, hair: 'cut', gun: true, holster: true, belt: true, head: 'clean' },
  { pants: 3, jacket: 0, shirt: 2, shirtOpt: 0, scarf: 0, hat: 0, hair: 'full', gun: false, holster: false, belt: true, head: 'dirt' }
];

// Per-seat skin tones for every character at the table (saloon and octagon).
// The Cowboy Girl GLB is baked with the lightest neutral skin; Jacob /
// Sheriff / Bandit heads and hands use their light baked albedo the same way.
// Each entry is a shader multiply on skin and hair materials only.
// Factors are linear-space ratios of the target tone to the baked base
// (sRGB ratio ^ 2.2). Keep in sync with bake_cowgirl_npc.py SKIN_BASE.
export const GIRL_SKIN_TONES = [
  { id: 'caucasian', skin: [1.0, 1.0, 1.0], hair: [1.0, 1.0, 1.0] },
  { id: 'asian', skin: [0.72, 0.58, 0.45], hair: [1.0, 1.0, 1.0] },
  { id: 'tan', skin: [0.48, 0.34, 0.22], hair: [1.0, 1.0, 1.0] },
  { id: 'black', skin: [0.24, 0.13, 0.08], hair: [1.0, 1.0, 1.0] },
  { id: 'dark', skin: [0.14, 0.07, 0.045], hair: [1.0, 1.0, 1.0] },
];
export const TABLE_SKIN_TONES = GIRL_SKIN_TONES;

const LIGHT_SKIN_IDS = new Set(['caucasian']);
const DARK_SKIN_IDS = new Set(['asian', 'tan', 'black', 'dark']);
const PLAYER_DARK_SKIN_IDS = new Set(['black', 'dark']);

export function isLightSkinTone(id) {
  return LIGHT_SKIN_IDS.has(id);
}

export function isDarkSkinTone(id) {
  return DARK_SKIN_IDS.has(id);
}

function cloneSkinTone(tone) {
  if (!tone) return null;
  return {
    id: tone.id,
    skin: tone.skin && [...tone.skin],
    hair: tone.hair && [...tone.hair],
  };
}

function pickFromPool(pool) {
  return pool[Math.floor(Math.random() * pool.length)];
}

// Your seat when a new session starts: light (caucasian) or dark (black/dark).
export function pickPlayerStartingSkinTone() {
  const light = TABLE_SKIN_TONES.find((t) => t.id === 'caucasian');
  if (Math.random() < 0.5) return cloneSkinTone(light);
  const darkPool = TABLE_SKIN_TONES.filter((t) => PLAYER_DARK_SKIN_IDS.has(t.id));
  return cloneSkinTone(pickFromPool(darkPool));
}

export function isPlayerStartingSkinTone(id) {
  return id === 'caucasian' || PLAYER_DARK_SKIN_IDS.has(id);
}

// NPC seats mix lighter and darker skin tones (~50/50 with jitter). Seat 0
// uses pickPlayerStartingSkinTone. Repeating a tone across seats is fine;
// the two Cowboy Girls still get distinct tones via ensureSaloonWomen.
export function assignBalancedSkinTones(lineup) {
  if (!lineup || !lineup.length) return lineup;
  const n = lineup.length;
  const playerTone = pickPlayerStartingSkinTone();
  const npcN = n - 1;
  let darkCount = Math.floor(npcN / 2);
  if (Math.random() < 0.5) darkCount += Math.random() < 0.5 ? 1 : -1;
  darkCount = Math.max(1, Math.min(npcN - 1, darkCount));
  const lightCount = npcN - darkCount;
  const lightPool = TABLE_SKIN_TONES.filter((t) => isLightSkinTone(t.id));
  const darkPool = TABLE_SKIN_TONES.filter((t) => isDarkSkinTone(t.id));
  const npcTones = [];
  for (let i = 0; i < lightCount; i++) npcTones.push(cloneSkinTone(pickFromPool(lightPool)));
  for (let i = 0; i < darkCount; i++) npcTones.push(cloneSkinTone(pickFromPool(darkPool)));
  const tones = [playerTone, ...shuffle(npcTones)];
  return lineup.map((pick, i) => ({
    ...pick,
    look: pick.look ? { ...pick.look } : null,
    skinTone: tones[i],
  }));
}

export function countLightAndDark(lineup) {
  let light = 0;
  let dark = 0;
  for (const pick of lineup || []) {
    const id = toneId(pick && pick.skinTone);
    if (isLightSkinTone(id)) light++;
    else if (isDarkSkinTone(id)) dark++;
  }
  return { light, dark };
}

// Casts saved before every seat had a tone, or lineups that lost balance on
// migration, are topped up without reshuffling tones that are already set.
export function ensureTableSkinTones(lineup) {
  if (!lineup || !lineup.length) return lineup;
  const { light, dark } = countLightAndDark(lineup);
  if (light > 0 && dark > 0 && lineup.every((p) => p && p.skinTone)) return lineup;
  const locked = lineup.map((pick) => (pick && pick.skinTone ? cloneSkinTone(pick.skinTone) : null));
  const fresh = assignBalancedSkinTones(lineup.map((pick) => ({
    ...pick,
    look: pick.look ? { ...pick.look } : null,
    skinTone: null,
  })));
  return lineup.map((pick, i) => ({
    ...pick,
    look: pick.look ? { ...pick.look } : null,
    skinTone: locked[i] || (i === 0 ? pickPlayerStartingSkinTone() : fresh[i].skinTone),
  }));
}

export function shuffle(list) {
  const a = list.slice();
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    const tmp = a[i];
    a[i] = a[j];
    a[j] = tmp;
  }
  return a;
}

export function femaleFighterModel(modelIdx) {
  return /FemaleFighter/.test(FIGHTER_URLS[modelIdx]);
}

export function isWoman(pick) {
  if (!pick) return false;
  if (pick.kind === 'girl') return true;
  return pick.kind === 'fighter' && femaleFighterModel(pick.model);
}

export function countWomen(lineup) {
  return (lineup || []).filter(isWoman).length;
}

function saloonLookWithHat(look, i) {
  return look.hat ? look : { ...look, hat: 1 + (i % 2), hair: 'cut' };
}

function isFemaleSaloonKind(kind) {
  return kind === 'girl';
}

function toneId(tone) {
  return tone && tone.id ? tone.id : '';
}

function takeGirlTone(used) {
  const tone = GIRL_SKIN_TONES.find((t) => !used.has(t.id)) || GIRL_SKIN_TONES[used.size % GIRL_SKIN_TONES.length];
  used.add(tone.id);
  return tone;
}

// Older sessions stored a street fighter as a saloon guest. Map that seat to
// a Jacob look so a refresh drops the arcade body. ensureSaloonWomen then
// puts a second Cowboy Girl back if the table would otherwise be short.
export function migrateSaloonPick(pick, seatIndex) {
  if (!pick || pick.kind !== 'femaleGuest') return pick;
  const look = WESTERN_LOOKS[seatIndex % WESTERN_LOOKS.length];
  return { kind: 'jacob', look: saloonLookWithHat(look, seatIndex) };
}

export function buildSaloonLineup(ultraLite) {
  const jacobPool = shuffle(WESTERN_LOOKS.map((look, i) => ({
    kind: 'jacob',
    look: saloonLookWithHat(look, i),
  })));
  const playerPick = jacobPool[0];
  const femaleNpcs = Array.from({ length: MIN_WOMEN }, () => ({ kind: 'girl', look: null }));
  const specialNpcs = ultraLite
    ? femaleNpcs
    : [{ kind: 'sheriff', look: null }, { kind: 'bandit', look: null }, ...femaleNpcs];
  const npcJacob = jacobPool.slice(1, 1 + (SEATS - 1 - specialNpcs.length));
  const npcPool = shuffle([...npcJacob, ...specialNpcs]);
  return ensureSaloonWomen(assignBalancedSkinTones([playerPick, ...npcPool]));
}

export function buildStreetLineupFresh() {
  const fighterOrder = shuffle(FIGHTER_URLS.map((_, i) => i));
  if (femaleFighterModel(fighterOrder[0])) {
    const male = fighterOrder.findIndex((model, i) => i > 0 && !femaleFighterModel(model));
    if (male > 0) [fighterOrder[0], fighterOrder[male]] = [fighterOrder[male], fighterOrder[0]];
  }
  return assignBalancedSkinTones(ensureStreetWomen(
    fighterOrder.slice(0, SEATS).map((model) => ({ kind: 'fighter', look: null, model })),
  ));
}

function assignUniqueFighterModels(lineup) {
  const used = new Set();
  const malePool = FIGHTER_URLS.map((_, i) => i).filter((i) => !femaleFighterModel(i));
  const femalePool = FIGHTER_URLS.map((_, i) => i).filter((i) => femaleFighterModel(i));
  const take = (pool) => {
    const free = pool.find((m) => !used.has(m));
    if (free !== undefined) { used.add(free); return free; }
    return pool[used.size % pool.length];
  };
  const out = lineup.map((pick, i) => {
    const wantFemale = i > 0 && isFemaleSaloonKind(pick.kind);
    const model = i === 0
      ? take(malePool)
      : (wantFemale ? take(femalePool) : take(malePool));
    return {
      kind: 'fighter',
      look: null,
      model,
      skinTone: pick.skinTone ? cloneSkinTone(pick.skinTone) : null,
    };
  });
  return ensureStreetWomen(out);
}

export function deriveStreetFromSaloon(saloonLineup) {
  return assignUniqueFighterModels(saloonLineup);
}

function unusedJacob(jacobLooks, lineup, i, model) {
  const used = new Set(lineup.filter((p) => p.kind === 'jacob').map((p) => JSON.stringify(p.look)));
  const start = (i + (model || 0)) % jacobLooks.length;
  for (let k = 0; k < jacobLooks.length; k++) {
    const pick = jacobLooks[(start + k) % jacobLooks.length];
    if (!used.has(JSON.stringify(pick.look))) return pick;
  }
  return jacobLooks[start];
}

export function deriveSaloonFromStreet(streetLineup, ultraLite) {
  const jacobLooks = WESTERN_LOOKS.map((look, i) => ({
    kind: 'jacob',
    look: saloonLookWithHat(look, i),
  }));
  const usedTones = new Set();
  const lineup = [{
    ...jacobLooks[0],
    skinTone: streetLineup[0].skinTone ? cloneSkinTone(streetLineup[0].skinTone) : null,
  }];
  let women = 0;
  for (let i = 1; i < SEATS; i++) {
    const female = femaleFighterModel(streetLineup[i].model);
    if (female && women < MIN_WOMEN) {
      let tone = streetLineup[i].skinTone
        ? cloneSkinTone(streetLineup[i].skinTone)
        : takeGirlTone(usedTones);
      const id = toneId(tone);
      if (!id || usedTones.has(id)) tone = takeGirlTone(usedTones);
      usedTones.add(toneId(tone));
      lineup.push({ kind: 'girl', look: null, skinTone: tone });
      women++;
    } else if (!ultraLite && !lineup.some((p) => p.kind === 'sheriff')) {
      lineup.push({
        kind: 'sheriff',
        look: null,
        skinTone: streetLineup[i].skinTone ? cloneSkinTone(streetLineup[i].skinTone) : null,
      });
    } else if (!ultraLite && !lineup.some((p) => p.kind === 'bandit')) {
      lineup.push({
        kind: 'bandit',
        look: null,
        skinTone: streetLineup[i].skinTone ? cloneSkinTone(streetLineup[i].skinTone) : null,
      });
    } else {
      const jacob = unusedJacob(jacobLooks, lineup, i, streetLineup[i].model);
      lineup.push({
        ...jacob,
        skinTone: streetLineup[i].skinTone ? cloneSkinTone(streetLineup[i].skinTone) : null,
      });
    }
  }
  return ensureSaloonWomen(lineup);
}

// Saved saloon casts from before the two-woman rule, or a cast that lost a
// woman in migration, get another Cowboy Girl. Jacob seats are replaced
// before Sheriff or Bandit. The two girls never share a skin tone.
export function ensureSaloonWomen(lineup) {
  const out = lineup.map((pick) => ({
    ...pick,
    look: pick.look ? { ...pick.look } : null,
    skinTone: pick.skinTone
      ? { ...pick.skinTone, skin: pick.skinTone.skin && [...pick.skinTone.skin], hair: pick.skinTone.hair && [...pick.skinTone.hair] }
      : null,
  }));
  const used = new Set();
  for (const pick of out) {
    if (pick.kind !== 'girl') continue;
    const id = toneId(pick.skinTone);
    if (!id || used.has(id) || !pick.skinTone.skin) pick.skinTone = takeGirlTone(used);
    else used.add(id);
  }
  const replaceAt = [];
  for (let i = 1; i < out.length; i++) if (out[i].kind === 'jacob') replaceAt.push(i);
  for (let i = 1; i < out.length; i++) {
    if (out[i].kind !== 'girl' && out[i].kind !== 'jacob') replaceAt.push(i);
  }
  let women = out.filter((p) => p.kind === 'girl').length;
  for (const i of replaceAt) {
    if (women >= MIN_WOMEN) break;
    out[i] = { kind: 'girl', look: null, skinTone: takeGirlTone(used) };
    women++;
  }
  return out;
}

// The octagon player stays a male fighter. NPC seats are filled from the
// unused female GLBs until two women are seated.
export function ensureStreetWomen(lineup) {
  const out = lineup.map((pick) => ({ ...pick }));
  if (out[0] && femaleFighterModel(out[0].model)) {
    const swap = out.findIndex((p, idx) => idx > 0 && p && !femaleFighterModel(p.model));
    if (swap > 0) {
      const t = out[0].model;
      out[0].model = out[swap].model;
      out[swap].model = t;
    }
  }
  const used = new Set(out.map((p) => p.model));
  const femalePool = FIGHTER_URLS.map((_, i) => i).filter((i) => femaleFighterModel(i));
  let femaleNpc = out.slice(1).filter((p) => p && femaleFighterModel(p.model)).length;
  for (let i = 1; i < out.length && femaleNpc < MIN_WOMEN; i++) {
    if (femaleFighterModel(out[i].model)) continue;
    const free = femalePool.find((m) => !used.has(m));
    if (free === undefined) break;
    used.delete(out[i].model);
    out[i] = { kind: 'fighter', look: null, model: free };
    used.add(free);
    femaleNpc++;
  }
  return out;
}
