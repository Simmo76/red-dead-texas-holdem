#!/usr/bin/env python3
"""Bake the Street Fighter table scene for the web client.

Reads the cartoon MMA arena showroom (featuring the UFC-style octagon,
supplied as ufc_octagon.unitypackage) and the two Arcade Fighter characters (plus
their texture variations) out of an unpacked Unity export, retargets the
existing poker animation clips onto those skeletons, and writes:

  wwwroot/models/env/street-arena.glb
  wwwroot/models/characters/street/Fighter{1,2}.glb
  wwwroot/models/characters/street/Fighter{1,2}-variation.glb

The poker clips keep their names (IdleHoldCards*, IdleSit*, Win*, Lose*,
Bet, Check, Fold, Talk, ...) so the scene player can drive the new bodies
with the same catalogue. Only the meshes change.

Environment:
  SF_ASSETS   unpacked Assets tree (default /tmp/sf-assets)
  SF_PREVIEW  if set, write a couple of PNG checks there
"""
from __future__ import annotations

import os
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
ASSETS = os.environ.get("SF_ASSETS", "/tmp/sf-assets")
WEB = os.path.join(ROOT, "Web/CinematicPoker.Web/wwwroot/models")
ARENA_OUT = os.path.join(WEB, "env/street-arena.glb")
CHAR_DIR = os.path.join(WEB, "characters/street")
JACOB = os.path.join(WEB, "characters/western/Jacob.glb")
MIGHTY = os.path.join(WEB, "characters/western/mighty-cat.glb")
PREVIEW = os.environ.get("SF_PREVIEW", "")

# cm -> m, then enlarge so the featured arena clears the poker seats
# (seat radius 1.74 m). At 0.018 the octagon cage lands ~2.0 m from centre in
# the GLB (~4.4 m once the scene scales it), same room the old ring ropes had.
ARENA_SCALE = 0.018
# The pack is a showroom of six arenas. The featured one is the UFC-style
# octagon (mesh 1, supplied as ufc_octagon.unitypackage); its cage mesh gives
# the true play-area centre because mesh 1's bbox also covers the entry ramp.
RING_MESH = "cartoon boxing arena 1"
CAGE_MESH = "cartoon boxing arena 1_Fecen"
# Venue dressing modelled around showroom slot 5 (where the boxing ring used
# to be featured): the crowd stands, the entrance barrier, and the coloured
# spotlight beams. Re-anchor these around the octagon with the same offsets
# they had from slot 5, so the crowd still sits across from the player.
VENUE_MESHES = {
    " cartoon boxing arena_stands fence",
    "cartoon boxing arena_ stands",
    "cartoon boxing arena_fence ",
    "Object005",
    "cartoon boxing arena_high light _1",
    "cartoon boxing arena_high light _2",
    "cartoon boxing arena_high light _3",
    "cartoon boxing arena_high light _4",
    "cartoon boxing arena_high light _5",
    "cartoon boxing arena_high light _6",
}
VENUE_ANCHOR = "cartoon boxing arena 5"

RENAME = {
    "Pelvis": "pelvis",
    "Spine": "spine_01",
    "Spine1": "spine_02",
    "Spine2": "spine_03",
    "Neck": "neck_01",
    "Head": "head",
    "L Clavicle": "clavicle_l",
    "L UpperArm": "upperarm_l",
    "L Forearm": "lowerarm_l",
    "L Hand": "hand_l",
    "R Clavicle": "clavicle_r",
    "R UpperArm": "upperarm_r",
    "R Forearm": "lowerarm_r",
    "R Hand": "hand_r",
    "L Thigh": "thigh_l",
    "L Calf": "calf_l",
    "L Foot": "foot_l",
    "L Toe0": "ball_l",
    "R Thigh": "thigh_r",
    "R Calf": "calf_r",
    "R Foot": "foot_r",
    "R Toe0": "ball_r",
    "L Finger0": "thumb_01_l",
    "L Finger01": "thumb_02_l",
    "L Finger02": "thumb_03_l",
    "L Finger1": "index_01_l",
    "L Finger11": "index_02_l",
    "L Finger12": "index_03_l",
    "L Finger2": "middle_01_l",
    "L Finger21": "middle_02_l",
    "L Finger22": "middle_03_l",
    "L Finger3": "ring_01_l",
    "L Finger31": "ring_02_l",
    "L Finger32": "ring_03_l",
    "L Finger4": "pinky_01_l",
    "L Finger41": "pinky_02_l",
    "L Finger42": "pinky_03_l",
    "R Finger0": "thumb_01_r",
    "R Finger01": "thumb_02_r",
    "R Finger02": "thumb_03_r",
    "R Finger1": "index_01_r",
    "R Finger11": "index_02_r",
    "R Finger12": "index_03_r",
    "R Finger2": "middle_01_r",
    "R Finger21": "middle_02_r",
    "R Finger22": "middle_03_r",
    "R Finger3": "ring_01_r",
    "R Finger31": "ring_02_r",
    "R Finger32": "ring_03_r",
    "R Finger4": "pinky_01_r",
    "R Finger41": "pinky_02_r",
    "R Finger42": "pinky_03_r",
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
    "thigh_l": "calf_l",
    "calf_l": "foot_l",
    "foot_l": "ball_l",
    "thigh_r": "calf_r",
    "calf_r": "foot_r",
    "foot_r": "ball_r",
}
for side in ("l", "r"):
    for finger in ("thumb", "index", "middle", "ring", "pinky"):
        CHILD[f"{finger}_01_{side}"] = f"{finger}_02_{side}"
        CHILD[f"{finger}_02_{side}"] = f"{finger}_03_{side}"

AIM_ORDER = [
    "spine_01", "spine_02", "spine_03", "neck_01",
    "clavicle_l", "upperarm_l", "lowerarm_l", "hand_l",
    "clavicle_r", "upperarm_r", "lowerarm_r", "hand_r",
    "thigh_l", "calf_l", "foot_l",
    "thigh_r", "calf_r", "foot_r",
] + [n for n in CHILD if n.split("_")[0] in ("thumb", "index", "middle", "ring", "pinky")]

PREFIXES = (
    "Fighter1 Variation ",
    "Fighter2 Variation ",
    "Fighter1 ",
    "Fighter2 ",
)


def reset_file():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = 30


def suffix_of(name: str):
    for prefix in PREFIXES:
        if name.startswith(prefix):
            return name[len(prefix):]
    return None


def rename_fighter(arm, mesh):
    for bone in list(arm.data.bones):
        suf = suffix_of(bone.name)
        new = RENAME.get(suf) if suf else None
        if not new:
            continue
        old = bone.name
        bone.name = new
        if mesh and old in mesh.vertex_groups:
            mesh.vertex_groups[old].name = new
    if "pelvis" not in arm.data.bones:
        raise RuntimeError(f"{arm.name} has no pelvis after rename: {[b.name for b in arm.data.bones]}")


def import_fbx(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    created = [o for o in bpy.data.objects if o not in before]
    arm = next(o for o in created if o.type == "ARMATURE")
    mesh = next(o for o in created if o.type == "MESH")
    return arm, mesh


# Clips the table actually plays. Blackjack/poker extras in the mood file are
# not selected at runtime, so they are not baked onto the fighters.
MOOD_CLIPS = [
    "IdleHoldCards01", "IdleHoldCards02", "IdleHoldCards03",
    "IdleSit01", "IdleSit02", "IdleSit03", "IdleSit04", "IdleSit05", "IdleSit06",
    "IdleTableObserveLeft01", "IdleTableObserveRight01",
    "IdleTableObserveLeftRight01", "IdleTableProtectCards01",
    "IdleSitThinking01", "IdleSitArmsFolded01",
    "Win01", "Win02", "Win03",
    "Lose01", "Lose02", "Lose03", "Lose04",
]


class ArmSource:
    """Jacob-style skinned armature. Actions are named '<clip>_root.001'."""

    def __init__(self, arm, actions):
        self.arm = arm
        self.actions = {}
        for action in actions:
            clean = action.name[:-len("_root.001")] if action.name.endswith("_root.001") else action.name
            self.actions[clean] = action
        if arm.animation_data is None:
            arm.animation_data_create()

    def names(self):
        return list(self.actions)

    def has(self, name):
        return name in self.arm.pose.bones

    def set_clip(self, name):
        self.arm.animation_data.action = self.actions[name]
        return self.actions[name].frame_range

    def wpos(self, name):
        return (self.arm.matrix_world @ self.arm.pose.bones[name].matrix).to_translation()

    def wq(self, name):
        return (self.arm.matrix_world @ self.arm.pose.bones[name].matrix).to_quaternion()


class EmptySource:
    """Mighty-cat mood file imports as animated empties, one action per bone."""

    def __init__(self, objs):
        self.objs = objs
        self.actions = {}
        pelvis = objs.get("pelvis")
        if pelvis and pelvis.animation_data:
            for track in pelvis.animation_data.nla_tracks:
                if track.strips:
                    self.actions[track.name] = track.strips[0].action
                track.mute = True
        for obj in objs.values():
            if obj.animation_data:
                for track in obj.animation_data.nla_tracks:
                    track.mute = True

    def names(self):
        return [n for n in MOOD_CLIPS if n in self.actions]

    def has(self, name):
        return name in self.objs

    def set_clip(self, name):
        action = None
        for bone, obj in self.objs.items():
            act = bpy.data.actions.get(f"{name}_{bone}")
            if obj.animation_data is None:
                obj.animation_data_create()
            obj.animation_data.action = act
            if bone == "pelvis":
                action = act
        if action is None:
            raise RuntimeError(f"missing mood clip {name}")
        return action.frame_range

    def wpos(self, name):
        return self.objs[name].matrix_world.to_translation()

    def wq(self, name):
        return self.objs[name].matrix_world.to_quaternion()


def import_sources():
    before_obj = set(bpy.data.objects)
    before_act = set(bpy.data.actions)
    bpy.ops.import_scene.gltf(filepath=JACOB)
    jacob_objs = [o for o in bpy.data.objects if o not in before_obj]
    jacob_acts = [a for a in bpy.data.actions if a not in before_act]
    jacob_arm = next(o for o in jacob_objs if o.type == "ARMATURE")
    for obj in jacob_objs:
        if obj.type == "MESH":
            bpy.data.objects.remove(obj, do_unlink=True)
    jacob = ArmSource(jacob_arm, jacob_acts)
    print("jacob clips", jacob.names())

    before_obj = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=MIGHTY)
    mood_objs = {o.name: o for o in bpy.data.objects if o not in before_obj and o.type == "EMPTY"}
    mood = EmptySource(mood_objs)
    print("mood clips", mood.names())
    return jacob, mood


def facing_q(source, left="clavicle_l", right="clavicle_r"):
    # `source` is either an ArmSource/EmptySource or a raw armature (target rest).
    def pos(name):
        if hasattr(source, "wpos"):
            return source.wpos(name)
        return (source.matrix_world @ source.pose.bones[name].matrix).to_translation()

    x = pos(left) - pos(right)
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


def reset_pose(arm):
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = (1, 0, 0, 0)
        pb.location = (0, 0, 0)
        pb.scale = (1, 1, 1)


class ArmView:
    """Same wpos/wq/has surface as the clip sources, for the target armature."""

    def __init__(self, arm):
        self.arm = arm

    def has(self, name):
        return name in self.arm.pose.bones

    def wpos(self, name):
        return (self.arm.matrix_world @ self.arm.pose.bones[name].matrix).to_translation()

    def wq(self, name):
        return (self.arm.matrix_world @ self.arm.pose.bones[name].matrix).to_quaternion()


def _cols(x, y, z):
    m = Matrix.Identity(3)
    m[0][0], m[1][0], m[2][0] = x
    m[0][1], m[1][1], m[2][1] = y
    m[0][2], m[1][2], m[2][2] = z
    return m


def orient_quat(local_a, local_b, world_a, world_b):
    """Quaternion mapping two local axes onto two world directions.

    Both pairs are orthonormalised into a right-handed basis, so a rig whose
    bone axes don't match the poker skeleton still carries the mesh the same
    way (face forward, knee forward) instead of copying the raw bone roll.
    """
    a = local_a.normalized()
    b = local_b - a * local_b.dot(a)
    if b.length < 1e-5 or world_a.length < 1e-6:
        return None
    b.normalize()
    c = a.cross(b).normalized()
    wa = world_a.normalized()
    wb = world_b - wa * world_b.dot(wa)
    if wb.length < 1e-5:
        return None
    wb.normalize()
    wc = wa.cross(wb).normalized()
    return (_cols(wa, wb, wc) @ _cols(a, b, c).inverted()).to_quaternion()


def capture_rest(src, face):
    """Per-bone directions that pointed along character-forward / up at rest.

    `aim` is the rest direction toward the child, in bone-local space. `pole`
    is the part of character-forward perpendicular to that aim, so it records
    the twist (which way the knee, sole, or palm faced).
    """
    forward = face @ Vector((0, 1, 0))
    up = Vector((0, 0, 1))
    names = set(CHILD) | set(CHILD.values()) | {"pelvis", "head"}
    out = {}
    for name in names:
        if not src.has(name):
            continue
        q = src.wq(name)
        fwd_l = q.inverted() @ forward
        up_l = q.inverted() @ up
        rec = {"fwd": fwd_l, "up": up_l}
        child = CHILD.get(name)
        if child and src.has(child):
            aim_w = src.wpos(child) - src.wpos(name)
            if aim_w.length > 1e-6:
                aim_l = (q.inverted() @ aim_w).normalized()
                pole = fwd_l - aim_l * fwd_l.dot(aim_l)
                if pole.length < 0.25:
                    pole = up_l - aim_l * up_l.dot(aim_l)
                if pole.length > 1e-4:
                    rec["aim"] = aim_l
                    rec["pole"] = pole.normalized()
        out[name] = rec
    return out


def _in_target_space(vec, s_face, t_face):
    return t_face @ (s_face.inverted() @ vec)


# Bones whose mood-file roll does not match this mesh. The limb direction still
# comes from the poker clip; the twist is taken from the fighter mesh so the
# kneecap stays forward and the sole stays down.
MESH_POLE_WORLD = {
    "thigh_l": "forward",
    "thigh_r": "forward",
    "calf_l": "forward",
    "calf_r": "forward",
    "foot_l": "down",
    "foot_r": "down",
}


def capture_mesh_hints(arm, mesh):
    """Bone-local direction toward the kneecap or the sole, from the bind mesh."""
    groups = {g.name: g.index for g in mesh.vertex_groups}
    hints = {}
    for name, mode in MESH_POLE_WORLD.items():
        idx = groups.get(name)
        if idx is None or name not in arm.pose.bones:
            continue
        pts = []
        for vert in mesh.data.vertices:
            weight = next((g.weight for g in vert.groups if g.group == idx), 0.0)
            if weight > 0.4:
                pts.append(mesh.matrix_world @ vert.co)
        if len(pts) < 8:
            continue
        bone_m = arm.matrix_world @ arm.pose.bones[name].matrix
        bone_q = bone_m.to_quaternion()
        bone_p = bone_m.to_translation()
        if mode == "down":
            pts.sort(key=lambda p: p.z)
        else:
            pts.sort(key=lambda p: -p.y)
        sample = pts[:max(8, len(pts) // 5)]
        avg = sum(sample, Vector((0.0, 0.0, 0.0))) / len(sample)
        local = bone_q.inverted() @ (avg - bone_p)
        if local.length > 1e-4:
            hints[name] = local.normalized()
    return hints


def apply_frame(src, tgt, t_face, src_rest, tgt_rest, mesh_hints=None):
    reset_pose(tgt)
    s_face = facing_q(src)
    cache = {}

    def put_orientation(name):
        if name not in src_rest or name not in tgt_rest or not src.has(name):
            return
        if name not in tgt.pose.bones:
            return
        s_q = src.wq(name)
        world_fwd = _in_target_space(s_q @ src_rest[name]["fwd"], s_face, t_face)
        world_up = _in_target_space(s_q @ src_rest[name]["up"], s_face, t_face)
        q = orient_quat(tgt_rest[name]["fwd"], tgt_rest[name]["up"], world_fwd, world_up)
        if q is not None:
            set_world_q(tgt, name, q, cache)

    # Hips first, in mesh space. Copying the poker bone quaternion straight
    # across yaws this skeleton about 90° because its pelvis axes differ.
    put_orientation("pelvis")
    for name in AIM_ORDER:
        child = CHILD.get(name)
        if not child or name not in tgt.pose.bones or child not in tgt.pose.bones:
            continue
        if not src.has(name) or not src.has(child):
            continue
        src_aim = src.wpos(child) - src.wpos(name)
        if src_aim.length < 1e-6:
            continue
        world_dir = _in_target_space(src_aim.normalized(), s_face, t_face)
        s_rec = src_rest.get(name) or {}
        t_rec = tgt_rest.get(name) or {}
        q = None
        hint = (mesh_hints or {}).get(name)
        if hint is not None and "aim" in t_rec:
            kind = MESH_POLE_WORLD.get(name)
            world_pole = Vector((0.0, 0.0, -1.0)) if kind == "down" else (t_face @ Vector((0.0, 1.0, 0.0)))
            q = orient_quat(t_rec["aim"], hint, world_dir, world_pole)
        elif "pole" in s_rec and "aim" in t_rec and "pole" in t_rec:
            world_pole = _in_target_space(src.wq(name) @ s_rec["pole"], s_face, t_face)
            q = orient_quat(t_rec["aim"], t_rec["pole"], world_dir, world_pole)
        if q is None:
            la = t_rec.get("aim")
            if la is None:
                continue
            cur = (tgt.matrix_world @ pose_arm_matrix(tgt, name, cache)).to_quaternion()
            q = (cur @ la).rotation_difference(world_dir) @ cur
        set_world_q(tgt, name, q, cache)
    # Head after the neck. Posing it earlier leaves the look stuck to the
    # rest neck, so the face yaws off to the side once the neck aims.
    put_orientation("head")
    rots = {}
    for pb in tgt.pose.bones:
        if pb.rotation_mode != "QUATERNION":
            continue
        rots[pb.name] = pb.rotation_quaternion.copy()
    return rots


def write_action(name, frames, tracks):
    action = bpy.data.actions.new(name)
    for bone, series in tracks.items():
        path = f'pose.bones["{bone}"].rotation_quaternion'
        # mathutils Quaternion is w, x, y, z and fcurve index follows that.
        for index in range(4):
            fc = action.fcurves.new(path, index=index)
            fc.keyframe_points.add(len(frames))
            prev = None
            for k, (frame, quat) in enumerate(zip(frames, series)):
                q = quat.copy()
                if prev is not None and prev.dot(q) < 0:
                    q.negate()
                fc.keyframe_points[k].co = (frame, q[index])
                fc.keyframe_points[k].interpolation = "LINEAR"
                prev = q
    return action


def bake_character(fbx_path, out_path):
    reset_file()
    arm, mesh = import_fbx(fbx_path)
    rename_fighter(arm, mesh)
    # The FBX ships a fighting idle. Leaving it on the armature makes the
    # exporter sample that clip instead of the poker actions we bake.
    if arm.animation_data:
        arm.animation_data_clear()
    bpy.ops.object.select_all(action="DESELECT")
    mesh.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    # One material slot, albedo already wired by the FBX importer.
    for mat in mesh.data.materials:
        if not mat or not mat.use_nodes:
            continue
        bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if not bsdf:
            continue
        if "Roughness" in bsdf.inputs:
            bsdf.inputs["Roughness"].default_value = max(bsdf.inputs["Roughness"].default_value, 0.55)
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = 0.0

    jacob, mood = import_sources()
    reset_pose(arm)
    bpy.context.view_layer.update()
    t_face = facing_q(arm)
    tgt_rest = capture_rest(ArmView(arm), t_face)
    mesh_hints = capture_mesh_hints(arm, mesh)

    # Rest axes are the mesh bind. Capture them with no clip posed, then the
    # frame loop assigns actions on top.
    if jacob.arm.animation_data:
        jacob.arm.animation_data.action = None
    reset_pose(jacob.arm)
    for obj in mood.objs.values():
        if obj.animation_data:
            obj.animation_data.action = None
    bpy.context.view_layer.update()
    jacob_rest = capture_rest(jacob, facing_q(jacob))
    mood_rest = capture_rest(mood, facing_q(mood))
    rest_for = {id(jacob): jacob_rest, id(mood): mood_rest}

    mapped = [n for n in (["pelvis", "head"] + AIM_ORDER) if n in arm.pose.bones]
    scene = bpy.context.scene
    baked = []
    jobs = [(jacob, n) for n in jacob.names()] + [(mood, n) for n in mood.names()]
    for src, clean in jobs:
        frame_range = src.set_clip(clean)
        start = max(1, int(round(frame_range[0])))
        end = int(round(frame_range[1]))
        if end < start:
            start, end = end, start
        frames = list(range(start, end + 1))
        span = frames[-1] - frames[0] if frames else 0
        if span > 240:
            frames = frames[::3]
        elif span > 120:
            frames = frames[::2]
        tracks = {n: [] for n in mapped}
        for frame in frames:
            scene.frame_set(frame)
            bpy.context.view_layer.update()
            rots = apply_frame(src, arm, t_face, rest_for[id(src)], tgt_rest, mesh_hints)
            for n in mapped:
                tracks[n].append(rots.get(n, Quaternion((1, 0, 0, 0))))
        action = write_action(clean, frames, tracks)
        action.use_fake_user = True
        try:
            action.id_root = "OBJECT"
        except Exception:
            pass
        baked.append(clean)
        print(f"  baked {clean} ({len(frames)}f)", flush=True)

    keep = set(baked)
    for obj in list(bpy.data.objects):
        if obj is arm or obj is mesh:
            continue
        bpy.data.objects.remove(obj, do_unlink=True)
    for action in list(bpy.data.actions):
        if action.name not in keep:
            bpy.data.actions.remove(action)
    if arm.animation_data is None:
        arm.animation_data_create()
    # The glTF exporter only writes actions that are the active action, a
    # single-strip NLA track, or an OBJECT-rooted armature action. Stash each
    # clip on its own track so every poker take survives under its real name.
    arm.animation_data.action = None
    for name in baked:
        action = bpy.data.actions.get(name)
        if action is None:
            continue
        track = arm.animation_data.nla_tracks.new()
        track.name = name
        strip = track.strips.new(name, 1, action)
        strip.name = name
    scene.frame_start = 1
    scene.frame_end = 500

    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.gltf(
        filepath=out_path,
        export_format="GLB",
        use_selection=True,
        export_animations=True,
        export_animation_mode="ACTIONS",
        export_skins=True,
        export_yup=True,
        export_apply=False,
    )
    print("wrote", out_path, "clips", baked)
    if PREVIEW and "Fighter1.glb" in out_path.replace("\\", "/").split("/")[-1]:
        render_fighter_preview(arm, mesh)


def render_fighter_preview(arm, mesh):
    os.makedirs(PREVIEW, exist_ok=True)
    scene = bpy.context.scene
    action = bpy.data.actions.get("IdleHoldCards01") or bpy.data.actions.get("IdleSit01")
    if action and arm.animation_data:
        arm.animation_data.action = action
        scene.frame_set(int(action.frame_range[0] + action.frame_range[1]) // 2)
    bpy.context.view_layer.update()
    light = bpy.data.lights.new("key", "AREA")
    light.energy = 400
    lo = bpy.data.objects.new("key", light)
    bpy.context.collection.objects.link(lo)
    lo.location = (1.5, -2.2, 2.4)
    cam_data = bpy.data.cameras.new("cam")
    cam = bpy.data.objects.new("cam", cam_data)
    bpy.context.collection.objects.link(cam)
    cam.location = (1.1, -2.4, 1.35)
    look = Vector((0, 0, 1.0))
    cam.rotation_euler = (look - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 640
    scene.render.resolution_y = 480
    scene.render.filepath = os.path.join(PREVIEW, "fighter-sit.png")
    bpy.ops.render.render(write_still=True)
    print("preview", scene.render.filepath)


def world_bbox(obj):
    mins = Vector((1e9, 1e9, 1e9))
    maxs = Vector((-1e9, -1e9, -1e9))
    for corner in obj.bound_box:
        w = obj.matrix_world @ Vector(corner)
        mins = Vector((min(mins.x, w.x), min(mins.y, w.y), min(mins.z, w.z)))
        maxs = Vector((max(maxs.x, w.x), max(maxs.y, w.y), max(maxs.z, w.z)))
    return mins, maxs


def arena_texture(mat_name: str):
    folder = os.path.join(ASSETS, "cartoon boxing arena")
    key = " ".join(mat_name.strip().lower().split())
    table = {
        "cartoon boxing arena 1": "cartoon boxing arena 1.png",
        "cartoon boxing arena 2": "cartoon boxing arena 2.png",
        "cartoon boxing arena 3": "cartoon boxing arena 2.png",
        "cartoon boxing arena 4": "cartoon boxing arena 4.png",
        "cartoon boxing arena 5": "cartoon boxing arena 5.png",
        "cartoon boxing arena 6": "cartoon boxing arena 6.png",
        "cartoon boxing arena _led": "cartoon boxing arena_led.png",
        "cartoon boxing arena_ stands": "cartoon boxing arena_stands.png",
        "cartoon boxing arena_stands fence": "cartoon boxing arena_stands fence.png",
    }
    if key in table:
        return os.path.join(folder, table[key])
    if "high light" in key:
        digits = "".join(ch for ch in key if ch.isdigit()) or "1"
        path = os.path.join(folder, f"cartoon boxing arena_high light _{digits}.png")
        if not os.path.exists(path):
            path = os.path.join(folder, "cartoon boxing arena_high light _1.png")
        return path
    if "led" in key:
        return os.path.join(folder, "cartoon boxing arena_led.png")
    if "stand" in key and "fence" in key:
        return os.path.join(folder, "cartoon boxing arena_stands fence.png")
    if "stand" in key:
        return os.path.join(folder, "cartoon boxing arena_stands.png")
    return os.path.join(folder, "cartoon boxing arena 1.png")


def assign_arena_material(mat):
    path = arena_texture(mat.name)
    if not os.path.exists(path):
        print("missing texture", mat.name, path)
        return
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None:
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
        nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(path)
    tex.image.colorspace_settings.name = "sRGB"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if "Roughness" in bsdf.inputs:
        bsdf.inputs["Roughness"].default_value = 0.82
    if "Metallic" in bsdf.inputs:
        bsdf.inputs["Metallic"].default_value = 0.0
    glow = any(token in mat.name.lower() for token in ("led", "light"))
    if glow and "Emission Color" in bsdf.inputs and "Emission Strength" in bsdf.inputs:
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = 4.0


def bake_arena():
    reset_file()
    bpy.ops.import_scene.fbx(filepath=os.path.join(ASSETS, "cartoon boxing arena/cartoon boxing arena.FBX"))
    ring = bpy.data.objects.get(RING_MESH)
    cage = bpy.data.objects.get(CAGE_MESH)
    if ring is None or cage is None:
        raise RuntimeError("featured octagon mesh missing")
    mins, maxs = world_bbox(ring)
    cage_mins, cage_maxs = world_bbox(cage)
    center = (cage_mins + cage_maxs) / 2
    print("octagon center cm", tuple(round(v, 1) for v in center), "size", tuple(round(v, 1) for v in (maxs - mins)))

    anchor = bpy.data.objects.get(VENUE_ANCHOR)
    if anchor is None:
        raise RuntimeError("venue anchor mesh missing")
    a_mins, a_maxs = world_bbox(anchor)
    a_center = (a_mins + a_maxs) / 2

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    shift = Matrix.Translation((-center.x, -center.y, -mins.z))
    venue_shift = Matrix.Translation((-a_center.x, -a_center.y, -a_mins.z))
    scale = Matrix.Diagonal((ARENA_SCALE, ARENA_SCALE, ARENA_SCALE, 1))
    for obj in list(bpy.data.objects):
        local = venue_shift if obj.name in VENUE_MESHES else shift
        obj.matrix_world = (scale @ local) @ obj.matrix_world
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    for mat in bpy.data.materials:
        assign_arena_material(mat)

    # Drop empty non-mesh helpers.
    for obj in list(bpy.data.objects):
        if obj.type not in ("MESH",):
            bpy.data.objects.remove(obj, do_unlink=True)

    os.makedirs(os.path.dirname(ARENA_OUT), exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=ARENA_OUT,
        export_format="GLB",
        export_animations=False,
        export_yup=True,
        export_apply=False,
    )
    print("wrote", ARENA_OUT)
    if PREVIEW:
        os.makedirs(PREVIEW, exist_ok=True)
        scene = bpy.context.scene
        light = bpy.data.lights.new("key", "SUN")
        light.energy = 3
        lo = bpy.data.objects.new("key", light)
        bpy.context.collection.objects.link(lo)
        lo.rotation_euler = (0.9, 0.2, 0.4)
        cam_data = bpy.data.cameras.new("cam")
        cam_data.lens = 28
        cam = bpy.data.objects.new("cam", cam_data)
        bpy.context.collection.objects.link(cam)
        # Player sits on local -Y in this file (stands were +Y). Matches the
        # glTF yaw: blender +Y becomes glTF -Z, across the table.
        cam.location = (0.4, -3.4, 1.7)
        look = Vector((0, 0.5, 1.1))
        cam.rotation_euler = (look - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.camera = cam
        scene.render.engine = "BLENDER_EEVEE"
        scene.render.resolution_x = 768
        scene.render.resolution_y = 432
        scene.render.filepath = os.path.join(PREVIEW, "arena.png")
        bpy.ops.render.render(write_still=True)
        print("preview", scene.render.filepath)


def main():
    fighters = [
        ("Arcade Fighters/Fighter1/3D/Fighter1.FBX", "Fighter1.glb"),
        ("Arcade Fighters/Fighter1/3D/Fighter1 Variation.FBX", "Fighter1-variation.glb"),
        ("Arcade Fighters/Fighter2/3D/Fighter2.FBX", "Fighter2.glb"),
        ("Arcade Fighters/Fighter2/3D/Fighter2 Variation.FBX", "Fighter2-variation.glb"),
    ]
    for rel, name in fighters:
        print("===", name)
        bake_character(os.path.join(ASSETS, rel), os.path.join(CHAR_DIR, name))
    print("=== arena")
    bake_arena()


if __name__ == "__main__":
    main()
