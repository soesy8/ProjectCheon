import bpy
import hashlib
import json
import math
import os
from collections import Counter
from mathutils import Vector

WORK = r'D:\project\ProjectCheon\Assets\Temp\TerrainExpansion~'
OUTPUT = os.path.join(WORK, 'Mountain_2Try_MaterialPreview.blend')


def geometry_signature(obj):
    digest = hashlib.sha256()
    digest.update(str([tuple(row) for row in obj.matrix_world]).encode())
    for vertex in obj.data.vertices:
        digest.update(str(tuple(vertex.co)).encode())
    for face in obj.data.polygons:
        digest.update(str(tuple(face.vertices)).encode())
    return digest.hexdigest()


def color(hex_color):
    values = [int(hex_color[index:index + 2], 16) / 255.0 for index in (0, 2, 4)]
    return tuple(value / 12.92 if value <= 0.04045 else ((value + 0.055) / 1.055) ** 2.4
                 for value in values) + (1.0,)


def material(name, display_color):
    result = bpy.data.materials.new(name)
    result.use_nodes = True
    result.diffuse_color = color(display_color)
    nodes = result.node_tree.nodes
    nodes.clear()
    output = nodes.new('ShaderNodeOutputMaterial')
    output.location = (1000, 80)
    shader = nodes.new('ShaderNodeBsdfPrincipled')
    shader.location = (720, 80)
    shader.inputs['Roughness'].default_value = 0.88
    shader.inputs['Specular IOR Level'].default_value = 0.24
    result.node_tree.links.new(shader.outputs['BSDF'], output.inputs['Surface'])
    position = nodes.new('ShaderNodeNewGeometry')
    position.location = (-1100, 100)
    return result, nodes, result.node_tree.links, shader, position


def noise(nodes, links, coordinates, scale, location, detail=3.0):
    node = nodes.new('ShaderNodeTexNoise')
    node.location = location
    node.inputs['Scale'].default_value = scale
    node.inputs['Detail'].default_value = detail
    node.inputs['Roughness'].default_value = 0.7
    links.new(coordinates, node.inputs['Vector'])
    return node.outputs['Fac']


def ramp(nodes, links, factor, stops, location):
    node = nodes.new('ShaderNodeValToRGB')
    node.location = location
    elements = node.color_ramp.elements
    elements.remove(elements[1])
    for index, (position, value) in enumerate(stops):
        element = elements[0] if index == 0 else elements.new(position)
        element.position = position
        element.color = color(value) if isinstance(value, str) else (value, value, value, 1)
    links.new(factor, node.inputs['Fac'])
    return node.outputs['Color']


def math_node(nodes, links, operation, first, second, location):
    node = nodes.new('ShaderNodeMath')
    node.operation = operation
    node.location = location
    for index, value in enumerate((first, second)):
        if isinstance(value, (float, int)):
            node.inputs[index].default_value = value
        else:
            links.new(value, node.inputs[index])
    return node.outputs[0]


def mix(nodes, links, factor, first, second, location):
    node = nodes.new('ShaderNodeMixRGB')
    node.location = location
    for index, value in enumerate((factor, first, second)):
        if isinstance(value, (float, int)):
            node.inputs[index].default_value = value
        elif isinstance(value, str):
            node.inputs[index].default_value = color(value)
        else:
            links.new(value, node.inputs[index])
    return node.outputs[0]


def bump(nodes, links, coordinates, shader, scale, distance, strength):
    height = noise(nodes, links, coordinates, scale, (-400, -520), 2.0)
    node = nodes.new('ShaderNodeBump')
    node.location = (450, -200)
    node.inputs['Distance'].default_value = distance
    node.inputs['Strength'].default_value = strength
    links.new(height, node.inputs['Height'])
    links.new(node.outputs['Normal'], shader.inputs['Normal'])


def assign(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    for face in obj.data.polygons:
        face.material_index = 0


before = {obj.name: geometry_signature(obj) for obj in bpy.data.objects if obj.type == 'MESH'}

# 바닥 가장자리에서만 잔디 비중을 높이며 실제 메쉬의 높이는 바꾸지 않는다.
ground, nodes, links, shader, geometry = material('M_Preview_MountainGround', 'A89874')
coarse = noise(nodes, links, geometry.outputs['Position'], 0.13, (-850, 360), 3.5)
soil = ramp(nodes, links, coarse, [(0.23, '88795D'), (0.52, 'A79672'), (0.78, 'C0B18D')], (-560, 430))
grass = ramp(nodes, links, coarse, [(0.25, '596D43'), (0.55, '83915D'), (0.78, 'A2A875')], (-560, 180))
attribute = nodes.new('ShaderNodeAttribute')
attribute.attribute_name = 'TerrainEdge'
attribute.location = (-900, -40)
edge_noise = math_node(nodes, links, 'MULTIPLY_ADD', coarse, 0.7, (-570, -50))
# 세 번째 입력을 0.3으로 두어 가장자리 잔디에도 미세한 농도 차이를 남긴다.
edge_noise_node = edge_noise.node
edge_noise_node.inputs[2].default_value = 0.3
edge_grass = math_node(nodes, links, 'MULTIPLY', attribute.outputs['Fac'], edge_noise, (-320, -40))
grass_factor = math_node(nodes, links, 'MULTIPLY_ADD', edge_grass, 0.9, (-100, -40))
grass_factor.node.inputs[2].default_value = 0.035
ground_color = mix(nodes, links, grass_factor, soil, grass, (200, 250))
links.new(ground_color, shader.inputs['Base Color'])
bump(nodes, links, geometry.outputs['Position'], shader, 4.0, 0.07, 0.28)

for name in ['Arena_Floor_Flat_UV', 'Shrine_Floor_Flat_UV']:
    obj = bpy.data.objects[name]
    highest = max(vertex.co.z for vertex in obj.data.vertices)
    top_faces = [face for face in obj.data.polygons
                 if all(abs(obj.data.vertices[index].co.z - highest) < 0.0001 for index in face.vertices)]
    edges = Counter(tuple(sorted((face.vertices[index], face.vertices[(index + 1) % len(face.vertices)])))
                    for face in top_faces for index in range(len(face.vertices)))
    segments = [(obj.data.vertices[first].co.xy, obj.data.vertices[second].co.xy)
                for (first, second), count in edges.items() if count == 1]
    edge_attribute = obj.data.attributes.new('TerrainEdge', 'FLOAT', 'POINT')
    border_width = 7.0 if name.startswith('Arena') else 4.0
    for vertex in obj.data.vertices:
        point = vertex.co.xy
        shortest = float('inf')
        for first, second in segments:
            delta = second - first
            amount = max(0.0, min(1.0, (point - first).dot(delta) / delta.length_squared))
            shortest = min(shortest, (point - (first + delta * amount)).length)
        edge_attribute.data[vertex.index].value = max(0.0, 1.0 - shortest / border_width)
    assign(obj, ground)

granite, nodes, links, shader, geometry = material('M_Preview_WeatheredGranite', '939485')
coarse = noise(nodes, links, geometry.outputs['Position'], 0.3, (-850, 330), 3.8)
stone_color = ramp(nodes, links, coarse, [(0.2, '62685F'), (0.48, '858B7E'), (0.8, 'AEAD9C')], (-580, 370))
normal = nodes.new('ShaderNodeSeparateXYZ')
normal.location = (-880, -50)
links.new(geometry.outputs['Normal'], normal.inputs[0])
upward = ramp(nodes, links, normal.outputs['Z'], [(0.15, 0.0), (0.8, 1.0)], (-650, -80))
moss_noise = ramp(nodes, links, coarse, [(0.48, 0.0), (0.69, 0.42)], (-350, -130))
moss = math_node(nodes, links, 'MULTIPLY', upward, moss_noise, (-70, -20))
rock_color = mix(nodes, links, moss, stone_color, '566C44', (230, 260))
links.new(rock_color, shader.inputs['Base Color'])
bump(nodes, links, geometry.outputs['Position'], shader, 2.8, 0.095, 0.32)
for name in ['Granite_Cliff_Lower', 'Granite_Cliff_Upper', 'Summit_Rim_Outcrops_Lower',
             'Summit_Rim_Outcrops_Upper', 'Rocky_Ledges_And_Foothills']:
    assign(bpy.data.objects[name], granite)

mountain, nodes, links, shader, geometry = material('M_Preview_MountainSlope', '7E8963')
coarse = noise(nodes, links, geometry.outputs['Position'], 0.15, (-850, 360), 3.0)
rock_color = ramp(nodes, links, coarse, [(0.2, '656A5C'), (0.52, '8B8D79'), (0.8, 'A6A38D')], (-560, 390))
grass_color = ramp(nodes, links, coarse, [(0.22, '53673E'), (0.5, '78844E'), (0.8, 'A0A36B')], (-560, 170))
normal = nodes.new('ShaderNodeSeparateXYZ')
normal.location = (-850, -70)
links.new(geometry.outputs['Normal'], normal.inputs[0])
slope = ramp(nodes, links, normal.outputs['Z'], [(0.25, 0.04), (0.78, 0.94)], (-580, -70))
variation = math_node(nodes, links, 'MULTIPLY_ADD', coarse, 0.5, (-330, -140))
variation.node.inputs[2].default_value = 0.42
grass_factor = math_node(nodes, links, 'MULTIPLY', slope, variation, (-80, -70))
surface_color = mix(nodes, links, grass_factor, rock_color, grass_color, (230, 260))
links.new(surface_color, shader.inputs['Base Color'])
bump(nodes, links, geometry.outputs['Position'], shader, 2.0, 0.08, 0.25)
assign(bpy.data.objects['Mountain_Continuous_Core'], mountain)

stairs, nodes, links, shader, geometry = material('M_Preview_WornStoneSteps', 'AAA58E')
coarse = noise(nodes, links, geometry.outputs['Position'], 1.1, (-850, 300), 2.5)
step_color = ramp(nodes, links, coarse, [(0.15, '7E8276'), (0.52, 'A4A28B'), (0.85, 'C1BAA0')], (-430, 280))
links.new(step_color, shader.inputs['Base Color'])
bump(nodes, links, geometry.outputs['Position'], shader, 5.0, 0.035, 0.3)
for name in ['Stairway_Between_Summits', 'Stairway_Winding_Approach']:
    assign(bpy.data.objects[name], stairs)

assert all(geometry_signature(bpy.data.objects[name]) == signature for name, signature in before.items())
scene = bpy.context.scene
preview_collection = bpy.data.collections.new('08_Material_Preview')
scene.collection.children.link(preview_collection)
world = bpy.data.worlds.new('MaterialPreviewWorld')
world.use_nodes = True
world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.60, 0.70, 0.82, 1)
world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.5
scene.world = world
sun_data = bpy.data.lights.new('Preview_Sun', 'SUN')
sun_data.energy = 2.5
sun_data.angle = 0.12
sun_data.color = (1.0, 0.93, 0.80)
sun = bpy.data.objects.new('Preview_Sun', sun_data)
preview_collection.objects.link(sun)
sun.rotation_euler = (math.radians(25), math.radians(-30), math.radians(-35))
fill_data = bpy.data.lights.new('Preview_SoftFill', 'AREA')
fill_data.energy = 80000
fill_data.shape = 'DISK'
fill_data.size = 100
fill_data.color = (0.76, 0.86, 1.0)
fill = bpy.data.objects.new('Preview_SoftFill', fill_data)
preview_collection.objects.link(fill)
fill.location = (85, -100, 150)
fill.rotation_euler = (Vector((10, 10, 15)) - fill.location).to_track_quat('-Z', 'Y').to_euler()
scene.render.engine = 'CYCLES'
scene.cycles.samples = 40
scene.cycles.use_denoising = True
scene.cycles.adaptive_threshold = 0.04
scene.cycles.max_bounces = 5
scene.cycles.diffuse_bounces = 3
cycles_preferences = bpy.context.preferences.addons['cycles'].preferences
cycles_preferences.compute_device_type = 'OPTIX'
cycles_preferences.get_devices()
for device in cycles_preferences.devices:
    device.use = device.type == 'OPTIX'
scene.cycles.device = 'GPU'
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
scene.render.film_transparent = True
scene.render.resolution_percentage = 100
scene.view_settings.view_transform = 'AgX'
scene.view_settings.exposure = 0.3


def camera(name, target, offset, resolution):
    data = bpy.data.cameras.new(name)
    result = bpy.data.objects.new(name, data)
    preview_collection.objects.link(result)
    data.type = 'ORTHO'
    result.location = Vector(target) + Vector(offset)
    result.rotation_euler = (Vector(target) - result.location).to_track_quat('-Z', 'Y').to_euler()
    scene.render.resolution_x, scene.render.resolution_y = resolution
    bpy.context.view_layer.update()
    rotation = result.rotation_euler.to_quaternion()
    points = [rotation.conjugated() @ (obj.matrix_world @ Vector(corner) - result.location)
              for obj in scene.objects if obj.type == 'MESH' and not obj.hide_render
              for corner in obj.bound_box]
    left, right = min(point.x for point in points), max(point.x for point in points)
    bottom, top = min(point.y for point in points), max(point.y for point in points)
    result.location += rotation @ Vector(((left + right) * 0.5, (bottom + top) * 0.5, 0))
    aspect = resolution[0] / resolution[1]
    data.ortho_scale = 1.08 * max(top - bottom, (right - left) / aspect)
    return result


perspective_camera = camera('Preview_Isometric', (20, 0, 18), (155, -225, 200), (1600, 1350))
top_camera = camera('Preview_Top', (20, 20, 0), (0, 0, 350), (1500, 1500))
scene.camera = perspective_camera
scene.render.resolution_x, scene.render.resolution_y = 1600, 1350
scene.render.filepath = os.path.join(WORK, 'MaterialPreview_Perspective.png')
bpy.ops.object.select_all(action='DESELECT')
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            area.spaces.active.shading.type = 'MATERIAL'
            area.spaces.active.shading.use_scene_world = False
            area.spaces.active.region_3d.view_location = Vector((20, 0, 18))
            area.spaces.active.region_3d.view_distance = 260
            area.spaces.active.region_3d.view_rotation = perspective_camera.rotation_euler.to_quaternion()
scene['임시_머테리얼'] = '흙·잔디 바닥, 경사에 따른 산기슭 초목, 이끼 화강암, 돌계단. 기존 소나무·건물 머테리얼 유지. 모든 지형 형상 유지.'
bpy.context.preferences.filepaths.save_version = 0
bpy.context.preferences.filepaths.file_preview_type = 'NONE'
bpy.ops.wm.save_as_mainfile(filepath=OUTPUT, check_existing=False)
report = {'file': OUTPUT, 'geometry_unchanged': True, 'mesh_objects': len(before),
          'preview_materials': [ground.name, granite.name, mountain.name, stairs.name],
          'file_images_packed': all(bool(image.packed_file) for image in bpy.data.images if image.source == 'FILE'),
          'renderer': 'Cycles OPTIX', 'samples': scene.cycles.samples}
with open(os.path.join(WORK, 'material_preview_report.json'), 'w', encoding='utf-8') as output:
    json.dump(report, output, ensure_ascii=False, indent=2)
print('MATERIALS_READY:' + json.dumps(report), flush=True)
bpy.ops.render.render(write_still=True)
print('PERSPECTIVE_RENDER_COMPLETE', flush=True)
scene.camera = top_camera
scene.render.resolution_x, scene.render.resolution_y = 1500, 1500
scene.render.filepath = os.path.join(WORK, 'MaterialPreview_Top.png')
bpy.ops.render.render(write_still=True)
print('TOP_RENDER_COMPLETE', flush=True)
