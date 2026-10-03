#!/usr/bin/env python3
"""Rebuild the Hand-Painted Forest Lite Unity demo scene as one web GLB.

The original .unitypackage is not committed. Point EXTRACTED at an unpacked
copy of the pack, then instances every object from `Assets scene.unity`
with Unity's TRS and writes a textured, table-centred forest.glb.
"""
from __future__ import annotations

import math
import subprocess
from pathlib import Path

import numpy as np
import trimesh
import UnityPy
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
OUT_GLB = ROOT / "Web/CinematicPoker.Web/wwwroot/models/env/forest.glb"

EXTRACTED = Path("/tmp/forest-pack/project")
GLTF_DIR = Path("/tmp/forest-pack/glb")

# Unity 4 FBX importer's Z-up → Y-up rotation (‑90° about X).
IMPORT_Q = np.array([-0.70710678118, 0.0, 0.0, 0.70710678118])  # xyzw

MESH_FILE = {
    "forest grass": "forest grass.gltf",
    "grass terrain": "grass terrain.gltf",
    "forest ground": "forest ground.gltf",
    "willow tree": "willow tree.gltf",
    "rock tiny": "rock tiny.gltf",
    "rock flat": "rock flat.gltf",
    "statue": "statue.gltf",
    "statue head": "statue head.gltf",
    "statue broken": "statue broken.gltf",
}

# Prototype → albedo. Leaves/grass keep alpha; the rest are opaque earth tones.
TEXTURE = {
    "forest grass": ("Assets/forest/foliage/vegetation lite.tga", True),
    "grass terrain": ("Assets/forest/foliage/grass.tga", True),
    "forest ground": ("Assets/forest/terrain/forest grass.tga", False),
    "willow tree": ("Assets/forest/foliage/willow bark.tga", False),
    "willow tree_1": ("Assets/forest/foliage/willow leaves.tga", True),
    "rock tiny": ("Assets/forest/rock_clifs/rocks.tga", False),
    "rock flat": ("Assets/forest/rock_clifs/rocks.tga", False),
    "statue": ("Assets/forest/props/statue.tga", False),
    "statue head": ("Assets/forest/props/statue.tga", False),
    "statue broken": ("Assets/forest/props/statue.tga", False),
}

TABLE_CLEAR_RADIUS = 2.35  # metres: keep the poker table pocket empty of grass


def qmul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return np.array([
        aw * bx + ax * bw + ay * bz - az * by,
        aw * by - ax * bz + ay * bw + az * bx,
        aw * bz + ax * by - ay * bx + az * bw,
        aw * bw - ax * bx - ay * by - az * bz,
    ])


def qconj(q):
    return np.array([-q[0], -q[1], -q[2], q[3]])


def qdot(a, b):
    return float(abs(np.dot(a, b)))


def qnormalize(q):
    n = np.linalg.norm(q)
    return q / n if n > 1e-8 else np.array([0.0, 0.0, 0.0, 1.0])


def trs_matrix(pos, quat_xyzw, scale):
    x, y, z, w = qnormalize(quat_xyzw)
    xx, yy, zz = x * x, y * y, z * z
    xy, xz, yz = x * y, x * z, y * z
    wx, wy, wz = w * x, w * y, w * z
    rot = np.array([
        [1 - 2 * (yy + zz), 2 * (xy - wz), 2 * (xz + wy)],
        [2 * (xy + wz), 1 - 2 * (xx + zz), 2 * (yz - wx)],
        [2 * (xz - wy), 2 * (yz + wx), 1 - 2 * (xx + yy)],
    ])
    m = np.eye(4)
    m[:3, :3] = rot * np.array(scale)
    m[:3, 3] = pos
    return m


def load_texture(rel: str, alpha: bool) -> Image.Image:
    img = Image.open(EXTRACTED / rel)
    img = img.convert("RGBA")
    img.thumbnail((512, 512), Image.Resampling.LANCZOS)
    if not alpha:
        img = img.convert("RGB")
    return img


def textured_copy(mesh: trimesh.Trimesh, image: Image.Image) -> trimesh.Trimesh:
    out = mesh.copy()
    uv = None
    if hasattr(out.visual, "uv") and out.visual.uv is not None:
        uv = np.array(out.visual.uv)
    out.visual = trimesh.visual.TextureVisuals(uv=uv, image=image)
    return out


def unity_instances():
    env = UnityPy.load(str(EXTRACTED / "Assets/forest/scenes/Assets scene.unity"))
    names = {}
    for obj in env.objects:
        if obj.type.name == "GameObject":
            d = obj.read()
            names[obj.path_id] = d.m_Name
    rows = []
    for obj in env.objects:
        if obj.type.name != "Transform":
            continue
        d = obj.read()
        name = names.get(d.m_GameObject.m_PathID, "")
        if name not in MESH_FILE:
            continue
        p, r, s = d.m_LocalPosition, d.m_LocalRotation, d.m_LocalScale
        rows.append({
            "name": name,
            "pos": np.array([p.x, p.y, p.z], dtype=float),
            "rot": qnormalize(np.array([r.x, r.y, r.z, r.w], dtype=float)),
            "scale": np.array([s.x, s.y, s.z], dtype=float),
        })
    return rows


def instance_matrix(inst):
    """Unity TRS, with the FBX importer's ‑90° X removed when it is already
    baked into the glTF via Assimp's PreRotation node."""
    q = inst["rot"]
    if qdot(q, IMPORT_Q) > 0.9:
        q = qmul(q, qconj(IMPORT_Q))
    return trs_matrix(inst["pos"], q, inst["scale"])


def ensure_gltf_exports():
    GLTF_DIR.mkdir(parents=True, exist_ok=True)
    src = {
        "willow tree.gltf": EXTRACTED / "Assets/forest/foliage/willow tree.FBX",
        "forest grass.gltf": EXTRACTED / "Assets/forest/foliage/forest grass.FBX",
        "forest ground.gltf": EXTRACTED / "Assets/forest/terrain/forest ground.FBX",
        "rock flat.gltf": EXTRACTED / "Assets/forest/rock_clifs/rock flat.FBX",
        "statue.gltf": EXTRACTED / "Assets/forest/props/statue.FBX",
        "statue head.gltf": EXTRACTED / "Assets/forest/props/statue head.FBX",
        "statue broken.gltf": EXTRACTED / "Assets/forest/props/statue broken.FBX",
        "grass terrain.gltf": EXTRACTED / "Assets/forest/foliage/grass terrain.3DS",
        "rock tiny.gltf": EXTRACTED / "Assets/forest/rock_clifs/rock tiny.3DS",
    }
    for name, fbx in src.items():
        out = GLTF_DIR / name
        if out.exists():
            continue
        subprocess.check_call(["assimp", "export", str(fbx), str(out)], stdout=subprocess.DEVNULL)


def load_prototypes():
    protos = {}
    for name, gltf_name in MESH_FILE.items():
        scene = trimesh.load(GLTF_DIR / gltf_name, force="scene")
        geoms = []
        for node_name in scene.graph.nodes_geometry:
            transform, geom_name = scene.graph.get(node_name)
            geom = scene.geometry[geom_name]
            if not isinstance(geom, trimesh.Trimesh) or len(geom.faces) == 0:
                continue
            baked = geom.copy()
            baked.apply_transform(transform)
            geoms.append((geom_name, baked))
        if not geoms:
            raise SystemExit(f"no mesh in {gltf_name}")
        protos[name] = geoms
    return protos


def main():
    if not EXTRACTED.exists():
        raise SystemExit(f"extracted pack missing at {EXTRACTED}")
    ensure_gltf_exports()
    protos = load_prototypes()
    tex_cache = {}
    placed = []
    for inst in unity_instances():
        mat = instance_matrix(inst)
        for geom_name, geom in protos[inst["name"]]:
            key = geom_name if geom_name in TEXTURE else inst["name"]
            rel, alpha = TEXTURE[key]
            if rel not in tex_cache:
                tex_cache[rel] = load_texture(rel, alpha)
            mesh = textured_copy(geom, tex_cache[rel])
            mesh.apply_transform(mat)
            placed.append((inst["name"], key, mesh, alpha))

    # Centre the ground under the table and sit its highest point on y=0.
    grounds = [m for n, _, m, _ in placed if n == "forest ground"]
    if not grounds:
        raise SystemExit("forest ground missing from scene")
    gb = grounds[0].bounds
    offset = np.array([
        -(gb[0][0] + gb[1][0]) * 0.5,
        -gb[1][1],
        -(gb[0][2] + gb[1][2]) * 0.5,
    ])
    shift = np.eye(4)
    shift[:3, 3] = offset

    scene = trimesh.Scene()
    kept = 0
    for name, key, mesh, alpha in placed:
        mesh.apply_transform(shift)
        if name in ("forest grass", "grass terrain"):
            c = mesh.centroid
            if math.hypot(c[0], c[2]) < TABLE_CLEAR_RADIUS:
                continue
        scene.add_geometry(mesh, node_name=f"{key}_{kept}")
        kept += 1

    OUT_GLB.parent.mkdir(parents=True, exist_ok=True)
    scene.export(OUT_GLB)
    print(f"wrote {OUT_GLB} ({OUT_GLB.stat().st_size} bytes, {kept} instances, offset {offset})")


if __name__ == "__main__":
    main()
