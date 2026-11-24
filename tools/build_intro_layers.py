#!/usr/bin/env python3
"""Split intro one-sheets into bg + title plate + tap-to-play PNG layers."""
from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "Web/CinematicPoker.Web/wwwroot/img"
OUT = SRC / "intro"

SCENES = {
    "saloon": {
        "src": SRC / "intro-splash.jpg",
        "title": (0.14, 0.10, 0.86, 0.51),
        "tap": (0.29, 0.56, 0.71, 0.73),
    },
    "street": {
        "src": SRC / "intro-splash-street.jpg",
        "title": (0.14, 0.08, 0.86, 0.53),
        "tap": (0.29, 0.61, 0.71, 0.77),
    },
}


def to_px(box: tuple[float, float, float, float], w: int, h: int) -> tuple[int, int, int, int]:
    x0, y0, x1, y1 = box
    return (int(x0 * w), int(y0 * h), int(x1 * w), int(y1 * h))


def punch(im: Image.Image, rect: tuple[int, int, int, int]) -> None:
    blurred = im.filter(ImageFilter.GaussianBlur(10))
    x0, y0, x1, y1 = rect
    patch = blurred.crop((x0, y0, x1, y1))
    im.paste(patch, (x0, y0))


def build_scene(name: str, spec: dict) -> None:
    src: Path = spec["src"]
    if not src.is_file():
        raise FileNotFoundError(src)
    base = Image.open(src).convert("RGB")
    w, h = base.size
    title_px = to_px(spec["title"], w, h)
    tap_px = to_px(spec["tap"], w, h)
    title = base.crop(title_px)
    tap = base.crop(tap_px)
    bg = base.copy()
    punch(bg, title_px)
    punch(bg, tap_px)
    OUT.mkdir(parents=True, exist_ok=True)
    bg.save(OUT / f"{name}-bg.jpg", quality=88, optimize=True)
    title.save(OUT / f"{name}-title.png", optimize=True)
    tap.save(OUT / f"{name}-tap.png", optimize=True)
    tw = title.width * 2
    th = title.height * 2
    tap.scale = None
    title.resize((tw, th), Image.Resampling.LANCZOS).save(OUT / f"{name}-title@2x.png", optimize=True)
    tap.resize((tap.width * 2, tap.height * 2), Image.Resampling.LANCZOS).save(
        OUT / f"{name}-tap@2x.png", optimize=True
    )
    print(f"{name}: title {title.size} tap {tap.size} bg {bg.size}")


def main() -> int:
    for name, spec in SCENES.items():
        build_scene(name, spec)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())