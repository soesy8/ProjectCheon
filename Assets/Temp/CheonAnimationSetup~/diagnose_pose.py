import bpy
from mathutils import Matrix

bpy.ops.wm.open_mainfile(filepath=r"E:\C.J.I\Blender\AutoRigging\Cheon_Player_Mixamo_RigFixed.blend", load_ui=False, use_scripts=False)
armature = bpy.data.objects["Armature"]
mesh = bpy.data.objects["Cheon_Player"]
print("FRAME", bpy.context.scene.frame_current)
print("MODE", bpy.context.mode, [(obj.name, obj.mode) for obj in bpy.context.scene.objects])
if bpy.context.object and bpy.context.object.mode != "OBJECT":
    bpy.ops.object.mode_set(mode="OBJECT")
for field in ["hide_render", "hide_viewport", "show_in_front"]:
    print("ARMATURE_FIELD", field, getattr(armature, field))
print("VISIBLE", armature.hide_get(), armature.visible_get())
for modifier in mesh.modifiers:
    print("MODIFIER", modifier.name, {prop: getattr(modifier, prop) for prop in ["show_viewport", "show_render", "vertex_group", "invert_vertex_group", "use_vertex_groups", "use_bone_envelopes", "use_multi_modifier"]})

for kind in ["original", "cleared"]:
    if kind == "cleared":
        armature.animation_data_clear()
        for bone in armature.pose.bones:
            bone.matrix_basis = Matrix.Identity(4)
    armature.update_tag(refresh={"OBJECT", "DATA", "TIME"})
    mesh.update_tag(refresh={"OBJECT", "DATA", "TIME"})
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()
    graph = bpy.context.evaluated_depsgraph_get()
    eval_armature = armature.evaluated_get(graph)
    eval_mesh_obj = mesh.evaluated_get(graph)
    eval_mesh = eval_mesh_obj.to_mesh()
    print("POSE_KIND", kind)
    for name in ["mixamorig:LeftArm", "mixamorig:LeftHand", "mixamorig:LeftFoot"]:
        print("BONE", name, "raw", list(armature.pose.bones[name].head), "evaluated", list(eval_armature.pose.bones[name].head))
    displacement = max((eval_mesh.vertices[index].co - vertex.co).length for index, vertex in enumerate(mesh.data.vertices))
    print("MAX_DISPLACEMENT", displacement)
    print("BOUNDS", [[min((eval_mesh_obj.matrix_world @ vertex.co)[axis] for vertex in eval_mesh.vertices) for axis in range(3)], [max((eval_mesh_obj.matrix_world @ vertex.co)[axis] for vertex in eval_mesh.vertices) for axis in range(3)]])
    eval_mesh_obj.to_mesh_clear()
