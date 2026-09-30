"""Bake Mighty Cat seated takes onto Jacob via facing-space Humanoid aim-copy.

Jacob's bind is already a seated cowboy pose; the pack is Epic/MetaHuman A-pose.
Copying local quaternions or world-space rotation deltas twists the arms.
This baker:

1. Leaves Jacob's bind and inverse-skin alone (no Apply Pose as Rest).
2. Keeps his authored sit on hips/legs so he stays in the chair.
3. For spine, neck, arms, hands and fingers, swings each bone so its
   child-aim matches the source bone's child-aim in a shared character
   facing frame (clavicle-left x world-up). That is the Mixamo/Humanoid
   aim retarget without requiring matching bone rolls.

Clip list matches https://anims.themightycat.com/?asset=poker-blackjack
"""
import bpy, os, math, sys
from mathutils import Vector, Matrix

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
JACOB = os.path.join(ROOT, "Web/CinematicPoker.Web/wwwroot/models/characters/western/Jacob.glb")
SRC = os.environ.get("MIGHTY_CAT_SRC", "/tmp/reactions")
OUT = os.path.join(ROOT, "Web/CinematicPoker.Web/wwwroot/models/characters/western/mighty-cat.glb")
PREVIEW = os.environ.get("MIGHTY_CAT_PREVIEW", "/tmp/mighty-humanoid-bake")

MAP = {
    "spine_01": "spine_01",
    "spine_02": "spine_02",
    "spine_03": "spine_05",
    "neck_01": "neck_02",
    "clavicle_l": "clavicle_l",
    "upperarm_l": "upperarm_l",
    "lowerarm_l": "lowerarm_l",
    "hand_l": "hand_l",
    "clavicle_r": "clavicle_r",
    "upperarm_r": "upperarm_r",
    "lowerarm_r": "lowerarm_r",
    "hand_r": "hand_r",
}
CHILD = {
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
}
for side in ("l", "r"):
    for finger in ("index", "middle", "ring", "pinky", "thumb"):
        MAP[f"{finger}_01_{side}"] = f"{finger}_01_{side}"
        MAP[f"{finger}_02_{side}"] = f"{finger}_02_{side}"
        CHILD[f"{finger}_01_{side}"] = f"{finger}_02_{side}"
        CHILD[f"{finger}_02_{side}"] = f"{finger}_03_{side}"

ORDER = [
    "spine_01", "spine_02", "spine_03", "neck_01",
    "clavicle_l", "upperarm_l", "lowerarm_l", "hand_l",
    "clavicle_r", "upperarm_r", "lowerarm_r", "hand_r",
] + [n for n in MAP if n.split("_")[0] in ("index", "middle", "ring", "pinky", "thumb") and n in CHILD]

TAKES = [
    ("IdleBlackjack01", "AS_Blackjack_Player_01.FBX", "idle"),
    ("IdleBlackjack02", "AS_Blackjack_Player_02.FBX", "idle"),
    ("IdleBlackjack03", "AS_Blackjack_Player_03.FBX", "idle"),
    ("IdlePoker01", "AS_Poker_Player_01.FBX", "idle"),
    ("IdlePoker02", "AS_Poker_Player_02.FBX", "idle"),
    ("IdlePoker03", "AS_Poker_Player_03.FBX", "idle"),
    ("Win01", "AS_Reactions_Player01_Win_01.FBX", "react"),
    ("Win02", "AS_Reactions_Player01_Win_02.FBX", "react"),
    ("Win03", "AS_Reactions_Player01_Win_03.FBX", "react"),
    ("Lose01", "AS_Reactions_Player01_Lose_01.FBX", "react"),
    ("Lose02", "AS_Reactions_Player01_Lose_02.FBX", "react"),
    ("Lose03", "AS_Reactions_Player02_Lose_01.FBX", "react"),
    ("Lose04", "AS_Reactions_Player02_Lose_02.FBX", "react"),
]


def topo_names(arm):
    seen, ordered = set(), []

    def walk(b):
        if b.name in seen:
            return
        if b.parent:
            walk(b.parent)
        seen.add(b.name)
        ordered.append(b.name)

    for b in arm.pose.bones:
        walk(b)
    return ordered


def wpos(arm, n):
    return (arm.matrix_world @ arm.pose.bones[n].matrix).to_translation()


def wq(arm, n):
    return (arm.matrix_world @ arm.pose.bones[n].matrix).to_quaternion()


def facing_q(arm):
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
    """Armature-space pose matrix from current matrix_basis (no depsgraph)."""
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


def capture_local(arm):
    out = {}
    for pb in arm.pose.bones:
        if pb.rotation_mode != "QUATERNION":
            pb.rotation_mode = "QUATERNION"
        out[pb.name] = (pb.location.copy(), pb.rotation_quaternion.copy(), pb.scale.copy())
    return out


def reset_sit(jacob, sit_local):
    for name, (loc, rot, scale) in sit_local.items():
        pb = jacob.pose.bones[name]
        pb.rotation_mode = "QUATERNION"
        pb.location = loc
        pb.rotation_quaternion = rot
        pb.scale = scale


def apply_aim(jacob, src, j_face):
    s_face = facing_q(src)
    cache = {}
    for dn in ORDER:
        sn, ch = MAP[dn], CHILD[dn]
        sc = MAP.get(ch, ch)
        if ch not in jacob.pose.bones or sn not in src.pose.bones or sc not in src.pose.bones:
            continue
        src_aim = wpos(src, sc) - wpos(src, sn)
        if src_aim.length < 1e-6:
            continue
        world_dir = j_face @ (s_face.conjugated() @ src_aim.normalized())
        la = bone_aim_local(jacob, dn, ch)
        cur = (jacob.matrix_world @ pose_arm_matrix(jacob, dn, cache)).to_quaternion()
        set_world_q(jacob, dn, (cur @ la).rotation_difference(world_dir) @ cur, cache)


def pose_frame(jacob, ordered, sit_local, j_face, src):
    reset_sit(jacob, sit_local)
    apply_aim(jacob, src, j_face)
    rots = {}
    for name in ordered:
        pb = jacob.pose.bones[name]
        rots[name] = pb.rotation_quaternion.copy()
    return rots


def scan_peak(src, start, end, step=4):
    scene = bpy.context.scene
    scene.frame_set(start)
    bpy.context.view_layer.update()
    ref = wpos(src, "hand_l") + wpos(src, "hand_r") + wpos(src, "head")
    best_f, best = start, -1.0
    for f in range(start, end + 1, step):
        scene.frame_set(f)
        bpy.context.view_layer.update()
        cur = wpos(src, "hand_l") + wpos(src, "hand_r") + wpos(src, "head")
        score = (cur - ref).length
        if score > best:
            best, best_f = score, f
    return best_f, best


def choose_window(src, kind):
    action = src.animation_data.action
    start, end = int(action.frame_range[0]), int(action.frame_range[1])
    if kind == "react":
        return start, end, 1
    peak, _ = scan_peak(src, start, end)
    # ~8s at 30fps around the most visible beat so the loop is the viewer take,
    # not a quiet hold at the start of a 30s performance.
    win_s = max(start, peak - 60)
    win_e = min(end, win_s + 240)
    if peak > win_e:
        win_e = min(end, peak + 45)
        win_s = max(start, win_e - 240)
    return win_s, win_e, 2


def write_action(name, ordered, sit_local, rot_frames):
    action = bpy.data.actions.new(name)
    n = len(rot_frames)
    last = float(max(1, n))

    def curve(path, index, values):
        fc = action.fcurves.new(path, index=index)
        fc.keyframe_points.add(len(values))
        for i, value in enumerate(values):
            kp = fc.keyframe_points[i]
            kp.co = (values[i][0], values[i][1])
            kp.interpolation = "LINEAR"
        fc.update()

    for bone in ordered:
        loc, sit_rot, scale = sit_local[bone]
        for index in range(3):
            curve(f'pose.bones["{bone}"].location', index, [(1.0, loc[index]), (last, loc[index])])
            curve(f'pose.bones["{bone}"].scale', index, [(1.0, scale[index]), (last, scale[index])])
        samples = [frame[bone] for frame in rot_frames]
        for i in range(1, len(samples)):
            samples[i].make_compatible(samples[i - 1])
        path = f'pose.bones["{bone}"].rotation_quaternion'
        for index in range(4):
            keys = [(float(i + 1), samples[i][index]) for i in range(n)]
            curve(path, index, keys)
    action.frame_range = (1, last)
    return action


def render_preview(arm, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    scene = bpy.context.scene
    keep = ("Jacket__1", "Pants__1", "Head__clean", "ShirtOpt__1", "Hair_cut", "Hands_Optimized")
    for o in bpy.data.objects:
        if o.type == "MESH":
            o.hide_render = not any(k in (o.name or "") for k in keep)
    head = wpos(arm, "head")
    hips = wpos(arm, "pelvis")
    cam_data = bpy.data.cameras.new("cam")
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    target = (head + hips) * 0.5
    cam.location = target + Vector((1.45, -1.7, 0.28))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = 420
    scene.render.resolution_y = 560
    scene.render.filepath = path
    scene.display.shading.light = "STUDIO"
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam, do_unlink=True)
    bpy.data.cameras.remove(cam_data)


def load_jacob():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=JACOB)
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    sit = next(a for a in bpy.data.actions if a.name.startswith("Sit_"))
    arm.animation_data.action = sit
    bpy.context.scene.frame_set(int(sit.frame_range[0]) + 2)
    bpy.context.view_layer.update()
    ordered = topo_names(arm)
    sit_local = capture_local(arm)
    j_face = facing_q(arm)
    arm.animation_data.action = None
    for track in list(arm.animation_data.nla_tracks):
        arm.animation_data.nla_tracks.remove(track)
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    return arm, ordered, sit_local, j_face


def import_source(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    return next(o for o in new if o.type == "ARMATURE"), new


def main():
    jacob, ordered, sit_local, j_face = load_jacob()
    actions = {}
    previews = {}
    for label, filename, kind in TAKES:
        path = os.path.join(SRC, filename)
        if not os.path.isfile(path):
            print("missing", path, file=sys.stderr)
            continue
        src, created = import_source(path)
        win_s, win_e, step = choose_window(src, kind)
        rot_frames = []
        for f in range(win_s, win_e + 1, step):
            bpy.context.scene.frame_set(f)
            bpy.context.view_layer.update()
            rot_frames.append(pose_frame(jacob, ordered, sit_local, j_face, src))
        actions[label] = write_action(label, ordered, sit_local, rot_frames)
        mid = rot_frames[len(rot_frames) // 2]
        previews[label] = mid
        bpy.context.view_layer.update()
        hl = wpos(jacob, "hand_l")
        print(f"baked {label} {len(rot_frames)}f from {filename} {win_s}-{win_e} step {step} "
              f"hand_l=({hl.x:.2f},{hl.y:.2f},{hl.z:.2f})", flush=True)
        for obj in created:
            bpy.data.objects.remove(obj, do_unlink=True)
        keep = set(actions.values())
        for action in list(bpy.data.actions):
            if action not in keep:
                bpy.data.actions.remove(action)

    os.makedirs(PREVIEW, exist_ok=True)
    for label in ("IdlePoker01", "IdleBlackjack01", "Win01", "Win02", "Lose01", "Lose03"):
        if label not in previews:
            continue
        for name in ordered:
            jacob.pose.bones[name].rotation_mode = "QUATERNION"
            jacob.pose.bones[name].rotation_quaternion = previews[label][name]
            jacob.pose.bones[name].location = sit_local[name][0]
        bpy.context.view_layer.update()
        render_preview(jacob, os.path.join(PREVIEW, f"{label}.png"))
        print("preview", label, flush=True)

    for obj in list(bpy.data.objects):
        if obj.type != "ARMATURE":
            bpy.data.objects.remove(obj, do_unlink=True)
    if jacob.animation_data is None:
        jacob.animation_data_create()
    while jacob.animation_data.nla_tracks:
        jacob.animation_data.nla_tracks.remove(jacob.animation_data.nla_tracks[0])
    jacob.animation_data.action = None
    for label, action in actions.items():
        action.id_root = "OBJECT"
        track = jacob.animation_data.nla_tracks.new()
        track.name = label
        strip = track.strips.new(label, 1, action)
        strip.name = label
    bpy.ops.object.select_all(action="DESELECT")
    jacob.select_set(True)
    bpy.context.view_layer.objects.active = jacob
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=OUT,
        export_format="GLB",
        use_selection=True,
        export_animations=True,
        export_animation_mode="NLA_TRACKS",
        export_optimize_animation_size=True,
        export_skins=False,
        export_morph=False,
        export_lights=False,
        export_cameras=False,
        export_yup=True,
    )
    print("exported", OUT, os.path.getsize(OUT), flush=True)


if __name__ == "__main__":
    main()
