import bpy, os, json, bmesh, shutil
from mathutils import Vector
OUT=r'E:\C.J.I\Blender'
TEMP=os.path.dirname(os.path.abspath(__file__))
bpy.ops.wm.open_mainfile(filepath=os.path.join(OUT,'Cheon_Player.blend'))
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
assert len(objects)==1
ob=objects[0];me=ob.data;me.calc_loop_triangles()
assert len(me.materials)==1 and len(me.uv_layers)==1
assert all(p.area>1e-10 for p in me.polygons)
bm=bmesh.new();bm.from_mesh(me)
assert not [v for v in bm.verts if not v.link_faces]
bm.free()
images=[n.image for n in me.materials[0].node_tree.nodes if n.type=='TEX_IMAGE']
assert len(images)==1 and images[0].packed_file
assert tuple(images[0].size)==(1024,1024)
assert not any(o.type=='ARMATURE' for o in bpy.context.scene.objects)
stats={'mesh_objects':1,'vertices':len(me.vertices),'triangles':len(me.loop_triangles),'materials':1,'textures':1,'texture_size':'1024 x 1024','packed_texture':True,'rigged':False,'degenerate_faces':0,'loose_vertices':0}

# 저장된 원본을 기준으로 뒤쪽도 확인한다. 검증 카메라 변경은 원본에 저장하지 않는다.
cam=bpy.context.scene.camera;cam.location=(3,6,2.7)
cam.rotation_euler=(Vector((0,0,1.13))-cam.location).to_track_quat('-Z','Y').to_euler()
bpy.context.scene.render.resolution_x=700;bpy.context.scene.render.resolution_y=700
bpy.context.scene.render.filepath=os.path.join(TEMP,'Preview_Back.png')
bpy.ops.render.render(write_still=True)

# FBX를 임시 폴더로 복사해 재가져오기한다. 임베디드 텍스처 추출 잔여물도 임시 폴더에만 남긴다.
fbx_temp=os.path.join(TEMP,'Cheon_Player_Verify.fbx')
shutil.copy2(os.path.join(OUT,'Cheon_Player.fbx'),fbx_temp)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx_temp)
models=[o for o in bpy.context.scene.objects if o.type=='MESH']
assert len(models)==1
fm=models[0].data;fm.calc_loop_triangles()
assert len(fm.loop_triangles)==stats['triangles']
assert len(fm.materials)==1 and len(fm.uv_layers)==1
stats['fbx_round_trip']='PASS'
with open(os.path.join(TEMP,'verification.json'),'w',encoding='utf-8') as f:json.dump(stats,f,ensure_ascii=False,indent=2)
print('VERIFICATION_PASS',json.dumps(stats),flush=True)
