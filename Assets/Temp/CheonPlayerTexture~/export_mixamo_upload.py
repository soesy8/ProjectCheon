import bpy
import json
import hashlib
import zipfile
from pathlib import Path
from collections import Counter
from io_scene_fbx import parse_fbx

work=Path(r'D:\project\ProjectCheon\Assets\Temp\CheonPlayerTexture~')
out=work/'MixamoUpload~'
out.mkdir(exist_ok=True)
source=bpy.data.objects['Cheon_Player']
mesh=source.data
mesh.calc_loop_triangles()
before={'vertices':[tuple(v.co) for v in mesh.vertices],
        'uvs':[tuple(v.uv) for v in mesh.uv_layers.active.data],
        'triangles':len(mesh.loop_triangles)}

# 믹사모 업로드용 사본에는 편집 선택용 그룹을 넣지 않는다.
for group in list(source.vertex_groups):source.vertex_groups.remove(group)
for key in list(source.keys()):del source[key]
for obj in list(bpy.data.objects):
    if obj!=source:bpy.data.objects.remove(obj,do_unlink=True)

image=bpy.data.images.get('T_Cheon_Player_BaseColor')
assert image and image.packed_file
image.alpha_mode='NONE'
image.filepath_raw=str(out/'T_Cheon_Player_BaseColor.png')
image.file_format='PNG'
image.save()
image.filepath=str(out/'T_Cheon_Player_BaseColor.png')
material=bpy.data.materials.new('M_Cheon_Player_Mixamo')
material.use_nodes=True
material.diffuse_color=(.11,.44,.52,1)
nodes=material.node_tree.nodes
nodes.clear()
output=nodes.new('ShaderNodeOutputMaterial')
shader=nodes.new('ShaderNodeBsdfPrincipled')
texture=nodes.new('ShaderNodeTexImage')
texture.image=image
texture.extension='EXTEND'
shader.inputs['Metallic'].default_value=0
shader.inputs['Roughness'].default_value=.75
shader.inputs['Alpha'].default_value=1
material.node_tree.links.new(texture.outputs['Color'],shader.inputs['Base Color'])
material.node_tree.links.new(shader.outputs['BSDF'],output.inputs['Surface'])
mesh.materials.clear()
mesh.materials.append(material)
for polygon in mesh.polygons:polygon.material_index=0
for mat in list(bpy.data.materials):
    if mat!=material:bpy.data.materials.remove(mat)
for im in list(bpy.data.images):
    if im!=image and im.users==0:bpy.data.images.remove(im)
assert not source.vertex_groups
assert len(mesh.uv_layers)==1 and not mesh.color_attributes
assert all(tuple(v.co)==p for v,p in zip(mesh.vertices,before['vertices']))
assert all(tuple(v.uv)==p for v,p in zip(mesh.uv_layers.active.data,before['uvs']))
bpy.context.view_layer.objects.active=source
bpy.ops.object.select_all(action='DESELECT')
source.select_set(True)

# 색상 이미지 하나만 담아 믹사모의 FBX 재질 판독 과정을 단순화한다.
bpy.ops.export_scene.fbx(filepath=str(out/'Cheon_Player_Mixamo.fbx'),
    use_selection=True,object_types={'MESH'},global_scale=1,
    apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',
    use_mesh_modifiers=True,mesh_smooth_type='FACE',use_triangles=True,use_tspace=False,
    colors_type='NONE',add_leaf_bones=False,bake_anim=False,path_mode='COPY',embed_textures=True,
    use_custom_props=False)
root,version=parse_fbx.parse(str(out/'Cheon_Player_Mixamo.fbx'))
objects=next(e for e in root.elems if e.id==b'Objects')
counts=Counter(e.id.decode() for e in objects.elems)
assert counts['Geometry']==1 and counts['Model']==1 and counts['Material']==1
assert counts['Texture']==1 and counts['Video']==1
assert counts['Deformer']==0 and counts['AnimationStack']==0
assert all(e.props[2]==b'Mesh' for e in objects.elems if e.id==b'Model')
video=next(e for e in objects.elems if e.id==b'Video')
content=next(e for e in video.elems if e.id==b'Content').props[0]
assert content
assert hashlib.sha256(content).digest()==hashlib.sha256((out/'T_Cheon_Player_BaseColor.png').read_bytes()).digest()

# FBX 판독 자체를 우회할 수 있도록 동일 메시의 OBJ+MTL+텍스처 ZIP도 준비한다.
obj_path=out/'Cheon_Player_Mixamo.obj'
properties=bpy.ops.wm.obj_export.get_rna_type().properties
settings={'filepath':str(obj_path),'export_selected_objects':True,
          'forward_axis':'NEGATIVE_Z','up_axis':'Y','global_scale':1,
          'export_uv':True,'export_normals':True,'export_materials':True,
          'export_triangulated_mesh':True,'export_vertex_groups':False,'export_colors':False,
          'path_mode':'RELATIVE'}
settings={key:value for key,value in settings.items() if key in properties}
bpy.ops.wm.obj_export(**settings)
mtl_path=obj_path.with_suffix('.mtl')
text=mtl_path.read_text(encoding='utf-8')
# ZIP을 푼 위치와 관계없이 텍스처를 찾도록 외부 경로를 파일명으로 정리한다.
lines=[]
for line in text.splitlines():
    if line.startswith('map_Kd '):line='map_Kd T_Cheon_Player_BaseColor.png'
    if line.startswith('map_d '):continue
    lines.append(line)
mtl_path.write_text('\n'.join(lines)+'\n',encoding='utf-8')
with zipfile.ZipFile(out/'Cheon_Player_Mixamo_OBJ.zip','w',compression=zipfile.ZIP_DEFLATED) as archive:
    for path in [obj_path,mtl_path,out/'T_Cheon_Player_BaseColor.png']:
        archive.write(path,path.name)
with zipfile.ZipFile(out/'Cheon_Player_Mixamo_OBJ.zip') as archive:
    assert archive.testzip() is None
    assert len(archive.namelist())==3
report={'fbx_counts':dict(counts),'mesh_objects':1,'materials':1,'uv_maps':1,'color_attributes':0,
        'skeleton_nodes':0,'skin_deformers':0,'texture_maps':1,'embedded_basecolor_bytes':len(content),
        'geometry_unchanged':True,'uvs_unchanged':True,'triangles':before['triangles'],
        'zip_files':archive.namelist(),'outputs':['Cheon_Player_Mixamo.fbx','Cheon_Player_Mixamo_OBJ.zip']}
(out/'mixamo_upload_validation.json').write_text(json.dumps(report,indent=2))
print('MIXAMO_UPLOAD_FILES_VALIDATED',json.dumps(report),flush=True)
