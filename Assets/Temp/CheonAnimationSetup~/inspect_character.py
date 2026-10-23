import argparse
import hashlib
import json
import sys
from collections import Counter
from pathlib import Path

import bpy
from mathutils import Vector


def rounded(values):
    return [round(float(value), 7) for value in values]


def matrix_values(matrix):
    return [rounded(row) for row in matrix]


def json_properties(item):
    result = {}
    for key in item.keys():
        try:
            value = item[key]
            result[key] = value.to_list() if hasattr(value, "to_list") else value
            json.dumps(result[key])
        except (TypeError, ValueError):
            result[key] = str(value)
    return result


parser = argparse.ArgumentParser()
parser.add_argument("source")
parser.add_argument("output")
args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
source = Path(args.source)
if source.suffix.lower() == ".blend":
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False, use_scripts=False)
else:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source), automatic_bone_orientation=False)
bpy.context.scene.frame_set(bpy.context.scene.frame_current)
bpy.context.view_layer.update()

report = {
    "source": str(source),
    "blender_version": bpy.app.version_string,
    "unit_system": bpy.context.scene.unit_settings.system,
    "unit_scale": bpy.context.scene.unit_settings.scale_length,
    "fps": bpy.context.scene.render.fps,
    "current_frame": bpy.context.scene.frame_current,
    "frame_range": [bpy.context.scene.frame_start, bpy.context.scene.frame_end],
    "scene_properties": json_properties(bpy.context.scene),
    "actions": [],
    "objects": [],
    "armatures": [],
    "meshes": [],
    "images": [],
    "text_blocks": [{"name": item.name, "lines": len(item.lines)} for item in bpy.data.texts],
}
for action in bpy.data.actions:
    report["actions"].append({"name": action.name, "frames": rounded(action.frame_range), "users": action.users})
for obj in bpy.data.objects:
    report["objects"].append({
        "name": obj.name, "type": obj.type,
        "parent": obj.parent.name if obj.parent else None,
        "parent_bone": obj.parent_bone,
        "matrix_world": matrix_values(obj.matrix_world),
        "matrix_parent_inverse": matrix_values(obj.matrix_parent_inverse),
        "dimensions": rounded(obj.dimensions),
        "hide_render": obj.hide_render,
        "hide_viewport": obj.hide_viewport,
        "hidden": obj.hide_get(),
        "properties": json_properties(obj),
        "modifiers": [{"name": mod.name, "type": mod.type,
                       "object": getattr(getattr(mod, "object", None), "name", None),
                       "show_viewport": mod.show_viewport, "show_render": mod.show_render,
                       "vertex_group": getattr(mod, "vertex_group", None),
                       "invert_vertex_group": getattr(mod, "invert_vertex_group", None),
                       "use_vertex_groups": getattr(mod, "use_vertex_groups", None),
                       "use_bone_envelopes": getattr(mod, "use_bone_envelopes", None)} for mod in obj.modifiers],
        "active_action": obj.animation_data.action.name if obj.animation_data and obj.animation_data.action else None,
    })
    if obj.type == "ARMATURE":
        bones = []
        for bone in obj.data.bones:
            pose = obj.pose.bones[bone.name]
            bones.append({
                "name": bone.name,
                "parent": bone.parent.name if bone.parent else None,
                "deform": bone.use_deform,
                "connected": bone.use_connect,
                "head": rounded(bone.head_local),
                "tail": rounded(bone.tail_local),
                "world_head": rounded(obj.matrix_world @ bone.head_local),
                "world_tail": rounded(obj.matrix_world @ bone.tail_local),
                "matrix_local": matrix_values(bone.matrix_local),
                "pose_basis": matrix_values(pose.matrix_basis),
                "rotation_mode": pose.rotation_mode,
                "constraints": [{"name": constraint.name, "type": constraint.type} for constraint in pose.constraints],
                "properties": json_properties(bone),
            })
        report["armatures"].append({"name": obj.name, "pose_position": obj.data.pose_position,
                                   "bone_count": len(bones), "bones": bones})
    if obj.type == "MESH":
        mesh = obj.data
        group_names = {group.index: group.name for group in obj.vertex_groups}
        influences = Counter()
        weight_sums = []
        unweighted = []
        over_four = 0
        for vertex in mesh.vertices:
            weights = [group for group in vertex.groups if group.weight > 0.000001]
            if not weights:
                unweighted.append(vertex.index)
            weight_sums.append(sum(group.weight for group in weights))
            over_four += len(weights) > 4
            for group in weights:
                influences[group_names.get(group.group, str(group.group))] += 1
        coords = [rounded(vertex.co) for vertex in mesh.vertices]
        groups = [[[group.group, round(group.weight, 8)] for group in vertex.groups] for vertex in mesh.vertices]
        uvs = [[rounded(loop.uv) for loop in layer.data] for layer in mesh.uv_layers]
        evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
        bounds = [evaluated.matrix_world @ Vector(corner) for corner in evaluated.bound_box]
        report["meshes"].append({
            "name": obj.name, "vertices": len(mesh.vertices), "polygons": len(mesh.polygons),
            "materials": [material.name if material else None for material in mesh.materials],
            "uv_layers": [layer.name for layer in mesh.uv_layers],
            "shape_keys": [key.name for key in mesh.shape_keys.key_blocks] if mesh.shape_keys else [],
            "vertex_group_count": len(obj.vertex_groups), "weighted_groups": dict(influences),
            "unweighted_vertices": unweighted, "over_four_weights": over_four,
            "weight_sum_range": [min(weight_sums, default=0), max(weight_sums, default=0)],
            "geometry_hash": hashlib.sha256(json.dumps(coords).encode()).hexdigest(),
            "uv_hash": hashlib.sha256(json.dumps(uvs).encode()).hexdigest(),
            "weights_hash": hashlib.sha256(json.dumps(groups).encode()).hexdigest(),
            "world_bounds": [[min(point[axis] for point in bounds) for axis in range(3)],
                             [max(point[axis] for point in bounds) for axis in range(3)]],
            "raw_bounds": [[min(vertex.co[axis] for vertex in mesh.vertices) for axis in range(3)],
                           [max(vertex.co[axis] for vertex in mesh.vertices) for axis in range(3)]],
        })
        evaluated_mesh = evaluated.to_mesh()
        differences = [(evaluated_mesh.vertices[index].co - vertex.co).length for index, vertex in enumerate(mesh.vertices)]
        report["meshes"][-1]["maximum_modifier_displacement"] = max(differences, default=0)
        evaluated.to_mesh_clear()
for image in bpy.data.images:
    report["images"].append({"name": image.name, "filepath": image.filepath,
                             "size": list(image.size), "packed": bool(image.packed_file),
                             "packed_hash": hashlib.sha256(image.packed_file.data).hexdigest() if image.packed_file else None})

if source.suffix.lower() == ".fbx":
    from io_scene_fbx import parse_fbx
    root, version = parse_fbx.parse(str(source))
    objects = next(element for element in root.elems if element.id == b"Objects")
    report["fbx"] = {"version": version, "object_counts": dict(Counter(element.id.decode() for element in objects.elems))}

output = Path(args.output)
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
summary = {key: report[key] for key in ["source", "unit_system", "unit_scale", "fps", "actions", "objects", "meshes", "images", "text_blocks"]}
summary["armatures"] = [{"name": arm["name"], "bone_count": arm["bone_count"],
                         "bones": [{key: bone[key] for key in ["name", "parent", "deform", "world_head", "world_tail", "constraints"]} for bone in arm["bones"]]} for arm in report["armatures"]]
print("CHARACTER_INSPECTION " + json.dumps(summary, ensure_ascii=False), flush=True)
