"""Export a David Grette western FBX as a web GLB matching Jacob.glb conventions."""
import bpy
import sys
from pathlib import Path

args = sys.argv[sys.argv.index("--") + 1 :]
SRC_FBX, JACOB_GLB, OUT_GLB = Path(args[0]), Path(args[1]), Path(args[2])
MAX_TEX = 1024

CLIP_NAMES = [
    "Sit",
    "Attack",
    "Bet",
    "Check",
    "Crouch",
    "Fold",
    "FoldShake",
    "SitCards",
    "Talk",
]


def armatures():
    return [o for o in bpy.data.objects if o.type == "ARMATURE"]


def delete_meshes():
    for obj in list(bpy.data.objects):
        if obj.type == "MESH":
            bpy.data.objects.remove(obj, do_unlink=True)


def resize_textures():
    for img in bpy.data.images:
        if not img.size[0] or not img.size[1]:
            continue
        w, h = img.size
        if w <= MAX_TEX and h <= MAX_TEX:
            continue
        scale = MAX_TEX / max(w, h)
        nw, nh = max(1, int(w * scale)), max(1, int(h * scale))
        img.scale(nw, nh)
        img.pack()


def simplify_materials():
    for mat in bpy.data.materials:
        if not mat.use_nodes:
            continue
        for node in mat.node_tree.nodes:
            if node.type != "TEX_IMAGE" or not node.image:
                continue
            links = mat.node_tree.links
            out = None
            for n in mat.node_tree.nodes:
                if n.type == "BSDF_PRINCIPLED":
                    out = n
                    break
            if not out:
                continue
            for inp in ("Normal", "Roughness", "Metallic", "Specular"):
                sock = out.inputs.get(inp)
                if sock and sock.is_linked:
                    for l in list(sock.links):
                        links.remove(l)
            base = out.inputs.get("Base Color")
            if base and not base.is_linked:
                links.new(node.outputs["Color"], base)


def rename_head_meshes():
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        low = obj.name.lower()
        if low in ("head", "jacob_head", "michael_head") or low.endswith("_head"):
            obj.name = "Head__clean"
        elif obj.name == "Boot":
            obj.name = "Boots"
        elif obj.name == "Jeans":
            obj.name = "Pants"


def apply_source_pose(source_arm, target_arm, action, frame):
    source_arm.animation_data_create()
    source_arm.animation_data.action = action
    bpy.context.view_layer.update()
    bpy.context.scene.frame_set(int(frame))
    for pb in source_arm.pose.bones:
        if pb.name not in target_arm.pose.bones:
            continue
        tpb = target_arm.pose.bones[pb.name]
        tpb.rotation_mode = pb.rotation_mode
        if pb.rotation_mode == "QUATERNION":
            tpb.rotation_quaternion = pb.rotation_quaternion.copy()
        elif pb.rotation_mode == "AXIS_ANGLE":
            tpb.rotation_axis_angle = pb.rotation_axis_angle[:]
        else:
            tpb.rotation_euler = pb.rotation_euler[:]
        if pb.name in ("root", "pelvis", "hips", "root.001"):
            tpb.location = pb.location[:]


def bake_action_from_source(source_arm, target_arm, action, new_name):
    if not action:
        return None
    act = bpy.data.actions.new(new_name)
    target_arm.animation_data_create()
    target_arm.animation_data.action = act
    start, end = int(action.frame_range[0]), int(action.frame_range[1])
    if new_name == "Sit":
        end = start
    for frame in range(start, end + 1):
        apply_source_pose(source_arm, target_arm, action, frame)
        for pb in target_arm.pose.bones:
            if pb.rotation_mode == "QUATERNION":
                pb.keyframe_insert(data_path="rotation_quaternion", frame=frame)
            elif pb.rotation_mode == "AXIS_ANGLE":
                pb.keyframe_insert(data_path="rotation_axis_angle", frame=frame)
            else:
                pb.keyframe_insert(data_path="rotation_euler", frame=frame)
            if pb.name in ("root", "pelvis", "hips", "root.001"):
                pb.keyframe_insert(data_path="location", frame=frame)
    act.use_fake_user = True
    return act


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)

    bpy.ops.import_scene.gltf(filepath=str(JACOB_GLB))
    source_arm = armatures()[0]
    jacob_actions = {}
    for a in bpy.data.actions:
        base = a.name.split("_")[0] if "_" in a.name else a.name
        jacob_actions[base] = a
    delete_meshes()

    bpy.ops.import_scene.fbx(filepath=str(SRC_FBX))
    arms = armatures()
    target_arm = [a for a in arms if a != source_arm][0]

    rename_head_meshes()
    resize_textures()
    simplify_materials()

    baked = []
    for name in CLIP_NAMES:
        src = jacob_actions.get(name)
        if not src:
            continue
        act = bake_action_from_source(source_arm, target_arm, src, name)
        if act:
            baked.append(act)

    if baked:
        target_arm.animation_data.action = baked[0]
    bpy.data.objects.remove(source_arm, do_unlink=True)

    bpy.ops.export_scene.gltf(
        filepath=str(OUT_GLB),
        export_format="GLB",
        export_animations=True,
        export_skins=True,
        export_morph=False,
        export_texcoords=True,
        export_materials="EXPORT",
        export_image_format="AUTO",
        export_yup=True,
    )
    print("WROTE", OUT_GLB, "clips", [a.name for a in baked])


if __name__ == "__main__":
    main()
