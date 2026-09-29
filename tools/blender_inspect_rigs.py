import bpy
import sys
from pathlib import Path

fbx = Path(sys.argv[-1])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(fbx))
armatures = [o for o in bpy.data.objects if o.type == 'ARMATURE']
print('FILE', fbx.name)
for arm in armatures:
    print('ARMATURE', arm.name, 'bones', len(arm.data.bones))
    names = sorted(b.name for b in arm.data.bones)
    print('SAMPLE', names[:8], '...', names[-4:])
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
print('MESH_COUNT', len(meshes))
for m in sorted(meshes, key=lambda x: x.name)[:25]:
    print(' MESH', m.name)
