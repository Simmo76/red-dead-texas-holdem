"""Bake a David Grette western NPC (Sheriff / Bandit) onto Jacob's biped.

Imports the Unity FBX outfit, aim-poses it to Jacob's seated bind, freezes the
meshes in that shape, then **rebinds** them to Jacob's armature so the NPC
shares Jacob's bone axes, WESTERN_SCALE seating, and the mighty-cat mood
catalogue at runtime (same constraints as every other biped at the table).

Usage:
  CHAR=sheriff blender --background --python tools/bake_western_npc.py
  CHAR=bandit  blender --background --python tools/bake_western_npc.py

Source FBX trees are expected under /tmp/{sheriff,bandit}-pkg/tree/.
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

AIM_CHILD = {
    "pelvis": "spine_01", "spine_01": "spine_02", "spine_02": "spine_03",
    "spine_03": "neck_01", "neck_01": "head",
    "clavicle_l": "upperarm_l", "upperarm_l": "lowerarm_l", "lowerarm_l": "hand_l",
    "hand_l": "middle_01_l",
    "clavicle_r": "upperarm_r", "upperarm_r": "lowerarm_r", "lowerarm_r": "hand_r",
    "hand_r": "middle_01_r",
    "thigh_l": "calf_l", "calf_l": "foot_l", "foot_l": "ball_l",
    "thigh_r": "calf_r", "calf_r": "foot_r", "foot_r": "ball_r",
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
    if use_mask and alpha_img:
        atex = nt.nodes.new("ShaderNodeTexImage")
        atex.image = alpha_img
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


def force_unit_scale(arm: bpy.types.Object) -> None:
    apply_object_scales(arm)
    arm.scale = (1.0, 1.0, 1.0)
    for o in bpy.data.objects:
        if o.parent == arm:
            o.scale = (1.0, 1.0, 1.0)
    bpy.context.view_layer.update()


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
        alpha_path = prepared.get(f"{orig}__alpha")
        color_img = load_image(color_path) if color_path else None
        alpha_img = load_image(alpha_path) if alpha_path else None
        base = hair_color if "Hair" in orig else (1, 1, 1, 1)
        use_mask = "Hair" in orig
        mat = make_principled(o.name, color_img, alpha_img, base_color=base, use_mask=use_mask)
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


def facing_q(arm):
    from mathutils import Matrix
    x = wpos(arm, "clavicle_l") - wpos(arm, "clavicle_r")
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


def aim_copy_frame(dst, src):
    clear_pose(dst)
    bpy.context.view_layer.update()
    if "pelvis" in dst.pose.bones and "pelvis" in src.pose.bones:
        src_pelvis = wpos(src, "pelvis")
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
        if dn not in src.pose.bones or ch not in src.pose.bones:
            continue
        src_aim = wpos(src, ch) - wpos(src, dn)
        if src_aim.length < 1e-6:
            continue
        world_dir = (d_face @ (s_face.conjugated() @ src_aim.normalized())).normalized()
        la = bone_aim_local(dst, dn, ch)
        cur = (dst.matrix_world @ pose_arm_matrix(dst, dn, cache)).to_quaternion()
        aimed = (cur @ la).rotation_difference(world_dir) @ cur
        set_world_q(dst, dn, aimed, cache)


def freeze_posed_meshes(arm: bpy.types.Object) -> list[bpy.types.Object]:
    """Replace each skinned mesh with its evaluated (posed) geometry, keep VGroups."""
    dg = bpy.context.evaluated_depsgraph_get()
    frozen = []
    for obj in list(bpy.data.objects):
        if obj.type != "MESH" or obj.parent != arm:
            continue
        # Preserve vertex groups by name before replacing mesh data.
        vg_names = [vg.name for vg in obj.vertex_groups]
        eval_obj = obj.evaluated_get(dg)
        new_me = bpy.data.meshes.new_from_object(eval_obj, preserve_all_data_layers=True, depsgraph=dg)
        old_me = obj.data
        # Copy vertex groups from evaluated if present; else restore empty groups.
        obj.data = new_me
        bpy.data.meshes.remove(old_me)
        # Ensure every original group name exists (weights come from new_from_object when possible).
        for name in vg_names:
            if name not in obj.vertex_groups:
                obj.vertex_groups.new(name=name)
        # Drop armature modifiers; we'll rebind to Jacob next.
        for mod in list(obj.modifiers):
            if mod.type == "ARMATURE":
                obj.modifiers.remove(mod)
        obj.parent = None
        frozen.append(obj)
        print("froze mesh", obj.name, "vgroups", len(obj.vertex_groups))
    return frozen


def rebind_to_jacob(meshes: list[bpy.types.Object], jacob: bpy.types.Object) -> None:
    jacob_bones = {b.name for b in jacob.data.bones}
    for obj in meshes:
        obj.parent = jacob
        obj.parent_type = "OBJECT"
        mod = obj.modifiers.new(name="Armature", type="ARMATURE")
        mod.object = jacob
        mod.use_vertex_groups = True
        # Drop weights for bones Jacob doesn't have.
        for vg in list(obj.vertex_groups):
            if vg.name not in jacob_bones:
                obj.vertex_groups.remove(vg)
        print("rebound", obj.name, "->", jacob.name, "vgroups", len(obj.vertex_groups))


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

    print(f"=== baking {CHAR} onto Jacob biped ===")
    prepared = prepare_textures(spec)

    clear_scene()
    npc_arm = import_fbx(spec["fbx"])
    npc_arm.name = "npc_root"
    force_unit_scale(npc_arm)
    cull_and_rename(spec)
    assign_materials(spec, prepared)
    strip_shape_keys()

    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(JACOB))
    jacob_objs = [o for o in bpy.data.objects if o not in before]
    jacob = next(o for o in jacob_objs if o.type == "ARMATURE")
    jacob.name = "root"
    jacob.location = (0, 0, 0)
    # Hide Jacob's own meshes — we only want his armature + animations.
    for o in jacob_objs:
        if o.type == "MESH":
            bpy.data.objects.remove(o, do_unlink=True)

    # Pose NPC to Jacob's seated bind, freeze meshes, rebind to Jacob.
    if jacob.animation_data is None:
        jacob.animation_data_create()
    sit = next((a for a in bpy.data.actions if a.name == "Sit" or a.name.startswith("Sit")), None)
    if sit is None:
        die("Jacob Sit missing")
    # Jacob's rest is already the sit bind; clear pose to rest.
    clear_pose(jacob)
    bpy.context.view_layer.update()
    aim_copy_frame(npc_arm, jacob)
    bpy.context.view_layer.update()
    print(
        "npc sit pelvis", tuple(round(c, 3) for c in wpos(npc_arm, "pelvis")),
        "head", tuple(round(c, 3) for c in wpos(npc_arm, "head")),
        "hand_l", tuple(round(c, 3) for c in wpos(npc_arm, "hand_l")),
        "jacob pelvis", tuple(round(c, 3) for c in wpos(jacob, "pelvis")),
        "jacob head", tuple(round(c, 3) for c in wpos(jacob, "head")),
        "jacob hand_l", tuple(round(c, 3) for c in wpos(jacob, "hand_l")),
    )

    meshes = freeze_posed_meshes(npc_arm)
    if not meshes:
        die("no meshes frozen")
    rebind_to_jacob(meshes, jacob)

    # Remove the NPC armature — Jacob is the sole biped now.
    bpy.data.objects.remove(npc_arm, do_unlink=True)

    # Keep Jacob body actions; drop junk.
    keep = {"Attack", "Bet", "Check", "Crouch", "Fold", "FoldShake", "Sit", "SitCards", "Talk"}
    for act in list(bpy.data.actions):
        base = act.name.split("_")[0] if "_" in act.name else act.name
        # Jacob exports as Sit / Sit_root etc — keep clean names and NLA on jacob.
        if act.name not in keep and base not in keep and not act.name.startswith(tuple(keep)):
            # Keep actions referenced by Jacob NLA strips.
            used = False
            if jacob.animation_data:
                for t in jacob.animation_data.nla_tracks:
                    for s in t.strips:
                        if s.action == act:
                            used = True
            if not used:
                try:
                    bpy.data.actions.remove(act)
                except RuntimeError:
                    pass

    # Ensure a Sit action is active for export bind preview.
    sit_act = next((a for a in bpy.data.actions if a.name == "Sit"), None)
    if sit_act and jacob.animation_data:
        jacob.animation_data.action = sit_act
        bpy.context.scene.frame_set(1)
        bpy.context.view_layer.update()

    print("meshes", sorted(o.name for o in bpy.data.objects if o.type == "MESH"))
    print("arm scale", tuple(jacob.scale))
    print(
        "final pelvis", tuple(round(c, 3) for c in wpos(jacob, "pelvis")),
        "head", tuple(round(c, 3) for c in wpos(jacob, "head")),
        "hand_l", tuple(round(c, 3) for c in wpos(jacob, "hand_l")),
    )
    export_glb(spec["out"])


if __name__ == "__main__":
    main()
