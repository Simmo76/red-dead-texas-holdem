"""Bake Mighty Cat seated idles and win/lose takes onto Jacob via IK.

Arm bones cannot copy local rotations from the pack — Jacob and the Epic
source do not share the same bind axes, which twisted the left arm. Hands
are placed in a shared character-facing space (chest origin, clavicle right,
world up) and solved with two-bone IK. Spine/neck/head use rest-relative
local deltas. Fingers use rest-relative local deltas (same names). Legs stay
on Jacob's authored sit.
"""
import bpy, os, math, sys
from mathutils import Vector, Matrix, Quaternion

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
JACOB = os.path.join(ROOT, "Web/CinematicPoker.Web/wwwroot/models/characters/western/Jacob.glb")
SRC = os.environ.get("MIGHTY_CAT_SRC", "/tmp/reactions")
OUT = os.path.join(ROOT, "Web/CinematicPoker.Web/wwwroot/models/characters/western/mighty-cat.glb")
PREVIEW = os.environ.get("MIGHTY_CAT_PREVIEW", "/tmp/mighty-cat-preview")

TORSO = {
    "spine_01": "spine_01",
    "spine_02": "spine_02",
    "spine_03": "spine_03",
    "neck_01": "neck_02",
    "head": "head",
}

FINGERS = []
for side in ("l", "r"):
    for finger in ("index", "middle", "ring", "pinky", "thumb"):
        for i in ("01", "02", "03"):
            FINGERS.append(f"{finger}_{i}_{side}")

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


def world_mat(arm, name):
    return arm.matrix_world @ arm.pose.bones[name].matrix


def world_pos(arm, name):
    return world_mat(arm, name).to_translation()


def local_quat(arm, name):
    pb = arm.pose.bones[name]
    if pb.parent:
        return (pb.parent.matrix.inverted() @ pb.matrix).to_quaternion()
    return pb.matrix.to_quaternion()


def facing_matrix(arm, chest="spine_03"):
    """Character space: origin at chest, +X toward left clavicle, +Z world-upish."""
    origin = world_pos(arm, chest)
    left = world_pos(arm, "clavicle_l")
    right = world_pos(arm, "clavicle_r")
    x = (left - right)
    if x.length < 1e-5:
        x = Vector((1, 0, 0))
    x.normalize()
    z = Vector((0, 0, 1))
    y = z.cross(x)
    if y.length < 1e-5:
        y = Vector((0, 1, 0))
    y.normalize()
    z = x.cross(y).normalized()
    m = Matrix.Identity(4)
    m.col[0][0:3] = x
    m.col[1][0:3] = y
    m.col[2][0:3] = z
    m.translation = origin
    return m


def facing_offset(arm, name, chest="spine_03"):
    return facing_matrix(arm, chest).inverted() @ world_pos(arm, name)


def load_jacob():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=JACOB)
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    for track in list(arm.animation_data.nla_tracks):
        arm.animation_data.nla_tracks.remove(track)
    sit = next(a for a in bpy.data.actions if a.name.startswith("Sit_"))
    arm.animation_data.action = sit
    scene = bpy.context.scene
    scene.frame_set(int(sit.frame_range[0]) + 2)
    bpy.context.view_layer.update()
    ordered = topo_names(arm)
    local = {}
    rest_aim = {}
    for name in ordered:
        pb = arm.pose.bones[name]
        if pb.rotation_mode != "QUATERNION":
            pb.rotation_mode = "QUATERNION"
        local[name] = (pb.location.copy(), pb.rotation_quaternion.copy(), pb.scale.copy())
    for parent, child in (
        ("upperarm_l", "lowerarm_l"),
        ("lowerarm_l", "hand_l"),
        ("upperarm_r", "lowerarm_r"),
        ("lowerarm_r", "hand_r"),
    ):
        rest_aim[parent] = aim_local(arm, parent, child)
    sit_face = facing_matrix(arm)
    sit_hands = {
        "l": facing_offset(arm, "hand_l"),
        "r": facing_offset(arm, "hand_r"),
        "el": facing_offset(arm, "lowerarm_l"),
        "er": facing_offset(arm, "lowerarm_r"),
    }
    len_l = (world_pos(arm, "lowerarm_l") - world_pos(arm, "upperarm_l")).length
    len_l2 = (world_pos(arm, "hand_l") - world_pos(arm, "lowerarm_l")).length
    len_r = (world_pos(arm, "lowerarm_r") - world_pos(arm, "upperarm_r")).length
    len_r2 = (world_pos(arm, "hand_r") - world_pos(arm, "lowerarm_r")).length
    arm.animation_data.action = None
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    return arm, ordered, local, rest_aim, sit_face, sit_hands, (len_l, len_l2, len_r, len_r2)


def aim_local(arm, parent, child):
    p = arm.data.bones[parent]
    c = arm.data.bones[child]
    return (p.matrix_local.inverted() @ c.head_local).normalized()


def import_source(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    return next(o for o in new if o.type == "ARMATURE"), new


def choose_window(src, kind):
    action = src.animation_data.action
    start, end = int(action.frame_range[0]), int(action.frame_range[1])
    fps = bpy.context.scene.render.fps or 30
    if kind == "idle":
        win_s = min(end - 8, start + int(1.2 * fps))
        win_e = min(end, win_s + int(3.6 * fps))
        return win_s, win_e, 2
    # reaction: around the largest chest/hand motion
    scene = bpy.context.scene
    scene.frame_set(start)
    bpy.context.view_layer.update()
    ref = facing_offset(src, "hand_l") + facing_offset(src, "hand_r")
    best_f, best = start, -1.0
    for f in range(start, end + 1, 3):
        scene.frame_set(f)
        bpy.context.view_layer.update()
        cur = facing_offset(src, "hand_l") + facing_offset(src, "hand_r")
        score = (cur - ref).length
        if score > best:
            best, best_f = score, f
    win_s = max(start, best_f - int(0.4 * fps))
    win_e = min(end, best_f + int(2.2 * fps))
    return win_s, win_e, 1


def set_basis_quat(arm, name, quat):
    pb = arm.pose.bones[name]
    pb.rotation_mode = "QUATERNION"
    pb.rotation_quaternion = quat
    pb.location = pb.location


def apply_torso(jacob, local, src, src_bind_local):
    for dst, srcn in TORSO.items():
        pb = src.pose.bones.get(srcn)
        if not pb:
            continue
        bind = src_bind_local.get(srcn)
        if bind is None:
            continue
        delta = local_quat(src, srcn) @ bind.conjugated()
        rot = local[dst][1] @ delta
        rot.normalize()
        set_basis_quat(jacob, dst, rot)
    bpy.context.view_layer.update()


def apply_fingers(jacob, local, src, src_bind_local, weight=0.75):
    for name in FINGERS:
        if name not in jacob.pose.bones or name not in src.pose.bones:
            continue
        bind = src_bind_local.get(name)
        if bind is None:
            continue
        delta = local_quat(src, name) @ bind.conjugated()
        # damp extreme finger crumple
        if delta.angle > math.radians(70):
            delta = Quaternion(delta.axis, math.radians(70) * (1 if delta.angle >= 0 else -1))
        rot = local[name][1] @ delta
        rot.normalize()
        sit = local[name][1]
        rot = sit.slerp(rot, weight)
        set_basis_quat(jacob, name, rot)


def set_world_rotation(arm, name, world_q):
    pb = arm.pose.bones[name]
    parent_world = arm.matrix_world if pb.parent is None else (arm.matrix_world @ pb.parent.matrix)
    local_m = parent_world.inverted() @ world_q.to_matrix().to_4x4()
    rest = pb.bone.matrix_local
    if pb.parent:
        basis = rest.inverted() @ pb.parent.bone.matrix_local @ local_m
    else:
        basis = rest.inverted() @ local_m
    pb.rotation_mode = "QUATERNION"
    pb.rotation_quaternion = basis.to_quaternion()


def aim_bone(arm, name, local_aim, world_dir, preserve_roll_q):
    """Rotate so local_aim points along world_dir; keep roll from preserve_roll_q as much as possible."""
    # Build a rotation that maps local_aim -> world_dir, using preserve roll's secondary axis.
    aim = world_dir.normalized()
    current = preserve_roll_q @ local_aim
    swing = current.rotation_difference(aim)
    new_q = swing @ preserve_roll_q
    set_world_rotation(arm, name, new_q)
    bpy.context.view_layer.update()


def ik_arm(jacob, rest_aim, local, side, target, pole, lens):
    upper = f"upperarm_{side}"
    lower = f"lowerarm_{side}"
    hand = f"hand_{side}"
    u_len, l_len = lens
    root = world_pos(jacob, upper)
    to_t = target - root
    dist = to_t.length
    max_reach = u_len + l_len - 0.012
    min_reach = abs(u_len - l_len) + 0.02
    if dist > max_reach:
        target = root + to_t.normalized() * max_reach
        to_t = target - root
        dist = max_reach
    elif dist < min_reach:
        target = root + (to_t.normalized() if dist > 1e-5 else Vector((0, -1, 0))) * min_reach
        to_t = target - root
        dist = min_reach
    axis = to_t.normalized()
    pole_dir = pole - root
    planar = pole_dir - axis * axis.dot(pole_dir)
    if planar.length < 1e-5:
        planar = Vector((0, 0, 1)).cross(axis)
    planar.normalize()
    cos_a = (u_len * u_len + dist * dist - l_len * l_len) / (2 * u_len * dist)
    cos_a = max(-1.0, min(1.0, cos_a))
    a = math.acos(cos_a)
    upper_dir = (math.cos(a) * axis + math.sin(a) * planar).normalized()
    sit_q = (jacob.matrix_world @ _bone_world_from_sit(jacob, local, upper)).to_quaternion()
    # sit world rot of the upper from sit basis
    sit_world_u = sit_bone_world_quat(jacob, local, upper)
    aim_bone(jacob, upper, rest_aim[upper], upper_dir, sit_world_u)
    mid = world_pos(jacob, lower)
    lower_dir = (target - mid)
    if lower_dir.length < 1e-5:
        lower_dir = axis
    sit_world_l = sit_bone_world_quat(jacob, local, lower)
    aim_bone(jacob, lower, rest_aim[lower], lower_dir.normalized(), sit_world_l)
    # wrist: keep sit wrist relative to forearm
    sit_hand = sit_bone_world_quat(jacob, local, hand)
    sit_low = sit_bone_world_quat(jacob, local, lower)
    rel = sit_low.conjugated() @ sit_hand
    now_low = world_mat(jacob, lower).to_quaternion()
    set_world_rotation(jacob, hand, now_low @ rel)
    bpy.context.view_layer.update()


def _bone_world_from_sit(arm, local, name):
    return arm.pose.bones[name].matrix


def sit_bone_world_quat(arm, local, name):
    """World rotation of the bone if it were on the sit pose (siblings already updated)."""
    # Approximate: reconstruct from current parent world * sit local
    pb = arm.pose.bones[name]
    sit_basis = Quaternion(local[name][1])
    rest = pb.bone.matrix_local
    if pb.parent:
        parent_rest = pb.parent.bone.matrix_local
        local_pose = parent_rest.inverted() @ rest @ sit_basis.to_matrix().to_4x4()
        arm_space = pb.parent.matrix @ local_pose
    else:
        arm_space = rest @ sit_basis.to_matrix().to_4x4()
    return (arm.matrix_world @ arm_space).to_quaternion()


def sample_bind(src):
    bind = {}
    for name in list(TORSO.values()) + FINGERS:
        if name in src.pose.bones:
            bind[name] = local_quat(src, name)
    bind["hand_l"] = facing_offset(src, "hand_l")
    bind["hand_r"] = facing_offset(src, "hand_r")
    bind["el"] = facing_offset(src, "lowerarm_l")
    bind["er"] = facing_offset(src, "lowerarm_r")
    return bind


def map_hand_target(src, bind, sit_hands, side, motion=1.0):
    key = "hand_" + side if False else ("hand_l" if side == "l" else "hand_r")
    elbow_key = "el" if side == "l" else "er"
    sit_key = "l" if side == "l" else "r"
    cur = facing_offset(src, "hand_l" if side == "l" else "hand_r")
    delta = cur - bind[key]
    # Keep motion, but start from Jacob's sit hand so the hold stays in his lap.
    target_local = sit_hands[sit_key] + delta * motion
    # Do not let a hand travel behind the torso (negative character-forward is toward the table).
    # Character +Y is facing-forward from facing_matrix (world-up cross right).
    # Clamp extreme travel so a celebration still reads but cannot flip behind the back.
    target_local.x = max(-0.38, min(0.38, target_local.x))
    target_local.y = max(-0.12, min(0.55, target_local.y))
    target_local.z = max(-0.55, min(0.35, target_local.z))
    pole_local = sit_hands[elbow_key] + (facing_offset(src, "lowerarm_l" if side == "l" else "lowerarm_r") - bind[elbow_key]) * motion
    return target_local, pole_local


def pose_frame(jacob, ordered, local, rest_aim, sit_face, sit_hands, lens, src, src_bind, motion):
    for name in ordered:
        pb = jacob.pose.bones[name]
        pb.rotation_mode = "QUATERNION"
        pb.location = local[name][0]
        pb.rotation_quaternion = local[name][1]
        pb.scale = local[name][2]
    bpy.context.view_layer.update()
    apply_torso(jacob, local, src, src_bind)
    tl, pl = map_hand_target(src, src_bind, sit_hands, "l", motion)
    tr, pr = map_hand_target(src, src_bind, sit_hands, "r", motion)
    face = facing_matrix(jacob)
    ik_arm(jacob, rest_aim, local, "l", face @ tl, face @ pl, (lens[0], lens[1]))
    ik_arm(jacob, rest_aim, local, "r", face @ tr, face @ pr, (lens[2], lens[3]))
    apply_fingers(jacob, local, src, src_bind)
    bpy.context.view_layer.update()
    rots = {}
    for name in ordered:
        pb = jacob.pose.bones[name]
        rots[name] = pb.rotation_quaternion.copy()
    return rots


def write_action(name, ordered, local, rot_frames):
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
        loc, _, scale = local[bone]
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
            o.hide_render = not any(k in o.name for k in keep)
    head = world_pos(arm, "head")
    hips = world_pos(arm, "pelvis")
    cam_data = bpy.data.cameras.new("cam")
    cam = bpy.data.objects.new("cam", cam_data)
    scene.collection.objects.link(cam)
    target = (head + hips) * 0.5
    cam.location = target + Vector((1.4, -1.6, 0.35))
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x = 480
    scene.render.resolution_y = 640
    scene.render.filepath = path
    scene.display.shading.light = "STUDIO"
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam, do_unlink=True)
    bpy.data.cameras.remove(cam_data)


def main():
    jacob, ordered, local, rest_aim, sit_face, sit_hands, lens = load_jacob()
    actions = {}
    previews = {}
    for label, filename, kind in TAKES:
        path = os.path.join(SRC, filename)
        if not os.path.isfile(path):
            print("missing", path, file=sys.stderr)
            continue
        src, created = import_source(path)
        win_s, win_e, step = choose_window(src, kind)
        bpy.context.scene.frame_set(win_s)
        bpy.context.view_layer.update()
        src_bind = sample_bind(src)
        motion = 0.85 if kind == "idle" else 1.0
        rot_frames = []
        for f in range(win_s, win_e + 1, step):
            bpy.context.scene.frame_set(f)
            bpy.context.view_layer.update()
            rot_frames.append(
                pose_frame(jacob, ordered, local, rest_aim, sit_face, sit_hands, lens, src, src_bind, motion)
            )
        actions[label] = write_action(label, ordered, local, rot_frames)
        mid = rot_frames[len(rot_frames) // 2]
        previews[label] = mid
        print(f"baked {label} {len(rot_frames)}f from {filename} {win_s}-{win_e} step {step}")
        for obj in created:
            bpy.data.objects.remove(obj, do_unlink=True)
        keep = set(actions.values())
        for action in list(bpy.data.actions):
            if action not in keep:
                bpy.data.actions.remove(action)

    os.makedirs(PREVIEW, exist_ok=True)
    for label in ("IdlePoker01", "IdleBlackjack01", "Win01", "Lose01"):
        if label not in previews:
            continue
        for name in ordered:
            set_basis_quat(jacob, name, previews[label][name])
            jacob.pose.bones[name].location = local[name][0]
        bpy.context.view_layer.update()
        render_preview(jacob, os.path.join(PREVIEW, f"{label}.png"))

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
    print("exported", OUT, os.path.getsize(OUT))


if __name__ == "__main__":
    main()
