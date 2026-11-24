#!/usr/bin/env python3
"""Build transparent intro hero plate (title + tap) from owner art."""
from __future__ import annotations

import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
IMG = ROOT / "Web/CinematicPoker.Web/wwwroot/img/intro"
SOURCE = IMG / "hero-plate-source.png"
# Pixels darker than this become transparent (keeps glow on title).
BLACK_KEY = 28


def key_and_export(width: int) -> Image.Image:
    base = Image.open(SOURCE).convert("RGBA")
    scale = width / base.width
    height = max(1, round(base.height * scale))
    im = base.resize((width, height), Image.Resampling.LANCZOS)
    arr = np.array(im)
    rgb = arr[:, :, :3].astype(np.int16)
    mx = rgb.max(axis=2)
    # Soft edge on near-black fringe
    alpha = np.clip((mx - (BLACK_KEY - 12)) * 18, 0, 255).astype(np.uint8)
    arr[:, :, 3] = np.minimum(arr[:, :, 3], alpha)
    out = Image.fromarray(arr, "RGBA")
    # trim transparent margins
    bbox = out.getbbox()
    if bbox:
        out = out.crop(bbox)
    return out


def main() -> int:
    if not SOURCE.is_file():
        print(f"missing {SOURCE}", file=sys.stderr)
        return 1
    one_x = key_and_export(1086)
    two_x = key_and_export(2172)
    IMG.mkdir(parents=True, exist_ok=True)
    one_x.save(IMG / "hero-plate.png", optimize=True)
    two_x.save(IMG / "hero-plate@2x.png", optimize=True)
    print(f"wrote hero-plate.png {one_x.size}")
    print(f"wrote hero-plate@2x.png {two_x.size}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())