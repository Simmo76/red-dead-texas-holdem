#!/usr/bin/env python3
"""Build settings-panel.png shell from the full mock (labels + footer art only).

Control slots on the right are punched transparent so live HTML inputs and
dropdown pills are not doubled over baked art.
"""
from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
IMG = ROOT / "Web/CinematicPoker.Web/wwwroot/img"
FULL = IMG / "settings-panel-full.png"

# Normalized (x0, y0, x1, y1) — right-column interactive rows only; footer stays painted.
CUTOUTS = (
    (0.380, 0.198, 0.952, 0.292),
    (0.380, 0.292, 0.952, 0.368),
    (0.380, 0.368, 0.952, 0.444),
    (0.380, 0.444, 0.952, 0.520),
    (0.380, 0.520, 0.952, 0.596),
    (0.380, 0.596, 0.952, 0.672),
)


def punch_shell(width: int) -> Image.Image:
    height = round(width * 594 / 520)
    base = Image.open(FULL).convert("RGBA")
    im = base.resize((width, height), Image.Resampling.LANCZOS)
    pix = im.load()
    w, h = im.size
    for x0, y0, x1, y1 in CUTOUTS:
        ix0, iy0 = int(x0 * w), int(y0 * h)
        ix1, iy1 = int(x1 * w), int(y1 * h)
        for y in range(iy0, min(iy1, h)):
            for x in range(ix0, min(ix1, w)):
                r, g, b, a = pix[x, y]
                if a:
                    pix[x, y] = (r, g, b, 0)
    return im


def main() -> int:
    if not FULL.is_file():
        print(f"missing {FULL}", file=sys.stderr)
        return 1
    one_x = punch_shell(520)
    two_x = punch_shell(1040)
    out1 = IMG / "settings-panel.png"
    out2 = IMG / "settings-panel@2x.png"
    shell_copy = IMG / "settings-panel-shell-full.png"
    one_x.save(out1, optimize=True)
    two_x.save(out2, optimize=True)
    one_x.save(shell_copy, optimize=True)
    print(f"wrote {out1} ({one_x.size[0]}x{one_x.size[1]})")
    print(f"wrote {out2} ({two_x.size[0]}x{two_x.size[1]})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())