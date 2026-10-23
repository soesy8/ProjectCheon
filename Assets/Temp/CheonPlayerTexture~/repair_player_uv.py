import bpy
import numpy as np
import json
import math
from pathlib import Path
from mathutils import Vector

out=Path(r'D:\project\ProjectCheon\Assets\Temp\CheonPlayerTexture~')
model=bpy.data.objects['Cheon_Player']
old=model.data
old.calc_loop_triangles()
group_weights={group.name:[] for group in model.vertex_groups}
for vertex in old.vertices:
    for assignment in vertex.groups:
        group_weights[model.vertex_groups[assignment.group].name].append((vertex.index,assignment.weight))
if not any(group_weights.values()):
    # 첫 재구성 전의 백업에서 파츠별 선택 그룹을 복원한다. 정점 순서는 동일하다.
    backup=out/'Cheon_Player_Atlas_Work.blend1'
    with bpy.data.libraries.load(str(backup),link=False) as (available,loaded):
        loaded.objects=['Cheon_Player']
    reference=loaded.objects[0]
    assert len(reference.data.vertices)==len(old.vertices)
    group_weights={group.name:[] for group in reference.vertex_groups}
    for vertex in reference.data.vertices:
        for assignment in vertex.groups:
            group_weights[reference.vertex_groups[assignment.group].name].append((vertex.index,assignment.weight))
    bpy.data.objects.remove(reference,do_unlink=True)
report=json.loads((out/'uv_overlap_report.json').read_text())
bad=set(report['bad_faces'])
triangles={p.index:[] for p in old.polygons}
ratios=[]
for tri in old.loop_triangles:
    triangles[tri.polygon_index].append(tri)
    points=[old.uv_layers.active.data[loop].uv.copy() for loop in tri.loops]
    uv_area=abs((points[1]-points[0]).cross(points[2]-points[0]))*.5
    if tri.area>1e-8 and uv_area>1e-10:ratios.append(math.sqrt(uv_area/tri.area))
scale=float(np.median(ratios))
faces=[]
uvs=[]
materials=[]
split_count=0
for face in old.polygons:
    if face.index not in bad:
        faces.append(list(face.vertices))
        uvs.append([old.uv_layers.active.data[i].uv.copy() for i in face.loop_indices])
        materials.append(face.material_index)
        continue
    # 문제가 있는 면만 기존 삼각분할 그대로 분리하여 겹침 없는 UV 조각으로 만든다.
    for tri in triangles[face.index]:
        indices=list(tri.vertices)
        a,b,c=[old.vertices[i].co.copy() for i in indices]
        axis=(b-a).normalized()
        perpendicular=(c-a)-axis*(c-a).dot(axis)
        perpendicular.normalize()
        offset=Vector((2+split_count*2,2))
        coords=[Vector(((point-a).dot(axis)*scale,(point-a).dot(perpendicular)*scale))+offset for point in (a,b,c)]
        faces.append(indices)
        uvs.append(coords)
        materials.append(face.material_index)
        split_count+=1
mesh=bpy.data.meshes.new('Cheon_Player_Mesh_Repacked')
mesh.from_pydata([v.co[:] for v in old.vertices],[],faces)
mesh.update()
for material in old.materials:mesh.materials.append(material)
layer=mesh.uv_layers.new(name='UVMap')
for poly,coords,mat_index in zip(mesh.polygons,uvs,materials):
    poly.material_index=mat_index
    for li,coord in zip(poly.loop_indices,coords):layer.data[li].uv=coord
model.data=mesh
for group in list(model.vertex_groups):model.vertex_groups.remove(group)
for name,weights in group_weights.items():
    group=model.vertex_groups.new(name=name)
    for index,weight in weights:group.add([index],weight,'REPLACE')
bpy.data.meshes.remove(old)
mesh.name='Cheon_Player_Mesh'
bpy.context.view_layer.objects.active=model
model.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.pack_islands(rotate=True,margin_method='FRACTION',margin=.003,shape_method='CONCAVE',scale=True)
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'Cheon_Player_Atlas_Work.blend'))
print('REPAIRED_FACES',len(bad),'SEPARATE_TRIANGLES',split_count,'FINAL_FACES',len(mesh.polygons),flush=True)
