#!/usr/bin/env python3
"""Rebuild the Dead Man's Hand saloon Unity scene as one web GLB.

The original .unitypackage is not committed. Point EXTRACTED at an unpacked
copy of the pack (Assets/ tree), then instance every PrefabInstance from
`Assets/dead_mans_hand_saloon.unity` with Unity's TRS, clear a pocket at the
players' table mark, and write a textured, table-centred backdrop.glb.
"""
from __future__ import annotations

import math
import re
import subprocess
from pathlib import Path

import numpy as np
import trimesh
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
OUT_RAW = Path("/tmp/saloon-backdrop-raw.glb")
OUT_GLB = ROOT / "Web/CinematicPoker.Web/wwwroot/models/env/backdrop.glb"
GLTFPACK = Path("/tmp/gltfpack")

EXTRACTED = Path("/tmp/unity-pkg/Assets_tree")
GLTF_DIR = Path("/tmp/unity-pkg/glb")
SCENE = EXTRACTED / "Assets/dead_mans_hand_saloon.unity"

# Red-circle mark in the top-down screenshot: open floor south of the three
# central round tables and north of the south wall (Unity scene space).
TABLE_POCKET = np.array([1.5, 0.0, -1.2], dtype=float)
TABLE_CLEAR_RADIUS = 2.55  # keep poker table + seated players clear of props

# Unity 4 FBX importer's Z-up → Y-up rotation (−90° about X).
IMPORT_Q = np.array([-0.70710678118, 0.0, 0.0, 0.70710678118])  # xyzw

# Skip tiny glassware clutter that blows polycount without reading at table
# distance. Shelved bottle sets and bar gear stay.
SKIP_PREFABS = {
    "PREF_glass_01",
    "PREF_glass_02",
    "PREF_glass_03",
    "PREF_glass_04",
    "PREF_glass_05",
    "PREF_glass_06",
}


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
    m[:3, :3] = rot * np.asarray(scale, dtype=float)
    m[:3, 3] = pos
    return m


def load_guid_map() -> dict[str, Path]:
    out: dict[str, Path] = {}
    for meta in EXTRACTED.rglob("*.meta"):
        text = meta.read_text(errors="replace")
        m = re.search(r"^guid: ([a-f0-9]+)", text, re.M)
        if m:
            out[m.group(1)] = Path(str(meta)[:-5])
    return out


def parse_yaml_objects(text: str):
    """Split a Unity YAML asset into {fileID: (class_id, body)}."""
    objs = {}
    parts = re.split(r"\n--- !u!", text)
    for part in parts[1:]:
        m = re.match(r"(\d+) &(\d+)\n(.*)", part, re.S)
        if not m:
            continue
        class_id, fid, body = m.group(1), int(m.group(2)), m.group(3)
        objs[fid] = (class_id, body)
    return objs


def _vec3(body: str, key: str, default=(0.0, 0.0, 0.0)):
    m = re.search(
        rf"{key}: \{{x: ([^,]+), y: ([^,]+), z: ([^}}]+)\}}", body)
    if not m:
        return np.array(default, dtype=float)
    return np.array([float(m.group(1)), float(m.group(2)), float(m.group(3))], dtype=float)


def _quat(body: str, key: str = "m_LocalRotation"):
    m = re.search(
        rf"{key}: \{{x: ([^,]+), y: ([^,]+), z: ([^,]+), w: ([^}}]+)\}}", body)
    if not m:
        return np.array([0.0, 0.0, 0.0, 1.0], dtype=float)
    return qnormalize(np.array(
        [float(m.group(1)), float(m.group(2)), float(m.group(3)), float(m.group(4))],
        dtype=float))


def _file_ref(body: str, key: str):
    m = re.search(rf"{key}: \{{fileID: (\d+)(?:, guid: ([a-f0-9]+), type: \d+)?\}}", body)
    if not m:
        return None, None
    return int(m.group(1)), m.group(2)


def parse_prefab_meshes(prefab_path: Path, guid_map: dict[str, Path]):
    """Return list of {fbx, local_matrix, mat_guid} relative to prefab root."""
    text = prefab_path.read_text(errors="replace")
    objs = parse_yaml_objects(text)

    go_name = {}
    go_components = {}
    for fid, (cid, body) in objs.items():
        if cid != "1":
            continue
        nm = re.search(r"m_Name: (.+)", body)
        go_name[fid] = nm.group(1).strip() if nm else ""
        comps = [int(x) for x in re.findall(r"- component: \{fileID: (\d+)\}", body)]
        go_components[fid] = comps

    transforms = {}
    mesh_filters = {}  # go_fid -> (mesh_file_id, mesh_guid)
    renderers = {}  # go_fid -> [mat_guid, ...]

    for fid, (cid, body) in objs.items():
        if cid == "4":
            go, _ = _file_ref(body, "m_GameObject")
            father, _ = _file_ref(body, "m_Father")
            transforms[fid] = {
                "go": go,
                "father": father or 0,
                "pos": _vec3(body, "m_LocalPosition"),
                "rot": _quat(body),
                "scale": _vec3(body, "m_LocalScale", (1.0, 1.0, 1.0)),
            }
        elif cid == "33":
            go, _ = _file_ref(body, "m_GameObject")
            mesh_fid, mesh_guid = _file_ref(body, "m_Mesh")
            if go and mesh_guid:
                mesh_filters[go] = (mesh_fid, mesh_guid)
        elif cid == "23":
            go, _ = _file_ref(body, "m_GameObject")
            mats = re.findall(r"guid: ([a-f0-9]+), type: 2", body)
            if go:
                renderers[go] = mats

    # Transform fileID keyed by GameObject
    go_xform = {t["go"]: fid for fid, t in transforms.items() if t["go"]}

    def world_matrix(tf_fid: int) -> np.ndarray:
        chain = []
        seen = set()
        cur = tf_fid
        while cur and cur not in seen:
            seen.add(cur)
            t = transforms[cur]
            chain.append(t)
            cur = t["father"]
        m = np.eye(4)
        for t in reversed(chain):
            m = m @ trs_matrix(t["pos"], t["rot"], t["scale"])
        return m

    # Prefab root = transform with no father
    roots = [fid for fid, t in transforms.items() if not t["father"]]
    if not roots:
        return []
    root_mat = world_matrix(roots[0])
    root_inv = np.linalg.inv(root_mat)

    out = []
    for go, (mesh_fid, mesh_guid) in mesh_filters.items():
        fbx = guid_map.get(mesh_guid)
        if not fbx or not fbx.exists():
            continue
        tf_fid = go_xform.get(go)
        if not tf_fid:
            continue
        local = root_inv @ world_matrix(tf_fid)
        mats = renderers.get(go, [])
        out.append({
            "fbx": fbx,
            "mesh_file_id": mesh_fid,
            "local": local,
            "mat_guids": mats,
            "name": go_name.get(go, fbx.stem),
        })
    return out


def material_albedo(mat_path: Path, guid_map: dict[str, Path]):
    if not mat_path or not mat_path.exists():
        return None, (1.0, 1.0, 1.0), False
    text = mat_path.read_text(errors="replace")
    m = re.search(
        r"_MainTex:\n\s+m_Texture: \{fileID: \d+, guid: ([a-f0-9]+)", text)
    c = re.search(
        r"_Color: \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)\}", text)
    color = (1.0, 1.0, 1.0)
    alpha = 1.0
    if c:
        color = (float(c.group(1)), float(c.group(2)), float(c.group(3)))
        alpha = float(c.group(4))
    tex = guid_map.get(m.group(1)) if m else None
    transparent = "glass" in mat_path.name.lower() or alpha < 0.99
    return tex, color, transparent


_tex_cache: dict[str, Image.Image] = {}


def load_texture(path: Path) -> Image.Image:
    key = str(path)
    if key not in _tex_cache:
        img = Image.open(path)
        # TGA often has unused alpha; keep RGB for opaque wood/walls.
        if img.mode not in ("RGB", "RGBA"):
            img = img.convert("RGBA")
        img.thumbnail((512, 512), Image.Resampling.LANCZOS)
        _tex_cache[key] = img.convert("RGB")
    return _tex_cache[key]


def textured_copy(mesh: trimesh.Trimesh, image: Image.Image | None, color, transparent: bool):
    out = mesh.copy()
    if image is not None:
        uv = None
        if hasattr(out.visual, "uv") and out.visual.uv is not None:
            uv = np.array(out.visual.uv)
        out.visual = trimesh.visual.TextureVisuals(uv=uv, image=image)
    else:
        rgba = [int(255 * c) for c in color] + ([140] if transparent else [255])
        out.visual = trimesh.visual.ColorVisuals(
            mesh=out, vertex_colors=[rgba] * len(out.vertices))
    return out


def unity_prefab_instances(guid_map: dict[str, Path]):
    text = SCENE.read_text(errors="replace")
    blocks = re.split(r"\n--- !u!1001 &\d+\n", text)[1:]
    rows = []
    for b in blocks:
        mguid = re.search(
            r"m_SourcePrefab: \{fileID: 100100000, guid: ([a-f0-9]+), type: 3\}", b)
        if not mguid:
            continue
        guid = mguid.group(1)
        path = guid_map.get(guid)
        if not path or not path.exists():
            continue
        stem = path.stem
        if stem in SKIP_PREFABS:
            continue
        mods = {}
        for prop in (
            "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z",
            "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z",
            "m_LocalRotation.w",
            "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z",
            "m_Name",
        ):
            m = re.search(rf"propertyPath: {re.escape(prop)}\n\s+value: (.+)", b)
            if m:
                mods[prop] = m.group(1).strip()
        try:
            pos = np.array([
                float(mods.get("m_LocalPosition.x", "0")),
                float(mods.get("m_LocalPosition.y", "0")),
                float(mods.get("m_LocalPosition.z", "0")),
            ], dtype=float)
            rot = qnormalize(np.array([
                float(mods.get("m_LocalRotation.x", "0")),
                float(mods.get("m_LocalRotation.y", "0")),
                float(mods.get("m_LocalRotation.z", "0")),
                float(mods.get("m_LocalRotation.w", "1")),
            ], dtype=float))
            scale = np.array([
                float(mods.get("m_LocalScale.x", "1")),
                float(mods.get("m_LocalScale.y", "1")),
                float(mods.get("m_LocalScale.z", "1")),
            ], dtype=float)
        except ValueError:
            continue
        rows.append({
            "prefab": path,
            "stem": stem,
            "name": mods.get("m_Name", stem),
            "pos": pos,
            "rot": rot,
            "scale": scale,
        })
    return rows


def instance_matrix(inst):
    """Unity TRS on Assimp's already Y-up meshes (strip FBX importer pitch)."""
    q_unity = inst["rot"]
    remaining = qmul(q_unity, qconj(IMPORT_Q))
    if abs(remaining[0]) < 0.3 and abs(remaining[2]) < 0.3:
        q = remaining
    else:
        q = qnormalize(np.array([0.0, q_unity[1], 0.0, q_unity[3]]))
    return trs_matrix(inst["pos"], q, inst["scale"])


def ensure_gltf(fbx: Path) -> Path:
    GLTF_DIR.mkdir(parents=True, exist_ok=True)
    out = GLTF_DIR / (fbx.stem + ".gltf")
    if out.exists():
        return out
    subprocess.check_call(
        ["assimp", "export", str(fbx), str(out)],
        stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    return out


_proto_cache: dict[str, list[trimesh.Trimesh]] = {}


def load_fbx_meshes(fbx: Path) -> list[trimesh.Trimesh]:
    key = str(fbx)
    if key in _proto_cache:
        return _proto_cache[key]
    gltf = ensure_gltf(fbx)
    scene = trimesh.load(gltf, force="scene")
    geoms = []
    for node_name in scene.graph.nodes_geometry:
        transform, geom_name = scene.graph.get(node_name)
        geom = scene.geometry[geom_name]
        if not isinstance(geom, trimesh.Trimesh) or len(geom.faces) == 0:
            continue
        baked = geom.copy()
        baked.apply_transform(transform)
        # Keep submesh identity for multi-material FBX (chair fabric/wood).
        baked.metadata["submesh"] = geom_name
        geoms.append(baked)
    if not geoms:
        # Some exports are a single Trimesh, not a Scene.
        mesh = trimesh.load(gltf, force="mesh")
        if isinstance(mesh, trimesh.Trimesh) and len(mesh.faces):
            geoms = [mesh]
    _proto_cache[key] = geoms
    return geoms


def main():
    if not SCENE.exists():
        raise SystemExit(f"scene missing at {SCENE}")
    guid_map = load_guid_map()
    instances = unity_prefab_instances(guid_map)
    print(f"prefab instances: {len(instances)}")

    prefab_meshes: dict[str, list] = {}
    placed = []
    skipped_clear = 0

    for inst in instances:
        key = str(inst["prefab"])
        if key not in prefab_meshes:
            prefab_meshes[key] = parse_prefab_meshes(inst["prefab"], guid_map)
        parts = prefab_meshes[key]
        if not parts:
            continue

        # Drop furniture whose root sits inside the players' table pocket.
        dx = inst["pos"][0] - TABLE_POCKET[0]
        dz = inst["pos"][2] - TABLE_POCKET[2]
        if math.hypot(dx, dz) < TABLE_CLEAR_RADIUS:
            # Always keep structural shells (floor/wall/ceiling/columns).
            structural = any(
                s in inst["stem"].lower()
                for s in ("floor", "wall", "ceiling", "column", "door")
            )
            if not structural:
                skipped_clear += 1
                continue

        world = instance_matrix(inst)
        for part in parts:
            meshes = load_fbx_meshes(part["fbx"])
            if not meshes:
                continue
            mat_guids = part["mat_guids"]
            for mi, geom in enumerate(meshes):
                mat_guid = mat_guids[mi] if mi < len(mat_guids) else (
                    mat_guids[0] if mat_guids else None)
                mat_path = guid_map.get(mat_guid) if mat_guid else None
                tex_path, color, transparent = material_albedo(mat_path, guid_map)
                image = load_texture(tex_path) if tex_path and tex_path.exists() else None
                mesh = textured_copy(geom, image, color, transparent)
                mesh.apply_transform(world @ part["local"])
                placed.append(mesh)

    if not placed:
        raise SystemExit("no meshes placed")

    # Shift the marked table pocket to the world origin and sit floor on y=0.
    shift = np.eye(4)
    shift[:3, 3] = -TABLE_POCKET
    # Floor is already at y=0 in the scene; keep it.

    scene = trimesh.Scene()
    for i, mesh in enumerate(placed):
        mesh.apply_transform(shift)
        scene.add_geometry(mesh, node_name=f"p_{i}")

    OUT_RAW.parent.mkdir(parents=True, exist_ok=True)
    scene.export(OUT_RAW)
    print(f"raw {OUT_RAW} ({OUT_RAW.stat().st_size} bytes, "
          f"{len(placed)} meshes, cleared {skipped_clear}, pocket {TABLE_POCKET})")

    OUT_GLB.parent.mkdir(parents=True, exist_ok=True)
    if GLTFPACK.exists():
        # Meshopt only — the web loader has MeshoptDecoder but not a KTX2
        # transcoder, so keep textures as embedded PNG (already ≤512px).
        subprocess.check_call([
            str(GLTFPACK), "-i", str(OUT_RAW), "-o", str(OUT_GLB), "-cc",
        ])
        print(f"wrote {OUT_GLB} ({OUT_GLB.stat().st_size} bytes, meshopt)")
    else:
        OUT_GLB.write_bytes(OUT_RAW.read_bytes())
        print(f"wrote {OUT_GLB} ({OUT_GLB.stat().st_size} bytes, uncompressed)")


if __name__ == "__main__":
    main()
