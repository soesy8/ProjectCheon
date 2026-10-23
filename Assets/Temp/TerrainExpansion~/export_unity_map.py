import bpy
import hashlib
import json
import math
import os
import re
import time
from mathutils import Vector

PROJECT = r'D:\project\ProjectCheon'
WORK = os.path.join(PROJECT, 'Assets', 'Temp', 'TerrainUnityImport~')
TEXTURES = os.path.join(WORK, 'Textures')
DESTINATION = 'Assets/Test/CheonMountainMap'
os.makedirs(TEXTURES, exist_ok=True)


def safe_name(value):
    return re.sub(r'[^A-Za-z0-9_]+', '_', value).strip('_')


def digest_file(path):
    with open(path, 'rb') as file:
        return hashlib.sha256(file.read()).hexdigest()


def geometry_signature(obj):
    digest = hashlib.sha256()
    digest.update(str([tuple(row) for row in obj.matrix_world]).encode())
    for vertex in obj.data.vertices:
        digest.update(str(tuple(vertex.co)).encode())
    for face in obj.data.polygons:
        digest.update(str(tuple(face.vertices)).encode())
    return digest.hexdigest()


original_meshes = [obj for obj in bpy.data.objects if obj.type == 'MESH']
before_geometry = {obj.name: geometry_signature(obj) for obj in original_meshes}
project_textures = {}
for directory, folders, filenames in os.walk(os.path.join(PROJECT, 'Assets', 'Arts')):
    for filename in filenames:
        if filename.lower().endswith(('.png', '.jpg', '.jpeg', '.tga')):
            path = os.path.join(directory, filename)
            project_textures[digest_file(path)] = os.path.relpath(path, PROJECT).replace('\\', '/')

manifest = {'source': bpy.data.filepath, 'source_sha256': digest_file(bpy.data.filepath),
            'materials': [], 'textures_to_copy': [], 'mesh_count': len(original_meshes)}
saved_images = {}


def export_image(image):
    if image.name in saved_images:
        return saved_images[image.name]
    if image.packed_file:
        data = bytes(image.packed_file.data)
    else:
        with open(bpy.path.abspath(image.filepath), 'rb') as file:
            data = file.read()
    digest = hashlib.sha256(data).hexdigest()
    if digest in project_textures:
        target = project_textures[digest]
    else:
        filename = 'Test_T_Map_' + safe_name(os.path.splitext(image.name)[0]) + '_' + digest[:8] + '.png'
        with open(os.path.join(TEXTURES, filename), 'wb') as output:
            output.write(data)
        target = DESTINATION + '/Textures/' + filename
        manifest['textures_to_copy'].append(filename)
    saved_images[image.name] = target
    return target


def base_image(material):
    if not material.use_nodes:
        return None, None
    shader = next((node for node in material.node_tree.nodes if node.type == 'BSDF_PRINCIPLED'), None)
    if shader is None:
        return None, None
    visited = set()
    pending = [link.from_node for link in shader.inputs['Base Color'].links]
    while pending:
        node = pending.pop(0)
        if node in visited:
            continue
        visited.add(node)
        if node.type == 'TEX_IMAGE' and node.image is not None:
            return shader, node.image
        pending.extend(link.from_node for socket in node.inputs for link in socket.links)
    return shader, None


native_materials = {slot.material for obj in original_meshes for slot in obj.material_slots
                    if slot.material and not slot.material.name.startswith('M_Preview_')}
for material in sorted(native_materials, key=lambda item: item.name):
    shader, image = base_image(material)
    base_color = list(shader.inputs['Base Color'].default_value) if shader else list(material.diffuse_color)
    if image:
        base_color = [1.0, 1.0, 1.0, 1.0]
    manifest['materials'].append({'source_name': material.name,
                                  'asset_name': 'Test_M_Map_' + safe_name(material.name.removeprefix('M_')),
                                  'texture': export_image(image) if image else '',
                                  'base_color': base_color, 'terrain': False})

scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = 1
scene.cycles.use_denoising = False
preferences = bpy.context.preferences.addons['cycles'].preferences
preferences.compute_device_type = 'OPTIX'
preferences.get_devices()
for device in preferences.devices:
    device.use = device.type == 'OPTIX'
scene.cycles.device = 'GPU'
scene.render.bake.margin = 12
scene.render.bake.use_clear = True


def select(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.hide_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def floor_bake_mesh(obj):
    maximum_z = max(vertex.co.z for vertex in obj.data.vertices)
    faces = [tuple(face.vertices) for face in obj.data.polygons
             if all(abs(obj.data.vertices[index].co.z - maximum_z) < 0.0001 for index in face.vertices)]
    indices = sorted({index for face in faces for index in face})
    lookup = {old: new for new, old in enumerate(indices)}
    mesh = bpy.data.meshes.new('Temporary_Floor_Bake')
    mesh.from_pydata([obj.data.vertices[index].co.copy() for index in indices], [],
                     [tuple(lookup[index] for index in face) for face in faces])
    mesh.update()
    duplicate = bpy.data.objects.new('Temporary_Floor_Bake', mesh)
    scene.collection.objects.link(duplicate)
    duplicate.matrix_world = obj.matrix_world.copy()
    uv = mesh.uv_layers.new(name='UVMap')
    original_uv = {}
    for loop in obj.data.loops:
        original_uv[loop.vertex_index] = obj.data.uv_layers['UVMap'].data[loop.index].uv.copy()
    for loop in mesh.loops:
        uv.data[loop.index].uv = original_uv[indices[loop.vertex_index]]
    attribute = mesh.attributes.new('TerrainEdge', 'FLOAT', 'POINT')
    source_attribute = obj.data.attributes['TerrainEdge']
    for index, old_index in enumerate(indices):
        attribute.data[index].value = source_attribute.data[old_index].value
    return duplicate


bake_targets = [
    ('Arena_Floor_Flat_UV', 'UpperGround', 4096),
    ('Shrine_Floor_Flat_UV', 'ShrineGround', 2048),
    ('Mountain_Continuous_Core', 'MountainSlope', 4096),
    ('Granite_Cliff_Upper', 'UpperCliffs', 2048),
    ('Granite_Cliff_Lower', 'LowerCliffs', 2048),
    ('Summit_Rim_Outcrops_Upper', 'UpperRocks', 2048),
    ('Summit_Rim_Outcrops_Lower', 'LowerRocks', 2048),
    ('Rocky_Ledges_And_Foothills', 'FoothillRocks', 2048),
    ('Stairway_Between_Summits', 'SummitStairs', 2048),
    ('Stairway_Winding_Approach', 'ApproachStairs', 2048),
]
for number, (object_name, label, resolution) in enumerate(bake_targets, 1):
    started = time.time()
    obj = bpy.data.objects[object_name]
    source_material = obj.data.materials[0]
    baked_material = source_material.copy()
    baked_material.name = 'Test_M_Map_' + label
    obj.data.materials.clear()
    obj.data.materials.append(baked_material)
    for face in obj.data.polygons:
        face.material_index = 0
    duplicate = floor_bake_mesh(obj) if 'Floor_Flat_UV' in object_name else None
    target = duplicate or obj
    if duplicate:
        duplicate.data.materials.append(baked_material)
    select(target)
    if not duplicate:
        if not obj.data.uv_layers:
            obj.data.uv_layers.new(name='UVMap')
        obj.data.uv_layers.active_index = 0
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.008, area_weight=0.05)
        bpy.ops.object.mode_set(mode='OBJECT')
    filename = 'Test_T_Map_' + label + '.png'
    texture_path = os.path.join(TEXTURES, filename)
    reuse_texture = os.path.exists(texture_path)
    image = bpy.data.images.load(texture_path, check_existing=False) if reuse_texture else bpy.data.images.new('Test_T_Map_' + label, width=resolution, height=resolution, alpha=False)
    image.colorspace_settings.name = 'sRGB'
    nodes = baked_material.node_tree.nodes
    links = baked_material.node_tree.links
    shader = next(node for node in nodes if node.type == 'BSDF_PRINCIPLED')
    output = next(node for node in nodes if node.type == 'OUTPUT_MATERIAL')
    emission = nodes.new('ShaderNodeEmission')
    emission.inputs['Strength'].default_value = 1
    if shader.inputs['Base Color'].is_linked:
        links.new(shader.inputs['Base Color'].links[0].from_socket, emission.inputs['Color'])
    else:
        emission.inputs['Color'].default_value = shader.inputs['Base Color'].default_value
    links.new(emission.outputs[0], output.inputs['Surface'])
    image_node = nodes.new('ShaderNodeTexImage')
    image_node.image = image
    nodes.active = image_node
    if not reuse_texture:
        bpy.ops.object.bake(type='EMIT')
        image.filepath_raw = texture_path
        image.file_format = 'PNG'
        image.save()
    # FBX에는 절차형 노드 대신 베이크된 색상 텍스처를 연결한다.
    links.new(image_node.outputs['Color'], shader.inputs['Base Color'])
    links.new(shader.outputs['BSDF'], output.inputs['Surface'])
    if duplicate:
        mesh = duplicate.data
        bpy.data.objects.remove(duplicate, do_unlink=True)
        bpy.data.meshes.remove(mesh)
    manifest['materials'].append({'source_name': baked_material.name, 'asset_name': baked_material.name,
                                  'texture': DESTINATION + '/Textures/' + filename,
                                  'base_color': [1, 1, 1, 1], 'terrain': True})
    manifest['textures_to_copy'].append(filename)
    print('BAKE_COMPLETE:%d/%d %s %.1fs' % (number, len(bake_targets), label, time.time()-started), flush=True)

assert all(geometry_signature(bpy.data.objects[name]) == digest for name, digest in before_geometry.items())
# 공유 소나무 메쉬의 오브젝트별 슬롯을 데이터 슬롯으로 통일해 FBX 참조를 보존한다.
pine_material = bpy.data.materials['M_Pine_ReferencePaint']
for obj in original_meshes:
    if obj.name.startswith('Pine_'):
        obj.data = obj.data.copy()
        for slot in obj.material_slots:
            slot.link = 'DATA'
            slot.material = pine_material
# Unity가 산신각 계단의 오목한 다각형을 버리지 않도록 내보낼 때만 삼각화한다.
shrine_stairs = bpy.data.objects.get('Stairs')
if shrine_stairs is not None and shrine_stairs.type == 'MESH':
    triangulate = shrine_stairs.modifiers.new('Unity_Export_Triangulate', 'TRIANGULATE')
    triangulate.quad_method = 'BEAUTY'
    triangulate.ngon_method = 'BEAUTY'
bpy.ops.object.select_all(action='DESELECT')
export_objects = [obj for obj in bpy.data.objects if obj.type in {'MESH', 'EMPTY'}]
for obj in export_objects:
    obj.select_set(True)
model_path = os.path.join(WORK, 'Test_CheonMountainMap.fbx')
bpy.ops.export_scene.fbx(filepath=model_path, use_selection=True, object_types={'MESH', 'EMPTY'},
                         axis_forward='-Z', axis_up='Y', apply_unit_scale=True, global_scale=1,
                         use_mesh_modifiers=True, mesh_smooth_type='FACE', bake_anim=False,
                         path_mode='STRIP', add_leaf_bones=False)
points = [obj.matrix_world @ Vector(corner) for obj in original_meshes for corner in obj.bound_box]
manifest['bounds_blender'] = [[min(point[axis] for point in points) for axis in range(3)],
                              [max(point[axis] for point in points) for axis in range(3)]]
manifest['geometry_unchanged'] = True
manifest['texture_count'] = len(manifest['textures_to_copy'])
manifest['material_count'] = len(manifest['materials'])
with open(os.path.join(WORK, 'map_manifest.json'), 'w', encoding='utf-8') as output:
    json.dump(manifest, output, ensure_ascii=False, indent=2)
print('UNITY_EXPORT_COMPLETE:' + json.dumps({key: value for key, value in manifest.items()
                                           if key not in {'materials', 'textures_to_copy'}}), flush=True)
