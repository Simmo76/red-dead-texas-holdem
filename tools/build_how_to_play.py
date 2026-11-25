#!/usr/bin/env python3
"""Export how-to-play overlay PNGs from owner full mock."""
from __future__ import annotations

import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
IMG = ROOT / "Web/CinematicPoker.Web/wwwroot/img"
SOURCE = IMG / "how-to-play-source.jpg"


def export(width: int) -> Image.Image:
    im = Image.open(SOURCE).convert("RGBA")
    arr = np.array(im)
    dark = (arr[:, :, 0] < 24) & (arr[:, :, 1] < 24) & (arr[:, :, 2] < 24)
    arr[dark, 3] = 0
    out = Image.fromarray(arr, "RGBA")
    height = round(out.height * (width / out.width))
    return out.resize((width, height), Image.Resampling.LANCZOS)


def main() -> int:
    if not SOURCE.is_file():
        print(f"missing {SOURCE}", file=sys.stderr)
        return 1
    one = export(580)
    two = export(1160)
    one.save(IMG / "how-to-play.png", optimize=True)
    two.save(IMG / "how-to-play@2x.png", optimize=True)
    print(f"how-to-play.png {one.size}")
    print(f"how-to-play@2x.png {two.size}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())