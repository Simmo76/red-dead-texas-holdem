#!/usr/bin/env python3
"""Extract a .unitypackage into a folder with original asset paths."""
import sys
import tarfile
from pathlib import Path


def extract(pkg: Path, out: Path) -> None:
    out.mkdir(parents=True, exist_ok=True)
    with tarfile.open(pkg, "r:gz") as tar:
        tar.extractall(out / "_raw")
    raw = out / "_raw"
    for guid_dir in raw.iterdir():
        if not guid_dir.is_dir():
            continue
        pathname_file = guid_dir / "pathname"
        asset_file = guid_dir / "asset"
        if not pathname_file.is_file() or not asset_file.is_file():
            continue
        rel = pathname_file.read_bytes().split(b"\x00")[0].decode("utf-8", errors="replace").strip().lstrip("/")
        if not rel:
            continue
        dest = out / rel
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_bytes(asset_file.read_bytes())


if __name__ == "__main__":
    extract(Path(sys.argv[1]), Path(sys.argv[2]))
