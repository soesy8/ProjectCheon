import hashlib
import json
import shutil
import sys
import uuid
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


WORK = Path(r"D:\project\ProjectCheon\Assets\Temp\CheonAnimationSetup~")
SOURCE = Path(r"E:\C.J.I\Blender\AutoRigging\Cheon_Player_Mixamo_RigFixed.blend")
SOURCE_FBX = SOURCE.with_suffix(".fbx")
CANDIDATE = WORK / "Candidate"
CANDIDATE.mkdir(exist_ok=True)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def preserve_backup(path):
    backup = WORK / (path.name + ".backup")
    if not backup.exists():
        shutil.copy2(path, backup)
    assert digest(backup) == digest(path), "원본 파일이 작업 중 변경되었습니다."
    return {"source": str(path), "backup": str(backup), "sha256": digest(path)}


backups = [preserve_backup(path) for path in [SOURCE, SOURCE_FBX]]
bpy.ops.wm.open_mainfile(filepath=str(SOURCE), load_ui=False, use_scripts=False)
bpy.data.use_autopack = False
if bpy.context.object and bpy.context.object.mode != "OBJECT":
    bpy.ops.object.mode_set(mode="OBJECT")
armature = bpy.data.objects["Armature"]
mesh_object = bpy.data.objects["Cheon_Player"]
mesh = mesh_object.data
original_bones = {bone.name: bone.parent.name if bone.parent else None for bone in armature.data.bones}
original_uvs = [[tuple(loop.uv) for loop in layer.data] for layer in mesh.uv_layers]
original_topology = [tuple(polygon.vertices) for polygon in mesh.polygons]
original_weights = [[(group.group, group.weight) for group in vertex.groups] for vertex in mesh.vertices]
basecolor = bpy.data.images["T_Cheon_Player_BaseColor"]
assert basecolor.packed_file, "원본 텍스처가 패킹되어 있어야 합니다."
original_texture_hash = hashlib.sha256(basecolor.packed_file.data).hexdigest()

# 편집 모드로 저장되어 있던 리그의 변경을 확정하고, 이전 바인드 자세용 2프레임 동작은 비활성화한다.
# 기존 동작 데이터는 Blender 안에 남겨 두되 FBX에는 함께 내보내지 않는다.
if armature.animation_data and armature.animation_data.action:
    armature.animation_data.action.use_fake_user = True
armature.animation_data_clear()
for bone in armature.pose.bones:
    bone.matrix_basis = Matrix.Identity(4)
armature.data.pose_position = "POSE"
bpy.context.scene.frame_set(1)
bpy.context.view_layer.update()

# 부모 보정 행렬과 90도 회전을 에셋 데이터에 반영해, 런타임에서는 단위 스케일과 회전을 사용한다.
mesh_world = mesh_object.matrix_world.copy()
mesh_object.parent = None
mesh_object.matrix_world = mesh_world
for obj in [mesh_object, armature]:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
mesh_object.parent = armature
mesh_object.matrix_parent_inverse = Matrix.Identity(4)
mesh_object.matrix_basis = Matrix.Identity(4)
bpy.context.view_layer.update()


def point_bone(name, direction):
    bone = armature.pose.bones[name]
    current = bone.matrix.copy()
    current_direction = (bone.tail - bone.head).normalized()
    turn = current_direction.rotation_difference(Vector(direction).normalized())
    rotated = turn.to_matrix().to_4x4() @ current
    rotated.translation = current.translation
    bone.matrix = rotated
    bpy.context.view_layer.update()


# 팔을 수평으로 편 기준 자세를 실제 바인드 자세로 만들어, Humanoid 매핑의 기준을 명확히 한다.
for side, sign in [("Left", 1), ("Right", -1)]:
    for part in ["Arm", "ForeArm", "Hand"]:
        point_bone("mixamorig:" + side + part, (sign, 0, 0))
graph = bpy.context.evaluated_depsgraph_get()
evaluated = mesh_object.evaluated_get(graph)
evaluated_mesh = evaluated.to_mesh(preserve_all_data_layers=True, depsgraph=graph)
posed_coordinates = [vertex.co.copy() for vertex in evaluated_mesh.vertices]
evaluated.to_mesh_clear()

bpy.ops.object.select_all(action="DESELECT")
armature.select_set(True)
bpy.context.view_layer.objects.active = armature
bpy.ops.object.mode_set(mode="POSE")
bpy.ops.pose.armature_apply(selected=False)
bpy.ops.object.mode_set(mode="OBJECT")
for vertex, position in zip(mesh.vertices, posed_coordinates):
    vertex.co = position
mesh.update()
bpy.context.view_layer.update()

# 신발 밑창을 원점 높이에 맞춘다. 뼈대와 메시를 함께 옮겨 스킨 바인딩은 유지한다.
ground_offset = -min(vertex.co.z for vertex in mesh.vertices)
ground_transform = Matrix.Translation((0, 0, ground_offset))
mesh.transform(ground_transform)
armature.data.transform(ground_transform)
mesh.update()
bpy.context.view_layer.update()

# 가중치 비율·메시 연결·UV·텍스처와 믹사모 뼈 이름/계층은 그대로 보존한다.
assert {bone.name: bone.parent.name if bone.parent else None for bone in armature.data.bones} == original_bones
assert [[tuple(loop.uv) for loop in layer.data] for layer in mesh.uv_layers] == original_uvs
assert [tuple(polygon.vertices) for polygon in mesh.polygons] == original_topology
assert [[(group.group, group.weight) for group in vertex.groups] for vertex in mesh.vertices] == original_weights
assert hashlib.sha256(basecolor.packed_file.data).hexdigest() == original_texture_hash
assert abs(min(vertex.co.z for vertex in mesh.vertices)) < 0.000001
assert armature.matrix_world.is_identity and mesh_object.matrix_world.is_identity
for bone in armature.pose.bones:
    assert max(abs(bone.matrix_basis[row][column] - (1 if row == column else 0))
               for row in range(4) for column in range(4)) < 0.00001

scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0
scene.render.fps = 30
scene.render.fps_base = 1
scene.frame_start = 1
scene.frame_end = 1
scene.frame_set(1)
basecolor.filepath_raw = "//T_Cheon_Player_BaseColor.png"
armature.show_in_front = True
for obj in [armature, mesh_object]:
    obj.hide_set(False)
    obj.hide_viewport = False
    obj.hide_render = False
bpy.ops.object.select_all(action="DESELECT")
armature.select_set(True)
mesh_object.select_set(True)
bpy.context.view_layer.objects.active = armature

candidate_blend = CANDIDATE / SOURCE.name
candidate_fbx = CANDIDATE / SOURCE_FBX.name
bpy.context.preferences.filepaths.save_version = 0
if hasattr(bpy.context.preferences.filepaths, "file_preview_type"):
    bpy.context.preferences.filepaths.file_preview_type = "NONE"
bpy.ops.wm.save_as_mainfile(filepath=str(candidate_blend), check_existing=False, relative_remap=False)
bpy.ops.export_scene.fbx(
    filepath=str(candidate_fbx), use_selection=True, object_types={"ARMATURE", "MESH"},
    global_scale=1.0, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
    axis_forward="-Z", axis_up="Y", bake_space_transform=False,
    use_mesh_modifiers=True, mesh_smooth_type="FACE", use_triangles=False,
    use_tspace=False, colors_type="NONE", add_leaf_bones=False,
    use_armature_deform_only=False, armature_nodetype="NULL",
    primary_bone_axis="Y", secondary_bone_axis="X", bake_anim=False,
    path_mode="COPY", embed_textures=True, use_custom_props=False,
)

from io_scene_fbx import parse_fbx
root, version = parse_fbx.parse(str(candidate_fbx))
fbx_objects = next(element for element in root.elems if element.id == b"Objects")
videos = [element for element in fbx_objects.elems if element.id == b"Video"]
assert len(videos) == 1
embedded_texture = next(element for element in videos[0].elems if element.id == b"Content").props[0]
assert hashlib.sha256(embedded_texture).hexdigest() == original_texture_hash
assert not any(element.id == b"AnimationStack" for element in fbx_objects.elems)
fbx_bones = [element for element in fbx_objects.elems if element.id == b"Model" and element.props[2] == b"LimbNode"]
assert len(fbx_bones) == len(original_bones)

report = {
    "backups": backups,
    "candidate_blend": str(candidate_blend), "candidate_fbx": str(candidate_fbx),
    "candidate_hashes": {path.name: digest(path) for path in [candidate_blend, candidate_fbx]},
    "bone_count": len(original_bones), "bone_names_and_hierarchy_preserved": True,
    "vertices": len(mesh.vertices), "polygons": len(mesh.polygons),
    "uv_preserved": True, "skin_weights_preserved": True, "texture_preserved": True,
    "embedded_texture_bytes": len(embedded_texture), "ground_offset_m": ground_offset,
    "object_transforms_identity": True, "t_pose_is_bind_pose": True,
    "active_legacy_animation_removed": True, "legacy_action_retained_in_blend": True,
    "unity_project_implementation": False,
}
(WORK / "preparation_report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print("CHARACTER_PREPARED " + json.dumps(report, ensure_ascii=False), flush=True)
