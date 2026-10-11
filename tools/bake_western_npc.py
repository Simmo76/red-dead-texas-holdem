"""Bake a David Grette western NPC (Sheriff / Bandit) to a web GLB.

Imports the owner-supplied Unity FBX, keeps one seated-table outfit, assigns
downscaled albedo (and hair/lash alpha) maps, then **aim-copies** Jacob's
body clips and the mighty-cat mood catalogue onto the NPC so it shares the
table's biped constraints (metres at scale 1, seated pelvis/hand/foot
heights, same animation names the scene plays on Jacob).

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
MIGHTY = ROOT / "Web/CinematicPoker.Web/wwwroot/models/characters/western/mighty-cat.glb"
OUT_DIR = ROOT / "Web/CinematicPoker.Web/wwwroot/models/characters/western"
TEX_CACHE = Path("/tmp/western-npc-tex")
MAX_TEX = int(os.environ.get("MAX_TEX", "1024"))
HEAD_TEX = int(os.environ.get("HEAD_TEX", "1024"))

CHAR = os.environ.get("CHAR", "sheriff").strip().lower()

# Clips the table plays on every biped. Body takes live on Jacob.glb; mood
# takes live on mighty-cat.glb (already Humanoid-retargeted onto Jacob).
BODY_CLIPS = [
    "Attack", "Bet", "Check", "Crouch", "Fold", "FoldShake", "Sit", "SitCards", "Talk",
]
MOOD_CLIPS = [
    "IdleSitThinking01", "IdleSitArmsFolded01",
    "IdleHoldCards01", "IdleHoldCards02", "IdleHoldCards03",
    "IdleSit01", "IdleSit02", "IdleSit03", "IdleSit04", "IdleSit05", "IdleSit06",
    "IdleTableObserveLeft01", "IdleTableObserveRight01",
    "IdleTableObserveLeftRight01", "IdleTableProtectCards01",
    "Win01", "Win02", "Win03", "Lose01", "Lose02", "Lose03", "Lose04",
]

# Facing-space aim-copy bone map (same as tools/bake_mighty_cat_humanoid.py).
AIM_CHILD = {
    "pelvis": "spine_01",
    "spine_01": "spine_02",
    "spine_02": "spine_03",
    "spine_03": "neck_01",
    "neck_01": "head",
    "clavicle_l": "upperarm_l",
    "upperarm_l": "lowerarm_l",
    "lowerarm_l": "hand_l",
    "hand_l": "middle_01_l",
    "clavicle_r": "upperarm_r",
    "upperarm_r": "lowerarm_r",
    "lowerarm_r": "hand_r",
    "hand_r": "middle_01_r",
    "thigh_l": "calf_l",
    "calf_l": "foot_l",
    "foot_l": "ball_l",
    "thigh_r": "calf_r",
    "calf_r": "foot_r",
    "foot_r": "ball_r",
}
for side in ("l", "r"):
    for finger in ("index", "middle", "ring", "pinky", "thumb"):
        AIM_CHILD[f"{finger}_01_{side}"] = f"{finger}_02_{side}"
        if finger != "thumb":
            AIM_CHILD[f"{finger}_02_{side}"] = f"{finger}_03_{side}"

AIM_ORDER = [
    "pelvis", "spine_01", "spine_02", "spine_03", "neck_01",
    "clavicle_l", "upperarm_l", "lowerarm_l", "hand_l",
    "clavicle_r", "upperarm_r", "lowerarm_r", "hand_r",
    "thigh_l", "calf_l", "foot_l",
    "thigh_r", "calf_r", "foot_r",
] + [n for n in AIM_CHILD if n.split("_")[0] in ("index", "middle", "ring", "pinky", "thumb")]

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


def strip_shape_keys() -> None:
    for obj in list(bpy.data.objects):
        if obj.type == "MESH" and obj.data.shape_keys is not None:
            obj.shape_key_clear()


def force_unit_scale(arm: bpy.types.Object) -> None:
    """Bake any residual object scale so the GLB root is metres at scale 1."""
    apply_object_scales(arm)
    arm.scale = (1.0, 1.0, 1.0)
    for o in bpy.data.objects:
        if o.parent == arm:
            o.scale = (1.0, 1.0, 1.0)
    bpy.context.view_layer.update()
    print("arm scale after unitize", tuple(arm.scale))


# ---- Jacob → NPC facing-space aim retarget (same biped constraints) ----

def wpos(arm, n):
    return (arm.matrix_world @ arm.pose.bones[n].matrix).to_translation()


def wpos_any(src, n):
    """World position of a bone on an armature, or of a same-named empty."""
    if getattr(src, "type", None) == "ARMATURE":
        return wpos(src, n)
    # src is a dict name→Object (mighty-cat empties)
    obj = src.get(n) if isinstance(src, dict) else None
    if obj is None:
        from mathutils import Vector
        return Vector((0, 0, 0))
    return obj.matrix_world.to_translation()


def facing_q(src):
    from mathutils import Vector, Matrix
    x = wpos_any(src, "clavicle_l") - wpos_any(src, "clavicle_r")
    x = x.normalized() if x.length > 1e-5 else Vector((1, 0, 0))
    z = Vector((0, 0, 1))
    y = z.cross(x)
    y = y.normalized() if y.length > 1e-5 else Vector((0, 1, 0))
    z = x.cross(y).normalized()
    m = Matrix.Identity(4)
    m.col[0][0:3] = x
    m.col[1][0:3] = y
    m.col[2][0:3] = z
    return m.to_quaternion()


def pose_arm_matrix(arm, name, cache):
    if name in cache:
        return cache[name]
    pb = arm.pose.bones[name]
    rest = pb.bone.matrix_local
    basis = pb.matrix_basis
    if pb.parent:
        parent = pose_arm_matrix(arm, pb.parent.name, cache)
        mat = parent @ pb.parent.bone.matrix_local.inverted() @ rest @ basis
    else:
        mat = rest @ basis
    cache[name] = mat
    return mat


def set_world_q(arm, name, world_q, cache):
    pb = arm.pose.bones[name]
    if pb.parent:
        parent_world = arm.matrix_world @ pose_arm_matrix(arm, pb.parent.name, cache)
    else:
        parent_world = arm.matrix_world
    local_m = parent_world.inverted() @ world_q.to_matrix().to_4x4()
    rest = pb.bone.matrix_local
    if pb.parent:
        basis = rest.inverted() @ pb.parent.bone.matrix_local @ local_m
    else:
        basis = rest.inverted() @ local_m
    pb.rotation_mode = "QUATERNION"
    pb.rotation_quaternion = basis.to_quaternion()
    cache.pop(name, None)


def bone_aim_local(arm, name, child):
    return (arm.data.bones[name].matrix_local.inverted() @ arm.data.bones[child].head_local).normalized()


def clear_pose(arm):
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.location = (0, 0, 0)
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.scale = (1, 1, 1)


def src_has(src, n):
    if getattr(src, "type", None) == "ARMATURE":
        return n in src.pose.bones
    return isinstance(src, dict) and n in src


def aim_copy_frame(dst, src):
    """Full-body aim-copy: match source limb aims + pelvis world translation.

    `src` is either Jacob's armature or a dict of mighty-cat bone empties.
    """
    clear_pose(dst)
    bpy.context.view_layer.update()
    # Plant pelvis at the source world position (same chair height / depth).
    if "pelvis" in dst.pose.bones and src_has(src, "pelvis"):
        src_pelvis = wpos_any(src, "pelvis")
        pb = dst.pose.bones["pelvis"]
        mat = pb.matrix.copy()
        mat.translation = dst.matrix_world.inverted() @ src_pelvis
        pb.matrix = mat
        bpy.context.view_layer.update()

    s_face = facing_q(src)
    d_face = facing_q(dst)
    cache = {}
    for dn in AIM_ORDER:
        ch = AIM_CHILD.get(dn)
        if not ch or dn not in dst.pose.bones or ch not in dst.pose.bones:
            continue
        if not src_has(src, dn) or not src_has(src, ch):
            continue
        src_aim = wpos_any(src, ch) - wpos_any(src, dn)
        if src_aim.length < 1e-6:
            continue
        world_dir = d_face @ (s_face.conjugated() @ src_aim.normalized())
        la = bone_aim_local(dst, dn, ch)
        cur = (dst.matrix_world @ pose_arm_matrix(dst, dn, cache)).to_quaternion()
        set_world_q(dst, dn, (cur @ la).rotation_difference(world_dir) @ cur, cache)


def collect_actions(prefix_names):
    """Map clean clip name → Action from recently imported glTF data."""
    found = {}
    for act in bpy.data.actions:
        base = act.name.split("_")[0]
        # mighty-cat clips are already clean names; Jacob is Name_root.001
        for want in prefix_names:
            if act.name == want or base == want:
                found.setdefault(want, act)
    return found


def enable_empty_nla(empties: dict) -> None:
    for obj in empties.values():
        if not obj.animation_data:
            continue
        obj.animation_data.use_nla = True
        obj.animation_data.action = None
        for track in obj.animation_data.nla_tracks:
            track.mute = False


def mute_empty_tracks_except(empties: dict, clip_name: str) -> int:
    """Solo the NLA tracks for one mood clip across all bone empties."""
    hit = 0
    for obj in empties.values():
        if not obj.animation_data:
            continue
        for track in obj.animation_data.nla_tracks:
            keep = any(
                s.action and (
                    s.action.name == f"{clip_name}_{obj.name}"
                    or s.action.name.startswith(f"{clip_name}_")
                    or track.name == clip_name
                )
                for s in track.strips
            )
            # Tracks are named like IdleSit01
            keep = keep or track.name == clip_name
            track.mute = not keep
            if keep:
                hit += 1
    return hit


def mood_frame_range(empties: dict, clip_name: str):
    f0, f1 = None, None
    for obj in empties.values():
        if not obj.animation_data:
            continue
        for track in obj.animation_data.nla_tracks:
            if track.name != clip_name:
                continue
            for s in track.strips:
                if s.action:
                    a0, a1 = int(s.action.frame_range[0]), int(s.action.frame_range[1])
                    f0 = a0 if f0 is None else min(f0, a0)
                    f1 = a1 if f1 is None else max(f1, a1)
    return f0, f1


def retarget_biped_clips(dst_arm: bpy.types.Object) -> None:
    """Bake Jacob body + mighty-cat mood clips onto the NPC via aim-copy.

    Resulting GLB carries the same clip catalogue the table plays on Jacob, so
    Sheriff/Bandit share scale, seating, and animation constraints.
    """
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(JACOB))
    jacob_objs = [o for o in bpy.data.objects if o not in before]
    jacob = next(o for o in jacob_objs if o.type == "ARMATURE")
    jacob.location = (0.0, 0.0, 0.0)
    if jacob.animation_data is None:
        jacob.animation_data_create()

    body_acts = collect_actions(BODY_CLIPS)
    print("jacob body clips", sorted(body_acts))
    if "Sit" not in body_acts:
        die("Jacob Sit missing")

    # Mighty-cat imports as per-bone empties with NLA tracks (not one armature).
    before_m = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(MIGHTY))
    empties = {
        o.name: o
        for o in bpy.data.objects
        if o not in before_m and o.type == "EMPTY" and o.name in jacob.pose.bones
    }
    enable_empty_nla(empties)
    print("mighty empties", len(empties))

    if dst_arm.animation_data is None:
        dst_arm.animation_data_create()
    while dst_arm.animation_data.nla_tracks:
        dst_arm.animation_data.nla_tracks.remove(dst_arm.animation_data.nla_tracks[0])

    scene = bpy.context.scene
    key_bones = [n for n in AIM_ORDER if n in dst_arm.pose.bones and n in jacob.pose.bones]
    if "head" in dst_arm.pose.bones and "head" in jacob.pose.bones:
        key_bones.append("head")

    def bake_from_source(name, src, f0, f1, step):
        new_act = bpy.data.actions.new(name=name)
        new_act.use_fake_user = True
        dst_arm.animation_data.action = new_act
        frames = list(range(f0, f1 + 1, step))
        if frames[-1] != f1:
            frames.append(f1)
        print(f"  retarget {name}: {len(frames)} frames ({f0}-{f1} step {step})")
        for i, frame in enumerate(frames):
            scene.frame_set(frame)
            bpy.context.view_layer.update()
            aim_copy_frame(dst_arm, src)
            bpy.context.view_layer.update()
            out_f = 1 + i
            for bname in key_bones:
                pb = dst_arm.pose.bones[bname]
                pb.rotation_mode = "QUATERNION"
                pb.keyframe_insert(data_path="location", frame=out_f)
                pb.keyframe_insert(data_path="rotation_quaternion", frame=out_f)
                pb.keyframe_insert(data_path="scale", frame=out_f)
        for fc in new_act.fcurves:
            for kp in fc.keyframe_points:
                kp.interpolation = "LINEAR"
        track = dst_arm.animation_data.nla_tracks.new()
        track.name = name
        track.strips.new(name, 1, new_act).action = new_act

    for name in BODY_CLIPS:
        if name not in body_acts:
            continue
        src_act = body_acts[name]
        jacob.animation_data.action = src_act
        bake_from_source(name, jacob, int(src_act.frame_range[0]), int(src_act.frame_range[1]), 1)

    for name in MOOD_CLIPS:
        hit = mute_empty_tracks_except(empties, name)
        f0, f1 = mood_frame_range(empties, name)
        if not hit or f0 is None:
            print(f"  skip mood {name}: no NLA tracks")
            continue
        # Cap long performance takes so the bake stays web-sized.
        f1 = min(f1, f0 + 120)
        bake_from_source(name, empties, f0, f1, 2)

    # Preview Sit for bounds / export.
    jacob.animation_data.action = body_acts["Sit"]
    scene.frame_set(int(body_acts["Sit"].frame_range[0]))
    bpy.context.view_layer.update()
    aim_copy_frame(dst_arm, jacob)
    bpy.context.view_layer.update()
    dst_arm.animation_data.action = bpy.data.actions.get("Sit")
    print(
        "preview sit pelvis",
        tuple(round(c, 3) for c in wpos(dst_arm, "pelvis")),
        "head",
        tuple(round(c, 3) for c in wpos(dst_arm, "head")),
        "hand_l",
        tuple(round(c, 3) for c in wpos(dst_arm, "hand_l")),
        "foot_l",
        tuple(round(c, 3) for c in wpos(dst_arm, "foot_l")),
    )

    for o in list(jacob_objs) + list(empties.values()):
        try:
            bpy.data.objects.remove(o, do_unlink=True)
        except ReferenceError:
            pass


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

    if not MIGHTY.exists():
        die(f"missing mighty-cat {MIGHTY}")

    clear_scene()
    arm = import_fbx(spec["fbx"])
    arm.name = "root"
    force_unit_scale(arm)
    cull_and_rename(spec)
    assign_materials(spec, prepared)
    strip_shape_keys()
    # Aim-copy Jacob body + mighty-cat mood clips so this biped shares the
    # table's seated height, hand plant, and animation catalogue.
    retarget_biped_clips(arm)
    force_unit_scale(arm)

    # Evaluated bounds under the preview Sit pose.
    dg = bpy.context.evaluated_depsgraph_get()
    mins = Vector((1e9, 1e9, 1e9))
    maxs = Vector((-1e9, -1e9, -1e9))
    for o in bpy.data.objects:
        if o.type != "MESH" or o.parent != arm:
            continue
        eo = o.evaluated_get(dg)
        for v in eo.bound_box:
            w = eo.matrix_world @ Vector(v)
            mins.x, mins.y, mins.z = min(mins.x, w.x), min(mins.y, w.y), min(mins.z, w.z)
            maxs.x, maxs.y, maxs.z = max(maxs.x, w.x), max(maxs.y, w.y), max(maxs.z, w.z)
    print("eval sit bounds", tuple(round(c, 3) for c in mins), tuple(round(c, 3) for c in maxs),
          "height", round(maxs.z - mins.z, 3))
    print("meshes", sorted(o.name for o in bpy.data.objects if o.type == "MESH"))
    print("arm scale", tuple(arm.scale))

    export_glb(spec["out"])


if __name__ == "__main__":
    main()
