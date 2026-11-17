"""Bake the Dead Man's Hand duo (Jeremy / Jimmy) web GLBs.

Unlike Sheriff / Bandit / Cowboy Girl (David Grette UE4 bipeds that share
Jacob's bone NAMES), these two ship on Mixamo-named skeletons (Hips /
Spine / LeftArm ...) in their own rest poses — Jeremy in a loose A-pose,
Jimmy in a full T-pose. Jacob's shared clip catalogue can only drive them
after two fixes done here:

- every Mixamo bone is renamed to its UE4 twin (Hips -> pelvis,
  LeftForeArm -> lowerarm_l ...) so clips bind by name at runtime;
- the rest SKELETON SHAPE is aligned to Jacob's: each mapped bone is
  rotated (shortest arc, world space) until its chain direction matches
  Jacob's rest chain, then that pose is applied as the new bind pose with
  the meshes re-skinned to it. Bone lengths stay native, so each character
  keeps his own proportions. Without this step Jimmy's T-pose arms would
  read ~45 degrees high under every seated clip.

The retarget that gives the bones Jacob's local FRAMES (axes) happens
afterwards in tools/retarget_deadmans.mjs, exactly like the Cowboy Girl:
rotation-only reframe + inverse-bind patch + Jacob's clips copied with
position tracks dropped for everything but the pelvis.

Usage:
  CHAR=jeremy blender --background --python tools/bake_deadmans_npc.py
  CHAR=jimmy  blender --background --python tools/bake_deadmans_npc.py
  node tools/retarget_deadmans.mjs
"""
from __future__ import annotations

import os
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[1]
OUT_DIR = ROOT / "Web/CinematicPoker.Web/wwwroot/models/characters/western"
TEX_CACHE = Path("/tmp/deadmans-tex")
MAX_TEX = int(os.environ.get("MAX_TEX", "1024"))
CHAR = os.environ.get("CHAR", "jeremy").strip().lower()

JEREMY_PKG = Path(os.environ.get("JEREMY_PKG", "/tmp/deadmans/jeremy-pkg/tree/Assets/JEREMY"))
JIMMY_PKG = Path(os.environ.get("JIMMY_PKG", "/tmp/deadmans/jimmy-pkg/tree/Assets"))

# Mixamo -> UE4 biped names (the names Jacob's clips animate).
BONE_MAP = {
    "Hips": "pelvis",
    "Spine": "spine_01", "Spine1": "spine_02", "Spine2": "spine_03",
    "Neck": "neck_01", "Head": "head",
}
for side, s in (("Left", "l"), ("Right", "r")):
    BONE_MAP[f"{side}Shoulder"] = f"clavicle_{s}"
    BONE_MAP[f"{side}Arm"] = f"upperarm_{s}"
    BONE_MAP[f"{side}ForeArm"] = f"lowerarm_{s}"
    BONE_MAP[f"{side}Hand"] = f"hand_{s}"
    BONE_MAP[f"{side}UpLeg"] = f"thigh_{s}"
    BONE_MAP[f"{side}Leg"] = f"calf_{s}"
    BONE_MAP[f"{side}Foot"] = f"foot_{s}"
    BONE_MAP[f"{side}ToeBase"] = f"ball_{s}"
    for mix, ue in (("Thumb", "thumb"), ("Index", "index"), ("Middle", "middle"),
                    ("Ring", "ring"), ("Pinky", "pinky")):
        for n in (1, 2, 3):
            BONE_MAP[f"{side}Hand{mix}{n}"] = f"{ue}_0{n}_{s}"

# Jacob's rest joint world positions (Blender Z-up; from Jacob.glb, glTF
# (x,y,z) -> Blender (x,-z,y)). Left side only — right is mirrored in x.
JACOB_REST_L = {
    "pelvis": (0.000, -0.028, 0.937),
    "spine_01": (0.000, -0.025, 1.075),
    "spine_02": (0.000, -0.011, 1.217),
    "spine_03": (0.000, 0.015, 1.353),
    "neck_01": (0.000, 0.005, 1.509),
    "head": (0.000, -0.033, 1.627),
    "clavicle_l": (0.053, -0.012, 1.459),
    "upperarm_l": (0.217, 0.036, 1.458),
    "lowerarm_l": (0.423, 0.015, 1.252),
    "hand_l": (0.567, -0.163, 1.111),
    "thumb_01_l": (0.553, -0.182, 1.101),
    "thumb_02_l": (0.550, -0.218, 1.077),
    "thumb_03_l": (0.542, -0.257, 1.054),
    "index_01_l": (0.594, -0.245, 1.050),
    "index_02_l": (0.601, -0.268, 1.034),
    "index_03_l": (0.603, -0.285, 1.016),
    "middle_01_l": (0.608, -0.229, 1.039),
    "middle_02_l": (0.619, -0.251, 1.016),
    "middle_03_l": (0.621, -0.268, 0.992),
    "ring_01_l": (0.614, -0.212, 1.029),
    "ring_02_l": (0.625, -0.225, 1.009),
    "ring_03_l": (0.630, -0.239, 0.980),
    "pinky_01_l": (0.621, -0.190, 1.021),
    "pinky_02_l": (0.630, -0.197, 1.003),
    "pinky_03_l": (0.637, -0.204, 0.985),
    "thigh_l": (0.109, -0.028, 0.937),
    "calf_l": (0.126, -0.001, 0.526),
    "foot_l": (0.144, 0.066, 0.104),
    "ball_l": (0.162, -0.060, 0.008),
}


def jacob_rest() -> dict[str, Vector]:
    rest = {}
    for name, pos in JACOB_REST_L.items():
        rest[name] = Vector(pos)
        if name.endswith("_l"):
            rest[name[:-2] + "_r"] = Vector((-pos[0], pos[1], pos[2]))
    return rest


# bone -> the joint its chain aims at in Jacob's rest. Fingers' last
# segments keep the direction of the previous segment (straight finger).
def aim_pairs() -> dict[str, tuple[str, str]]:
    pairs = {
        "pelvis": ("pelvis", "spine_01"),
        "spine_01": ("spine_01", "spine_02"),
        "spine_02": ("spine_02", "spine_03"),
        "spine_03": ("spine_03", "neck_01"),
        "neck_01": ("neck_01", "head"),
    }
    for s in ("l", "r"):
        pairs[f"clavicle_{s}"] = (f"clavicle_{s}", f"upperarm_{s}")
        pairs[f"upperarm_{s}"] = (f"upperarm_{s}", f"lowerarm_{s}")
        pairs[f"lowerarm_{s}"] = (f"lowerarm_{s}", f"hand_{s}")
        pairs[f"hand_{s}"] = (f"hand_{s}", f"middle_01_{s}")
        pairs[f"thigh_{s}"] = (f"thigh_{s}", f"calf_{s}")
        pairs[f"calf_{s}"] = (f"calf_{s}", f"foot_{s}")
        pairs[f"foot_{s}"] = (f"foot_{s}", f"ball_{s}")
        for f in ("thumb", "index", "middle", "ring", "pinky"):
            pairs[f"{f}_01_{s}"] = (f"{f}_01_{s}", f"{f}_02_{s}")
            pairs[f"{f}_02_{s}"] = (f"{f}_02_{s}", f"{f}_03_{s}")
            pairs[f"{f}_03_{s}"] = (f"{f}_02_{s}", f"{f}_03_{s}")
    return pairs


SPECS = {
    "jeremy": {
        "fbx": JEREMY_PKG / "Models/Jeremy_Assembled.fbx",
        "out": OUT_DIR / "Jeremy.glb",
        # Backpack + Battery would clip the chair back; everything else stays.
        "drop": {"Backpack", "Battery"},
        # Eye shells carry inward-facing normals on this export; the Head__
        # prefix makes the runtime draw them double-sided (same as the girl).
        "rename": {"Eye_LL": "Head__eyeL", "Eye_RR": "Head__eyeR"},
    },
    "jimmy": {
        "fbx": JIMMY_PKG / "jimmy_adventure/Meshes/main/jmy_adv_combined.fbx",
        "out": OUT_DIR / "JimmyDMH.glb",
        "drop": {"jmy_adv_bpack_a"},
        # Hat__1 receives the seat accent tint at runtime.
        "rename": {"jmy_adv_hat_a": "Hat__1", "jmy_eyes_a": "Head__eyes"},
    },
}


def die(msg: str) -> None:
    print("ERROR:", msg, file=sys.stderr)
    sys.exit(1)


def resize_image(src: Path, dest: Path, max_edge: int, keep_alpha: bool = False) -> Path:
    from PIL import Image

    dest.parent.mkdir(parents=True, exist_ok=True)
    img = Image.open(src)
    w, h = img.size
    scale = min(1.0, max_edge / float(max(w, h)))
    if scale < 1.0:
        img = img.resize((max(1, int(w * scale)), max(1, int(h * scale))), Image.Resampling.LANCZOS)
    if keep_alpha:
        dest = dest.with_suffix(".png")
        img.convert("RGBA").save(dest, optimize=True)
    else:
        dest = dest.with_suffix(".jpg")
        img.convert("RGB").save(dest, quality=85, optimize=True)
    return dest


def load_image(path: Path) -> bpy.types.Image:
    img = bpy.data.images.load(str(path))
    img.pack()
    return img


def make_principled(name: str, color_img, base_color=(1, 1, 1, 1),
                    roughness: float = 0.75, use_mask: bool = False,
                    cutoff: float = 0.35, blend_alpha: float = None) -> bpy.types.Material:
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Base Color"].default_value = base_color
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    if color_img:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = color_img
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if use_mask and color_img.channels == 4:
            nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    if blend_alpha is not None:
        bsdf.inputs["Alpha"].default_value = blend_alpha
        mat.blend_method = "BLEND"
    elif use_mask:
        mat.blend_method = "CLIP"
        mat.alpha_threshold = cutoff
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
    # Rotation must be applied too: these FBX imports leave the armature
    # OBJECT rotated, which would export as a rotated 'root' node. Jacob's
    # pelvis position/rotation curves are authored in an identity root space,
    # so a rotated root pitches the whole body 90 degrees under his clips.
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    for o in bpy.data.objects:
        if o.parent == arm:
            o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    arm.scale = (1.0, 1.0, 1.0)
    for o in bpy.data.objects:
        if o.parent == arm:
            o.scale = (1.0, 1.0, 1.0)
    bpy.context.view_layer.update()


def drop_root_bone(arm: bpy.types.Object) -> None:
    """Jimmy carries a 'Root' bone above the hips; Jacob's pelvis sits
    directly under the armature, and his pelvis position curves assume that
    parent space, so remove the extra level (children keep their rest)."""
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    eb = arm.data.edit_bones
    root = eb.get("Root")
    if root:
        for child in list(root.children):
            child.parent = None
        eb.remove(root)
        print("removed extra Root bone")
    bpy.ops.object.mode_set(mode="OBJECT")


def rename_bones(arm: bpy.types.Object) -> None:
    n = 0
    for bone in arm.data.bones:
        if bone.name in BONE_MAP:
            bone.name = BONE_MAP[bone.name]  # vertex groups follow automatically
            n += 1
    print(f"renamed {n} bones to UE4 names")


def align_rest_to_jacob(arm: bpy.types.Object) -> None:
    """Rotate each mapped pose bone (shortest arc, world space) so its chain
    direction matches Jacob's rest skeleton, then apply as the new rest pose
    with every mesh re-skinned to it."""
    rest = jacob_rest()
    pairs = aim_pairs()

    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.select_all(action="SELECT")
    bpy.ops.pose.transforms_clear()
    bpy.context.view_layer.update()

    def world_head(pb):
        return (arm.matrix_world @ pb.matrix).to_translation()

    def walk(pb):
        pair = pairs.get(pb.name)
        child_pb = None
        if pair:
            a, b = pair
            child_pb = arm.pose.bones.get(b)
            # The finger-tip rule aims the last segment along the previous
            # segment's Jacob direction; the live vector still comes from the
            # bone's own child when present, else from its tail.
        if pair and pb.name in rest:
            target = (rest[pair[1]] - rest[pair[0]])
            if target.length > 1e-8:
                target.normalize()
                if child_pb is not None and child_pb.name != pb.name:
                    cur = world_head(child_pb) - world_head(pb)
                else:
                    tail = (arm.matrix_world @ pb.matrix @ Matrix.Translation((0, pb.length, 0))).to_translation()
                    cur = tail - world_head(pb)
                if cur.length > 1e-8:
                    cur.normalize()
                    q = cur.rotation_difference(target)
                    head = world_head(pb)
                    w = arm.matrix_world @ pb.matrix
                    w_new = (Matrix.Translation(head) @ q.to_matrix().to_4x4()
                             @ Matrix.Translation(-head) @ w)
                    pb.matrix = arm.matrix_world.inverted() @ w_new
                    bpy.context.view_layer.update()
        for c in pb.children:
            walk(c)

    for pb in arm.pose.bones:
        if pb.parent is None:
            walk(pb)

    # Report the aligned arm slope for a sanity check.
    for n in ("upperarm_l", "hand_l", "pelvis", "head"):
        if n in arm.pose.bones:
            print("aligned", n, tuple(round(v, 3) for v in world_head(arm.pose.bones[n])))

    bpy.ops.object.mode_set(mode="OBJECT")

    # Bake the aligned pose into the meshes, then make it the rest pose.
    for o in bpy.data.objects:
        if o.type != "MESH":
            continue
        mods = [m for m in o.modifiers if m.type == "ARMATURE"]
        if not mods:
            continue
        src = mods[0]
        keep = o.modifiers.new("ArmatureRebind", "ARMATURE")
        keep.object = src.object
        with bpy.context.temp_override(object=o, active_object=o, selected_objects=[o]):
            bpy.ops.object.modifier_apply(modifier=src.name)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    print("applied Jacob-aligned pose as the new rest / bind pose")


def assign_materials_jeremy() -> None:
    tex = JEREMY_PKG / "Textures"
    atlas = resize_image(tex / "Jeremy_D.png", TEX_CACHE / "jeremy_atlas", MAX_TEX)
    atlas_a = resize_image(tex / "Jeremy_D.png", TEX_CACHE / "jeremy_atlas_a", MAX_TEX, keep_alpha=True)
    eye = resize_image(tex / "Eye.png", TEX_CACHE / "jeremy_eye", 256)

    cloth = make_principled("Cloth_jeremy", load_image(atlas), roughness=0.78)
    hair = make_principled("Hair_jeremy", load_image(atlas_a), roughness=0.6,
                           use_mask=True, cutoff=0.3)
    iris = make_principled("Eye_iris", load_image(eye), roughness=0.25)
    cornea = make_principled("Eye_cornea", None, base_color=(1, 1, 1, 1),
                             roughness=0.05, blend_alpha=0.05)
    for o in bpy.data.objects:
        if o.type != "MESH":
            continue
        if o.name.startswith("Head__eye"):
            # blinn15 is the textured iris shell, blinn16 the clear cornea.
            for slot in o.material_slots:
                key = slot.material.name.split(".")[0] if slot.material else ""
                slot.material = cornea if key == "blinn16" else iris
        elif o.name == "Hair":
            o.data.materials.clear()
            o.data.materials.append(hair)
        else:
            for i in range(len(o.material_slots)):
                o.material_slots[i].material = cloth
            if not o.material_slots:
                o.data.materials.append(cloth)


def assign_materials_jimmy() -> None:
    adv = JIMMY_PKG / "jimmy_adventure/Textures"
    scifi = JIMMY_PKG / "jimmy_jenny_scifi/jimmy_scifi/Textures"
    body = resize_image(adv / "main/jmy_adv_body_t01_Albedo.tga", TEX_CACHE / "jimmy_body", MAX_TEX)
    props = resize_image(adv / "props/jmy_adv_props_t01_Albedo.tga", TEX_CACHE / "jimmy_props", MAX_TEX)
    hat = resize_image(scifi / "props/jmy_hat_a_t01_Albedo.tga", TEX_CACHE / "jimmy_hat", 512)
    face = resize_image(scifi / "head/jmy_face_t01_Albedo.tga", TEX_CACHE / "jimmy_face", MAX_TEX)
    eyes = resize_image(scifi / "head/jmy_eyes_Albedo.tga", TEX_CACHE / "jimmy_eyes", 256)
    hairt = resize_image(scifi / "head/jmy_hair_a_t01_Albedo.tga", TEX_CACHE / "jimmy_hair", 512)

    mats = {
        "jmy_adv_upper": make_principled("Cloth_upper", load_image(body), roughness=0.78),
        "jmy_adv_lower": make_principled("Cloth_lower", load_image(body), roughness=0.78),
        "jmy_adv_props": make_principled("Cloth_props", load_image(props), roughness=0.75),
        "jmy_adv_chat": make_principled("Cloth_hat", load_image(hat), roughness=0.7),
        "blinn2": make_principled("Skin_face", load_image(face), roughness=0.62),
        "blinn3": make_principled("Eye_eyes", load_image(eyes), roughness=0.25),
        "blinn4": make_principled("Hair_jimmy", load_image(hairt), roughness=0.6),
    }
    for o in bpy.data.objects:
        if o.type != "MESH":
            continue
        for slot in o.material_slots:
            key = slot.material.name.split(".")[0] if slot.material else ""
            if key in mats:
                slot.material = mats[key]


def main() -> None:
    if CHAR not in SPECS:
        die(f"unknown CHAR={CHAR}")
    spec = SPECS[CHAR]
    if not spec["fbx"].exists():
        die(f"missing FBX {spec['fbx']} — extract the unitypackage first")
    TEX_CACHE.mkdir(parents=True, exist_ok=True)

    print(f"=== baking {CHAR} ===")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    arm = import_fbx(spec["fbx"])
    arm.name = "root"
    force_unit_scale(arm)

    for o in list(bpy.data.objects):
        if o.type == "MESH" and o.name in spec["drop"]:
            bpy.data.objects.remove(o, do_unlink=True)
        elif o.type in ("LIGHT", "CAMERA", "EMPTY"):
            bpy.data.objects.remove(o, do_unlink=True)

    drop_root_bone(arm)
    rename_bones(arm)
    align_rest_to_jacob(arm)

    for o in list(bpy.data.objects):
        if o.type == "MESH" and o.name in spec["rename"]:
            o.name = spec["rename"][o.name]

    if CHAR == "jeremy":
        assign_materials_jeremy()
    else:
        assign_materials_jimmy()

    # Table NPCs never talk/blink on the web — drop morph targets and clips.
    for obj in list(bpy.data.objects):
        if obj.type == "MESH" and obj.data.shape_keys is not None:
            obj.shape_key_clear()
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)

    print("meshes", sorted(o.name for o in bpy.data.objects if o.type == "MESH"))
    out: Path = spec["out"]
    out.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=str(out),
        export_format="GLB",
        use_selection=False,
        export_animations=False,
        export_def_bones=False,
        export_apply=False,
        export_texcoords=True,
        export_normals=True,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_skins=True,
        export_morph=False,
    )
    print("wrote", out, "bytes", out.stat().st_size)


if __name__ == "__main__":
    main()
