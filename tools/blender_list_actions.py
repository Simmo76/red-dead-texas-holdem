import bpy
import sys
from pathlib import Path
p = Path(sys.argv[-1])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(p))
print('actions', [a.name for a in bpy.data.actions])
print('nla', [(o.name, o.animation_data and o.animation_data.nla_tracks) for o in bpy.data.objects if o.type=='ARMATURE'])
