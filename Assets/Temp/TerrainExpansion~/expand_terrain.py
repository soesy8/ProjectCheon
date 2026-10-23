import bpy
import bmesh
import hashlib
import json
import math
import os
import random
from collections import Counter
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

WORK = r'D:\project\ProjectCheon\Assets\Temp\TerrainExpansion~'
OUTPUT = os.path.join(WORK, 'Mountain_2Try_Expanded.blend')
GATE_X = 6.753618240356445
ARENA_CENTER = Vector((7.0, 40.0, 37.0))
FLOOR_HEIGHT = 37.12
random.seed(1023)


def smooth_step(value):
    value = max(0.0, min(1.0, value))
    return value * value * (3.0 - 2.0 * value)


def deform_point(point):
    # 계단 주변은 고정하고, 북쪽으로 갈수록 넓어지는 단조 변형으로 면 뒤집힘을 피한다.
    x, y, z = point
    horizontal = x - GATE_X
    upper_weight = smooth_step((y - 3.0) / 17.0)
    side_weight = smooth_step((abs(horizontal) - 8.0) / 16.0)
    north_weight = smooth_step((y - 24.0) / 10.0)
    expansion_weight = side_weight + (1.0 - side_weight) * north_weight
    # 산기슭은 정상부의 이동량만 따라가게 하여 사면 폭까지 과하게 늘어나지 않게 한다.
    limited_horizontal = max(-32.0, min(32.0, horizontal))
    side_expansion = 0.55 if horizontal < 0.0 else 1.2
    new_x = x + side_expansion * limited_horizontal * expansion_weight * upper_weight
    center_shift_weight = 1.0 - smooth_step(max(0.0, -horizontal) / 32.0)
    new_x += 22.1 * smooth_step((y - 24.0) / 31.0) * center_shift_weight
    distance = max(0.0, y - 24.0)
    if distance < 8.0:
        ratio = distance / 8.0
        extension = 8.0 * (ratio ** 3 - 0.5 * ratio ** 4)
    elif y <= 68.0:
        extension = distance - 4.0
    else:
        north_distance = min(8.0, y - 68.0)
        ratio = north_distance / 8.0
        extension = 40.0 + north_distance - 8.0 * (ratio ** 3 - 0.5 * ratio ** 4)
    return Vector((new_x, y + 1.3 * extension, z))


def local_rotation(center):
    tangent = Vector((-(center.y - ARENA_CENTER.y), center.x - ARENA_CENTER.x, 0.0))
    if tangent.length < 0.01:
        return Matrix.Identity(3)
    tangent.normalize()
    mapped = deform_point(center + tangent * 0.01) - deform_point(center - tangent * 0.01)
    angle = math.atan2(mapped.y, mapped.x) - math.atan2(tangent.y, tangent.x)
    return Matrix.Rotation(angle, 3, 'Z')


def components(obj):
    vertices = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
    adjacency = [[] for _ in vertices]
    for edge in obj.data.edges:
        first, second = edge.vertices
        adjacency[first].append(second)
        adjacency[second].append(first)
    labels = [-1] * len(vertices)
    groups = []
    for seed in range(len(vertices)):
        if labels[seed] >= 0:
            continue
        indices = [seed]
        labels[seed] = len(groups)
        stack = [seed]
        while stack:
            for neighbor in adjacency[stack.pop()]:
                if labels[neighbor] < 0:
                    labels[neighbor] = len(groups)
                    indices.append(neighbor)
                    stack.append(neighbor)
        lookup = {old: new for new, old in enumerate(indices)}
        points = [vertices[i] for i in indices]
        low = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
        high = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
        groups.append({'vertices': points, 'indices': lookup, 'faces': [],
                       'center': (low + high) * 0.5})
    for face in obj.data.polygons:
        group = groups[labels[face.vertices[0]]]
        group['faces'].append(tuple(group['indices'][index] for index in face.vertices))
    return groups


def mesh_signature(obj):
    digest = hashlib.sha256()
    digest.update(str([tuple(row) for row in obj.matrix_world]).encode())
    if obj.type == 'MESH':
        for vertex in obj.data.vertices:
            digest.update(str(tuple(vertex.co)).encode())
        for polygon in obj.data.polygons:
            digest.update(str(tuple(polygon.vertices)).encode())
    return digest.hexdigest()


def ordered_angle(center):
    return math.atan2(center.y - ARENA_CENTER.y, center.x - ARENA_CENTER.x)


def is_entrance_gap(first, second):
    middle = (first['center'] + second['center']) * 0.5
    return middle.y < 21.0 and abs(middle.x - GATE_X) < 11.5


def densify_rock_ring(obj):
    groups = components(obj)
    ordered = sorted(groups, key=lambda group: ordered_angle(group['center']))
    segments = []
    for index, first in enumerate(ordered):
        second = ordered[(index + 1) % len(ordered)]
        if is_entrance_gap(first, second):
            continue
        old_length = (first['center'] - second['center']).xy.length
        new_length = (deform_point(first['center']) - deform_point(second['center'])).xy.length
        segments.append({'first': first, 'second': second, 'old_length': old_length,
                         'new_length': new_length, 'additions': 0})
    old_perimeter = sum(segment['old_length'] for segment in segments)
    new_perimeter = sum(segment['new_length'] for segment in segments)
    extra_count = round(len(groups) * (new_perimeter / old_perimeter - 1.0))
    # 늘어난 구간에만 바위를 보충하며, 기존 바위의 크기와 세로 실루엣은 유지한다.
    for _ in range(max(0, extra_count)):
        chosen = max(segments, key=lambda segment: segment['new_length'] / (segment['additions'] + 1))
        chosen['additions'] += 1
    all_vertices = []
    all_faces = []

    def append_group(template, source_center, scale=1.0, extra_angle=0.0):
        destination = deform_point(source_center)
        rotation = local_rotation(source_center) @ Matrix.Rotation(extra_angle, 3, 'Z')
        offset = len(all_vertices)
        for point in template['vertices']:
            relative = point - template['center']
            transformed = rotation @ Vector((relative.x * scale, relative.y * scale, relative.z))
            all_vertices.append(destination + transformed)
        all_faces.extend(tuple(offset + index for index in face) for face in template['faces'])

    for group in groups:
        append_group(group, group['center'])
    for segment in segments:
        for addition in range(segment['additions']):
            factor = (addition + 1) / (segment['additions'] + 1)
            center = segment['first']['center'].lerp(segment['second']['center'], factor)
            template = segment['first'] if addition % 2 == 0 else segment['second']
            append_group(template, center, random.uniform(0.88, 1.05), random.uniform(-0.13, 0.13))
    previous_mesh = obj.data
    mesh = bpy.data.meshes.new(previous_mesh.name + '_Expanded')
    mesh.from_pydata(all_vertices, [], all_faces)
    mesh.update()
    for material in previous_mesh.materials:
        mesh.materials.append(material)
    obj.data = mesh
    obj.matrix_world = Matrix.Identity(4)
    if previous_mesh.users == 0:
        bpy.data.meshes.remove(previous_mesh)
    return {'before_count': len(groups), 'after_count': len(groups) + max(0, extra_count),
            'before_ring_length_m': old_perimeter, 'after_ring_length_m': new_perimeter,
            'density_ratio': ((len(groups) + max(0, extra_count)) / new_perimeter) /
                             (len(groups) / old_perimeter)}


protected_names = [obj.name for obj in bpy.data.objects if obj.name not in {
    'Mountain_Continuous_Core', 'Granite_Cliff_Upper', 'Summit_Rim_Outcrops_Upper',
    'Rocky_Ledges_And_Foothills', 'Terrace_Upper_Fractured_Bedrock',
    *['Pine_%02d' % number for number in range(8, 16)], 'Pine_20'}]
protected = {name: mesh_signature(bpy.data.objects[name]) for name in protected_names}
core = bpy.data.objects['Mountain_Continuous_Core']
source_points = [core.matrix_world @ vertex.co for vertex in core.data.vertices]
top_faces = [tuple(face.vertices) for face in core.data.polygons
             if all(source_points[index].z > 36.99 for index in face.vertices)]
top_indices = sorted({index for face in top_faces for index in face})
top_lookup = {old: new for new, old in enumerate(top_indices)}
floor_points = [deform_point(source_points[index]) for index in top_indices]
floor_faces = [tuple(top_lookup[index] for index in face) for face in top_faces]
floor_edges = Counter(tuple(sorted((face[index], face[(index + 1) % len(face)])))
                      for face in floor_faces for index in range(len(face)))
source_area = sum(abs(sum(source_points[face[i]].x * source_points[face[(i + 1) % len(face)]].y -
                         source_points[face[(i + 1) % len(face)]].x * source_points[face[i]].y
                         for i in range(len(face)))) * 0.5 for face in top_faces)

for vertex in core.data.vertices:
    vertex.co = deform_point(vertex.co)
core.data.update()
rock_reports = {}
for name in ['Granite_Cliff_Upper', 'Summit_Rim_Outcrops_Upper']:
    rock_reports[name] = densify_rock_ring(bpy.data.objects[name])

foothills = bpy.data.objects['Rocky_Ledges_And_Foothills']
for group in components(foothills):
    offset = deform_point(group['center']) - group['center']
    rotation = local_rotation(group['center']) if group['center'].y > 12.0 else Matrix.Identity(3)
    if offset.length < 0.001:
        continue
    for old_index in group['indices']:
        original = foothills.data.vertices[old_index].co.copy()
        foothills.data.vertices[old_index].co = group['center'] + offset + rotation @ (original - group['center'])
foothills.data.update()

old_floor = bpy.data.objects['Terrace_Upper_Fractured_Bedrock']
floor_collection = old_floor.users_collection[0]
old_floor_mesh = old_floor.data
bpy.data.objects.remove(old_floor, do_unlink=True)
if old_floor_mesh.users == 0:
    bpy.data.meshes.remove(old_floor_mesh)

# 판석별 높낮이와 틈을 없애고, 한 장으로 이어진 수평 상판 및 얇은 측면을 만든다.
point_count = len(floor_points)
positions = [(point.x, point.y, FLOOR_HEIGHT) for point in floor_points]
positions += [(point.x, point.y, FLOOR_HEIGHT - 0.18) for point in floor_points]
all_floor_faces = list(floor_faces)
all_floor_faces += [tuple(point_count + index for index in reversed(face)) for face in floor_faces]
for face in floor_faces:
    for index, first in enumerate(face):
        second = face[(index + 1) % len(face)]
        if floor_edges[tuple(sorted((first, second)))] == 1:
            all_floor_faces.append((second, first, point_count + first, point_count + second))
floor_mesh = bpy.data.meshes.new('Arena_Floor_Flat_UV_Mesh')
floor_mesh.from_pydata(positions, [], all_floor_faces)
floor_mesh.update()
floor_obj = bpy.data.objects.new('Arena_Floor_Flat_UV', floor_mesh)
floor_collection.objects.link(floor_obj)
floor_obj['설명'] = '확장 전투장용 연속 수평 바닥. 판석 틈과 단차 없음. 상판 Z=37.12m.'
floor_obj['UV_설명'] = 'UVMap: 0~1 정사각 비율, UV_Tile_8m: 8m 반복 텍스처용. 텍스처 이미지 미지정.'
ground_material = bpy.data.materials.new('M_Arena_Ground')
ground_material.diffuse_color = (0.48, 0.48, 0.48, 1.0)
ground_material.use_nodes = True
principled = ground_material.node_tree.nodes.get('Principled BSDF')
principled.inputs['Base Color'].default_value = (0.48, 0.48, 0.48, 1.0)
principled.inputs['Roughness'].default_value = 0.9
floor_mesh.materials.append(ground_material)
min_x = min(point.x for point in floor_points)
min_y = min(point.y for point in floor_points)
max_x = max(point.x for point in floor_points)
max_y = max(point.y for point in floor_points)
uv_size = max(max_x - min_x, max_y - min_y)
uv_map = floor_mesh.uv_layers.new(name='UVMap')
tiled_uv = floor_mesh.uv_layers.new(name='UV_Tile_8m')
for loop in floor_mesh.loops:
    coordinate = floor_mesh.vertices[loop.vertex_index].co
    uv_map.data[loop.index].uv = ((coordinate.x - min_x) / uv_size, (coordinate.y - min_y) / uv_size)
    tiled_uv.data[loop.index].uv = (coordinate.x / 8.0, coordinate.y / 8.0)
floor_mesh.uv_layers.active_index = 0
uv_map.active_render = True

tree_collection = bpy.data.collections['07_Planted_Pines']
upper_trees = [bpy.data.objects['Pine_%02d' % number] for number in range(8, 16)]
tree_positions = {tree.name: tree.location.copy() for tree in upper_trees}
ordered_trees = sorted(upper_trees, key=lambda tree: ordered_angle(tree.location))
tree_segments = []
for index, first in enumerate(ordered_trees):
    second = ordered_trees[(index + 1) % len(ordered_trees)]
    old_first = tree_positions[first.name]
    old_second = tree_positions[second.name]
    middle = (old_first + old_second) * 0.5
    if middle.y < 20.0 and abs(middle.x - GATE_X) < 12.0:
        continue
    tree_segments.append({'first': first, 'second': second, 'a': old_first, 'b': old_second,
                          'old': (old_first - old_second).xy.length,
                          'new': (deform_point(old_first) - deform_point(old_second)).xy.length,
                          'additions': 0})
old_tree_length = sum(segment['old'] for segment in tree_segments)
new_tree_length = sum(segment['new'] for segment in tree_segments)
new_tree_count = round(len(upper_trees) * new_tree_length / old_tree_length)
for _ in range(new_tree_count - len(upper_trees)):
    chosen = max(tree_segments, key=lambda segment: segment['new'] / (segment['additions'] + 1))
    chosen['additions'] += 1
all_upper_trees = list(upper_trees)
for tree in upper_trees + [bpy.data.objects['Pine_20']]:
    source_location = tree.location.copy()
    tree.location = deform_point(source_location)
    tree.rotation_euler.z += local_rotation(source_location).to_euler().z
for segment in tree_segments:
    for addition in range(segment['additions']):
        factor = (addition + 1) / (segment['additions'] + 1)
        source_location = segment['a'].lerp(segment['b'], factor)
        template = segment['first']
        tree = template.copy()
        tree.data = template.data
        tree.name = 'Pine_Arena_%02d' % (len([obj for obj in tree_collection.objects if obj.name.startswith('Pine_Arena_')]) + 1)
        tree_collection.objects.link(tree)
        tree.location = deform_point(source_location)
        tree.rotation_euler.z += random.uniform(-0.5, 0.5)
        tree.scale *= random.uniform(0.9, 1.07)
        all_upper_trees.append(tree)

# 소나무의 바닥 경계로부터의 간격은 확장 비율에 따라 늘리지 않고 일정하게 유지한다.
boundary_segments = [(Vector(positions[first][:2]), Vector(positions[second][:2]))
                     for (first, second), count in floor_edges.items() if count == 1]
floor_center = Vector(((min_x + max_x) * 0.5, (min_y + max_y) * 0.5))
for tree in all_upper_trees:
    location = tree.location.xy
    closest = None
    shortest = float('inf')
    for start, end in boundary_segments:
        direction = end - start
        ratio = max(0.0, min(1.0, (location - start).dot(direction) / direction.length_squared))
        point = start + direction * ratio
        distance = (point - location).length_squared
        if distance < shortest:
            shortest = distance
            closest = point
    inward = (floor_center - closest).normalized()
    destination = closest + inward * 3.3
    tree.location.x, tree.location.y = destination

bpy.context.view_layer.update()
terrain_vertices = []
terrain_faces = []
for obj in [core, floor_obj, bpy.data.objects['Granite_Cliff_Upper'],
            bpy.data.objects['Summit_Rim_Outcrops_Upper'], foothills]:
    start = len(terrain_vertices)
    terrain_vertices.extend(obj.matrix_world @ vertex.co for vertex in obj.data.vertices)
    terrain_faces.extend(tuple(start + index for index in face.vertices) for face in obj.data.polygons)
surface_bvh = BVHTree.FromPolygons(terrain_vertices, terrain_faces)
tree_contacts = []
for tree in all_upper_trees + [bpy.data.objects['Pine_20']]:
    hit, normal, face_index, distance = surface_bvh.ray_cast(Vector((tree.location.x, tree.location.y, 150)), Vector((0, 0, -1)))
    assert hit is not None, 'Tree has no supporting terrain: ' + tree.name
    minimum_z = min((tree.matrix_world @ vertex.co).z for vertex in tree.data.vertices)
    tree.location.z += hit.z - 0.055 - minimum_z
    tree['placement_surface_z'] = hit.z
    tree_contacts.append({'name': tree.name, 'ground_z': hit.z})

bpy.context.view_layer.update()
report = {'input': r'E:\C.J.I\Blender\AutoModeling\Mountain\2Try\Mountain_2Try_Expanded.blend',
          'input_sha256': hashlib.sha256(open(bpy.data.filepath, 'rb').read()).hexdigest(),
          'floor_height_m': FLOOR_HEIGHT,
          'floor_bounds_xy': [[min_x, min_y], [max_x, max_y]],
          'source_floor_area_m2': source_area,
          'expanded_floor_area_m2': sum(face.area for face in floor_mesh.polygons[:len(floor_faces)]),
          'rock_rings': rock_reports,
          'trees': {'before': len(upper_trees), 'after': new_tree_count,
                    'old_ring_length_m': old_tree_length, 'new_ring_length_m': new_tree_length,
                    'density_ratio': (new_tree_count / new_tree_length) / (len(upper_trees) / old_tree_length),
                    'contacts': tree_contacts},
          'protected_objects_unchanged': all(mesh_signature(bpy.data.objects[name]) == signature
                                             for name, signature in protected.items()),
          'protected_object_count': len(protected), 'validation': []}
for obj in [core, floor_obj, bpy.data.objects['Granite_Cliff_Upper'], bpy.data.objects['Summit_Rim_Outcrops_Upper']]:
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    report['validation'].append({'name': obj.name,
                                 'nonmanifold_edges': sum(not edge.is_manifold for edge in bm.edges),
                                 'degenerate_faces': sum(face.calc_area() < 1e-7 for face in bm.faces)})
    bm.free()
assert report['protected_objects_unchanged'], 'A protected object changed'
assert all(item['nonmanifold_edges'] == 0 and item['degenerate_faces'] == 0 for item in report['validation'])
floor_obj['surface_area_m2'] = report['expanded_floor_area_m2']
bpy.context.scene['arena_expansion_20261023'] = json.dumps(report, ensure_ascii=False)
bpy.context.scene['작업내용'] = '첨부 상단 뷰의 붉은 영역 방향으로 전투장 확장. 외곽 바위와 소나무 밀도 유지. 전투장 바닥은 연속 평면 및 텍스처용 UV 제공.'

for image in bpy.data.images:
    if image.source == 'FILE' and image.has_data and not image.packed_file:
        image.pack()
bpy.ops.object.select_all(action='DESELECT')
floor_obj.select_set(True)
bpy.context.view_layer.objects.active = floor_obj
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            area.spaces.active.region_3d.view_location = Vector((26, 27, 20))
            area.spaces.active.region_3d.view_distance = 240
bpy.context.preferences.filepaths.save_version = 0
if 'NONE' in bpy.context.preferences.filepaths.bl_rna.properties['file_preview_type'].enum_items.keys():
    bpy.context.preferences.filepaths.file_preview_type = 'NONE'
bpy.ops.wm.save_as_mainfile(filepath=OUTPUT, check_existing=False)
with open(os.path.join(WORK, 'expansion_report.json'), 'w', encoding='utf-8') as output:
    json.dump(report, output, indent=2, ensure_ascii=False)
print('EXPANSION_COMPLETE:' + json.dumps(report, ensure_ascii=True))

# 미리보기용 카메라는 저장한 작업 파일에 추가하지 않는다.
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
scene.render.resolution_x = 1300
scene.render.resolution_y = 1400
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
camera_data = bpy.data.cameras.new('TerrainPreviewCamera')
camera = bpy.data.objects.new('TerrainPreviewCamera', camera_data)
scene.collection.objects.link(camera)
camera_data.type = 'ORTHO'
camera_data.ortho_scale = 267
camera.location = (26, 27, 320)
camera.rotation_euler = (0, 0, 0)
scene.camera = camera


def frame_preview():
    bpy.context.view_layer.update()
    rotation = camera.rotation_euler.to_quaternion()
    inverse_rotation = rotation.conjugated()
    points = [inverse_rotation @ (obj.matrix_world @ Vector(corner) - camera.location)
              for obj in scene.objects if obj.type == 'MESH' and not obj.hide_render
              for corner in obj.bound_box]
    left, right = min(point.x for point in points), max(point.x for point in points)
    bottom, top = min(point.y for point in points), max(point.y for point in points)
    camera.location += rotation @ Vector(((left + right) * 0.5, (bottom + top) * 0.5, 0))
    aspect = scene.render.resolution_x / scene.render.resolution_y
    camera_data.ortho_scale = 1.09 * max(top - bottom, (right - left) / aspect)


frame_preview()
scene.render.filepath = os.path.join(WORK, 'Expanded_Top.png')
bpy.ops.render.render(write_still=True)
target = Vector((20, 22, 17))
camera.location = target + Vector((145, -220, 195))
camera.rotation_euler = (target - camera.location).to_track_quat('-Z', 'Y').to_euler()
camera_data.ortho_scale = 255
frame_preview()
scene.render.filepath = os.path.join(WORK, 'Expanded_Perspective.png')
bpy.ops.render.render(write_still=True)
