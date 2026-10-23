import bpy
import json
import hashlib
from pathlib import Path
from mathutils import Vector, Quaternion
from mathutils.kdtree import KDTree

out=Path(r'D:\project\ProjectCheon\Assets\Temp\CheonPlayerTexture~')
source=Path(r'E:\C.J.I\Blender\SelfModeling\Hair\Cheon_Player.blend')
model=bpy.data.objects['Cheon_Player']
mesh=model.data
scene=bpy.context.scene
mesh.calc_loop_triangles()
assert len(scene.objects)==1
assert len(mesh.materials)==1
assert len(bpy.data.materials)==1
assert len(mesh.uv_layers)==1
assert not mesh.color_attributes
assert all(p.material_index==0 for p in mesh.polygons)
assert not model.modifiers
assert all(-1e-6<=component<=1.000001 for item in mesh.uv_layers.active.data for component in item.uv)
assert all(n.type not in ('VERTEX_COLOR','ATTRIBUTE') for n in mesh.materials[0].node_tree.nodes)
uv_report=json.loads((out/'uv_overlap_report.json').read_text())
assert uv_report['overlap_area']<1e-12,uv_report

# 원본의 보이는 파츠와 통합본의 표면 정점을 대조해 형상이 유지되었는지 검증한다.
parts=['Body','Hair','Gat','GatStrap','GatRibbon','GatRibbonParts','GatStrapTail','BeltTail','Pouch']
with bpy.data.libraries.load(str(source),link=False) as (available,loaded):
    loaded.objects=parts[:]
reference_collection=bpy.data.collections.new('Validation_References')
scene.collection.children.link(reference_collection)
tree=KDTree(len(mesh.vertices))
for vertex in mesh.vertices:tree.insert(model.matrix_world@vertex.co,vertex.index)
tree.balance()
max_distance=0.0
expected_triangles=0
source_surface_vertices=0
for obj in loaded.objects:
    reference_collection.objects.link(obj)
    obj.hide_set(False)
    obj.hide_viewport=False
    bpy.context.view_layer.update()
    evaluated=obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    source_mesh=evaluated.to_mesh()
    source_mesh.calc_loop_triangles()
    expected_triangles+=len(source_mesh.loop_triangles)
    used={index for face in source_mesh.polygons for index in face.vertices}
    source_surface_vertices+=len(used)
    for index in used:
        position=obj.matrix_world@source_mesh.vertices[index].co
        distance=tree.find(position)[2]
        max_distance=max(max_distance,distance)
    evaluated.to_mesh_clear()
assert max_distance<1e-6,max_distance
assert expected_triangles==len(mesh.loop_triangles)
for obj in list(reference_collection.objects):bpy.data.objects.remove(obj,do_unlink=True)
bpy.data.collections.remove(reference_collection)
for orphan in list(bpy.data.meshes):
    if orphan.users==0:bpy.data.meshes.remove(orphan)
for orphan in list(bpy.data.materials):
    if orphan.users==0:bpy.data.materials.remove(orphan)
for orphan in list(bpy.data.images):
    if orphan.users==0 and orphan.name not in ('Render Result','Viewer Node'):bpy.data.images.remove(orphan)
for image in bpy.data.images:
    if image.name.startswith('T_Cheon_Player_'):
        assert image.packed_file
        image.filepath='//'+image.name+'.png'
model.name='Cheon_Player'
mesh.name='Cheon_Player_Mesh'
for collection in list(model.users_collection):collection.objects.unlink(model)
characters=bpy.data.collections.get('Character') or bpy.data.collections.new('Character')
if characters.name not in scene.collection.children:scene.collection.children.link(characters)
characters.objects.link(model)
for collection in list(bpy.data.collections):
    if collection!=characters and not collection.objects and not collection.children:bpy.data.collections.remove(collection)
bpy.ops.object.select_all(action='DESELECT')
model.select_set(True)
bpy.context.view_layer.objects.active=model
scene.cursor.location=(0,0,0)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            space=area.spaces.active
            space.shading.type='MATERIAL'
            space.shading.studiolight_rotate_z=.4
            space.overlay.show_overlays=False
            space.region_3d.view_distance=3.3
            space.region_3d.view_location=(0,0,1.04)
            space.region_3d.view_rotation=Quaternion((.70710678,.70710678,0,0))
            space.region_3d.view_perspective='ORTHO'
bpy.context.preferences.filepaths.save_version=0
bpy.context.preferences.filepaths.file_preview_type='NONE'
bpy.ops.wm.save_as_mainfile(filepath=str(out/'Cheon_Player_Textured.blend'))
bpy.ops.export_scene.fbx(filepath=str(out/'Cheon_Player_Textured.fbx'),
                         use_selection=True,object_types={'MESH'},
                         global_scale=1.0,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
                         axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,
                         mesh_smooth_type='FACE',use_triangles=True,use_tspace=True,
                         colors_type='NONE',add_leaf_bones=False,bake_anim=False,
                         path_mode='COPY',embed_textures=True,use_custom_props=False)
report={'original_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
        'source_parts':parts,'mesh_objects':1,'materials':1,'uv_maps':1,'vertex_color_attributes':0,
        'vertices':len(mesh.vertices),'triangles':len(mesh.loop_triangles),
        'uv_overlap_area':uv_report['overlap_area'],'source_surface_vertices':source_surface_vertices,
        'maximum_geometry_distance':max_distance,'embedded_textures':True,
        'part_groups':{g.name:sum(any(a.group==g.index for a in v.groups) for v in mesh.vertices) for g in model.vertex_groups if g.name.startswith('Part_')}}
(out/'export_validation.json').write_text(json.dumps(report,indent=2))
print('EXPORT_VALIDATED',json.dumps(report),flush=True)
