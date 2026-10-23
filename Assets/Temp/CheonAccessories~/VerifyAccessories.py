import bpy
import bmesh
import hashlib
import json
from pathlib import Path

directory=Path(r'D:\project\ProjectCheon\Assets\Temp\CheonAccessories~')
report=json.loads((directory/'accessories_validation.json').read_text(encoding='utf-8'))
def fingerprint(obj):
    data={'matrix':[list(row) for row in obj.matrix_world],
          'vertices':[list(v.co) for v in obj.data.vertices] if obj.type=='MESH' else [],
          'faces':[list(f.vertices) for f in obj.data.polygons] if obj.type=='MESH' else [],
          'modifiers':[(m.name,m.type) for m in obj.modifiers],
          'materials':[m.name if m else None for m in obj.data.materials] if obj.type=='MESH' else []}
    return hashlib.sha256(json.dumps(data,sort_keys=True).encode()).hexdigest()
for name,value in report['preserved'].items():
    assert fingerprint(bpy.data.objects[name])==value,name+' changed'
hair_objects=[obj for obj in bpy.context.scene.objects if obj.name.startswith('Hair')]
assert len(hair_objects)==1 and hair_objects[0].name=='Hair'
hair=hair_objects[0]
assert len(hair.data.vertices)==report['hair_vertices_before']
assert len(hair.data.polygons)==report['hair_faces_before']
for name in ['Hair','PouchStrap','GatChinStrap']:
    obj=bpy.data.objects[name]
    assert obj.visible_get() and not obj.hide_render
    bm=bmesh.new()
    bm.from_mesh(obj.data)
    assert all(edge.is_manifold for edge in bm.edges),name
    assert all(face.calc_area()>1e-10 for face in bm.faces),name
    bm.free()
    if name!='Hair':
        assert obj.data.uv_layers,name
assert all(image.packed_file for image in bpy.data.images if image.source=='FILE')
assert not any(obj.name=='PreviewCamera' for obj in bpy.context.scene.objects)
print('VERIFIED: one Hair object; complete shoulder and chin straps; closed meshes; UVs; packed references; original body, hat, pouch and other objects preserved.')
