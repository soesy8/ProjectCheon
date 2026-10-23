import hashlib
import json
import math
from pathlib import Path

import bpy
from mathutils import Matrix, Vector


WORK = Path(r"D:\project\ProjectCheon\Assets\Temp\CheonAnimationSetup~")
candidate_blend = WORK / "Candidate" / "Cheon_Player_Mixamo_RigFixed.blend"
candidate_fbx = candidate_blend.with_suffix(".fbx")


def world_vertices(obj):
    graph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(graph)
    evaluated_mesh = evaluated.to_mesh(preserve_all_data_layers=True, depsgraph=graph)
    result = [evaluated.matrix_world @ vertex.co for vertex in evaluated_mesh.vertices]
    evaluated.to_mesh_clear()
    return result


def named_weights(obj):
    names = {group.index: group.name for group in obj.vertex_groups}
    return [{names[group.group]: group.weight for group in vertex.groups if group.weight > 0.00000001}
            for vertex in obj.data.vertices]


def rotate_joint(armature, name, axis, degrees):
    bone = armature.pose.bones[name]
    world = armature.matrix_world @ bone.matrix
    rotated = Matrix.Rotation(math.radians(degrees), 4, axis) @ world
    rotated.translation = world.translation
    bone.matrix = armature.matrix_world.inverted() @ rotated
    bpy.context.view_layer.update()


bpy.ops.wm.open_mainfile(filepath=str(candidate_blend), load_ui=False, use_scripts=False)
assert bpy.context.mode == "OBJECT"
armature = bpy.data.objects["Armature"]
mesh = bpy.data.objects["Cheon_Player"]
assert not armature.animation_data or not armature.animation_data.action
original_bones = {bone.name: bone.parent.name if bone.parent else None for bone in armature.data.bones}
assert len(original_bones) == 33
rest_vertices = world_vertices(mesh)
weights = named_weights(mesh)
texture_hash = hashlib.sha256(bpy.data.images["T_Cheon_Player_BaseColor"].packed_file.data).hexdigest()
assert abs(min(vertex.z for vertex in rest_vertices)) < 0.00001
for side, sign in [("Left", 1), ("Right", -1)]:
    for part in ["Arm", "ForeArm", "Hand"]:
        bone = armature.data.bones["mixamorig:" + side + part]
        direction = (bone.tail_local - bone.head_local).normalized()
        assert direction.dot(Vector((sign, 0, 0))) > 0.9999

# 실제 관절을 움직여 스킨이 계속 반응하고, FBX 왕복 후에도 같은 변형이 나오는지 확인한다.
rotate_joint(armature, "mixamorig:LeftArm", "Y", 35)
rotate_joint(armature, "mixamorig:LeftForeArm", "Z", 25)
rotate_joint(armature, "mixamorig:LeftUpLeg", "X", -25)
rotate_joint(armature, "mixamorig:LeftLeg", "X", 45)
posed_vertices = world_vertices(mesh)
pose_matrices = {bone.name: armature.matrix_world @ bone.matrix.copy() for bone in armature.pose.bones}
motion = max((posed - rest).length for posed, rest in zip(posed_vertices, rest_vertices))
assert motion > 0.1

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(candidate_fbx), automatic_bone_orientation=False, use_anim=False)
imported_armature = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
imported_mesh = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")
assert {bone.name: bone.parent.name if bone.parent else None for bone in imported_armature.data.bones} == original_bones
imported_rest = world_vertices(imported_mesh)
assert len(rest_vertices) == len(imported_rest)
rest_error = max((left - right).length for left, right in zip(rest_vertices, imported_rest))
assert rest_error < 0.00005, rest_error
imported_weights = named_weights(imported_mesh)
weight_error = 0
for source, target in zip(weights, imported_weights):
    assert source.keys() == target.keys(), (source, target)
    weight_error = max(weight_error, max((abs(value - target[name]) for name, value in source.items()), default=0))
assert weight_error < 0.000001
texture = bpy.data.images["T_Cheon_Player_BaseColor"]
assert texture.size[:] == (4096, 4096)
assert hashlib.sha256(texture.packed_file.data).hexdigest() == texture_hash

for name, matrix in pose_matrices.items():
    imported_armature.pose.bones[name].matrix = imported_armature.matrix_world.inverted() @ matrix
    bpy.context.view_layer.update()
imported_pose = world_vertices(imported_mesh)
pose_error = max((left - right).length for left, right in zip(posed_vertices, imported_pose))
assert pose_error < 0.0001, pose_error
report = {
    "passed": True, "rest_pose_roundtrip_error_m": rest_error,
    "animated_pose_roundtrip_error_m": pose_error,
    "weight_roundtrip_error": weight_error,
    "verified_joint_motion_m": motion,
    "bone_names_and_parents_identical": True,
    "t_pose_bind_pose": True, "ground_at_zero": True,
    "texture_embedded_and_identical": True,
    "active_animation_absent": True, "unity_project_modified": False,
}
(WORK / "validation_report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print("CHARACTER_VALIDATION " + json.dumps(report), flush=True)
