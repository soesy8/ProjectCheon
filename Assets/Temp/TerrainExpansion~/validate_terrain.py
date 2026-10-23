import bpy
import bmesh
import json
import math
import os

WORK = r'D:\project\ProjectCheon\Assets\Temp\TerrainExpansion~'
floor = bpy.data.objects['Arena_Floor_Flat_UV']
heights = [vertex.co.z for vertex in floor.data.vertices]
top_height = max(heights)
top_faces = [face for face in floor.data.polygons
             if all(abs(floor.data.vertices[index].co.z - top_height) < 0.00001
                    for index in face.vertices)]
bm = bmesh.new()
bm.from_mesh(floor.data)
remaining = set(bm.verts)
islands = 0
while remaining:
    islands += 1
    stack = [remaining.pop()]
    while stack:
        for edge in stack.pop().link_edges:
            for vertex in edge.verts:
                if vertex in remaining:
                    remaining.remove(vertex)
                    stack.append(vertex)
file_images = [image for image in bpy.data.images if image.source == 'FILE']
packed_images = sum(bool(image.packed_file) for image in file_images)
report = {
    'file': bpy.data.filepath,
    'floor_connected_parts': islands,
    'top_faces': len(top_faces),
    'top_height_range': [min(floor.data.vertices[index].co.z for face in top_faces for index in face.vertices), top_height],
    'minimum_top_normal_z': min(face.normal.z for face in top_faces),
    'nonmanifold_floor_edges': sum(not edge.is_manifold for edge in bm.edges),
    'uv_layers': [layer.name for layer in floor.data.uv_layers],
    'finite_uvs': all(math.isfinite(value) for layer in floor.data.uv_layers for uv in layer.data for value in uv.uv),
    'packed_images': packed_images,
    'total_file_images': len(file_images),
    'linked_libraries': len(bpy.data.libraries),
    'preview_cameras_in_work_file': [obj.name for obj in bpy.data.objects if 'PreviewCamera' in obj.name],
}
bm.free()
assert islands == 1
assert report['nonmanifold_floor_edges'] == 0
assert report['minimum_top_normal_z'] > 0.99999
assert report['top_height_range'][1] - report['top_height_range'][0] < 0.00001
assert report['finite_uvs']
assert packed_images == len(file_images)
assert not report['preview_cameras_in_work_file']
with open(os.path.join(WORK, 'validation_report.json'), 'w', encoding='utf-8') as output:
    json.dump(report, output, ensure_ascii=False, indent=2)
print('VALIDATION_PASSED:' + json.dumps(report, ensure_ascii=True))
