import bpy
import json
from collections import defaultdict
from mathutils import Vector
from pathlib import Path

body = bpy.data.objects['Body']
levels = defaultdict(list)
for v in body.data.vertices:
    p = body.matrix_world @ v.co
    if p.z > 1.45:
        levels[round(p.z,4)].append(tuple(round(x,5) for x in p))
print('HEAD LEVELS', json.dumps(dict(sorted(levels.items())), indent=2))
obj = bpy.data.objects['HairOrigin']
print('SOURCE VERTICES', [tuple(round(x,5) for x in v.co) for v in obj.data.vertices])
print('SOURCE FACES', [list(p.vertices) for p in obj.data.polygons])
for obj in bpy.data.objects:
    if obj.type == 'MESH': print('VISIBILITY', obj.name, obj.show_wire, obj.show_all_edges)
