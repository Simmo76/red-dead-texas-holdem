#!/usr/bin/env python3
"""Bake the Meshy-rigged female fighters for the web client.

Reads the rigged GLBs out of tools/assets/female-fighters (generated with
Meshy Image-to-3D + rigging on the max_biped skeleton template, so the bones
carry 3ds Max Biped names behind a "Bip001 " prefix), retargets the existing
poker animation catalogue onto those skeletons with the same machinery the
Arcade Fighters went through, and writes:

  wwwroot/models/characters/street/FemaleFighter{1,2}.glb

Run headless:
  blender --background --python tools/bake_female_fighters.py
"""
from __future__ import annotations

import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bake_street_scene as base

SRC = os.path.join(base.ROOT, "tools/assets/female-fighters")


def import_glb(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    created = [o for o in bpy.data.objects if o not in before]
    arm = next(o for o in created if o.type == "ARMATURE")
    # The glTF importer also creates a small icosphere as the bones' display
    # shape; the body is the mesh the armature actually deforms.
    mesh = next(
        o for o in created
        if o.type == "MESH" and any(m.type == "ARMATURE" for m in o.modifiers)
    )
    return arm, mesh


# bake_character resolves these through the module, so the same pipeline
# drives the Meshy rig: GLB import instead of FBX, and the max_biped bone
# names sit behind Meshy's "Bip001 " prefix.
base.import_fbx = import_glb
base.PREFIXES = ("Bip001 ",)


def main():
    jobs = [
        ("FemaleFighter1-rigged.glb", "FemaleFighter1.glb"),
        ("FemaleFighter2-rigged.glb", "FemaleFighter2.glb"),
    ]
    for src_name, out_name in jobs:
        print("===", out_name, flush=True)
        base.bake_character(os.path.join(SRC, src_name), os.path.join(base.CHAR_DIR, out_name))


if __name__ == "__main__":
    main()
