import bpy
import json
from pathlib import Path
from mathutils import Vector

out = Path(r'D:\project\ProjectCheon\Assets\Temp\CheonPlayerTexture~')
scene = bpy.context.scene
if bpy.context.object and bpy.context.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')
report = []
for obj in scene.objects:
    if obj.type != 'MESH':
        continue
    mesh = obj.data
    adjacency = [[] for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = edge.vertices
        adjacency[a].append(b)
        adjacency[b].append(a)
    visited = set()
    components = []
    for vertex in mesh.vertices:
        if vertex.index in visited:
            continue
        stack = [vertex.index]
        indices = []
        visited.add(vertex.index)
        while stack:
            index = stack.pop()
            indices.append(index)
            for other in adjacency[index]:
                if other not in visited:
                    visited.add(other)
                    stack.append(other)
        coords = [obj.matrix_world @ mesh.vertices[i].co for i in indices]
        bounds = [[min(p[k] for p in coords), max(p[k] for p in coords)] for k in range(3)]
        index_set = set(indices)
        faces = [p.index for p in mesh.polygons if p.vertices[0] in index_set]
        comp = {'index': len(components), 'vertices': len(indices), 'faces': len(faces), 'bounds': bounds, 'vertex_indices': indices, 'face_indices': faces}
        components.append(comp)
    report.append({'name': obj.name, 'components': components})
(out / 'components.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
for name, filename in [('T_PlayerCharacter_Front.png','T_SourceFront.png'), ('T_PlayerCharacter_Side_Aligned.png','T_SourceSide.png')]:
    bpy.data.images[name].save_render(str(out / filename))
scene.render.engine = 'BLENDER_WORKBENCH'
scene.render.resolution_x = 1000
scene.render.resolution_y = 1200
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
shading = scene.display.shading
shading.light = 'STUDIO'
shading.color_type = 'OBJECT'
shading.show_shadows = True
shading.show_cavity = True
shading.cavity_type = 'BOTH'
shading.background_type = 'WORLD'
scene.world.color = (0.10, 0.11, 0.14)
for obj in scene.objects:
    obj.color = (0.65, 0.65, 0.65, 1)
    if obj.name == 'Hair':
        obj.color = (.15, .085, .045, 1)
    if obj.name.startswith('Gat'):
        obj.color = (.035, .04, .05, 1)
    if obj.name == 'Pouch':
        obj.color = (.28, .13, .045, 1)
    if obj.hide_get():
        obj.hide_render = True
camera_data = bpy.data.cameras.new('PreviewCamera')
camera = bpy.data.objects.new('PreviewCamera', camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
camera_data.type = 'ORTHO'
camera_data.ortho_scale = 2.4
for name, position in [('source_front.png', (0,-6,1.1)), ('source_back.png', (0,6,1.1)), ('source_threequarter.png', (4,-6,2.7))]:
    camera.location = position
    camera.rotation_euler = (Vector((0,0,1.1)) - camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath = str(out / name)
    bpy.ops.render.render(write_still=True)
print('ANALYSIS_COMPLETE')
