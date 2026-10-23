import bpy
import json
import bmesh
from pathlib import Path

out=Path(r'D:\project\ProjectCheon\Assets\Temp\CheonHair~')
original=json.loads((out/'original_report.json').read_text(encoding='utf-8'))
changes=[]
for entry in original['objects']:
    obj=bpy.data.objects.get(entry['name'])
    assert obj is not None,entry['name']
    if obj.type=='MESH':
        assert len(obj.data.vertices)==entry['vertices'],obj.name
        assert len(obj.data.polygons)==entry['polygons'],obj.name
    for key in ['location','scale']:
        assert all(abs(a-b)<1e-6 for a,b in zip(getattr(obj,key),entry[key])),(obj.name,key)
    assert [dict(name=m.name,type=m.type) for m in obj.modifiers]==entry['modifiers'],obj.name
    changes.append(obj.name+' preserved')
hair=bpy.data.objects['Hair_Completed']
assert hair.get('SourceObject')=='HairOrigin'
assert len(hair.vertex_groups)==30
assert len(hair.data.uv_layers)>0
assert len(hair.data.materials)==3
assert not hair.hide_render and not hair.hide_get()
assert bpy.data.collections['Hair_WorkSource'].hide_render
assert bpy.data.collections['Hair_WorkSource'].hide_viewport
assert not bpy.data.objects['Gat'].hide_render
assert len([o for o in scene.objects if o.type=='CAMERA'])==0 if (scene:=bpy.context.scene) else False
assert all(image.packed_file for image in bpy.data.images if image.source=='FILE')
bm=bmesh.new()
bm.from_mesh(hair.data)
assert all(edge.is_manifold for edge in bm.edges)
assert all(face.calc_area()>1e-10 for face in bm.faces)
bm.free()
print('VERIFIED',json.dumps(changes),'HairOrigin copies, UV, materials, closed mesh and packed references OK')
