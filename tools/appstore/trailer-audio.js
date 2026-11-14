// Renders the 30 s trailer soundtrack as PCM and writes trailer-audio.wav.
// Pure-JS DSP: Karplus-Strong guitar, whistle lead, gallop percussion,
// tremolo pad, plus gunshot / punch / whoosh / braaam effects.
// All event times are VIDEO seconds (trailer timeline minus the 0.8 s
// recording lead measured from the encoded mp4).
const fs = require('fs');

const SR = 44100, DUR = 30.0, N = Math.round(SR * DUR);
const L = new Float32Array(N), R = new Float32Array(N);

const noteHz = (semisFromA4) => 440 * Math.pow(2, semisFromA4 / 12);
// E minor helpers (semitones from A4)
const NOTE = { E1:-41, B1:-34, E2:-29, G2:-26, A2:-24, B2:-22, D3:-19, E3:-17,
               G3:-14, A3:-12, B3:-10, D4:-7, E4:-5, G4:-2, A4:0, B4:2,
               C5:3, D5:5, E5:7, FS5:9, G5:10, A5:12 };
const hz = (n) => noteHz(NOTE[n]);

function mix(buf, atSec, gain = 1, pan = 0) {
  const start = Math.round(atSec * SR);
  const gl = gain * Math.min(1, 1 - pan), gr = gain * Math.min(1, 1 + pan);
  for (let i = 0; i < buf.length; i++) {
    const j = start + i;
    if (j < 0 || j >= N) continue;
    L[j] += buf[i] * gl; R[j] += buf[i] * gr;
  }
}

function onepoleLP(buf, cutoffHz) {
  const a = 1 - Math.exp(-2 * Math.PI * cutoffHz / SR);
  let y = 0;
  for (let i = 0; i < buf.length; i++) { y += a * (buf[i] - y); buf[i] = y; }
  return buf;
}
function onepoleHP(buf, cutoffHz) {
  const a = 1 - Math.exp(-2 * Math.PI * cutoffHz / SR);
  let lp = 0;
  for (let i = 0; i < buf.length; i++) { lp += a * (buf[i] - lp); buf[i] = buf[i] - lp; }
  return buf;
}
const noise = (dur) => Float32Array.from({ length: Math.round(dur * SR) }, () => Math.random() * 2 - 1);

// ---------- instruments ----------

// Karplus-Strong plucked string.
function pluck(freq, dur, bright = 0.5) {
  const n = Math.round(dur * SR), p = Math.round(SR / freq);
  const out = new Float32Array(n), d = new Float32Array(p);
  for (let i = 0; i < p; i++) d[i] = Math.random() * 2 - 1;
  let idx = 0;
  const damp = 0.996;
  for (let i = 0; i < n; i++) {
    const cur = d[idx], nxt = d[(idx + 1) % p];
    out[i] = cur;
    d[idx] = damp * (bright * cur + (1 - bright) * ((cur + nxt) / 2) * 2 * 0.5 + (cur + nxt) / 2 * 0);
    d[idx] = damp * ((cur + nxt) / 2);
    idx = (idx + 1) % p;
  }
  // gentle attack to avoid click
  const atk = Math.min(64, n);
  for (let i = 0; i < atk; i++) out[i] *= i / atk;
  return out;
}

// Whistle: sine w/ vibrato, portamento and breath noise.
function whistle(f0, f1, dur, { vib = 5.3, vibDepth = 0.012, atk = 0.07, rel = 0.25, breath = 0.012 } = {}) {
  const n = Math.round(dur * SR), out = new Float32Array(n);
  let phase = 0;
  for (let i = 0; i < n; i++) {
    const t = i / SR, k = Math.min(1, t / (dur * 0.25));
    const f = (f0 + (f1 - f0) * k) * (1 + vibDepth * Math.sin(2 * Math.PI * vib * t) * Math.min(1, t / 0.3));
    phase += 2 * Math.PI * f / SR;
    let a = 1;
    if (t < atk) a = t / atk;
    if (t > dur - rel) a = Math.max(0, (dur - t) / rel);
    out[i] = (Math.sin(phase) + 0.18 * Math.sin(2 * phase)) * a + (Math.random() * 2 - 1) * breath * a;
  }
  return out;
}

// Sine with pitch sweep + exp decay (kicks, thumps).
function sweep(f0, f1, dur, decay = 8, gain = 1) {
  const n = Math.round(dur * SR), out = new Float32Array(n);
  let phase = 0;
  for (let i = 0; i < n; i++) {
    const t = i / SR, k = t / dur;
    const f = f0 + (f1 - f0) * k;
    phase += 2 * Math.PI * f / SR;
    out[i] = Math.sin(phase) * Math.exp(-decay * t) * gain;
  }
  return out;
}

function expNoise(dur, decay, lpHz, hpHz = 0) {
  const b = noise(dur);
  if (lpHz) onepoleLP(b, lpHz);
  if (hpHz) onepoleHP(b, hpHz);
  for (let i = 0; i < b.length; i++) b[i] *= Math.exp(-decay * (i / SR));
  return b;
}

// ---------- sound effects ----------

function gunshot(at, gain = 1) {
  const crack = expNoise(0.09, 55, 9000, 1200);
  mix(crack, at, 0.95 * gain);
  const body = expNoise(0.5, 9, 2600);
  mix(body, at + 0.004, 0.8 * gain);
  mix(sweep(130, 38, 0.3, 9, 1), at + 0.002, 0.85 * gain);
  // street echoes
  mix(expNoise(0.35, 10, 1200), at + 0.17, 0.3 * gain, 0.45);
  mix(expNoise(0.4, 9, 700), at + 0.38, 0.16 * gain, -0.5);
}

function punch(at, gain = 1) {
  mix(sweep(170, 42, 0.2, 11, 1), at, 1.0 * gain);          // thud
  mix(expNoise(0.05, 70, 5000, 700), at, 0.55 * gain);       // snap
  mix(sweep(60, 30, 0.4, 5, 1), at + 0.01, 0.8 * gain);      // sub drop
  mix(expNoise(0.3, 12, 1800), at + 0.02, 0.3 * gain);       // room
}

function whoosh(at, dur = 0.5, gain = 0.5) {
  const n = Math.round(dur * SR), b = noise(dur);
  onepoleLP(b, 2000); onepoleHP(b, 300);
  for (let i = 0; i < n; i++) {
    const k = i / n;
    b[i] *= Math.sin(Math.PI * Math.pow(k, 0.7));
  }
  mix(b, at, gain);
}

function braaam(at) {
  const dur = 4.5, n = Math.round(dur * SR), out = new Float32Array(n);
  const freqs = [hz('E1'), hz('E1') * 1.006, hz('E2'), hz('E2') * 0.994, hz('B1')];
  const ph = freqs.map(() => 0);
  for (let i = 0; i < n; i++) {
    const t = i / SR;
    let a = Math.min(1, t / 0.5) * Math.exp(-0.55 * Math.max(0, t - 1.2));
    let s = 0;
    for (let v = 0; v < freqs.length; v++) {
      const f = freqs[v] * (1 + 0.004 * Math.sin(2 * Math.PI * 0.7 * t + v));
      ph[v] += 2 * Math.PI * f / SR;
      // band-limited-ish saw: few harmonics
      s += Math.sin(ph[v]) + 0.5 * Math.sin(2 * ph[v]) + 0.33 * Math.sin(3 * ph[v]) + 0.25 * Math.sin(4 * ph[v]);
    }
    out[i] = (s / freqs.length) * a;
  }
  onepoleLP(out, 900);
  mix(out, at, 0.85);
  mix(sweep(55, 28, 1.2, 2.5, 1), at, 0.6); // sub swell
}

function chipShimmer(at, gain = 0.4) {
  for (let i = 0; i < 9; i++) {
    const f = 2200 + Math.random() * 3200;
    const b = sweep(f, f * 0.98, 0.12, 22, 1);
    mix(b, at + i * 0.045 + Math.random() * 0.02, gain * (0.5 + Math.random() * 0.5), (Math.random() * 2 - 1) * 0.6);
  }
}

// ---------- drums ----------

function kick(at, gain = 1) {
  mix(sweep(120, 44, 0.16, 14, 1), at, 0.9 * gain);
  mix(expNoise(0.02, 90, 4000), at, 0.18 * gain);
}
function shaker(at, gain = 1) { mix(expNoise(0.05, 60, 0, 4500), at, 0.3 * gain, 0.35); }
function tom(at, f, gain = 1) { mix(sweep(f, f * 0.55, 0.25, 9, 1), at, 0.7 * gain, -0.2); }
function tamb(at, gain = 1) { mix(expNoise(0.09, 32, 0, 5500), at, 0.22 * gain, -0.4); }

// ---------- arrangement ----------
// Tempo 120 BPM -> beat 0.5 s.
const BEAT = 0.5;

// Section map (video seconds)
const FLASH = 1.80, SLAM_HAND = 3.20, CUT_DEAL = 5.80, SLAM_SHOWDOWN = 6.40,
      CUT_BRAWL = 9.40, SLAM_ROUGH = 10.40, CUT_CAGE = 12.80, SLAM_OCTAGON = 13.40,
      CUT_COACH = 16.20, SLAM_LEGEND = 16.80, CUT_WIN = 19.40, SLAM_WIN = 20.00,
      END_CARD = 22.80, CTA = 25.20, FADE = 28.20;

// --- intro: wind + heartbeat + low drone (0 .. flash)
{
  const wind = noise(2.2); onepoleLP(wind, 420); onepoleHP(wind, 90);
  for (let i = 0; i < wind.length; i++) {
    const t = i / SR;
    wind[i] *= 0.4 * (0.5 + 0.5 * Math.sin(2 * Math.PI * 0.21 * t)) * Math.min(1, t / 0.7);
  }
  mix(wind, 0, 0.8);
  const drone = sweep(hz('E1'), hz('E1'), 2.4, 0.15, 1);
  mix(drone, 0, 0.4);
  kick(0.55, 0.45); kick(0.78, 0.3);   // heartbeat
  kick(1.35, 0.5); kick(1.58, 0.34);
}

// --- gunshots & impacts
gunshot(FLASH, 1.0);
gunshot(SLAM_HAND, 0.85);
gunshot(2.45, 0.5);                      // answering shot in the distance
punch(CUT_BRAWL, 1.0);
punch(SLAM_ROUGH, 0.9);
punch(11.4, 0.55);                       // scuffle follow-up
whoosh(CUT_DEAL - 0.18, 0.5, 0.5);
whoosh(CUT_CAGE - 0.18, 0.55, 0.55);
whoosh(CUT_COACH - 0.18, 0.5, 0.4);
whoosh(CUT_WIN - 0.18, 0.5, 0.5);
whoosh(END_CARD - 0.2, 0.6, 0.5);
chipShimmer(SLAM_WIN + 0.1, 0.5);
chipShimmer(21.0, 0.3);
braaam(END_CARD);

// accent hits on the remaining title slams
function slamHit(at, gain = 0.6) {
  kick(at, gain);
  mix(expNoise(0.25, 14, 2500), at, 0.35 * gain);
  mix(sweep(70, 34, 0.3, 6, 1), at, 0.5 * gain);
}
slamHit(SLAM_SHOWDOWN, 0.7);
slamHit(SLAM_OCTAGON, 0.6);
slamHit(SLAM_LEGEND, 0.45);
slamHit(SLAM_WIN, 0.75);

// --- gallop percussion: 2.0..CUT_COACH, then CUT_WIN..END_CARD
function gallopSpan(t0, t1, gain) {
  for (let b = t0; b < t1 - 0.01; b += BEAT) {
    shaker(b - 0.167, gain); shaker(b - 0.083, gain * 0.8); kick(b, gain);
    if (((Math.round((b - 2.0) / BEAT)) % 2) === 1) tamb(b, gain);
  }
}
gallopSpan(2.0, CUT_COACH, 0.8);
gallopSpan(CUT_WIN, END_CARD, 1.0);
// brawl-section toms for weight
for (let b = CUT_BRAWL; b < CUT_CAGE; b += BEAT * 2) tom(b + BEAT, 95, 0.8);

// --- bass (plucked low E ostinato)
function bassSpan(t0, t1, gain) {
  const pat = ['E2', 'E2', 'E2', 'G2', 'E2', 'E2', 'D3', 'B1'];
  let k = 0;
  for (let b = t0; b < t1 - 0.01; b += BEAT) {
    mix(onepoleLP(pluck(hz(pat[k % pat.length]), 0.55, 0.4), 700), b, gain);
    k++;
  }
}
bassSpan(2.0, CUT_COACH, 0.85);
bassSpan(CUT_COACH, CUT_WIN, 0.4);   // soft under the coach
bassSpan(CUT_WIN, END_CARD, 0.95);

// --- guitar riff (Karplus-Strong), eighth notes
function riffSpan(t0, t1, gain) {
  const pat = ['E3', 'B2', 'E3', 'G3', 'E3', 'B2', 'D3', 'E3'];
  let k = 0;
  for (let t = t0; t < t1 - 0.01; t += BEAT / 2) {
    mix(pluck(hz(pat[k % pat.length]), 0.4, 0.5), t, gain * (k % 2 ? 0.7 : 1), k % 2 ? 0.25 : -0.15);
    k++;
  }
}
riffSpan(2.0, CUT_DEAL, 0.5);
riffSpan(CUT_BRAWL, CUT_CAGE, 0.55);
riffSpan(CUT_WIN, END_CARD, 0.5);
// sparse plucks under deal + octagon so the whistle leads
function sparsePluck(t0, t1, gain) {
  const pat = ['E3', 'G3', 'B3', 'A3'];
  let k = 0;
  for (let t = t0; t < t1 - 0.01; t += BEAT) { mix(pluck(hz(pat[k % 4]), 0.5, 0.45), t, gain, 0.2); k++; }
}
sparsePluck(CUT_DEAL, CUT_BRAWL, 0.4);
sparsePluck(CUT_CAGE, CUT_COACH, 0.4);

// --- tremolo string pad (Em), swells by section
function pad(t0, dur, notes, gain, tremHz = 7.5) {
  const n = Math.round(dur * SR), out = new Float32Array(n);
  const ph = notes.map(() => 0);
  for (let i = 0; i < n; i++) {
    const t = i / SR;
    const trem = 0.72 + 0.28 * Math.sin(2 * Math.PI * tremHz * t);
    let a = Math.min(1, t / 0.8) * Math.min(1, (dur - t) / 1.0);
    let s = 0;
    for (let v = 0; v < notes.length; v++) {
      ph[v] += 2 * Math.PI * hz(notes[v]) / SR;
      s += Math.sin(ph[v]) + 0.3 * Math.sin(2 * ph[v]);
    }
    out[i] = (s / notes.length) * a * trem;
  }
  onepoleLP(out, 2200);
  mix(out, t0, gain);
}
pad(CUT_DEAL, CUT_BRAWL - CUT_DEAL + 0.5, ['E3', 'G3', 'B3'], 0.22);
pad(CUT_CAGE, CUT_COACH - CUT_CAGE + 0.5, ['E3', 'G3', 'B3', 'E4'], 0.26);
pad(CUT_COACH, CUT_WIN - CUT_COACH + 0.5, ['G3', 'B3', 'D4'], 0.2, 6);
pad(CUT_WIN, END_CARD - CUT_WIN + 0.3, ['E3', 'G3', 'B3', 'E4'], 0.3);
pad(END_CARD + 0.3, 6.2, ['E2', 'B2', 'E3', 'G3', 'B3'], 0.24, 5);

// --- whistle lead
function phrase(notes, gain = 0.5) {
  for (const [at, from, to, dur] of notes) mix(whistle(hz(from), hz(to), dur), at, gain, 0.1);
}
phrase([ // over the deal
  [6.0, 'B4', 'B4', 0.6], [6.7, 'E5', 'E5', 1.1],
  [7.9, 'D5', 'D5', 0.32], [8.25, 'C5', 'C5', 0.32], [8.6, 'B4', 'B4', 0.75],
], 0.5);
phrase([ // octagon
  [13.0, 'E5', 'E5', 0.5], [13.6, 'G5', 'G5', 0.9],
  [14.6, 'FS5', 'FS5', 0.32], [15.0, 'E5', 'E5', 0.4], [15.45, 'B4', 'B4', 0.7],
], 0.52);
phrase([ // coach — soft, low
  [16.9, 'G4', 'G4', 0.5], [17.5, 'A4', 'A4', 0.5], [18.1, 'B4', 'B4', 1.2],
], 0.34);
phrase([ // win — climb to the climax
  [19.6, 'B4', 'B4', 0.4], [20.1, 'E5', 'E5', 0.65], [20.85, 'G5', 'G5', 0.65],
  [21.6, 'A5', 'A5', 1.25],
], 0.55);

// --- CTA pulses (soft heartbeat under DOWNLOAD NOW)
for (let i = 0; i < 3; i++) { const t = CTA + 0.2 + i * 1.4; kick(t, 0.4); kick(t + 0.22, 0.26); }

// ---------- master ----------
{
  // final fade + gentle soft clip, then 16-bit WAV
  const fadeStart = Math.round(FADE * SR), fadeEnd = Math.round(29.75 * SR);
  for (let i = 0; i < N; i++) {
    let f = 1;
    if (i > fadeStart) f = Math.max(0, 1 - (i - fadeStart) / (fadeEnd - fadeStart));
    L[i] = Math.tanh(L[i] * 0.85) * f;
    R[i] = Math.tanh(R[i] * 0.85) * f;
  }
  let peak = 0;
  for (let i = 0; i < N; i++) peak = Math.max(peak, Math.abs(L[i]), Math.abs(R[i]));
  const g = 0.94 / peak;
  const pcm = Buffer.alloc(N * 4);
  for (let i = 0; i < N; i++) {
    pcm.writeInt16LE(Math.round(L[i] * g * 32767), i * 4);
    pcm.writeInt16LE(Math.round(R[i] * g * 32767), i * 4 + 2);
  }
  const hdr = Buffer.alloc(44);
  hdr.write('RIFF', 0); hdr.writeUInt32LE(36 + pcm.length, 4); hdr.write('WAVE', 8);
  hdr.write('fmt ', 12); hdr.writeUInt32LE(16, 16); hdr.writeUInt16LE(1, 20);
  hdr.writeUInt16LE(2, 22); hdr.writeUInt32LE(SR, 24); hdr.writeUInt32LE(SR * 4, 28);
  hdr.writeUInt16LE(4, 32); hdr.writeUInt16LE(16, 34);
  hdr.write('data', 36); hdr.writeUInt32LE(pcm.length, 40);
  fs.writeFileSync('trailer-audio.wav', Buffer.concat([hdr, pcm]));
  console.log('wrote trailer-audio.wav, peak pre-norm', peak.toFixed(3));
}
