import bpy
import json
from collections import Counter
from pathlib import Path
from io_scene_fbx import parse_fbx

out=Path(r'D:\project\ProjectCheon\Assets\Temp\CheonPlayerTexture~')
source=Path(r'E:\C.J.I\Blender\SelfModeling\Hair\Cheon_Player_Textured\Cheon_Player_Textured.fbx')
root,version=parse_fbx.parse(str(source))
objects=next(element for element in root.elems if element.id==b'Objects')
types=Counter(element.id.decode() for element in objects.elems)
models=[{'name':element.props[1].decode(errors='replace'),'type':element.props[2].decode(errors='replace')} for element in objects.elems if element.id==b'Model']
deformers=[element.props[2].decode(errors='replace') for element in objects.elems if element.id==b'Deformer']
report={'fbx_version':version,'object_counts':dict(types),'model_nodes':models,'deformers':deformers,
        'blend_objects':[(o.name,o.type) for o in bpy.data.objects],
        'vertex_group_count':len(bpy.data.objects['Cheon_Player'].vertex_groups),
        'material_links':[(link.from_node.type,link.from_socket.name,link.to_node.type,link.to_socket.name) for link in bpy.data.objects['Cheon_Player'].data.materials[0].node_tree.links]}
(out/'mixamo_source_diagnosis.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
