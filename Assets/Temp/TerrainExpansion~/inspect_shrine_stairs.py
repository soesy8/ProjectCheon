import bpy
import json
from mathutils import Vector
from mathutils.bvhtree import BVHTree

core = bpy.data.objects['Mountain_Continuous_Core']
terrain_vertices, terrain_faces = [], []
for obj in [core, bpy.data.objects['Arena_Floor_Flat_UV'], bpy.data.objects['Shrine_Floor_Flat_UV']]:
    offset = len(terrain_vertices)
    terrain_vertices.extend(obj.matrix_world @ vertex.co for vertex in obj.data.vertices)
    terrain_faces.extend(tuple(offset + index for index in face.vertices) for face in obj.data.polygons)
bvh = BVHTree.FromPolygons(terrain_vertices, terrain_faces)
report = []
for name in ['Stairway_Between_Summits', 'Stairway_Winding_Approach']:
    obj = bpy.data.objects[name]
    clearances = []
    buried = []
    for face in obj.data.polygons:
        if face.normal.z < 0.999:
            continue
        point = obj.matrix_world @ face.center
        hit, normal, index, distance = bvh.ray_cast(Vector((point.x, point.y, 100)), Vector((0, 0, -1)))
        if hit is not None:
            clearance = point.z - hit.z
            clearances.append(clearance)
            if clearance < 0:
                buried.append({'center': list(point), 'clearance': clearance})
    report.append({'name': name, 'minimum_clearance': min(clearances),
                   'buried_surfaces': buried, 'surfaces': len(clearances)})
print('STAIR_AUDIT:' + json.dumps(report))
assert all(not item['buried_surfaces'] for item in report)

# 같은 형상 검증을 하단 평면에도 적용한다.
with open(r'D:\project\ProjectCheon\Assets\Temp\TerrainExpansion~\validate_terrain.py', encoding='utf-8') as validation_file:
    validation_code = validation_file.read()
validation_code = validation_code.replace('Arena_Floor_Flat_UV', 'Shrine_Floor_Flat_UV')
validation_code = validation_code.replace('validation_report.json', 'shrine_validation_report.json')
exec(compile(validation_code, '<shrine_validation>', 'exec'))
