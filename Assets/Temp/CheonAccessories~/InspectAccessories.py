import bpy
import json
from pathlib import Path
from mathutils import Vector

out=Path(r'D:\project\ProjectCheon\Assets\Temp\CheonAccessories~')
scene=bpy.context.scene
for name,filename in [('T_PlayerCharacter_Front.png','T_RefFront.png'),('T_PlayerCharacter_Side_Aligned.png','T_RefSide.png')]:
    bpy.data.images[name].save_render(str(out/filename))
depsgraph=bpy.context.evaluated_depsgraph_get()
report={}
for name in ['Body','Pouch','Gat']:
    obj=bpy.data.objects[name]
    evaluated=obj.evaluated_get(depsgraph)
    mesh=evaluated.to_mesh()
    points=[tuple(evaluated.matrix_world@v.co) for v in mesh.vertices]
    report[name]={'points':points,'faces':[list(f.vertices) for f in mesh.polygons],
                  'matrix':[list(row) for row in obj.matrix_world]}
    evaluated.to_mesh_clear()
(out/'geometry.json').write_text(json.dumps(report))

scene.render.engine='BLENDER_WORKBENCH'
scene.render.resolution_x=1100
scene.render.resolution_y=1100
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.world.color=(.12,.12,.12)
sh=scene.display.shading
sh.light='STUDIO'
sh.studio_light='paint.sl'
sh.color_type='OBJECT'
sh.show_shadows=True
sh.show_cavity=True
sh.cavity_type='BOTH'
sh.curvature_ridge_factor=.6
sh.curvature_valley_factor=.5
sh.background_type='WORLD'
for obj in scene.objects:
    obj.color=(.60,.60,.60,1)
    if obj.name.startswith('Hair'): obj.color=(.2,.2,.2,1)
    if obj.name=='Pouch': obj.color=(.22,.115,.055,1)
camera_data=bpy.data.cameras.new('PreviewCamera')
camera=bpy.data.objects.new('PreviewCamera',camera_data)
scene.collection.objects.link(camera)
scene.camera=camera
camera_data.type='ORTHO'
def render(name,pos,target,scale):
    camera.location=pos
    camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
    camera_data.ortho_scale=scale
    scene.render.filepath=str(out/name)
    bpy.ops.render.render(write_still=True)
render('before_front.png',(0,-5,1.2),(0,0,1.2),1.9)
render('before_threequarter.png',(3,-5,2.1),(0,0,1.17),1.8)
render('before_side.png',(5,0,1.20),(0,0,1.2),1.8)
render('before_back.png',(0,5,1.20),(0,0,1.2),1.8)
