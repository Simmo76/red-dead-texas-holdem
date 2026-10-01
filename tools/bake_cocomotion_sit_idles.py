"""Bake seated Cocomotion Idle clips onto Jacob and merge them into mighty-cat.glb.

Standing, drink-prop, and one-shot pickup/putdown takes are skipped. Hips/legs
stay on Jacob's sit; upper body is facing-space aim-copied from the Koray rig.
"""
import glob
import json
import os
import struct
import sys

import bpy
from mathutils import Matrix, Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
JACOB = os.path.join(ROOT, "Web/CinematicPoker.Web/wwwroot/models/characters/western/Jacob.glb")
OUT = os.path.join(ROOT, "Web/CinematicPoker.Web/wwwroot/models/characters/western/mighty-cat.glb")
FBX_DIR = os.environ.get("SIT_IDLE_FBX", "/tmp/board-games-pack/fbx")
TMP = "/tmp/cocomotion-sit-idles.glb"
STEP = int(os.environ.get("SIT_IDLE_STEP", "2"))

FINGER = {"thumb": "1", "index": "2", "middle": "3", "ring": "4", "pinky": "5"}
MAP = {
    "spine_01": "Spine1",
    "spine_02": "Spine2",
    "spine_03": "Spine4",
    "neck_01": "Neck",
    "clavicle_l": "LeftShoulder",
    "upperarm_l": "LeftArm",
    "lowerarm_l": "LeftForeArm",
    "hand_l": "LeftHand",
    "clavicle_r": "RightShoulder",
    "upperarm_r": "RightArm",
    "lowerarm_r": "RightForeArm",
    "hand_r": "RightHand",
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
for side, src_side in (("l", "Left"), ("r", "Right")):
    for finger, num in FINGER.items():
        MAP[f"{finger}_01_{side}"] = f"{src_side}Finger{num}Proximal"
        if finger == "thumb":
            MAP[f"{finger}_02_{side}"] = f"{src_side}Finger{num}Distal"
            CHILD[f"{finger}_01_{side}"] = f"{finger}_02_{side}"
        else:
            MAP[f"{finger}_02_{side}"] = f"{src_side}Finger{num}Medial"
            CHILD[f"{finger}_01_{side}"] = f"{finger}_02_{side}"
            CHILD[f"{finger}_02_{side}"] = f"{finger}_03_{side}"

ORDER = [
    "spine_01", "spine_02", "spine_03", "neck_01",
    "clavicle_l", "upperarm_l", "lowerarm_l", "hand_l",
    "clavicle_r", "upperarm_r", "lowerarm_r", "hand_r",
] + [n for n in MAP if n.split("_")[0] in FINGER and n in CHILD]


def clip_name(path):
    stem = os.path.splitext(os.path.basename(path))[0]
    stem = stem.replace("AS_Idle_", "Idle_").replace("_ContinuousLoop", "")
    return "".join(part[:1].upper() + part[1:] for part in stem.split("_") if part)


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


def facing_q(arm, left, right):
    x = wpos(arm, left) - wpos(arm, right)
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
    parent_world = arm.matrix_world if pb.parent is None else (
        arm.matrix_world @ pose_arm_matrix(arm, pb.parent.name, cache)
    )
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
    s_face = facing_q(src, "LeftShoulder", "RightShoulder")
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
        loc, _, scale = sit_local[bone]
        for index in range(3):
            curve(f'pose.bones["{bone}"].location', index, [(1.0, loc[index]), (last, loc[index])])
            curve(f'pose.bones["{bone}"].scale', index, [(1.0, scale[index]), (last, scale[index])])
        samples = [frame[bone] for frame in rot_frames]
        for i in range(1, len(samples)):
            samples[i].make_compatible(samples[i - 1])
        path = f'pose.bones["{bone}"].rotation_quaternion'
        for index in range(4):
            curve(path, index, [(float(i + 1), samples[i][index]) for i in range(n)])
    action.frame_range = (1, last)
    return action


def read_glb(path):
    data = open(path, "rb").read()
    off = 12
    gltf, blob = None, b""
    while off < len(data):
        clen, ctype = struct.unpack_from("<II", data, off)
        chunk = data[off + 8:off + 8 + clen]
        if ctype == 0x4E4F534A:
            gltf = json.loads(chunk)
        elif ctype == 0x004E4942:
            blob = chunk
        off += 8 + clen
    return gltf, blob


def write_glb(path, gltf, blob):
    raw = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    raw += b" " * ((4 - len(raw) % 4) % 4)
    blob += b"\x00" * ((4 - (len(blob) % 4)) % 4)
    gltf["buffers"][0]["byteLength"] = len(blob)
    raw = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    raw += b" " * ((4 - len(raw) % 4) % 4)
    total = 12 + 8 + len(raw) + 8 + len(blob)
    out = bytearray()
    out += struct.pack("<III", 0x46546C67, 2, total)
    out += struct.pack("<II", len(raw), 0x4E4F534A)
    out += raw
    out += struct.pack("<II", len(blob), 0x004E4942)
    out += blob
    open(path, "wb").write(out)


def merge_clip(dst_path, src_path, clip_name):
    dst, dbin = read_glb(dst_path)
    src, sbin = read_glb(src_path)
    anim = next(a for a in src["animations"] if a.get("name") == clip_name)
    dst_nodes = {n.get("name"): i for i, n in enumerate(dst.get("nodes", []))}
    src_nodes = {i: n.get("name") for i, n in enumerate(src.get("nodes", []))}
    dbin = bytearray(dbin)
    acc_map, bv_map = {}, {}

    def copy_accessor(idx):
        if idx in acc_map:
            return acc_map[idx]
        acc = json.loads(json.dumps(src["accessors"][idx]))
        bvi = acc.get("bufferView")
        if bvi is not None:
            if bvi not in bv_map:
                bv = json.loads(json.dumps(src["bufferViews"][bvi]))
                start = bv.get("byteOffset", 0)
                chunk = sbin[start:start + bv["byteLength"]]
                pad = (4 - (len(dbin) % 4)) % 4
                dbin.extend(b"\x00" * pad)
                bv["byteOffset"] = len(dbin)
                bv["buffer"] = 0
                dbin.extend(chunk)
                dst.setdefault("bufferViews", []).append(bv)
                bv_map[bvi] = len(dst["bufferViews"]) - 1
            acc["bufferView"] = bv_map[bvi]
        dst.setdefault("accessors", []).append(acc)
        acc_map[idx] = len(dst["accessors"]) - 1
        return acc_map[idx]

    new_samplers = []
    for samp in anim.get("samplers", []):
        new_samplers.append({
            "input": copy_accessor(samp["input"]),
            "output": copy_accessor(samp["output"]),
            "interpolation": samp.get("interpolation", "LINEAR"),
        })
    new_channels = []
    for ch in anim.get("channels", []):
        node = ch.get("target", {}).get("node")
        name = src_nodes.get(node)
        if name not in dst_nodes:
            continue
        new_channels.append({
            "sampler": ch["sampler"],
            "target": {"node": dst_nodes[name], "path": ch["target"]["path"]},
        })
    dst.setdefault("animations", [])
    dst["animations"] = [a for a in dst["animations"] if a.get("name") != clip_name]
    dst["animations"].append({
        "name": clip_name,
        "samplers": new_samplers,
        "channels": new_channels,
    })
    dst["buffers"][0]["byteLength"] = len(dbin)
    write_glb(dst_path, dst, bytes(dbin))


def bake_one(jacob, ordered, sit_local, j_face, fbx_path):
    name = clip_name(fbx_path)
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=fbx_path)
    src = next(o for o in bpy.data.objects if o not in before and o.type == "ARMATURE")
    act = src.animation_data.action
    start, end = int(act.frame_range[0]), int(act.frame_range[1])
    rot_frames = []
    for f in range(start, end + 1, STEP):
        bpy.context.scene.frame_set(f)
        bpy.context.view_layer.update()
        reset_sit(jacob, sit_local)
        apply_aim(jacob, src, j_face)
        rots = {}
        for bone in ordered:
            rots[bone] = jacob.pose.bones[bone].rotation_quaternion.copy()
        rot_frames.append(rots)
    action = write_action(name, ordered, sit_local, rot_frames)
    action.id_root = "OBJECT"
    for obj in [o for o in list(bpy.data.objects) if o not in before]:
        bpy.data.objects.remove(obj, do_unlink=True)
    print(f"baked {name} {len(rot_frames)}f {start}-{end} step {STEP}", flush=True)
    return action


def main():
    files = sorted(glob.glob(os.path.join(FBX_DIR, "AS_Idle_*.fbx")))
    skip = {
        "AS_Idle_Stand_01.fbx",
        "AS_Idle_Host_Stand_01.fbx",
        "AS_Idle_Host_01.fbx",
        "AS_Idle_PickUpCards_01.fbx",
        "AS_Idle_PickUpCards_02.fbx",
        "AS_Idle_PickUpCards_03.fbx",
        "AS_Idle_PickUpHoldCards_01.fbx",
        "AS_Idle_PickUpHoldCards_02.fbx",
        "AS_Idle_PutDownCards_01.fbx",
        "AS_Idle_PutDownCards_03.fbx",
        "AS_Idle_HoldReleaseCards_01.fbx",
        "AS_Idle_Sit_Drinking_01_ContinuousLoop.fbx",
        "AS_Idle_Sit_HoldDrink_01.fbx",
        "AS_Idle_Sit_SipDrink_01.fbx",
        "AS_Idle_Sit_Chatting_01.fbx",
        "AS_Idle_Sit_Chatting_02.fbx",
        "AS_Idle_Sit_Chatting_03.fbx",
    }
    files = [f for f in files if os.path.basename(f) not in skip]
    if not files:
        print("no sit idle FBX in", FBX_DIR, file=sys.stderr)
        sys.exit(1)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=JACOB)
    jacob = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    sit = next(a for a in bpy.data.actions if a.name.startswith("Sit_"))
    jacob.animation_data.action = sit
    bpy.context.scene.frame_set(int(sit.frame_range[0]) + 2)
    bpy.context.view_layer.update()
    ordered = topo_names(jacob)
    sit_local = capture_local(jacob)
    j_face = facing_q(jacob, "clavicle_l", "clavicle_r")
    jacob.animation_data.action = None
    for track in list(jacob.animation_data.nla_tracks):
        jacob.animation_data.nla_tracks.remove(track)
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)

    baked = []
    for fbx in files:
        baked.append(bake_one(jacob, ordered, sit_local, j_face, fbx))

    if jacob.animation_data is None:
        jacob.animation_data_create()
    while jacob.animation_data.nla_tracks:
        jacob.animation_data.nla_tracks.remove(jacob.animation_data.nla_tracks[0])
    jacob.animation_data.action = None
    for action in baked:
        track = jacob.animation_data.nla_tracks.new()
        track.name = action.name
        strip = track.strips.new(action.name, 1, action)
        strip.name = action.name

    for obj in list(bpy.data.objects):
        if obj != jacob:
            bpy.data.objects.remove(obj, do_unlink=True)
    bpy.ops.object.select_all(action="DESELECT")
    jacob.select_set(True)
    bpy.context.view_layer.objects.active = jacob
    bpy.ops.export_scene.gltf(
        filepath=TMP,
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
    print("exported", TMP, os.path.getsize(TMP), flush=True)
    src, _ = read_glb(TMP)
    names = [a.get("name") for a in src.get("animations", [])]
    for name in names:
        merge_clip(OUT, TMP, name)
        print("merged", name, flush=True)
    dst, _ = read_glb(OUT)
    print("final", OUT, os.path.getsize(OUT), [a.get("name") for a in dst.get("animations", [])], flush=True)


if __name__ == "__main__":
    main()
