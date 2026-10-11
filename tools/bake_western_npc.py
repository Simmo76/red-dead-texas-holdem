"""Bake a David Grette western NPC (Sheriff / Bandit) to a web GLB.

Imports the owner-supplied Unity FBX, keeps one seated-table outfit, assigns
downscaled albedo (and hair/lash alpha) maps, copies Jacob's seated body
clips onto the shared UE4 bone names, and writes a web-optimised GLB.

Usage:
  CHAR=sheriff blender --background --python tools/bake_western_npc.py
  CHAR=bandit  blender --background --python tools/bake_western_npc.py

Source FBX trees are expected under /tmp/{sheriff,bandit}-pkg/tree/ (extracted
from the Dropbox .unitypackage). The original packages are not committed.
"""
from __future__ import annotations

import os
import sys
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[1]
JACOB = ROOT / "Web/CinematicPoker.Web/wwwroot/models/characters/western/Jacob.glb"
OUT_DIR = ROOT / "Web/CinematicPoker.Web/wwwroot/models/characters/western"
TEX_CACHE = Path("/tmp/western-npc-tex")
MAX_TEX = int(os.environ.get("MAX_TEX", "1024"))
HEAD_TEX = int(os.environ.get("HEAD_TEX", "1024"))

CHAR = os.environ.get("CHAR", "sheriff").strip().lower()

SPECS = {
    "sheriff": {
        "fbx": Path("/tmp/sheriff-pkg/tree/Assets/David Grette/Sheriff/FBX/SK_Sheriff.fbx"),
        "tex": Path("/tmp/sheriff-pkg/tree/Assets/David Grette/Sheriff/Texture"),
        "out": OUT_DIR / "Sheriff.glb",
        "keep": {
            "AmmoBelt", "Boots", "Hair_cap", "Hands_Optimized",
            "Hat", "Holster", "Jacket", "Pants", "Sheriff_Head", "Shirt",
            "Tie", "Vest",
        },
        "rename": {"Hat": "Hat__1", "Sheriff_Head": "Head__clean"},
        "albedo": {
            "AmmoBelt": "Cloth/1/T_sheriff_AmmoBelt_Albedo.png",
            "Boots": "Cloth/1/T_sheriff_Boots_Albedo.png",
            "Hat": "Cloth/1/T_sheriff_Hat_Albedo.png",
            "Holster": "Cloth/1/T_sheriff_Hosler_Albedo.png",
            "Jacket": "Cloth/1/T_sheriff_Jacket_Albedo.png",
            "Pants": "Cloth/1/T_sheriff_Pants_Albedo.png",
            "Shirt": "Cloth/1/T_sheriff_Shirt_Albedo.png",
            "Tie": "Cloth/1/T_sheriff_Tie_Albedo.png",
            "Vest": "Cloth/1/T_sheriff_Vest_Albedo.png",
            "Hands_Optimized": "Skin/Hands/T_Hands_D.png",
            "Sheriff_Head": "Skin/Head/T_Sheriff_head_Albedo.png",
        },
        "alpha": {
            "Hair_cap": ("Hair/T_Hair_Cap_Alpha.tga", True),
        },
        "hair_color": (0.12, 0.09, 0.07, 1.0),
    },
    "bandit": {
        "fbx": Path("/tmp/bandit-pkg/tree/Assets/David Grette/Sheriff/FBX/SK_Cowboy_2.fbx"),
        "tex": Path("/tmp/bandit-pkg/tree/Assets/David Grette/Sheriff/Texture"),
        "out": OUT_DIR / "Bandit.glb",
        "keep": {
            "Bandage", "Belt", "Boot", "Hair_cap", "Hair_cut", "Hands_Optimized",
            "Hat", "Holster", "Jacket", "Jeans", "Michael_head", "Scarf", "Shirt",
        },
        "rename": {
            "Hat": "Hat__1",
            "Michael_head": "Head__clean",
            "Boot": "Boots",
            "Jeans": "Pants",
        },
        "albedo": {
            "Bandage": "Cloth/1/T_Cowboy_2_Bandage_M_Albedo.png",
            "Belt": "Cloth/1/T_Cowboy_2_Belt_M_Albedo.png",
            "Boot": "Cloth/1/T_Cowboy_2_Boot_M_Albedo.png",
            "Hat": "Cloth/1/T_Cowboy_2_Hat_M_Albedo.png",
            "Holster": "Cloth/1/T_Cowboy_2_Holster_M_Albedo.png",
            "Jacket": "Cloth/1/T_Cowboy_2_Jacket_M_Albedo.png",
            "Jeans": "Cloth/1/T_Cowboy_2_Jeans_M_Albedo.png",
            "Scarf": "Cloth/1/T_Cowboy_2_Scarf_M_Albedo.png",
            "Shirt": "Cloth/1/T_Cowboy_2_Shirt_M_Albedo.png",
            "Hands_Optimized": "Skin/Hands/T_Hands_D.png",
            "Michael_head": "Skin/Head/T_Michael_head_Albedo.png",
            "Hair_cut": "Hair/T_Hair_D.tga",
        },
        "alpha": {
            "Hair_cap": ("Hair/T_Hair_Cap_Alpha.tga", True),
        },
        "hair_color": (0.18, 0.12, 0.08, 1.0),
    },
}


def die(msg: str) -> None:
    print("ERROR:", msg, file=sys.stderr)
    sys.exit(1)


def clear_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)


def resize_image(src: Path, dest: Path, max_edge: int, force_rgba: bool = False) -> Path:
    from PIL import Image

    dest.parent.mkdir(parents=True, exist_ok=True)
    img = Image.open(src)
    if force_rgba:
        img = img.convert("RGBA")
    else:
        img = img.convert("RGBA" if "A" in img.getbands() else "RGB")
    w, h = img.size
    scale = min(1.0, max_edge / float(max(w, h)))
    if scale < 1.0:
        img = img.resize((max(1, int(w * scale)), max(1, int(h * scale))), Image.Resampling.LANCZOS)
    # Prefer JPEG for opaque RGB; PNG for alpha.
    if img.mode == "RGBA" and any(a < 250 for a in img.getchannel("A").getdata()):
        dest = dest.with_suffix(".png")
        img.save(dest, optimize=True)
    else:
        dest = dest.with_suffix(".jpg")
        img.convert("RGB").save(dest, quality=85, optimize=True)
    return dest


def prepare_textures(spec: dict) -> dict[str, Path]:
    TEX_CACHE.mkdir(parents=True, exist_ok=True)
    prepared: dict[str, Path] = {}
    tex_root: Path = spec["tex"]
    for mesh, rel in spec["albedo"].items():
        src = tex_root / rel
        if not src.exists():
            die(f"missing albedo {src}")
        edge = HEAD_TEX if "head" in mesh.lower() or mesh in ("Sheriff_Head", "Michael_head") else MAX_TEX
        prepared[mesh] = resize_image(src, TEX_CACHE / f"{CHAR}_{mesh}_alb", edge)
    for mesh, (rel, _) in spec.get("alpha", {}).items():
        src = tex_root / rel
        if not src.exists():
            print("WARN: missing alpha", src)
            continue
        prepared[f"{mesh}__alpha"] = resize_image(src, TEX_CACHE / f"{CHAR}_{mesh}_a", MAX_TEX, force_rgba=True)
    return prepared


def load_image(path: Path) -> bpy.types.Image:
    img = bpy.data.images.load(str(path))
    img.pack()
    return img


def make_principled(name: str, color_img: bpy.types.Image | None, alpha_img: bpy.types.Image | None,
                    base_color=(1, 1, 1, 1), mask: bool = False) -> bpy.types.Material:
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = 0.75
    bsdf.inputs["Base Color"].default_value = base_color
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    if color_img:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = color_img
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if alpha_img is None and color_img.channels == 4:
            nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
            mat.blend_method = "CLIP"
            mat.alpha_threshold = 0.35
            mat.use_backface_culling = False
    if alpha_img:
        atex = nt.nodes.new("ShaderNodeTexImage")
        atex.image = alpha_img
        # Greyscale alpha maps: use Color.R as alpha.
        sep = nt.nodes.new("ShaderNodeSeparateColor")
        nt.links.new(atex.outputs["Color"], sep.inputs["Color"])
        nt.links.new(sep.outputs["Red"], bsdf.inputs["Alpha"])
        mat.blend_method = "CLIP"
        mat.alpha_threshold = 0.35
        mat.use_backface_culling = False
    if mask and not alpha_img and not color_img:
        mat.blend_method = "CLIP"
        mat.alpha_threshold = 0.35
    mat.use_backface_culling = False
    return mat


def import_fbx(path: Path) -> bpy.types.Object:
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(
        filepath=str(path),
        automatic_bone_orientation=False,
        ignore_leaf_bones=False,
        force_connect_children=False,
    )
    new = [o for o in bpy.data.objects if o not in before]
    arms = [o for o in new if o.type == "ARMATURE"]
    if not arms:
        die(f"no armature in {path}")
    return arms[0]


def apply_object_scales(arm: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    for o in bpy.data.objects:
        if o.parent == arm:
            o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)


def cull_and_rename(spec: dict) -> None:
    keep = set(spec["keep"])
    rename = dict(spec["rename"])
    for o in list(bpy.data.objects):
        if o.type != "MESH":
            continue
        if o.name not in keep:
            bpy.data.objects.remove(o, do_unlink=True)
            continue
        if o.name in rename:
            o.name = rename[o.name]


def assign_materials(spec: dict, prepared: dict[str, Path]) -> None:
    # Map by original mesh name before rename for texture lookup.
    inv_rename = {v: k for k, v in spec["rename"].items()}
    hair_color = spec.get("hair_color", (0.15, 0.1, 0.08, 1.0))
    for o in bpy.data.objects:
        if o.type != "MESH":
            continue
        orig = inv_rename.get(o.name, o.name)
        color_path = prepared.get(orig)
        alpha_path = prepared.get(f"{orig}__alpha")
        color_img = load_image(color_path) if color_path else None
        alpha_img = load_image(alpha_path) if alpha_path else None
        base = hair_color if "Hair" in orig else (1, 1, 1, 1)
        mat = make_principled(o.name, color_img, alpha_img, base_color=base)
        o.data.materials.clear()
        o.data.materials.append(mat)
        # Head: keep only the primary skin slot material on every slot so we
        # don't ship unused tongue/eye shells with missing maps as pink.
        if o.name.startswith("Head__"):
            # Drop tiny non-skin child materials by collapsing to one albedo.
            for i in range(len(o.material_slots)):
                o.material_slots[i].material = mat


def import_jacob_actions() -> dict[str, bpy.types.Action]:
    """Import Jacob into the current scene and return clean-named action copies.

    Must run while the NPC armature is already in the scene — a factory reset
    would drop the actions. Jacob mesh/armature objects are removed afterward.
    """
    before_objs = set(bpy.data.objects)
    before_acts = set(bpy.data.actions)
    bpy.ops.import_scene.gltf(filepath=str(JACOB))
    new_objs = [o for o in bpy.data.objects if o not in before_objs]
    wanted = {"Attack", "Bet", "Check", "Crouch", "Fold", "FoldShake", "Sit", "SitCards", "Talk"}
    found: dict[str, bpy.types.Action] = {}
    for act in bpy.data.actions:
        if act in before_acts:
            continue
        base = act.name.split("_")[0]
        if base in wanted and base not in found:
            found[base] = act
    print("captured jacob actions:", sorted(found))
    if "Sit" not in found:
        die("Jacob Sit action not captured")
    # Deep-copy under clean names before deleting Jacob (which may drop users).
    copies: dict[str, bpy.types.Action] = {}
    for name, act in found.items():
        dup = act.copy()
        dup.name = name
        dup.use_fake_user = True
        copies[name] = dup
    for o in new_objs:
        bpy.data.objects.remove(o, do_unlink=True)
    # Sweep orphaned Jacob meshes/armatures/images brought in by the import.
    for mesh in list(bpy.data.meshes):
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    for arm_data in list(bpy.data.armatures):
        if arm_data.users == 0:
            bpy.data.armatures.remove(arm_data)
    return copies


def attach_actions(arm: bpy.types.Object, actions: dict[str, bpy.types.Action]) -> None:
    if arm.animation_data is None:
        arm.animation_data_create()
    while arm.animation_data.nla_tracks:
        arm.animation_data.nla_tracks.remove(arm.animation_data.nla_tracks[0])
    frame = 1
    for name in sorted(actions):
        act = actions[name]
        track = arm.animation_data.nla_tracks.new()
        track.name = name
        strip = track.strips.new(name, int(frame), act)
        strip.action = act
        frame = strip.frame_end + 2
    if "Sit" in actions:
        arm.animation_data.action = actions["Sit"]


def export_glb(path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=str(path),
        export_format="GLB",
        use_selection=False,
        export_animations=True,
        export_force_sampling=True,
        export_nla_strips=True,
        export_def_bones=False,
        export_apply=False,
        export_texcoords=True,
        export_normals=True,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_skins=True,
        export_morph=False,
    )
    print("wrote", path, "bytes", path.stat().st_size)


def main() -> None:
    if CHAR not in SPECS:
        die(f"unknown CHAR={CHAR}; expected one of {sorted(SPECS)}")
    spec = SPECS[CHAR]
    if not spec["fbx"].exists():
        die(f"missing FBX {spec['fbx']}")
    if not JACOB.exists():
        die(f"missing Jacob {JACOB}")

    print(f"=== baking {CHAR} ===")
    prepared = prepare_textures(spec)
    print("textures prepared", len(prepared))

    clear_scene()
    arm = import_fbx(spec["fbx"])
    arm.name = "root"
    apply_object_scales(arm)
    cull_and_rename(spec)
    assign_materials(spec, prepared)
    actions = import_jacob_actions()
    attach_actions(arm, actions)

    # Quick height report after cull.
    mins = Vector((1e9, 1e9, 1e9))
    maxs = Vector((-1e9, -1e9, -1e9))
    for o in bpy.data.objects:
        if o.type != "MESH":
            continue
        for v in o.bound_box:
            w = o.matrix_world @ Vector(v)
            mins.x, mins.y, mins.z = min(mins.x, w.x), min(mins.y, w.y), min(mins.z, w.z)
            maxs.x, maxs.y, maxs.z = max(maxs.x, w.x), max(maxs.y, w.y), max(maxs.z, w.z)
    print("bounds", tuple(mins), tuple(maxs), "height", maxs.z - mins.z)
    print("meshes", sorted(o.name for o in bpy.data.objects if o.type == "MESH"))

    export_glb(spec["out"])


if __name__ == "__main__":
    main()
