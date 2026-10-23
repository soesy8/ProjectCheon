import bpy
import json
import math
from pathlib import Path

out=Path(r'D:\project\ProjectCheon\Assets\Temp\CheonPlayerTexture~\MixamoUpload~')
reports=[]
for extension in ['fbx','obj']:
    for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
    path=out/('Cheon_Player_Mixamo.'+extension)
    if extension=='fbx':bpy.ops.import_scene.fbx(filepath=str(path),use_image_search=True,colors_type='NONE')
    else:bpy.ops.wm.obj_import(filepath=str(path),forward_axis='NEGATIVE_Z',up_axis='Y')
    models=list(bpy.context.scene.objects)
    assert len(models)==1 and models[0].type=='MESH'
    model=models[0]
    mesh=model.data
    mesh.calc_loop_triangles()
    assert len(mesh.vertices)==5103
    assert len(mesh.loop_triangles)==9996
    assert len(mesh.materials)==1 and len(mesh.uv_layers)==1
    assert len(mesh.color_attributes)==0
    assert not model.modifiers and not model.vertex_groups
    assert all(math.isfinite(component) for v in mesh.vertices for component in v.co)
    image_nodes=[n for n in mesh.materials[0].node_tree.nodes if n.type=='TEX_IMAGE' and n.image]
    assert image_nodes
    assert all('BaseColor' in node.image.name for node in image_nodes)
    assert all(tuple(node.image.size)==(4096,4096) for node in image_nodes)
    links=[link.to_socket.name for link in mesh.materials[0].node_tree.links if link.from_node.type=='TEX_IMAGE']
    assert 'Roughness' not in links and 'Metallic' not in links
    reports.append({'format':extension,'meshes':1,'materials':1,'uv_maps':1,'vertices':len(mesh.vertices),
                    'triangles':len(mesh.loop_triangles),'image_links':links,'roundtrip':'passed'})
(out/'mixamo_roundtrip_validation.json').write_text(json.dumps(reports,indent=2))
print('MIXAMO_ROUNDTRIP_VERIFIED',json.dumps(reports))
