import bpy
from pathlib import Path
from mathutils import Vector


WORK = Path(r"D:\project\ProjectCheon\Assets\Temp\ModularWallTools~")


def recess_mortar(mesh):
    indices = [index for index, material in enumerate(mesh.materials) if material and material.name == "M_LimeMortar"]
    vertices = set()
    for polygon in mesh.polygons:
        if polygon.material_index in indices:
            vertices.update(polygon.vertices)
    if not vertices:
        return
    low = min(mesh.vertices[index].co.x for index in vertices)
    high = max(mesh.vertices[index].co.x for index in vertices)
    for index in vertices:
        vertex = mesh.vertices[index]
        if abs(vertex.co.x - low) < 0.0001:
            vertex.co.x += 0.002
        elif abs(vertex.co.x - high) < 0.0001:
            vertex.co.x -= 0.002
    mesh.update()


for filename in ["20_Wall_Middle.blend", "20_Wall_End.blend", "Wall_Preview.blend"]:
    bpy.ops.wm.open_mainfile(filepath=str(WORK / filename))
    unique_meshes = {obj.data for obj in bpy.context.scene.objects if obj.type == "MESH"}
    for mesh in unique_meshes:
        recess_mortar(mesh)
    bpy.context.preferences.filepaths.save_version = 0
    if hasattr(bpy.context.preferences.filepaths, "file_preview_type"):
        bpy.context.preferences.filepaths.file_preview_type = "NONE"
    bpy.ops.wm.save_as_mainfile(filepath=str(WORK / filename), compress=True)
    if filename == "Wall_Preview.blend":
        scene = bpy.context.scene
        scene.objects["Preview middle repeat"].hide_render = True
        scene.objects["Preview opposite end"].hide_render = True
        scene.objects["Wall_End"].location.x = -0.52
        camera = scene.camera
        camera.location = (5.4, -8.4, 5.4)
        camera.rotation_euler = (Vector((0.85, 0, 1.05)) - camera.location).to_track_quat("-Z", "Y").to_euler()
        camera.data.ortho_scale = 4.95
        scene.render.filepath = str(WORK / "T_Wall_Parts.png")
        bpy.ops.render.render(write_still=True)
print("WALL_FINISH_COMPLETE")
