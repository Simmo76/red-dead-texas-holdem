"""Bake Sheriff / Bandit web GLBs on their own UE4 armature.

Keeps the native FBX skin (no Jacob rebind — that exploded neck/hair verts
under mood clips). Authors a clean Sit hold at Jacob's seated bone heights,
unit-scales to metres, and exports opaque cloth + hair-cap alpha.

Runtime uses Sit (not mighty-cat mood) for these NPCs; placeSeatedCharacter
+ WESTERN_SCALE enforce table biped placement.

Usage:
  CHAR=sheriff blender --background --python tools/bake_western_npc.py
  CHAR=bandit  blender --background --python tools/bake_western_npc.py
"""
from __future__ import annotations

import os
import sys
from pathlib import Path

import bpy
from mathutils import Vector, Euler

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
            "AmmoBelt", "Boots", "Hands_Optimized",
            "Hat", "Holster", "Jacket", "Pants", "Sheriff_Head", "Shirt",
            "Tie", "Vest",
        },
        # Hair_cap cards explode under retarget — omit; hat covers the scalp.
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
        "alpha": {},
        "hair_color": (0.12, 0.09, 0.07, 1.0),
    },
    "bandit": {
        "fbx": Path("/tmp/bandit-pkg/tree/Assets/David Grette/Sheriff/FBX/SK_Cowboy_2.fbx"),
        "tex": Path("/tmp/bandit-pkg/tree/Assets/David Grette/Sheriff/Texture"),
        "out": OUT_DIR / "Bandit.glb",
        "keep": {
            "Bandage", "Belt", "Boot", "Hair_cut", "Hands_Optimized",
            "Hat", "Holster", "Jacket", "Jeans", "Michael_head", "Scarf", "Shirt",
        },
        # Drop Hair_cap cards; keep Hair_cut under the hat.
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
        "alpha": {},
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
        keep_alpha = mesh == "Hair_cut"
        prepared[mesh] = resize_image(src, TEX_CACHE / f"{CHAR}_{mesh}_alb", edge, keep_alpha=keep_alpha)
    return prepared


def load_image(path: Path) -> bpy.types.Image:
    img = bpy.data.images.load(str(path))
    img.pack()
    return img


def make_principled(name: str, color_img: bpy.types.Image | None,
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
        if use_mask and color_img.channels == 4:
            nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    if use_mask:
        mat.blend_method = "CLIP"
        mat.alpha_threshold = 0.35
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
    arms = [o for o in bpy.data.objects if o not in before and o.type == "ARMATURE"]
    if not arms:
        die(f"no armature in {path}")
    return arms[0]


def force_unit_scale(arm: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    for o in bpy.data.objects:
        if o.parent == arm:
            o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    arm.scale = (1.0, 1.0, 1.0)
    for o in bpy.data.objects:
        if o.parent == arm:
            o.scale = (1.0, 1.0, 1.0)
    bpy.context.view_layer.update()
    print("arm scale", tuple(arm.scale))


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
    inv_rename = {v: k for k, v in spec["rename"].items()}
    hair_color = spec.get("hair_color", (0.15, 0.1, 0.08, 1.0))
    for o in bpy.data.objects:
        if o.type != "MESH":
            continue
        orig = inv_rename.get(o.name, o.name)
        color_path = prepared.get(orig)
        color_img = load_image(color_path) if color_path else None
        base = hair_color if "Hair" in orig else (1, 1, 1, 1)
        use_mask = orig == "Hair_cut"
        mat = make_principled(o.name, color_img, base_color=base, use_mask=use_mask)
        o.data.materials.clear()
        o.data.materials.append(mat)
        if o.name.startswith("Head__"):
            for i in range(len(o.material_slots)):
                o.material_slots[i].material = mat


def strip_shape_keys() -> None:
    for obj in list(bpy.data.objects):
        if obj.type == "MESH" and obj.data.shape_keys is not None:
            obj.shape_key_clear()


def wpos(arm, n):
    return (arm.matrix_world @ arm.pose.bones[n].matrix).to_translation()


def author_sit_pose(arm: bpy.types.Object) -> None:
    """Stable chair sit on the standing FBX bind (no rebind, no mood retarget)."""
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.transforms_clear()
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"

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
        "sit pelvis", tuple(round(c, 3) for c in wpos(arm, "pelvis")),
        "head", tuple(round(c, 3) for c in wpos(arm, "head")),
        "hand_l", tuple(round(c, 3) for c in wpos(arm, "hand_l")),
        "foot_l", tuple(round(c, 3) for c in wpos(arm, "foot_l")),
    )


def key_sit_and_actions(arm: bpy.types.Object) -> None:
    """Key Sit hold + simple Check/Bet/Fold variants from the same seated base."""
    if arm.animation_data is None:
        arm.animation_data_create()
    while arm.animation_data.nla_tracks:
        arm.animation_data.nla_tracks.remove(arm.animation_data.nla_tracks[0])

    def capture():
        out = {}
        for pb in arm.pose.bones:
            pb.rotation_mode = "QUATERNION"
            out[pb.name] = (
                pb.location.copy(),
                pb.rotation_quaternion.copy(),
                pb.scale.copy(),
            )
        return out

    def restore(pose):
        for name, (loc, rot, scale) in pose.items():
            pb = arm.pose.bones[name]
            pb.rotation_mode = "QUATERNION"
            pb.location = loc
            pb.rotation_quaternion = rot
            pb.scale = scale

    def key_action(name, frames_poses):
        act = bpy.data.actions.new(name=name)
        act.use_fake_user = True
        arm.animation_data.action = act
        for frame, pose in frames_poses:
            restore(pose)
            bpy.context.view_layer.update()
            for pb in arm.pose.bones:
                pb.rotation_mode = "QUATERNION"
                pb.keyframe_insert(data_path="location", frame=frame)
                pb.keyframe_insert(data_path="rotation_quaternion", frame=frame)
                pb.keyframe_insert(data_path="scale", frame=frame)
        for fc in act.fcurves:
            for kp in fc.keyframe_points:
                kp.interpolation = "LINEAR"
        track = arm.animation_data.nla_tracks.new()
        track.name = name
        track.strips.new(name, 1, act).action = act
        print("keyed", name)

    sit = capture()
    key_action("Sit", [(1, sit), (48, sit)])

    # Mild arm raises for table gestures (stay on the same bind — no retarget).
    def nudge(base, bone, dx=0, dy=0, dz=0):
        pose = {k: (v[0].copy(), v[1].copy(), v[2].copy()) for k, v in base.items()}
        if bone in arm.pose.bones:
            restore(base)
            pb = arm.pose.bones[bone]
            pb.rotation_mode = "XYZ"
            e = pb.rotation_euler.copy()
            e.x += dx * 3.14159265 / 180.0
            e.y += dy * 3.14159265 / 180.0
            e.z += dz * 3.14159265 / 180.0
            pb.rotation_euler = e
            pb.rotation_mode = "QUATERNION"
            bpy.context.view_layer.update()
            pose = capture()
        return pose

    check = nudge(sit, "upperarm_r", dx=-12, dz=8)
    check = nudge(check, "lowerarm_r", dz=15)
    key_action("Check", [(1, sit), (12, check), (28, check), (40, sit)])

    bet = nudge(sit, "upperarm_r", dx=-20, dz=10)
    bet = nudge(bet, "lowerarm_r", dz=25)
    key_action("Bet", [(1, sit), (10, bet), (26, bet), (40, sit)])

    fold = nudge(sit, "upperarm_l", dx=-8, dz=-20)
    fold = nudge(fold, "upperarm_r", dx=-8, dz=20)
    fold = nudge(fold, "lowerarm_l", dz=-30)
    fold = nudge(fold, "lowerarm_r", dz=30)
    key_action("Fold", [(1, sit), (14, fold), (36, fold), (48, sit)])

    # Leave posed in Sit for export preview.
    restore(sit)
    arm.animation_data.action = bpy.data.actions.get("Sit")
    bpy.context.view_layer.update()


def export_glb(path: Path) -> None:
    # Purge junk meshes/lights/cameras.
    keep = {o.name for o in bpy.data.objects if o.type == "MESH"}
    for o in list(bpy.data.objects):
        if o.type in ("LIGHT", "CAMERA"):
            bpy.data.objects.remove(o, do_unlink=True)
        elif o.type == "MESH" and ("Icosphere" in o.name or "Cube" in o.name):
            bpy.data.objects.remove(o, do_unlink=True)

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
        die(f"unknown CHAR={CHAR}")
    spec = SPECS[CHAR]
    if not spec["fbx"].exists():
        die(f"missing FBX {spec['fbx']}")

    print(f"=== baking {CHAR} (native skin, clean Sit) ===")
    prepared = prepare_textures(spec)
    clear_scene()
    arm = import_fbx(spec["fbx"])
    arm.name = "root"
    force_unit_scale(arm)
    cull_and_rename(spec)
    assign_materials(spec, prepared)
    strip_shape_keys()
    author_sit_pose(arm)
    key_sit_and_actions(arm)

    dg = bpy.context.evaluated_depsgraph_get()
    for o in bpy.data.objects:
        if o.type != "MESH":
            continue
        eo = o.evaluated_get(dg)
        me = eo.to_mesh()
        mats = eo.matrix_world
        far = 0
        for v in me.vertices:
            w = mats @ v.co
            if w.length > 3.0:
                far += 1
        print(f"mesh {o.name}: verts={len(me.vertices)} far>3m={far}")
        eo.to_mesh_clear()

    print("meshes", sorted(o.name for o in bpy.data.objects if o.type == "MESH"))
    export_glb(spec["out"])


if __name__ == "__main__":
    main()
