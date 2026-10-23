import bpy
from mathutils import Vector
from pathlib import Path

out=Path(r'D:\project\ProjectCheon\Assets\Temp\CheonPlayerTexture~')
scene=bpy.context.scene
scene.render.engine='CYCLES'
scene.cycles.samples=24
scene.cycles.use_denoising=True
scene.render.resolution_x=1100
scene.render.resolution_y=1300
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.render.film_transparent=False
scene.world.use_nodes=True
background=scene.world.node_tree.nodes.get('Background')
background.inputs['Color'].default_value=(.36,.41,.5,1)
background.inputs['Strength'].default_value=.55
scene.view_settings.view_transform='AgX'
scene.view_settings.look='AgX - Medium High Contrast'
camera_data=bpy.data.cameras.new('PreviewCamera')
camera=bpy.data.objects.new('PreviewCamera',camera_data)
scene.collection.objects.link(camera)
scene.camera=camera
camera_data.type='ORTHO'
camera_data.ortho_scale=2.5

def area(name,position,power,size):
    data=bpy.data.lights.new(name,'AREA')
    data.energy=power
    data.shape='DISK'
    data.size=size
    obj=bpy.data.objects.new(name,data)
    scene.collection.objects.link(obj)
    obj.location=position
    obj.rotation_euler=(Vector((0,0,1))-obj.location).to_track_quat('-Z','Y').to_euler()

area('Preview_Key',(-3,-4,5),420,4)
area('Preview_Fill',(3,-3,2.8),240,3)
area('Preview_Rim',(1,3,4),480,3)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.041))
floor=bpy.context.object
mat=bpy.data.materials.new('Preview_Floor')
mat.use_nodes=True
mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.19,.23,.29,1)
mat.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.9
floor.data.materials.append(mat)
views=[('textured_front.png',(0,-6,1.10),(0,0,1.05),2.5),
       ('textured_threequarter.png',(3,-6,2.6),(0,0,1.05),2.55),
       ('textured_back.png',(3,6,2.1),(0,0,1.05),2.5),
       ('textured_face.png',(.5,-6,2.05),(0,-.02,1.61),.9)]
for filename,position,target,scale in views:
    camera.location=position
    camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
    camera_data.ortho_scale=scale
    scene.render.filepath=str(out/filename)
    bpy.ops.render.render(write_still=True)
    print('PREVIEW_SAVED',filename,flush=True)
