# App Store screenshot pipeline

Two scripts that produce the App Store screenshot set designed in
[`docs/app-store/app-store-listing.md`](../../docs/app-store/app-store-listing.md)
(see its "Asset production" section for full usage):

- `capture.js <saloon|street>` — runs the real web build headless, plays
  hands, opens the live advisor and the post-hand coach, and saves raw 2×
  gameplay captures to `/tmp/appstore/shots/<scene>/`.
- `compose.js` — lays the designed captions over the chosen captures and
  writes the final store panels (iPhone 6.9" 2868×1320 and iPad 13"
  2752×2064 JPEGs) to `/tmp/appstore/final/`.

Requires the web build running on `http://localhost:5180`, `playwright-core`
installed next to these scripts, Chrome at `/usr/local/bin/google-chrome`,
and the Rye font downloaded to `/tmp/appstore/rye.woff2`:

```bash
curl -sL "https://fonts.googleapis.com/css2?family=Rye" -A "Mozilla/5.0" \
  | grep -o "https://[^)]*" | head -1 | xargs curl -sL -o /tmp/appstore/rye.woff2
```

The committed set lives in `docs/app-store/screenshots/`.

- `trailer-audio.js` — renders the 30 s soundtrack for the cinematic
  trailer (`wwwroot/appstore-design/store/trailer.mp4`) as
  `trailer-audio.wav`: a synthesized spaghetti-western score (gallop
  percussion, Karplus-Strong guitar, whistle lead, tremolo pad) with
  gunshots, punches and a braaam placed on the trailer's cut points.
  Pure Node, no dependencies. Mux onto the silent trailer video with
  `ffmpeg -i trailer.mp4 -i trailer-audio.wav -c:v copy -c:a aac -b:a 160k -shortest out.mp4`.
