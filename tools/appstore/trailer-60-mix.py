import subprocess
import wave
import numpy as np

SR = 44100
AUD = '/agent/repos/red-dead-texas-holdem/Web/CinematicPoker.Web/wwwroot/audio/'
DUR = 53.16
N = int(DUR * SR)
music = np.zeros((N, 2), dtype=np.float64)   # duckable bed
front = np.zeros((N, 2), dtype=np.float64)   # VO + hits + fanfare


def decode(path, ss=None, t=None):
    cmd = ['ffmpeg', '-loglevel', 'error']
    if ss is not None:
        cmd += ['-ss', str(ss)]
    cmd += ['-i', path]
    if t is not None:
        cmd += ['-t', str(t)]
    cmd += ['-f', 'f32le', '-ac', '2', '-ar', str(SR), '-']
    raw = subprocess.run(cmd, capture_output=True).stdout
    return np.frombuffer(raw, dtype=np.float32).reshape(-1, 2).astype(np.float64)


def repitch(a, rate):
    """Resample playback rate: <1 = slower + lower pitch."""
    n_out = int(len(a) / rate)
    idx = np.linspace(0, len(a) - 1, n_out)
    lo = np.floor(idx).astype(int)
    hi = np.minimum(lo + 1, len(a) - 1)
    fr = (idx - lo)[:, None]
    return a[lo] * (1 - fr) + a[hi] * fr


def fade(a, fin=0.0, fout=0.0):
    a = a.copy()
    if fin > 0:
        n = min(int(fin * SR), len(a))
        a[:n] *= np.linspace(0, 1, n)[:, None]
    if fout > 0:
        n = min(int(fout * SR), len(a))
        a[-n:] *= np.linspace(1, 0, n)[:, None]
    return a


def place(buf, a, at, gain=1.0):
    s = int(at * SR)
    e = min(s + len(a), N)
    if s < N:
        buf[s:e] += a[:e - s] * gain


def norm(a, peak=0.9):
    m = np.abs(a).max()
    return a * (peak / m) if m > 0 else a


# ---------- act 1: saloon (ragtime + bar murmur, hard comedic stop at 17.6) ----------
amb = fade(decode(AUD + 'saloon-bar-ambience.mp3', ss=1.0, t=17.6), fin=0.4, fout=0.06)
place(music, amb, 0.0, 0.17)

rag = fade(decode(AUD + 'saloon/top-liner-rag.mp3', ss=0.3, t=17.35), fin=0.5, fout=0.035)
place(music, rag, 0.25, 0.5)

# ---------- act 2: octagon (Crushing Enemies drop lands on the flash at 20.4) ----------
battle = fade(decode(AUD + 'street/crushing-enemies-dos.mp3', ss=42.9, t=26.9), fin=0.08, fout=1.0)
t_ax = np.arange(len(battle)) / SR + 20.3
settle = np.where(t_ax < 43.4, 1.0, np.maximum(0.28, 1.0 - 0.72 * (t_ax - 43.4) / 0.9))
battle *= settle[:, None]
place(music, battle, 20.3, 0.74)

crowd = fade(decode(AUD + 'street/crowd-shouting.ogg', ss=0.5, t=23.0), fin=0.4, fout=0.9)
place(music, crowd, 20.4, 0.15)

# ---------- voice-overs (music ducks under each) ----------
vo_events = [
    ('voices/random/random-05.mp3', 9.6),         # "Don't touch the hat."
    ('voices/lose/lose-01.mp3', 15.1),            # "I'll be damned!"
    ('street/voices/street-voice-09.wav', 21.0),  # "Are you not entertained?"
    ('street/voices/street-voice-14.wav', 27.1),  # "...acquainted with that face"
    ('voices/win/win-01.mp3', 32.0),              # "Muhahahaha!"
    ('voices/win/win-02.mp3', 34.8),              # "ace up my sleeve"
    ('voices/random/random-10.mp3', 37.2),        # "Listen up now. I got a plan."
    ('street/voices/street-voice-03.wav', 40.6),  # "How embarrassing!"
    ('street/voices/street-voice-12.wav', 48.4),  # "Come on, do something!"
]
duck = np.ones(N)
for f, at in vo_events:
    a = norm(decode(AUD + f), 0.82)
    place(front, fade(a, fin=0.01, fout=0.05), at)
    s = max(0, int((at - 0.12) * SR))
    e = min(N, int((at + len(a) / SR + 0.08) * SR))
    duck[s:e] = np.minimum(duck[s:e], 0.45)
# smooth the duck curve (~80 ms) to avoid gain clicks
k = int(0.08 * SR)
duck = np.convolve(duck, np.ones(k) / k, mode='same')
music *= duck[:, None]

# ---------- end card fanfare ----------
place(front, fade(norm(decode(AUD + 'victory.mp3'), 0.85), fout=0.3), 43.6, 0.95)

# ---------- hit SFX on title lands / punches / stamps ----------
drop = decode(AUD + 'drop-chips.mp3')
coll1 = decode(AUD + 'chips-collide-1.mp3')
coll2 = decode(AUD + 'chips-collide-2.mp3')
shove = decode(AUD + 'card-shove-1.mp3')

big = norm(repitch(drop, 0.62), 0.9)
big[:len(coll1)] += norm(coll1, 0.5)
big = norm(big, 0.9)
mid = norm(repitch(drop, 0.85), 0.8)
mid[:len(coll2)] += norm(coll2, 0.45)
mid = norm(mid, 0.75)
soft = norm(shove, 0.5)

for t in [2.7, 9.3, 14.6, 15.7, 21.4, 26.8, 27.8, 30.8, 40.9, 45.4, 46.2, 47.0, 48.2]:
    place(front, big, t, 0.9)
for t in [24.8, 37.7]:
    place(front, mid, t, 0.8)
for t in [5.8, 12.2, 34.2, 38.6]:
    place(front, soft, t, 0.6)

# ---------- master ----------
mix = music + front
peak = np.abs(mix).max()
if peak > 0.97:
    mix *= 0.97 / peak
out = (np.clip(mix, -1, 1) * 32767).astype(np.int16)
with wave.open('/tmp/appstore/trailer/master60.wav', 'wb') as w:
    w.setnchannels(2)
    w.setsampwidth(2)
    w.setframerate(SR)
    w.writeframes(out.tobytes())
print('peak was', round(peak, 3))
