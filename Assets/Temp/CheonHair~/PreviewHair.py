import bpy
from mathutils import Vector
from pathlib import Path

out = Path(r'D:\project\ProjectCheon\Assets\Temp\CheonHair~')
scene = bpy.context.scene
for name, filename in [('T_PlayerCharacter_Front.png','T_RefFront.png'), ('T_PlayerCharacter_Side_Aligned.png','T_RefSide.png')]:
    bpy.data.images[name].save_render(str(out / filename))
scene.render.engine = 'BLENDER_WORKBENCH'
scene.render.resolution_x = 1000
scene.render.resolution_y = 1000
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.world.color = (0.18,0.18,0.18)
sh = scene.display.shading
sh.light = 'STUDIO'
sh.studiolight_rotate_z = 0.35
sh.color_type = 'OBJECT'
sh.show_shadows = True
sh.show_cavity = True
sh.cavity_type = 'BOTH'
sh.curvature_ridge_factor = 1.3
sh.curvature_valley_factor = 1.0
sh.background_type = 'WORLD'
sh.show_specular_highlight = True
for obj in scene.objects:
    obj.color = (0.64,0.64,0.64,1)
    if 'Hair' in obj.name: obj.color = (0.12,0.14,0.18,1)
camera_data = bpy.data.cameras.new('PreviewCamera')
camera = bpy.data.objects.new('PreviewCamera', camera_data)
scene.collection.objects.link(camera)
scene.camera = camera
camera_data.type = 'ORTHO'

def render(name, pos, target, scale):
    camera.location = pos
    camera.rotation_euler = (Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
    camera_data.ortho_scale = scale
    scene.render.filepath = str(out / name)
    bpy.ops.render.render(write_still=True)

bpy.data.objects['HairOrigin'].hide_render = True
bpy.data.objects['Pouch'].hide_render = True
render('original_full.png',(0,-5,1.05),(0,0,1.05),2.4)
bpy.data.objects['Gat'].hide_render = True
render('original_head_front.png',(0,-5,1.74),(0,-0.01,1.74),0.65)
render('original_head_side.png',(5,0,1.74),(0,-0.01,1.74),0.65)
render('original_head_threequarter.png',(4,-6,3.2),(0,-0.01,1.74),0.65)
bpy.data.objects['Hair'].hide_render = True
origin = bpy.data.objects['HairOrigin']
origin.hide_render = False
origin.location.x = 0
render('original_hair_origin.png',(1,-4,2.5),(0,-0.16,1.77),0.4)
