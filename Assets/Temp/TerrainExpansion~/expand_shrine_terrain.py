import ast
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
SOURCE = r'E:\C.J.I\Blender\AutoModeling\Mountain\2Try\Mountain_2Try_Expanded.blend'
OUTPUT = os.path.join(WORK, 'Mountain_2Try_ShrineExpanded.blend')
AREA_MULTIPLIER = 2.5
SCALE = math.sqrt(AREA_MULTIPLIER)
FLOOR_Z = 27.12
ARENA_CENTER = Vector((-7.0, -9.0, 27.0))
random.seed(10231)

# 이전 작업에서 검증한 메쉬 분리·검증·바위 보충 함수만 재사용한다.
with open(os.path.join(WORK, 'expand_terrain.py'), encoding='utf-8') as source_file:
    previous_source = source_file.read()
function_names = {'smooth_step', 'components', 'mesh_signature', 'local_rotation',
                  'ordered_angle', 'densify_rock_ring'}
helper_nodes = [node for node in ast.parse(previous_source).body
                if isinstance(node, ast.FunctionDef) and node.name in function_names]
exec(compile(ast.Module(body=helper_nodes, type_ignores=[]), '<terrain_helpers>', 'exec'))

core = bpy.data.objects['Mountain_Continuous_Core']
original_core_points = [vertex.co.copy() for vertex in core.data.vertices]
cap_faces = [tuple(face.vertices) for face in core.data.polygons
             if all(abs(original_core_points[index].z - 27.0) < 0.01 for index in face.vertices)]
cap_indices = sorted({index for face in cap_faces for index in face})
ANCHOR = Vector((-7.0, max(original_core_points[index].y for index in cap_indices), 27.0))
approach = bpy.data.objects['Stairway_Winding_Approach']
highest_step_z = max(vertex.co.z for vertex in approach.data.vertices)
entry_faces = [face for face in approach.data.polygons
               if face.normal.z > 0.99 and
               all(abs(approach.data.vertices[index].co.z - highest_step_z) < 0.0001
                   for index in face.vertices)]
entry_area = sum(face.area for face in entry_faces)
ENTRY_POINT = sum((face.center * face.area for face in entry_faces), Vector()) / entry_area
APPROACH_OFFSET = Vector(((ENTRY_POINT.x - ANCHOR.x) * (SCALE - 1),
                          (ENTRY_POINT.y - ANCHOR.y) * (SCALE - 1), 0.0))


def deform_point(point):
    x, y, z = point
    north_weight = 1.0 - smooth_step((y - ANCHOR.y) / 15.0)
    approach_weight = smooth_step((-y - 28.0) / 18.0)
    delta_x = (SCALE - 1) * max(-25.0, min(25.0, x - ANCHOR.x))
    delta_y = (SCALE - 1) * (y - ANCHOR.y)
    delta_x = delta_x * (1 - approach_weight) + APPROACH_OFFSET.x * approach_weight
    delta_y = delta_y * (1 - approach_weight) + APPROACH_OFFSET.y * approach_weight
    return Vector((x + delta_x * north_weight, y + delta_y * north_weight, z))


def deform_core(point):
    # 상단 전투장은 고정하고 하단 정상부부터 산기슭까지 연속적으로 확장한다.
    weight = 1.0 - smooth_step((point.z - 27.1) / (36.95 - 27.1))
    return point.lerp(deform_point(point), weight)


def is_entrance_gap(first, second):
    middle = (first['center'] + second['center']) * 0.5
    north_opening = middle.y > -3.0 and abs(middle.x + 2.0) < 10.0
    south_opening = middle.y < -18.0 and abs(middle.x - ENTRY_POINT.x) < 7.0
    return north_opening or south_opening


def projected_area(points, faces):
    return sum(abs(sum(points[face[index]].x * points[face[(index + 1) % len(face)]].y -
                       points[face[(index + 1) % len(face)]].x * points[face[index]].y
                       for index in range(len(face)))) * 0.5 for face in faces)


shrine_root = bpy.data.objects['Shrine_Building_Placement']
building_objects = [shrine_root] + list(shrine_root.children_recursive)
moving_trees = [obj for obj in bpy.data.objects if obj.name.startswith('Pine_') and
                (deform_core(obj.location) - obj.location).length > 0.001]
mutable_names = {'Mountain_Continuous_Core', 'Granite_Cliff_Lower', 'Summit_Rim_Outcrops_Lower',
                 'Rocky_Ledges_And_Foothills', 'Terrace_Lower_Fractured_Bedrock', approach.name,
                 *[obj.name for obj in building_objects], *[obj.name for obj in moving_trees]}
protected = {obj.name: mesh_signature(obj) for obj in bpy.data.objects if obj.name not in mutable_names}
approach_scale_before = tuple(approach.scale)
building_scale_before = tuple(shrine_root.scale)
building_rotation_before = tuple(shrine_root.rotation_euler)

floor_indices = {old: new for new, old in enumerate(cap_indices)}
floor_points = [deform_point(original_core_points[index]) for index in cap_indices]
floor_faces = [tuple(floor_indices[index] for index in face) for face in cap_faces]
old_area = projected_area(original_core_points, cap_faces)
new_area = projected_area(floor_points, floor_faces)
assert abs(new_area / old_area - AREA_MULTIPLIER) < 0.00001
for vertex in core.data.vertices:
    vertex.co = deform_core(vertex.co)
core.data.update()

rock_reports = {name: densify_rock_ring(bpy.data.objects[name])
                for name in ['Granite_Cliff_Lower', 'Summit_Rim_Outcrops_Lower']}
foothills = bpy.data.objects['Rocky_Ledges_And_Foothills']
for group in components(foothills):
    offset = deform_core(group['center']) - group['center']
    if offset.length < 0.001:
        continue
    for old_index in group['indices']:
        foothills.data.vertices[old_index].co += offset
foothills.data.update()
approach.location += APPROACH_OFFSET
building_offset = deform_point(shrine_root.location) - shrine_root.location
shrine_root.location += building_offset

old_floor = bpy.data.objects['Terrace_Lower_Fractured_Bedrock']
floor_collection = old_floor.users_collection[0]
old_mesh = old_floor.data
bpy.data.objects.remove(old_floor, do_unlink=True)
if old_mesh.users == 0:
    bpy.data.meshes.remove(old_mesh)
edges = Counter(tuple(sorted((face[index], face[(index + 1) % len(face)])))
                for face in floor_faces for index in range(len(face)))
point_count = len(floor_points)
positions = [(point.x, point.y, FLOOR_Z) for point in floor_points]
positions += [(point.x, point.y, FLOOR_Z - 0.18) for point in floor_points]
faces = list(floor_faces) + [tuple(point_count + index for index in reversed(face)) for face in floor_faces]
for face in floor_faces:
    for index, first in enumerate(face):
        second = face[(index + 1) % len(face)]
        if edges[tuple(sorted((first, second)))] == 1:
            faces.append((second, first, point_count + first, point_count + second))
floor_mesh = bpy.data.meshes.new('Shrine_Floor_Flat_UV_Mesh')
floor_mesh.from_pydata(positions, [], faces)
floor_mesh.update()
floor = bpy.data.objects.new('Shrine_Floor_Flat_UV', floor_mesh)
floor_collection.objects.link(floor)
floor_mesh.materials.append(bpy.data.materials['M_Arena_Ground'])
floor['설명'] = '산신각 평지 면적 2.5배 확장. 판석 틈과 단차가 없는 연속 수평 바닥.'
floor['surface_area_m2'] = new_area
floor['area_multiplier'] = new_area / old_area
floor['UV_설명'] = 'UVMap: 0~1 비율 유지, UV_Tile_8m: 8m 반복 텍스처용.'
low_x, high_x = min(point.x for point in floor_points), max(point.x for point in floor_points)
low_y, high_y = min(point.y for point in floor_points), max(point.y for point in floor_points)
uv_size = max(high_x - low_x, high_y - low_y)
uv_map = floor_mesh.uv_layers.new(name='UVMap')
tiled_uv = floor_mesh.uv_layers.new(name='UV_Tile_8m')
for loop in floor_mesh.loops:
    point = floor_mesh.vertices[loop.vertex_index].co
    uv_map.data[loop.index].uv = ((point.x - low_x) / uv_size, (point.y - low_y) / uv_size)
    tiled_uv.data[loop.index].uv = (point.x / 8.0, point.y / 8.0)
floor_mesh.uv_layers.active_index = 0
uv_map.active_render = True

tree_collection = bpy.data.collections['07_Planted_Pines']
rim_trees = [bpy.data.objects['Pine_%02d' % number] for number in range(1, 8)]
ordered_trees = sorted(rim_trees, key=lambda tree: ordered_angle(tree.location))
tree_segments = []
for index, first in enumerate(ordered_trees):
    second = ordered_trees[(index + 1) % len(ordered_trees)]
    if is_entrance_gap({'center': first.location}, {'center': second.location}):
        continue
    tree_segments.append({'a': first.location.copy(), 'b': second.location.copy(), 'template': first,
                          'old': (first.location - second.location).xy.length,
                          'new': (deform_point(first.location) - deform_point(second.location)).xy.length,
                          'additions': 0})
old_tree_length = sum(segment['old'] for segment in tree_segments)
new_tree_length = sum(segment['new'] for segment in tree_segments)
new_tree_count = round(len(rim_trees) * new_tree_length / old_tree_length)
for _ in range(new_tree_count - len(rim_trees)):
    segment = max(tree_segments, key=lambda item: item['new'] / (item['additions'] + 1))
    segment['additions'] += 1
for tree in moving_trees:
    tree.location = deform_point(tree.location) if tree in rim_trees else deform_core(tree.location)
all_rim_trees = list(rim_trees)
for segment in tree_segments:
    for addition in range(segment['additions']):
        factor = (addition + 1) / (segment['additions'] + 1)
        tree = segment['template'].copy()
        tree.data = segment['template'].data
        tree.name = 'Pine_Shrine_%02d' % (len(all_rim_trees) - len(rim_trees) + 1)
        tree_collection.objects.link(tree)
        tree.location = deform_point(segment['a'].lerp(segment['b'], factor))
        tree.rotation_euler.z += random.uniform(-0.6, 0.6)
        tree.scale *= random.uniform(0.93, 1.04)
        all_rim_trees.append(tree)
        moving_trees.append(tree)

boundary = [(Vector(positions[first][:2]), Vector(positions[second][:2]))
            for (first, second), count in edges.items() if count == 1]
floor_center = Vector(((low_x + high_x) * 0.5, (low_y + high_y) * 0.5))
for tree in all_rim_trees:
    candidates = []
    for first, second in boundary:
        direction = second - first
        ratio = max(0, min(1, (tree.location.xy - first).dot(direction) / direction.length_squared))
        point = first + direction * ratio
        candidates.append(((point - tree.location.xy).length_squared, point))
    closest = min(candidates, key=lambda item: item[0])[1]
    destination = closest + (floor_center - closest).normalized() * 2.8
    tree.location.x, tree.location.y = destination

bpy.context.view_layer.update()
# 삼각형의 가장자리까지 여유를 주어 계단 중앙으로 산체가 삐져나오지 않게 한다.
stair_surfaces = []
for name in ['Stairway_Winding_Approach', 'Stairway_Between_Summits']:
    stair = bpy.data.objects[name]
    for face in stair.data.polygons:
        if face.normal.z < 0.999:
            continue
        points = [stair.matrix_world @ stair.data.vertices[index].co for index in face.vertices]
        stair_surfaces.append((min(point.x for point in points) - 2.0,
                               max(point.x for point in points) + 2.0,
                               min(point.y for point in points) - 2.0,
                               max(point.y for point in points) + 2.0,
                               min(point.z for point in points) - 0.3))
carved_vertices = 0
for vertex in core.data.vertices:
    point = vertex.co
    if point.z < 1.0 or point.z >= 36.95:
        continue
    target_z = min((height for low_x, high_x, low_y, high_y, height in stair_surfaces
                    if low_x <= point.x <= high_x and low_y <= point.y <= high_y), default=point.z)
    if point.z > target_z:
        point.z = target_z
        carved_vertices += 1
core.data.update()

terrain_vertices, terrain_faces = [], []
terrain_names = ['Mountain_Continuous_Core', 'Shrine_Floor_Flat_UV', 'Arena_Floor_Flat_UV',
                 'Granite_Cliff_Lower', 'Summit_Rim_Outcrops_Lower',
                 'Granite_Cliff_Upper', 'Summit_Rim_Outcrops_Upper', 'Rocky_Ledges_And_Foothills']
for name in terrain_names:
    obj = bpy.data.objects[name]
    offset = len(terrain_vertices)
    terrain_vertices.extend(obj.matrix_world @ vertex.co for vertex in obj.data.vertices)
    terrain_faces.extend(tuple(offset + index for index in face.vertices) for face in obj.data.polygons)
terrain_bvh = BVHTree.FromPolygons(terrain_vertices, terrain_faces)
tree_contacts = []
for tree in moving_trees:
    hit, normal, face_index, distance = terrain_bvh.ray_cast(Vector((tree.location.x, tree.location.y, 100)), Vector((0, 0, -1)))
    assert hit is not None, 'Tree has no supporting terrain: ' + tree.name
    minimum_z = min((tree.matrix_world @ vertex.co).z for vertex in tree.data.vertices)
    tree.location.z += hit.z - 0.055 - minimum_z
    tree['placement_surface_z'] = hit.z
    tree_contacts.append({'name': tree.name, 'surface_z': hit.z})

bpy.context.view_layer.update()
report = {'input': SOURCE,
          'input_sha256': hashlib.sha256(open(bpy.data.filepath, 'rb').read()).hexdigest(),
          'area_before_m2': old_area, 'area_after_m2': new_area, 'area_ratio': new_area / old_area,
          'linear_scale': SCALE, 'floor_bounds_xy': [[low_x, low_y], [high_x, high_y]],
          'shrine_translation': list(building_offset), 'approach_translation': list(APPROACH_OFFSET),
          'building_size_unchanged': tuple(shrine_root.scale) == building_scale_before,
          'building_rotation_unchanged': tuple(shrine_root.rotation_euler) == building_rotation_before,
          'approach_size_unchanged': tuple(approach.scale) == approach_scale_before,
          'upper_objects_unchanged': all(mesh_signature(bpy.data.objects[name]) == signature
                                         for name, signature in protected.items()),
          'protected_object_count': len(protected), 'rocks': rock_reports,
          'trees': {'before': len(rim_trees), 'after': new_tree_count,
                    'density_ratio': (new_tree_count / new_tree_length) / (len(rim_trees) / old_tree_length),
                    'contacts': tree_contacts}, 'stair_clearance_carved_vertices': carved_vertices,
          'validation': []}
for name in ['Mountain_Continuous_Core', 'Shrine_Floor_Flat_UV', 'Granite_Cliff_Lower', 'Summit_Rim_Outcrops_Lower']:
    bm = bmesh.new()
    bm.from_mesh(bpy.data.objects[name].data)
    report['validation'].append({'name': name, 'nonmanifold_edges': sum(not edge.is_manifold for edge in bm.edges),
                                 'degenerate_faces': sum(face.calc_area() < 1e-7 for face in bm.faces)})
    bm.free()
assert report['upper_objects_unchanged']
assert report['building_size_unchanged'] and report['approach_size_unchanged']
assert all(item['nonmanifold_edges'] == 0 and item['degenerate_faces'] == 0 for item in report['validation'])
bpy.context.scene['shrine_expansion_20261023'] = json.dumps(report, ensure_ascii=False)
bpy.context.scene['작업내용'] = '상단 전투장 확장 유지. 산신각 평지는 면적 2.5배 확장, 바닥 평면 및 UV 제공. 외곽 바위·소나무 밀도 유지, 산신각·진입 계단 크기 유지.'
bpy.ops.object.select_all(action='DESELECT')
floor.select_set(True)
bpy.context.view_layer.objects.active = floor
bpy.context.preferences.filepaths.save_version = 0
bpy.context.preferences.filepaths.file_preview_type = 'NONE'
bpy.ops.wm.save_as_mainfile(filepath=OUTPUT, check_existing=False)
with open(os.path.join(WORK, 'shrine_expansion_report.json'), 'w', encoding='utf-8') as output:
    json.dump(report, output, ensure_ascii=False, indent=2)
print('SHRINE_EXPANSION_COMPLETE:' + json.dumps(report, ensure_ascii=True))

# 저장 후 미리보기만 생성한다. 카메라는 작업 파일에 들어가지 않는다.
preview_code = previous_source.split("# 미리보기용 카메라는 저장한 작업 파일에 추가하지 않는다.", 1)[1]
preview_code = preview_code.replace('Expanded_Top.png', 'ShrineExpanded_Top.png')
preview_code = preview_code.replace('Expanded_Perspective.png', 'ShrineExpanded_Perspective.png')
exec(compile(preview_code, '<terrain_preview>', 'exec'))
