import bpy
import bmesh
import json
from pathlib import Path


WORK = Path(r"D:\project\ProjectCheon\Assets\Temp\ModularWallTools~")
results = {}
profiles = {}
for name in ["Middle", "End"]:
    path = WORK / f"20_Wall_{name}.blend"
    bpy.ops.wm.open_mainfile(filepath=str(path))
    objects = list(bpy.context.scene.objects)
    assert len(objects) == 1, [(obj.name, obj.type) for obj in objects]
    obj = objects[0]
    assert obj.type == "MESH" and obj.name == f"Wall_{name}"
    assert tuple(obj.location) == (0, 0, 0)
    assert tuple(obj.scale) == (1, 1, 1)
    assert obj.asset_data and len(obj.data.uv_layers) == 1
    assert abs(min(vertex.co.z for vertex in obj.data.vertices)) < 1e-6
    assert not [image.filepath for image in bpy.data.images if image.source == "FILE" and not image.packed_file]
    assert all(slot.material is not None for slot in obj.material_slots)
    mesh = bmesh.new()
    mesh.from_mesh(obj.data)
    boundary_count = sum(edge.is_boundary for edge in mesh.edges)
    nonmanifold_count = sum(not edge.is_manifold for edge in mesh.edges)
    degenerate_count = sum(face.calc_area() < 1e-10 for face in mesh.faces)
    mesh.free()
    profiles[name] = {
        "left": sorted(set((round(v.co.y, 5), round(v.co.z, 5)) for v in obj.data.vertices if abs(v.co.x) < 1e-5)),
        "right": sorted(set((round(v.co.y, 5), round(v.co.z, 5)) for v in obj.data.vertices if abs(v.co.x - 2.4) < 1e-5)),
    }
    # 저장된 파일에 남은 임시 렌더와 반대 파츠의 고아 데이터는 최종본에서 제거한다.
    for mesh_data in bpy.data.meshes:
        mesh_data.use_fake_user = False
    for text in bpy.data.texts:
        text.use_fake_user = True
    bpy.data.orphans_purge(do_local_ids=True, do_linked_ids=True, do_recursive=True)
    bpy.context.preferences.filepaths.save_version = 0
    if hasattr(bpy.context.preferences.filepaths, "file_preview_type"):
        bpy.context.preferences.filepaths.file_preview_type = "NONE"
    bpy.ops.wm.save_as_mainfile(filepath=str(path), compress=True)
    results[name] = {
        "objects": len(objects),
        "mesh_datablocks": len(bpy.data.meshes),
        "vertices": len(obj.data.vertices),
        "triangles": sum(len(poly.vertices) - 2 for poly in obj.data.polygons),
        "boundary_edges": boundary_count,
        "nonmanifold_edges": nonmanifold_count,
        "degenerate_faces": degenerate_count,
        "external_images": 0,
        "dimensions_m": list(obj.dimensions),
    }
    if name == "Middle":
        repeat = obj.modifiers.get("Repeat length - 2.4 m per section")
        if repeat:
            repeat.count = 3
            bpy.context.view_layer.update()
            evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
            assert abs(evaluated.dimensions.x - 7.2) < 1e-5
            results[name]["array_three_sections_m"] = evaluated.dimensions.x
            repeat.count = 1


def worst_distance(first, second):
    return max(min(((a[0] - b[0]) ** 2 + (a[1] - b[1]) ** 2) ** 0.5 for b in second) for a in first)


results["seams"] = {
    "repeat_profile_error_m": max(worst_distance(profiles["Middle"]["left"], profiles["Middle"]["right"]),
                                  worst_distance(profiles["Middle"]["right"], profiles["Middle"]["left"])),
    "end_profile_error_m": worst_distance(profiles["Middle"]["left"], profiles["End"]["left"]),
    "reversed_end_profile_error_m": worst_distance(profiles["Middle"]["right"],
                                                   [(-y, z) for y, z in profiles["End"]["left"]]),
}
(WORK / "Test_WallValidation.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
print("WALL_VALIDATION", json.dumps(results))
assert results["seams"]["repeat_profile_error_m"] < 0.0001
assert results["seams"]["end_profile_error_m"] < 0.0001
assert results["seams"]["reversed_end_profile_error_m"] < 0.0001
assert all(results[name]["boundary_edges"] == 0 and results[name]["degenerate_faces"] == 0 for name in ["Middle", "End"])
