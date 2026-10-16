import bpy, os
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath=r'E:\C.J.I\Blender\Cheon_Player.blend')
scene=bpy.context.scene
camera=scene.camera
camera.location=(0,0,7)
camera.rotation_euler=(0,0,0)
camera.data.type='ORTHO'
camera.data.ortho_scale=1.6
scene.render.resolution_x=1024
scene.render.resolution_y=1024
scene.cycles.samples=24
scene.render.filepath=os.path.join(os.path.dirname(__file__),'ReferenceCamera_Top.png')
bpy.ops.render.render(write_still=True)
camera.location=(4,-4,5.13)
camera.rotation_euler=(Vector((0,0,1.13))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.ortho_scale=2.7
scene.render.filepath=os.path.join(os.path.dirname(__file__),'ReferenceCamera_Isometric.png')
bpy.ops.render.render(write_still=True)
