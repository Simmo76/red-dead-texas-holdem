"""Bake Mighty Cat poker + reaction clips onto Jacob's seated pose for web GLB."""
import bpy, os, math, sys
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
JACOB = os.path.join(ROOT, "Web/CinematicPoker.Web/wwwroot/models/characters/western/Jacob.glb")
SRC = os.environ.get("MIGHTY_CAT_SRC", "/tmp/reactions")
OUT = os.path.join(ROOT, "Web/CinematicPoker.Web/wwwroot/models/characters/western/mighty-cat.glb")

# Torso-only retarget: Jacob's arms/hands stay on the authored sit pose. The pack's
# poker-player clips need a full IK retarget (not done here) before they are safe.
UPPER = {
    "spine_01": "spine_01",
    "spine_02": "spine_02",
    "spine_03": "spine_03",
    "neck_01": "neck_02",
    "head": "head",
}

TAKES = [
    ("WinSmall", "AS_Reactions_Player01_Win_03.FBX"),
    ("WinBig", "AS_Reactions_Player01_Win_02.FBX"),
    ("WinBigAlt", "AS_Reactions_Player02_Win_02.FBX"),
    ("LoseSmall", "AS_Reactions_Player01_Lose_01.FBX"),
    ("LoseBig", "AS_Reactions_Player02_Lose_02.FBX"),
    ("LoseBigAlt", "AS_Reactions_Player03_Lose_02.FBX"),
    ("Wait", "AS_Reactions_Player03_WaitingOthers.FBX"),
    ("WaitHot", "AS_Reactions_Player01_WaitingOthers.FBX"),
    ("Neutral", "AS_Reactions_Player02_Neutral_01.FBX"),
    ("NeutralHot", "AS_Reactions_Player03_Neutral_01.FBX"),
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


def world_quat(arm, pb):
    return (arm.matrix_world @ pb.matrix).to_quaternion()


def bone_local_quat(arm, pb):
    if pb.parent:
        parent = arm.pose.bones[pb.parent.name]
        return (parent.matrix.inverted() @ pb.matrix).to_quaternion()
    return pb.matrix.to_quaternion()


def ang(a, b):
    q = a.conjugated() @ b
    return math.degrees(2 * math.acos(max(-1.0, min(1.0, abs(q.w)))))


def load_scene():
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
    base_world = {}
    rest_local = {}
    for name in ordered:
        pb = arm.pose.bones[name]
        if pb.rotation_mode != "QUATERNION":
            pb.rotation_mode = "QUATERNION"
        local[name] = (pb.location.copy(), pb.rotation_quaternion.copy(), pb.scale.copy())
        base_world[name] = (arm.matrix_world @ pb.matrix).copy()
        rest_local[name] = pb.bone.matrix_local.copy()
    parents = {name: arm.pose.bones[name].parent.name if arm.pose.bones[name].parent else None for name in ordered}
    arm.animation_data.action = None
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    return arm, ordered, local, base_world, rest_local, parents


def import_source(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    return next(o for o in new if o.type == "ARMATURE"), new


def choose_window(src, scene, label):
    action = src.animation_data.action
    start, end = int(action.frame_range[0]), int(action.frame_range[1])
    scene.frame_set(start)
    bpy.context.view_layer.update()
    ref = {}
    for dst, srcn in UPPER.items():
        pb = src.pose.bones.get(srcn)
        if pb:
            ref[dst] = world_quat(src, pb)
    best_f, best_s = start, -1.0
    for f in range(start, end + 1, 2):
        scene.frame_set(f)
        bpy.context.view_layer.update()
        score = 0.0
        for dst, rq in ref.items():
            pb = src.pose.bones.get(UPPER[dst])
            if pb:
                score += ang(rq, world_quat(src, pb))
        if score > best_s:
            best_s, best_f = score, f
    fps = scene.render.fps or 30
    win_s = max(start, best_f - int(round(0.45 * fps)))
    win_e = min(end, best_f + int(round(2.35 * fps)))
    if win_e <= win_s + 4:
        win_e = min(end, win_s + int(fps * 2))
    return win_s, win_e, best_f, fps


def sample_deltas(src, scene, win_s, win_e):
    """Per-bone rotation delta in parent-local space (avoids left/right world mirror bugs)."""
    scene.frame_set(win_s)
    bpy.context.view_layer.update()
    ref = {}
    for dst, srcn in UPPER.items():
        pb = src.pose.bones.get(srcn)
        if pb:
            ref[dst] = bone_local_quat(src, pb)
    frames = []
    for f in range(win_s, win_e + 1):
        scene.frame_set(f)
        bpy.context.view_layer.update()
        deltas = {}
        for dst, rq in ref.items():
            pb = src.pose.bones.get(UPPER[dst])
            if pb:
                q = bone_local_quat(src, pb)
                deltas[dst] = q @ rq.conjugated()
        frames.append(deltas)
    return frames


def locals_for_frame(deltas, ordered, local):
    rots = {}
    prev = {}
    for name in ordered:
        if name in deltas:
            sit_q = local[name][1]
            rot = sit_q @ deltas[name]
            rot.normalize()
        else:
            rot = local[name][1].copy()
        if name in prev:
            rot.make_compatible(prev[name])
        prev[name] = rot.copy()
        rots[name] = rot
    return rots


def write_action(name, ordered, local, rot_frames, loop=False):
    action = bpy.data.actions.new(name)
    n = len(rot_frames)
    last = float(n)

    def curve(path, index, values):
        fc = action.fcurves.new(path, index=index)
        fc.keyframe_points.add(len(values))
        for i, value in enumerate(values):
            kp = fc.keyframe_points[i]
            kp.co = (values[i][0], values[i][1])
            kp.interpolation = "LINEAR"
        fc.update()

    for bone in ordered:
        loc, sit_rot, scale = local[bone]
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
    if loop:
        action.use_cyclic = True
    return action


def main():
    jacob, ordered, local, base_world, rest_local, parents = load_scene()
    actions = {}
    for label, filename in TAKES:
        path = os.path.join(SRC, filename)
        if not os.path.isfile(path):
            print("missing", path, file=sys.stderr)
            continue
        src, created = import_source(path)
        win_s, win_e, peak, fps = choose_window(src, bpy.context.scene, label)
        deltas = sample_deltas(src, bpy.context.scene, win_s, win_e)
        rot_frames = [locals_for_frame(frame, ordered, local) for frame in deltas]
        loop = label == "IdleLoop"
        actions[label] = write_action(label, ordered, local, rot_frames, loop=loop)
        print(f"baked {label} from {filename} {len(rot_frames)}f window {win_s}-{win_e}")
        for obj in created:
            bpy.data.objects.remove(obj, do_unlink=True)
        keep = set(actions.values())
        for action in list(bpy.data.actions):
            if action not in keep:
                bpy.data.actions.remove(action)

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
        export_optimize_animation_size=False,
        export_skins=False,
        export_morph=False,
        export_lights=False,
        export_cameras=False,
        export_yup=True,
    )
    print("exported", OUT, os.path.getsize(OUT))


if __name__ == "__main__":
    main()
