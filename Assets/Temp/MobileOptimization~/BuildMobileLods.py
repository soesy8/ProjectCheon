import bpy
import bmesh
import json
from pathlib import Path

root = Path('D:/project/ProjectCheon')
folder = root / 'Assets/Temp/MobileOptimization~'
records = json.loads((folder / 'GeometryInput.json').read_text(encoding='utf-8'))
output = []
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

for record in records:
    points = record['positions']
    vertices = [points[i:i + 3] for i in range(0, len(points), 3)]
    faces, materials = [], []
    for material, indices in enumerate(record['submeshes']):
        faces.extend([indices[i:i + 3] for i in range(0, len(indices), 3)])
        materials.extend([material] * (len(indices) // 3))
    mesh = bpy.data.meshes.new(record['name'])
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    uv_layer = mesh.uv_layers.new(name='UVMap')
    for polygon, material in zip(mesh.polygons, materials):
        polygon.material_index = material
        polygon.use_smooth = True
        for loop_index in polygon.loop_indices:
            vertex = mesh.loops[loop_index].vertex_index
            uv_layer.data[loop_index].uv = record['uv'][vertex * 2:vertex * 2 + 2]
    # Unity의 노멀/UV 경계에서 분리된 정점을 연결하되 UV는 면 모서리에 보존한다.
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=0.000001)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    wall = '/SelfModeling/' in record['source']
    for level, ratio in [(1, 0.4 if wall else 0.55), (2, 0.15 if wall else 0.25)]:
        obj = bpy.data.objects.new('Simplification', mesh.copy())
        bpy.context.collection.objects.link(obj)
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        modifier = obj.modifiers.new('MobileLOD', 'DECIMATE')
        modifier.ratio = ratio
        modifier.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        reduced = obj.data
        reduced.calc_loop_triangles()
        positions, normals, uv, submeshes, lookup = [], [], [], [[] for _ in record['submeshes']], {}
        for triangle in reduced.loop_triangles:
            material = min(triangle.material_index, len(submeshes) - 1)
            for loop_index in triangle.loops:
                loop = reduced.loops[loop_index]
                vertex = reduced.vertices[loop.vertex_index]
                texcoord = reduced.uv_layers.active.data[loop_index].uv
                normal = reduced.corner_normals[loop_index].vector
                key = (loop.vertex_index, round(texcoord.x, 7), round(texcoord.y, 7), round(normal.x, 5), round(normal.y, 5), round(normal.z, 5))
                index = lookup.get(key)
                if index is None:
                    index = len(positions) // 3
                    lookup[key] = index
                    positions.extend(vertex.co)
                    normals.extend(normal)
                    uv.extend(texcoord)
                submeshes[material].append(index)
        output.append(dict(key=record['key'], name=record['name'], source=record['source'], level=level,
                           positions=positions, normals=normals, uv=uv, submeshes=submeshes))
        print(f"LOD {level} {record['name']}: {len(faces)} -> {sum(map(len, submeshes)) // 3}", flush=True)
        bpy.data.objects.remove(obj, do_unlink=True)
        bpy.data.meshes.remove(reduced)
    bpy.data.meshes.remove(mesh)

(folder / 'GeometryOutput.json').write_text(json.dumps(output, separators=(',', ':')), encoding='utf-8')
print(f'Completed {len(output)} LOD meshes', flush=True)
