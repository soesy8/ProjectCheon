import bpy
import json
from pathlib import Path

output = Path(r'D:\project\ProjectCheon\Assets\Temp\CheonPlayerTexture~')
output.mkdir(parents=True, exist_ok=True)
report = {'blender': bpy.app.version_string, 'filepath': bpy.data.filepath, 'objects': [], 'materials': [], 'images': []}
for obj in bpy.data.objects:
    item = {'name': obj.name, 'type': obj.type, 'collections': [c.name for c in obj.users_collection],
            'hide_render': obj.hide_render, 'hide_viewport': obj.hide_viewport, 'hidden': obj.hide_get(),
            'location': list(obj.location), 'dimensions': list(obj.dimensions),
            'parent': obj.parent.name if obj.parent else None,
            'modifiers': [{'name': m.name, 'type': m.type, 'visible': m.show_viewport, 'render': m.show_render} for m in obj.modifiers]}
    if obj.type == 'MESH':
        item.update(vertices=len(obj.data.vertices), polygons=len(obj.data.polygons),
                    materials=[m.name if m else None for m in obj.data.materials],
                    uv_layers=[u.name for u in obj.data.uv_layers],
                    color_attributes=[a.name for a in obj.data.color_attributes],
                    shape_keys=[k.name for k in obj.data.shape_keys.key_blocks] if obj.data.shape_keys else [],
                    vertex_groups=[g.name for g in obj.vertex_groups])
    report['objects'].append(item)
for mat in bpy.data.materials:
    item = {'name': mat.name, 'diffuse': list(mat.diffuse_color), 'use_nodes': mat.use_nodes, 'nodes': [], 'links': []}
    if mat.use_nodes:
        for node in mat.node_tree.nodes:
            inputs = {}
            for socket in node.inputs:
                if hasattr(socket, 'default_value'):
                    value = socket.default_value
                    try:
                        value = list(value)
                    except TypeError:
                        pass
                    if isinstance(value, (str, int, float, bool, list, type(None))):
                        inputs[socket.name] = value
            item['nodes'].append({'name': node.name, 'type': node.type, 'inputs': inputs, 'image': node.image.name if node.type == 'TEX_IMAGE' and node.image else None})
        item['links'] = [f'{l.from_node.name}.{l.from_socket.name} -> {l.to_node.name}.{l.to_socket.name}' for l in mat.node_tree.links]
    report['materials'].append(item)
for im in bpy.data.images:
    report['images'].append({'name': im.name, 'size': list(im.size), 'path': im.filepath, 'packed': bool(im.packed_file)})
report['scene_objects'] = [o.name for o in bpy.context.scene.objects]
report['world'] = bpy.context.scene.world.name if bpy.context.scene.world else None
(output / 'source_inventory.json').write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding='utf-8')
print('INVENTORY_SAVED', output / 'source_inventory.json')
