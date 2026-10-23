import argparse
import json
import sys
from pathlib import Path

import bpy
from mathutils import Vector


parser = argparse.ArgumentParser()
parser.add_argument("source")
parser.add_argument("output")
args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
source = Path(args.source)
output = Path(args.output)
output.mkdir(parents=True, exist_ok=True)
if source.suffix.lower() == ".blend":
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False, use_scripts=False)
else:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source), automatic_bone_orientation=False)
scene = bpy.context.scene
armatures = [obj for obj in scene.objects if obj.type == "ARMATURE"]
scene.frame_set(1)
bpy.context.view_layer.update()
camera_data = bpy.data.cameras.new("PreviewCamera")
camera = bpy.data.objects.new("PreviewCamera", camera_data)
scene.collection.objects.link(camera)
camera_data.type = "ORTHO"
camera_data.ortho_scale = 2.7
scene.camera = camera
scene.render.engine = "BLENDER_WORKBENCH"
scene.render.resolution_x = 720
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "TEXTURE"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.display.shading.background_type = "WORLD"
scene.world.color = (0.16, 0.16, 0.16)
report = {}
for pose in ["animated", "rest"]:
    for armature in armatures:
        armature.data.pose_position = "POSE" if pose == "animated" else "REST"
    bpy.context.view_layer.update()
    report[pose] = {}
    for armature in armatures:
        report[pose][armature.name] = {
            bone.name: list(armature.matrix_world @ bone.head)
            for bone in armature.pose.bones
            if any(bone.name.endswith(name) for name in ["Hips", "LeftArm", "LeftHand", "LeftFoot", "RightFoot"])
        }
    for view, location in [("front", Vector((0, -5, 1.05))), ("side", Vector((5, 0, 1.05)))]:
        camera.location = location
        camera.rotation_euler = (Vector((0, 0, 1.05)) - location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = str(output / (source.stem + "_" + pose + "_" + view + ".png"))
        bpy.ops.render.render(write_still=True)
(output / (source.stem + "_pose_samples.json")).write_text(json.dumps(report, indent=2), encoding="utf-8")
print("PREVIEWS_COMPLETE", str(output), flush=True)
