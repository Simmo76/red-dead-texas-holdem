#!/usr/bin/env python3
"""Build settings-panel.png from the portrait rules frame (transparent outside).

Source: owner-supplied portrait frame (see settings-portrait-frame.png).
Clears the rules copy area, composites the SETTINGS title, exports 1x/2x.
"""
from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
IMG = ROOT / "Web/CinematicPoker.Web/wwwroot/img"
FRAME = IMG / "settings-portrait-frame.png"
TITLE_SRC = IMG / "settings-panel-full.png"

# Wipe rules copy/red banners; keep header chrome + footer CLOSE art.
BODY = (0.07, 0.12, 0.93, 0.87)
TITLE_BAND = (0.15, 0.055, 0.85, 0.115)


def build(width: int) -> Image.Image:
    if not FRAME.is_file():
        raise FileNotFoundError(FRAME)
    base = Image.open(FRAME).convert("RGBA")
    scale = width / base.width
    height = max(1, round(base.height * scale))
    im = base.resize((width, height), Image.Resampling.LANCZOS)
    blur = im.filter(ImageFilter.GaussianBlur(10))
    w, h = im.size

    def px(box: tuple[float, float, float, float]) -> tuple[int, int, int, int]:
        x0, y0, x1, y1 = box
        return int(x0 * w), int(y0 * h), int(x1 * w), int(y1 * h)

    bx0, by0, bx1, by1 = px(BODY)
    im.paste(blur.crop((bx0, by0, bx1, by1)), (bx0, by0))

    tx0, ty0, tx1, ty1 = px(TITLE_BAND)
    im.paste(blur.crop((tx0, ty0, tx1, ty1)), (tx0, ty0))

    if TITLE_SRC.is_file():
        old = Image.open(TITLE_SRC).convert("RGBA")
        ow, oh = old.size
        title_crop = old.crop((int(ow * 0.18), int(oh * 0.02), int(ow * 0.82), int(oh * 0.11)))
        target_w = int(w * 0.52)
        th = max(1, round(title_crop.height * (target_w / title_crop.width)))
        title = title_crop.resize((target_w, th), Image.Resampling.LANCZOS)
        x = (w - target_w) // 2
        y = int(h * 0.068) - th // 2
        im.alpha_composite(title, (x, y))

    return im


def main() -> int:
    if not FRAME.is_file():
        print(f"missing {FRAME}", file=sys.stderr)
        return 1
    one_x = build(560)
    two_x = build(1120)
    one_x.save(IMG / "settings-panel.png", optimize=True)
    one_x.save(IMG / "settings-panel-shell-full.png", optimize=True)
    two_x.save(IMG / "settings-panel@2x.png", optimize=True)
    print(f"wrote settings-panel.png {one_x.size}")
    print(f"wrote settings-panel@2x.png {two_x.size}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())