import bpy
import json
from pathlib import Path

out=Path(r'D:\project\ProjectCheon\Assets\Temp\CheonPlayerTexture~')
for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
bpy.ops.import_scene.fbx(filepath=str(out/'Cheon_Player_Textured.fbx'),use_image_search=True,colors_type='NONE')
models=[o for o in bpy.context.scene.objects if o.type=='MESH']
assert len(models)==1
model=models[0]
mesh=model.data
mesh.calc_loop_triangles()
assert len(mesh.materials)==1
assert len(mesh.uv_layers)==1
assert not mesh.color_attributes
assert len(mesh.loop_triangles)==9996
assert max(abs(model.dimensions[i]-expected) for i,expected in enumerate((1.0770676,.97475565,2.162827)))<.0001,tuple(model.dimensions)
images=[n.image for n in mesh.materials[0].node_tree.nodes if n.type=='TEX_IMAGE' and n.image]
assert len({Path(bpy.path.abspath(image.filepath)).name for image in images})==3,[(i.name,i.filepath) for i in images]
assert all(tuple(i.size)==(4096,4096) for i in images)
assert any('BaseColor' in image.name for image in images)
report={'fbx_import':'passed','mesh_objects':len(models),'materials':len(mesh.materials),'uv_maps':len(mesh.uv_layers),
        'vertex_color_attributes':len(mesh.color_attributes),'vertices':len(mesh.vertices),'triangles':len(mesh.loop_triangles),
        'dimensions':list(model.dimensions),'images':[{'name':i.name,'size':list(i.size),'filepath':i.filepath} for i in images]}
(out/'fbx_roundtrip_validation.json').write_text(json.dumps(report,indent=2))
print('FBX_ROUNDTRIP_VERIFIED',json.dumps(report),flush=True)
