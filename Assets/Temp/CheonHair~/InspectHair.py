import bpy
import json
from mathutils import Vector
from pathlib import Path

output = Path(r'D:\project\ProjectCheon\Assets\Temp\CheonHair~')
report = {'version': bpy.app.version_string, 'objects': [], 'images': []}
for obj in bpy.data.objects:
    entry = {'name': obj.name, 'type': obj.type, 'location': list(obj.location),
             'rotation': list(obj.rotation_euler), 'scale': list(obj.scale),
             'dimensions': list(obj.dimensions), 'hide': obj.hide_get(),
             'hide_render': obj.hide_render, 'parent': obj.parent.name if obj.parent else None,
             'materials': [m.name if m else None for m in obj.data.materials] if hasattr(obj.data, 'materials') else [],
             'modifiers': [{'name': m.name, 'type': m.type} for m in obj.modifiers]}
    if obj.type == 'MESH':
        points = [obj.matrix_world @ v.co for v in obj.data.vertices]
        entry['vertices'] = len(points)
        entry['polygons'] = len(obj.data.polygons)
        entry['bounds'] = [[min(p[i] for p in points), max(p[i] for p in points)] for i in range(3)] if points else []
        if 'hair' in obj.name.lower():
            entry['coords'] = [list(p) for p in points]
            entry['modifier_details'] = [{k: getattr(m, k) for k in ['name','type','levels','render_levels','thickness','offset'] if hasattr(m,k)} for m in obj.modifiers]
    report['objects'].append(entry)
for img in bpy.data.images:
    report['images'].append({'name': img.name, 'filepath': img.filepath, 'packed': bool(img.packed_file)})
(output / 'original_report.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps({**report, 'objects': [{k:v for k,v in x.items() if k!='coords'} for x in report['objects']]}, indent=2))
