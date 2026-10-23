import bpy
import json
import os
from mathutils import Vector

OUTPUT = r'D:\project\ProjectCheon\Assets\Temp\TerrainExpansion~'


def mesh_components(obj):
    adjacency = [[] for _ in obj.data.vertices]
    for edge in obj.data.edges:
        first, second = edge.vertices
        adjacency[first].append(second)
        adjacency[second].append(first)
    pending = set(range(len(adjacency)))
    components = []
    while pending:
        seed = pending.pop()
        stack = [seed]
        indices = [seed]
        while stack:
            for neighbor in adjacency[stack.pop()]:
                if neighbor in pending:
                    pending.remove(neighbor)
                    stack.append(neighbor)
                    indices.append(neighbor)
        points = [obj.matrix_world @ obj.data.vertices[i].co for i in indices]
        low = [min(p[axis] for p in points) for axis in range(3)]
        high = [max(p[axis] for p in points) for axis in range(3)]
        components.append({'vertices': len(indices), 'low': low, 'high': high,
                           'center': [(low[i] + high[i]) / 2 for i in range(3)]})
    return components


report = {'file': bpy.data.filepath, 'objects': {},
          'scene_properties': dict(bpy.context.scene.items()),
          'texts': {t.name: t.as_string()[:12000] for t in bpy.data.texts}}
for obj in bpy.data.objects:
    if obj.type != 'MESH':
        continue
    points = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    entry = {'low': [min(p[axis] for p in points) for axis in range(3)],
             'high': [max(p[axis] for p in points) for axis in range(3)],
             'properties': dict(obj.items()),
             'uv': [layer.name for layer in obj.data.uv_layers],
             'components': mesh_components(obj)}
    if any(key in obj.name for key in ['Mountain_', 'Terrace_', 'Granite_', 'Outcrops', 'Rocky_']):
        entry['vertices'] = [list(p) for p in points]
        entry['faces'] = [list(face.vertices) for face in obj.data.polygons]
    report['objects'][obj.name] = entry
with open(os.path.join(OUTPUT, 'source_geometry.json'), 'w', encoding='utf-8') as output:
    json.dump(report, output, ensure_ascii=False, default=str)

scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.studiolight_rotate_z = 0.4
scene.display.shading.color_type = 'MATERIAL'
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.display.shading.cavity_type = 'BOTH'
scene.display.shading.curvature_ridge_factor = 1.1
scene.display.shading.curvature_valley_factor = 0.85
scene.display.shading.show_specular_highlight = False
scene.display.shading.background_type = 'WORLD'
scene.world.color = (0.065, 0.065, 0.065)
scene.render.resolution_x = 1000
scene.render.resolution_y = 1100
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
camera_data = bpy.data.cameras.new('TerrainInspectionCamera')
camera = bpy.data.objects.new('TerrainInspectionCamera', camera_data)
scene.collection.objects.link(camera)
camera.location = (0, -3, 240)
camera.rotation_euler = (0, 0, 0)
camera_data.type = 'ORTHO'
camera_data.ortho_scale = 205
scene.camera = camera
scene.render.filepath = os.path.join(OUTPUT, 'Source_Top.png')
bpy.ops.render.render(write_still=True)
print('INSPECTION_COMPLETE')
