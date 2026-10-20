import bpy
import bmesh
import math
import json
from pathlib import Path
from mathutils import Vector


WORK = Path(r"D:\project\ProjectCheon\Assets\Temp\ModularWallTools~")
SOURCE = Path(r"E:\C.J.I\Blender\SelfModeling\20_Wall_End.blend")
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
bpy.context.preferences.filepaths.save_version = 0
if hasattr(bpy.context.preferences.filepaths, "file_preview_type"):
    bpy.context.preferences.filepaths.file_preview_type = "NONE"
wall = bpy.data.objects["Wall_End"]
wall_mesh = wall.data
original_profile = sorted((round(v.co.y, 6), round(v.co.z, 6))
                          for v in wall_mesh.vertices if abs(v.co.x) < 0.00001)
stone_materials = {i for i, mat in enumerate(wall_mesh.materials)
                   if mat.name.startswith(("M_Sandstone_", "M_Foundation_"))}
mortar_materials = {i for i, mat in enumerate(wall_mesh.materials) if mat.name == "M_LimeMortar"}
joint_material = bpy.data.materials.new("M_EndStone_JointInterior")
joint_material.diffuse_color = (0.25, 0.198, 0.135, 1)
joint_material.use_nodes = True
joint_shader = joint_material.node_tree.nodes.get("Principled BSDF")
joint_shader.inputs["Base Color"].default_value = joint_material.diffuse_color
joint_shader.inputs["Roughness"].default_value = 0.95
wall_mesh.materials.append(joint_material)
joint_index = len(wall_mesh.materials) - 1

bm = bmesh.new()
bm.from_mesh(wall_mesh)
bm.verts.ensure_lookup_table()
remaining = set(bm.verts)
stone_groups = []
while remaining:
    seed = remaining.pop()
    group = {seed}
    queue = [seed]
    while queue:
        vertex = queue.pop()
        for edge in vertex.link_edges:
            neighbor = edge.other_vert(vertex)
            if neighbor in remaining:
                remaining.remove(neighbor)
                group.add(neighbor)
                queue.append(neighbor)
    faces = {face for vertex in group for face in vertex.link_faces}
    if faces and all(face.material_index in stone_materials for face in faces):
        stone_groups.append({vertex.index for vertex in group})

assert len(stone_groups) == 4, len(stone_groups)
stone_groups.sort(key=lambda group: min(bm.verts[index].co.z for index in group))
stones = []
for row, group in enumerate(stone_groups):
    piece = bm.copy()
    piece.verts.ensure_lookup_table()
    bmesh.ops.delete(piece, geom=[vertex for vertex in piece.verts if vertex.index not in group], context="VERTS")
    # 외부 절단면의 날카로운 테두리를 둥글게 깎아 가로 줄눈의 폭과 그림자를 확보한다.
    cap_edges = [edge for edge in piece.edges
                 if all(abs(vertex.co.x + 0.4) < 0.00001 for vertex in edge.verts)]
    if cap_edges:
        bmesh.ops.bevel(piece, geom=cap_edges, offset=0.024, segments=2,
                        affect="EDGES", clamp_overlap=True, loop_slide=True)
    bmesh.ops.recalc_face_normals(piece, faces=list(piece.faces))
    mesh = bpy.data.meshes.new(f"End stone row {row + 1}")
    piece.to_mesh(mesh)
    piece.free()
    for material in wall_mesh.materials:
        mesh.materials.append(material)
    stone = bpy.data.objects.new(mesh.name, mesh)
    bpy.context.collection.objects.link(stone)
    stones.append(stone)

delete_indices = set.union(*stone_groups)
bmesh.ops.delete(bm, geom=[vertex for vertex in bm.verts if vertex.index in delete_indices], context="VERTS")
# 줄눈 뒤의 회반죽을 35 mm 후퇴시킨다. X=0 연결면은 그대로 둔다.
for face in bm.faces:
    if face.material_index in mortar_materials:
        is_end_face = all(vertex.co.x < -0.39 for vertex in face.verts)
        for vertex in face.verts:
            if vertex.co.x < -0.39:
                vertex.co.x = -0.365
        if is_end_face:
            face.material_index = joint_index
    if wall_mesh.materials[face.material_index].name.startswith("M_Timber_"):
        for vertex in face.verts:
            # 돌과 목재의 끝면이 겹쳐 검게 보이지 않도록 목재 단면만 2 mm 돌출한다.
            if abs(vertex.co.x + 0.4) < 0.00001:
                vertex.co.x = -0.402
bm.to_mesh(wall_mesh)
bm.free()

for row, stone in enumerate(stones):
    # 단마다 세로 줄눈을 엇갈리게 배치한다. 홈은 바깥쪽 50 mm에만 생성한다.
    groove_center = [0.055, -0.065, 0.070, -0.040][row]
    min_z = min(vertex.co.z for vertex in stone.data.vertices) - 0.02
    max_z = max(vertex.co.z for vertex in stone.data.vertices) + 0.02
    profile = [(-0.47, groove_center - 0.033),
               (-0.47, groove_center + 0.033),
               (-0.35, groove_center + 0.006),
               (-0.35, groove_center - 0.006)]
    vertices = [(x, y, z) for z in [min_z, max_z] for x, y in profile]
    faces = [(0, 3, 2, 1), (4, 5, 6, 7),
             (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
    cutter_mesh = bpy.data.meshes.new("Temporary joint cutter")
    cutter_mesh.from_pydata(vertices, [], faces)
    cutter_mesh.update()
    cutter_bmesh = bmesh.new()
    cutter_bmesh.from_mesh(cutter_mesh)
    bmesh.ops.recalc_face_normals(cutter_bmesh, faces=list(cutter_bmesh.faces))
    cutter_bmesh.to_mesh(cutter_mesh)
    cutter_bmesh.free()
    cutter = bpy.data.objects.new("Temporary joint cutter", cutter_mesh)
    bpy.context.collection.objects.link(cutter)
    bpy.ops.object.select_all(action="DESELECT")
    stone.select_set(True)
    bpy.context.view_layer.objects.active = stone
    boolean = stone.modifiers.new("Staggered recessed stone joint", "BOOLEAN")
    boolean.operation = "DIFFERENCE"
    boolean.solver = "EXACT"
    boolean.object = cutter
    bpy.ops.object.modifier_apply(modifier=boolean.name)
    bpy.data.objects.remove(cutter, do_unlink=True)
    piece = bmesh.new()
    piece.from_mesh(stone.data)
    for face in piece.faces:
        center = face.calc_center_median()
        if center.x < -0.345 and abs(center.y - groove_center) < 0.026 and abs(face.normal.y) > 0.7:
            face.material_index = joint_index
    groove_edges = [edge for edge in piece.edges
                    if all(vertex.co.x < -0.345 and abs(vertex.co.y - groove_center) < 0.026
                           for vertex in edge.verts)
                    and abs(edge.verts[0].co.z - edge.verts[1].co.z) > 0.12]
    if groove_edges:
        bmesh.ops.bevel(piece, geom=groove_edges, offset=0.004, segments=1,
                        affect="EDGES", clamp_overlap=True)
    bmesh.ops.recalc_face_normals(piece, faces=list(piece.faces))
    piece.to_mesh(stone.data)
    piece.free()

bpy.ops.object.select_all(action="DESELECT")
wall.select_set(True)
for stone in stones:
    stone.select_set(True)
bpy.context.view_layer.objects.active = wall
bpy.ops.object.join()
for face in wall.data.polygons:
    face.use_smooth = True
wall.data.set_sharp_from_angle(angle=math.radians(38))
normal = wall.modifiers.new("Weighted stone corner normals", "WEIGHTED_NORMAL")
normal.keep_sharp = True
normal.weight = 25
bpy.ops.object.modifier_apply(modifier=normal.name)
wall["outer_end_joints"] = "Four staggered vertical V-grooves, rounded stone borders, recessed mortar."
updated_profile = sorted((round(v.co.y, 6), round(v.co.z, 6))
                         for v in wall.data.vertices if abs(v.co.x) < 0.00001)
assert original_profile == updated_profile, "The modular connection must remain unchanged."
check = bmesh.new()
check.from_mesh(wall.data)
validation = {
    "stone_courses": len(stone_groups),
    "vertical_grooves": len(stones),
    "connection_profile_unchanged": original_profile == updated_profile,
    "nonmanifold_edges": sum(not edge.is_manifold for edge in check.edges),
    "degenerate_faces": sum(face.calc_area() < 1e-10 for face in check.faces),
    "triangles": sum(len(poly.vertices) - 2 for poly in wall.data.polygons),
}
check.free()
assert validation["nonmanifold_edges"] == 0, validation
assert validation["degenerate_faces"] == 0, validation
assert len(wall.data.uv_layers) == 1
for text in bpy.data.texts:
    text.use_fake_user = True
bpy.data.orphans_purge(do_local_ids=True, do_linked_ids=True, do_recursive=True)
bpy.ops.wm.save_as_mainfile(filepath=str(WORK / "20_Wall_End.blend"), compress=True)
(WORK / "Test_EndGroovesValidation.json").write_text(json.dumps(validation, indent=2), encoding="utf-8")
print("END_GROOVES_VALIDATED", json.dumps(validation), flush=True)

# 파일 저장 후 미리보기 씬에서 바깥쪽 마감면과 실제 조립 상태를 확인한다.
bpy.ops.wm.open_mainfile(filepath=str(WORK / "Wall_Preview.blend"))
with bpy.data.libraries.load(str(WORK / "20_Wall_End.blend"), link=False) as (source, destination):
    destination.objects = ["Wall_End"]
updated = destination.objects[0]
for name in ["Wall_End", "Preview opposite end"]:
    bpy.data.objects[name].data = updated.data
bpy.data.objects.remove(updated, do_unlink=True)
scene = bpy.context.scene
scene.cycles.samples = 32
scene.render.resolution_x = 1024
scene.render.resolution_y = 1024
for name in ["Wall_Middle", "Preview middle repeat", "Preview opposite end"]:
    scene.objects[name].hide_render = True
camera = scene.camera
camera.location = (-3.8, -4.8, 3.0)
camera.rotation_euler = (Vector((-0.2, 0, 1.10)) - camera.location).to_track_quat("-Z", "Y").to_euler()
camera.data.ortho_scale = 2.85
scene.render.filepath = str(WORK / "T_Wall_EndGrooves.png")
bpy.ops.render.render(write_still=True)
for name in ["Wall_Middle", "Preview middle repeat", "Preview opposite end"]:
    scene.objects[name].hide_render = False
camera.location = (7.4, -9.3, 6.4)
camera.rotation_euler = (Vector((2.4, 0, 1.06)) - camera.location).to_track_quat("-Z", "Y").to_euler()
camera.data.ortho_scale = 7.7
scene.render.resolution_x = 1400
scene.render.resolution_y = 980
scene.render.filepath = str(WORK / "T_Wall_EndGroovesAssembly.png")
bpy.ops.render.render(write_still=True)
print("END_GROOVES_PREVIEWS_COMPLETE", flush=True)
