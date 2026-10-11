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


def resize_image(src: Path, dest: Path, max_edge: int, keep_alpha: bool = False) -> Path:
    from PIL import Image

    dest.parent.mkdir(parents=True, exist_ok=True)
    img = Image.open(src)
    w, h = img.size
    scale = min(1.0, max_edge / float(max(w, h)))
    if scale < 1.0:
        img = img.resize((max(1, int(w * scale)), max(1, int(h * scale))), Image.Resampling.LANCZOS)
    # Cloth albedos must stay opaque JPEG — Unity source PNGs often carry a
    # noisy alpha that turns into glTF MASK and punches the mesh into a ghost.
    if keep_alpha:
        img = img.convert("RGBA")
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
        # Hair diffuse TGAs may carry real alpha; cloth/skin stay opaque JPEG.
        keep_alpha = "Hair" in mesh and mesh != "Hair_cap"
        prepared[mesh] = resize_image(src, TEX_CACHE / f"{CHAR}_{mesh}_alb", edge, keep_alpha=keep_alpha)
    for mesh, (rel, _) in spec.get("alpha", {}).items():
        src = tex_root / rel
        if not src.exists():
            print("WARN: missing alpha", src)
            continue
        prepared[f"{mesh}__alpha"] = resize_image(
            src, TEX_CACHE / f"{CHAR}_{mesh}_a", MAX_TEX, keep_alpha=True)
    return prepared


def load_image(path: Path) -> bpy.types.Image:
    img = bpy.data.images.load(str(path))
    img.pack()
    return img


def make_principled(name: str, color_img: bpy.types.Image | None, alpha_img: bpy.types.Image | None,
                    base_color=(1, 1, 1, 1), use_mask: bool = False) -> bpy.types.Material:
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
        if use_mask and alpha_img is None and color_img.channels == 4:
            nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    if use_mask and alpha_img:
        atex = nt.nodes.new("ShaderNodeTexImage")
        atex.image = alpha_img
        # Greyscale alpha maps: use Color.R as alpha.
        sep = nt.nodes.new("ShaderNodeSeparateColor")
        nt.links.new(atex.outputs["Color"], sep.inputs["Color"])
        nt.links.new(sep.outputs["Red"], bsdf.inputs["Alpha"])
    if use_mask:
        mat.blend_method = "CLIP"
        mat.alpha_threshold = 0.35
        mat.use_backface_culling = False
    else:
        mat.blend_method = "OPAQUE"
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
        # Only hair cards use alpha MASK (matches Jacob.glb). Cloth/skin opaque.
        use_mask = "Hair" in orig
        mat = make_principled(o.name, color_img, alpha_img, base_color=base, use_mask=use_mask)
        o.data.materials.clear()
        o.data.materials.append(mat)
        # Head: keep only the primary skin slot material on every slot so we
        # don't ship unused tongue/eye shells with missing maps as pink.
        if o.name.startswith("Head__"):
            for i in range(len(o.material_slots)):
                o.material_slots[i].material = mat


def _quat_axis_angle(axis, degrees: float):
    from mathutils import Quaternion
    return Quaternion(axis, degrees * 3.14159265 / 180.0)


def strip_shape_keys() -> None:
    for obj in list(bpy.data.objects):
        if obj.type == "MESH" and obj.data.shape_keys is not None:
            obj.shape_key_clear()


def author_sit_pose(arm: bpy.types.Object) -> None:
    """Pose the standing UE4 rig into a chair sit (keeps the standing bind).

    Jacob's seated clips cannot be fcurve-copied onto this FBX bind — the glTF
    Jacob skeleton has different bone axes after its own sit-as-rest bake.
    Runtime plays the keyed Sit clip and skips Jacob-only mood takes.
    """
    from mathutils import Euler

    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.transforms_clear()
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"

    # Drop hips to cushion height in armature space, then bend legs/arms.
    pelvis = arm.pose.bones["pelvis"]
    mat = pelvis.matrix.copy()
    target = mat.to_translation().copy()
    target.y = -0.02
    target.z = 0.52
    mat.translation = target
    pelvis.matrix = mat
    bpy.context.view_layer.update()

    def set_euler(name, x, y, z):
        if name not in arm.pose.bones:
            return
        pb = arm.pose.bones[name]
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = Euler((
            x * 3.14159265 / 180.0,
            y * 3.14159265 / 180.0,
            z * 3.14159265 / 180.0,
        ))
        pb.rotation_mode = "QUATERNION"

    # Milder calf so feet stay near the floor (prior bake buried them).
    pose = {
        "thigh_l": (-50, 0, 10),
        "thigh_r": (-50, 0, -10),
        "calf_l": (70, 0, 0),
        "calf_r": (70, 0, 0),
        "foot_l": (-10, 0, 0),
        "foot_r": (-10, 0, 0),
        "spine_01": (8, 0, 0),
        "spine_02": (6, 0, 0),
        "spine_03": (4, 0, 0),
        "clavicle_l": (0, 0, -8),
        "clavicle_r": (0, 0, 8),
        "upperarm_l": (45, -12, -45),
        "upperarm_r": (45, 12, 45),
        "lowerarm_l": (8, 0, -50),
        "lowerarm_r": (8, 0, 50),
        "hand_l": (12, 0, -12),
        "hand_r": (12, 0, 12),
    }
    for name, xyz in pose.items():
        set_euler(name, *xyz)
    bpy.context.view_layer.update()
    print(
        "sit pose pelvis",
        tuple(round(c, 3) for c in (arm.matrix_world @ arm.pose.bones["pelvis"].head)),
        "head",
        tuple(round(c, 3) for c in (arm.matrix_world @ arm.pose.bones["head"].head)),
        "foot_l",
        tuple(round(c, 3) for c in (arm.matrix_world @ arm.pose.bones["foot_l"].head)),
    )


def key_sit_action(arm: bpy.types.Object) -> bpy.types.Action:
    """Key the current pose as a looping Sit clip (standing bind unchanged)."""
    if arm.animation_data is None:
        arm.animation_data_create()
    while arm.animation_data.nla_tracks:
        arm.animation_data.nla_tracks.remove(arm.animation_data.nla_tracks[0])
    act = bpy.data.actions.new(name="Sit")
    act.use_fake_user = True
    arm.animation_data.action = act
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
        for frame in (1, 48):
            pb.keyframe_insert(data_path="location", frame=frame)
            pb.keyframe_insert(data_path="rotation_quaternion", frame=frame)
            pb.keyframe_insert(data_path="scale", frame=frame)
    track = arm.animation_data.nla_tracks.new()
    track.name = "Sit"
    track.strips.new("Sit", 1, act).action = act
    print("keyed Sit hold action")
    return act


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
    # Standing bind + authored Sit clip. Jacob clip reuse shears this skeleton.
    strip_shape_keys()
    author_sit_pose(arm)
    key_sit_action(arm)

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
