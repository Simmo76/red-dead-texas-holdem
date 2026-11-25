#!/usr/bin/env python3
"""Slightly enlarge rank/suit corners and center pips on Western deck faces (300×420)."""
from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
CARDS = ROOT / "Web/CinematicPoker.Web/wwwroot/img/cards"
RANKS = ("2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A")
SUITS = ("C", "D", "H", "S")

CORNER_SCALE = 1.22
CENTER_SCALE = 1.10


def paste_zoom(
    base: Image.Image,
    box: tuple[int, int, int, int],
    scale: float,
    anchor: str,
) -> None:
    x0, y0, x1, y1 = box
    patch = base.crop((x0, y0, x1, y1))
    nw = max(1, round(patch.width * scale))
    nh = max(1, round(patch.height * scale))
    scaled = patch.resize((nw, nh), Image.Resampling.LANCZOS)
    if anchor == "tl":
        ox, oy = x0, y0
    elif anchor == "tr":
        ox, oy = x1 - nw, y0
    elif anchor == "bl":
        ox, oy = x0, y1 - nh
    elif anchor == "br":
        ox, oy = x1 - nw, y1 - nh
    else:
        ox = x0 + (x1 - x0 - nw) // 2
        oy = y0 + (y1 - y0 - nh) // 2
    base.alpha_composite(scaled, (ox, oy))


def process_card(path: Path) -> None:
    im = Image.open(path).convert("RGBA")
    w, h = im.size
    out = im.copy()
    paste_zoom(
        out,
        (int(w * 0.18), int(h * 0.20), int(w * 0.82), int(h * 0.80)),
        CENTER_SCALE,
        "center",
    )
    corners = (
        (0, 0, int(w * 0.36), int(h * 0.28), "tl"),
        (int(w * 0.64), 0, w, int(h * 0.28), "tr"),
        (0, int(h * 0.72), int(w * 0.36), h, "bl"),
        (int(w * 0.64), int(h * 0.72), w, h, "br"),
    )
    for x0, y0, x1, y1, anchor in corners:
        paste_zoom(out, (x0, y0, x1, y1), CORNER_SCALE, anchor)
    out = out.filter(ImageFilter.UnsharpMask(radius=1.0, percent=35, threshold=2))
    out.save(path, optimize=True)


def main() -> int:
    count = 0
    for rank in RANKS:
        for suit in SUITS:
            path = CARDS / f"{rank}{suit}.png"
            if not path.is_file():
                print(f"missing {path}", file=sys.stderr)
                return 1
            process_card(path)
            count += 1
    print(f"enlarged {count} card faces")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())