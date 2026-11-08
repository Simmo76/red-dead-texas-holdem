"""Bake the Cowboy Girl (David Grette "Cowboy_Girl_1") web GLB.

Same pipeline as bake_western_npc.py (Sheriff / Bandit) with two twists:

- The supplied .unitypackage ships NO skin albedo textures (head / torso /
  legs reference GUIDs that were never exported). The skin albedos are
  synthesised here: a light neutral skin base multiplied by the ambient
  occlusion stored in the G channel of the pack's MetallicAOGloss maps.
  The base is deliberately the LIGHTEST tone — at runtime poker-scene.js
  multiplies the skin materials by a per-seat tone (caucasian / asian /
  tan / black / dark), and a multiply can only darken, so light is the
  correct neutral. Skin materials are named `Skin_*` and hair materials
  `Hair_*` so the runtime can find them.

- Unlike Sheriff/Bandit (one material per mesh) the girl's head is a
  multi-material mesh: skin, brows, lashes, iris, sclera. Mouth interior
  and the eye-shadow/tear-line overlay cards are deleted (her mouth never
  opens at the table and the overlays need blended transparency that
  fights the fold ghosting).

IMPORTANT: like the other two, this bake leaves the skeleton on Blender's
FBX bone frames. After baking, always run

  node tools/retarget_cowgirl.mjs

which re-frames the bone AXES onto Jacob's (rotation only — the girl's
female joint POSITIONS are kept, unlike Sheriff/Bandit whose joints
coincide with Jacob's), copies Jacob's body clips with position tracks
filtered to the pelvis, and drapes the skirt chains for the seated pose.

Usage:
  blender --background --python tools/bake_cowgirl_npc.py
  node tools/retarget_cowgirl.mjs
"""
from __future__ import annotations

import os
import sys
from pathlib import Path

import bpy

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Web/CinematicPoker.Web/wwwroot/models/characters/western/CowboyGirl.glb"
PKG = Path(os.environ.get("GIRL_PKG", "/tmp/girl-pkg/tree/Assets/David Grette/Sheriff"))
FBX = PKG / "FBX/Cowboy_Girl_1.fbx"
TEX = PKG / "Texture"
TEX_CACHE = Path("/tmp/cowgirl-tex")
MAX_TEX = int(os.environ.get("MAX_TEX", "1024"))

# Lightest (caucasian) neutral the runtime tones multiply against. Keep in
# sync with GIRL_SKIN_TONES in poker-scene.js (the 1,1,1 tone assumes this).
SKIN_BASE = (234, 192, 170)
HAIR_COLOR = (0.16, 0.10, 0.07, 1.0)

# Meshes kept at the table (names as in the FBX).
KEEP = {
    "body_Optimized",   # torso (arms/hands) + legs skin, low LOD
    "Head",             # full head: skin, brows, lashes, iris, sclera
    "Hair_1_Cutted",    # short style that fits under the hat
    "HairCap",          # scalp fill under the hair cards
    "Hat", "Bandana", "Shirt", "Skirt", "Underwear",
    "Belt_1", "Boot", "Holster",
}
RENAME = {
    "Hat": "Hat__1",          # runtime hat accent tint targets Hat__*
    "Head": "Head__clean",    # runtime head handling targets Head__*
    "body_Optimized": "Body",
    "Hair_1_Cutted": "Hair_cut",
    "HairCap": "Hair_cap",
    "Boot": "Boots",
}
# Head material slots to delete outright (faces + slot).
HEAD_DROP_MATS = {"Tongue_1_lambert", "Eye_AO", "Eye_Tear_line"}

CLOTH_ALBEDO = {
    "Bandana": "Cloth/1/T_Fem_Cowb_1_Bandana_M_Albedo.png",
    "Belt_1": "Cloth/1/T_Fem_Cowb_1_Belt_1_M_Albedo.png",
    "Boot": "Cloth/1/T_Fem_Cowb_1_Boot_M_Albedo.png",
    "Hat": "Cloth/1/T_Fem_Cowb_1_Hat_M_Albedo.png",
    "Holster": "Cloth/1/T_Fem_Cowb_1_Holster_M_Albedo.png",
    "Shirt": "Cloth/1/T_Fem_Cowb_1_Shirt_M_Albedo.png",
    "Skirt": "Cloth/1/T_Fem_Cowb_1_Skirt_M_Albedo.png",
    "Underwear": "Cloth/1/T_Fem_Cowb_1_Underwear_M_D.png",
}
# AO (G channel of MetallicAOGloss) used to synthesise the skin albedos.
SKIN_AO = {
    "head": ("Skin/Body/T_Head_MetallicAOGloss.png", 1024),
    "torso": ("Skin/Body/T_Hands_MetallicAOGloss.png", 512),
    "legs": ("Skin/Body/T_Legs_MetallicAOGloss.png", 256),
}


def die(msg: str) -> None:
    print("ERROR:", msg, file=sys.stderr)
    sys.exit(1)


def resize_image(src: Path, dest: Path, max_edge: int, keep_alpha: bool = False,
                 alpha_boost: float = 1.0) -> Path:
    from PIL import Image

    dest.parent.mkdir(parents=True, exist_ok=True)
    img = Image.open(src)
    w, h = img.size
    scale = min(1.0, max_edge / float(max(w, h)))
    if scale < 1.0:
        img = img.resize((max(1, int(w * scale)), max(1, int(h * scale))), Image.Resampling.LANCZOS)
    if keep_alpha:
        img = img.convert("RGBA")
        if alpha_boost != 1.0:
            a = img.getchannel(3).point(lambda v: min(255, int(v * alpha_boost)))
            img.putalpha(a)
        dest = dest.with_suffix(".png")
        img.save(dest, optimize=True)
    else:
        dest = dest.with_suffix(".jpg")
        img.convert("RGB").save(dest, quality=85, optimize=True)
    return dest


def synth_skin(ao_src: Path, dest: Path, max_edge: int) -> Path:
    """Light neutral skin albedo = SKIN_BASE x AO(G channel), gently lifted."""
    from PIL import Image

    dest.parent.mkdir(parents=True, exist_ok=True)
    img = Image.open(ao_src)
    ao = img.getchannel(1).resize((max_edge, max_edge), Image.Resampling.LANCZOS)
    px = ao.load()
    out = Image.new("RGB", ao.size)
    opx = out.load()
    br, bg, bb = SKIN_BASE
    for y in range(ao.size[1]):
        for x in range(ao.size[0]):
            a = 0.45 + 0.55 * (px[x, y] / 255.0)
            opx[x, y] = (int(br * a), int(bg * a), int(bb * a))
    dest = dest.with_suffix(".jpg")
    out.save(dest, quality=88, optimize=True)
    return dest


def load_image(path: Path) -> bpy.types.Image:
    img = bpy.data.images.load(str(path))
    img.pack()
    return img


def make_principled(name: str, color_img, base_color=(1, 1, 1, 1),
                    use_mask: bool = False, roughness: float = 0.75,
                    cutoff: float = 0.35, blend_alpha: float = None,
                    mask_only: bool = False) -> bpy.types.Material:
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
        if not mask_only:
            # mask_only textures are opacity maps (white strands) — the flat
            # base_color is the strand colour, the image only cuts the shape.
            nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if (use_mask or mask_only) and color_img.channels == 4:
            nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    if blend_alpha is not None:
        bsdf.inputs["Alpha"].default_value = blend_alpha
        mat.blend_method = "BLEND"
    elif use_mask or mask_only:
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


def delete_material_faces(obj: bpy.types.Object, mat_names: set[str]) -> None:
    import bmesh

    idx = {i for i, ms in enumerate(obj.material_slots)
           if ms.material and ms.material.name.split(".")[0] in mat_names}
    if not idx:
        return
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.faces.ensure_lookup_table()
    doomed = [f for f in bm.faces if f.material_index in idx]
    bmesh.ops.delete(bm, geom=doomed, context="FACES")
    bm.to_mesh(obj.data)
    bm.free()
    # Drop now-empty slots (walk indices high to low so removal is stable).
    bpy.context.view_layer.objects.active = obj
    for i in sorted(idx, reverse=True):
        obj.active_material_index = i
        bpy.ops.object.material_slot_remove()


def main() -> None:
    if not FBX.exists():
        die(f"missing FBX {FBX} — extract girl_dress_rigged.unitypackage first")

    TEX_CACHE.mkdir(parents=True, exist_ok=True)

    print("=== preparing textures ===")
    cloth_tex: dict[str, Path] = {}
    for mesh, rel in CLOTH_ALBEDO.items():
        src = TEX / rel
        if not src.exists():
            die(f"missing albedo {src}")
        cloth_tex[mesh] = resize_image(src, TEX_CACHE / f"girl_{mesh}_alb", MAX_TEX)
    skin_tex: dict[str, Path] = {}
    for part, (rel, edge) in SKIN_AO.items():
        skin_tex[part] = synth_skin(TEX / rel, TEX_CACHE / f"girl_skin_{part}", edge)
        print("synthesised", part, skin_tex[part])
    brows_tex = resize_image(TEX / "Eyes/T_Fem_Cowb_1_Eyebrows_Alpha.tga",
                             TEX_CACHE / "girl_brows", 1024, keep_alpha=True, alpha_boost=2.2)
    lashes_tex = resize_image(TEX / "Eyes/T_eyeLashes_Alpha.tga",
                              TEX_CACHE / "girl_lashes", 1024, keep_alpha=True, alpha_boost=1.6)
    iris_tex = resize_image(TEX / "Eyes/T_Iris_D.png", TEX_CACHE / "girl_iris", 256)
    hair_tex = resize_image(TEX / "Hair/T_Hair_D.tga", TEX_CACHE / "girl_hair", 1024, keep_alpha=True)
    cap_tex = resize_image(TEX / "Hair/T_Haircap_Alpha.tga", TEX_CACHE / "girl_cap", 512, keep_alpha=True)

    print("=== importing FBX ===")
    bpy.ops.wm.read_factory_settings(use_empty=True)
    arm = import_fbx(FBX)
    arm.name = "root"
    force_unit_scale(arm)

    for o in list(bpy.data.objects):
        if o.type == "MESH" and o.name not in KEEP:
            bpy.data.objects.remove(o, do_unlink=True)

    head = bpy.data.objects["Head"]
    delete_material_faces(head, HEAD_DROP_MATS)

    for o in list(bpy.data.objects):
        if o.type == "MESH" and o.name in RENAME:
            o.name = RENAME[o.name]

    print("=== assigning materials ===")
    inv = {v: k for k, v in RENAME.items()}
    for o in bpy.data.objects:
        if o.type != "MESH":
            continue
        orig = inv.get(o.name, o.name)
        if o.name == "Head__clean":
            # Per-slot materials; slot order survives the face deletion.
            mapping = {
                "head": make_principled("Skin_head", load_image(skin_tex["head"]), roughness=0.62),
                # The pack's brow/lash textures are OPACITY masks (their
                # basecolor maps were never exported) — flat dark strands,
                # with a low cutoff so the downscaled strands survive.
                "eye brows": make_principled("Hair_brows", load_image(brows_tex),
                                             base_color=HAIR_COLOR,
                                             mask_only=True, cutoff=0.12),
                "eye Lashes": make_principled("Hair_lashes", load_image(lashes_tex),
                                              base_color=HAIR_COLOR,
                                              mask_only=True, cutoff=0.12),
                "eyeBall_iris": make_principled("Eye_iris", load_image(iris_tex), roughness=0.25),
                # The UE eye mesh is the wet cornea shell OVER the iris ball —
                # opaque it paints the eyes blank white.
                "Eyes_UE": make_principled("Eye_cornea", None,
                                           base_color=(1.0, 1.0, 1.0, 1.0),
                                           roughness=0.05, blend_alpha=0.05),
            }
            for slot in o.material_slots:
                key = slot.material.name.split(".")[0] if slot.material else ""
                slot.material = mapping.get(key) or mapping["head"]
        elif o.name == "Body":
            mapping = {
                "head": make_principled("Skin_head2", load_image(skin_tex["head"]), roughness=0.62),
                "torso": make_principled("Skin_torso", load_image(skin_tex["torso"]), roughness=0.62),
                "legs": make_principled("Skin_legs", load_image(skin_tex["legs"]), roughness=0.62),
            }
            for slot in o.material_slots:
                key = slot.material.name.split(".")[0] if slot.material else ""
                slot.material = mapping.get(key) or mapping["torso"]
        elif o.name == "Hair_cut":
            mat = make_principled("Hair_main", load_image(hair_tex), use_mask=True)
            o.data.materials.clear()
            o.data.materials.append(mat)
        elif o.name == "Hair_cap":
            mat = make_principled("Hair_cap", load_image(cap_tex),
                                  base_color=HAIR_COLOR, use_mask=True)
            o.data.materials.clear()
            o.data.materials.append(mat)
        else:
            img = load_image(cloth_tex[orig]) if orig in cloth_tex else None
            mat = make_principled(f"Cloth_{o.name}", img)
            o.data.materials.clear()
            o.data.materials.append(mat)

    # Table NPCs never talk/blink on the web — drop morph targets.
    for obj in list(bpy.data.objects):
        if obj.type == "MESH" and obj.data.shape_keys is not None:
            obj.shape_key_clear()

    # No authored clips: tools/retarget_cowgirl.mjs copies Jacob's real body
    # clips in after re-framing the bone axes, same as Sheriff/Bandit.
    for a in list(bpy.data.actions):
        bpy.data.actions.remove(a)

    for o in list(bpy.data.objects):
        if o.type in ("LIGHT", "CAMERA"):
            bpy.data.objects.remove(o, do_unlink=True)

    print("meshes", sorted(o.name for o in bpy.data.objects if o.type == "MESH"))
    OUT.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=str(OUT),
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
    print("wrote", OUT, "bytes", OUT.stat().st_size)


if __name__ == "__main__":
    main()
