import bpy
import bmesh
import json
import math
from pathlib import Path
from mathutils import Vector

OUT = Path(r'D:\project\ProjectCheon\Assets\Temp\CheonPlayerTexture~')
source_components = json.loads((OUT / 'components.json').read_text())
component_lookup = {o['name']: {f: c['index'] for c in o['components'] for f in c['face_indices']} for o in source_components}
scene = bpy.context.scene
if bpy.context.object and bpy.context.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')

palette = {
    'skin': (0.93, 0.62, 0.38), 'eyes': (0.105, 0.052, 0.025), 'brows': (0.20, 0.105, 0.05),
    'hair': (0.25, 0.12, 0.055), 'hat': (0.055, 0.065, 0.08), 'ribbon': (0.055, 0.065, 0.075),
    'robe': (0.055, 0.55, 0.64), 'sleeve': (0.91, 0.875, 0.79), 'collar': (0.96, 0.935, 0.86),
    'blue': (0.04, 0.25, 0.56), 'gold': (0.91, 0.58, 0.16), 'leather': (0.39, 0.195, 0.075),
    'button': (0.83, 0.54, 0.16), 'pants': (0.10, 0.105, 0.12), 'shoes': (0.07, 0.08, 0.10),
}
materials = {}
for name, color in palette.items():
    mat = bpy.data.materials.new('Preview_' + name)
    mat.diffuse_color = (*color, 1)
    materials[name] = mat

def classify(name, component, center):
    x, y, z = center
    ax = abs(x)
    if name == 'Hair': return 'hair'
    if name == 'Gat': return 'hat'
    if name.startswith('Gat'): return 'ribbon'
    if name == 'Pouch': return 'button' if component == 2 else 'leather'
    if name == 'BeltTail': return 'gold'
    if name != 'Body': return 'robe'
    if component == 2: return 'eyes'
    if component == 3: return 'brows'
    if component == 1: return 'shoes' if z < .12 else 'pants'
    if z > 1.468: return 'skin'
    if z < .64 + .54*ax and ax > .42: return 'skin'
    if (ax > .19 and z > .88) or (ax > .26 and z > .78): return 'sleeve'
    if 1.018 < z < 1.095: return 'gold'
    return 'robe'

objects = []
semantics = []
for obj in list(scene.objects):
    if obj.type != 'MESH' or obj.hide_get() or obj.hide_viewport or obj.hide_render:
        continue
    obj.hide_set(False)
    obj.select_set(False)
    obj.data.materials.clear()
    for mat in materials.values(): obj.data.materials.append(mat)
    names = list(materials)
    part = obj.vertex_groups.new(name='Part_' + obj.name)
    part.add(list(range(len(obj.data.vertices))), 1.0, 'REPLACE')
    region_attr = obj.data.attributes.new('surface_region', 'INT', 'FACE')
    for poly in obj.data.polygons:
        center = obj.matrix_world @ poly.center
        component = component_lookup[obj.name][poly.index]
        region = classify(obj.name, component, center)
        poly.material_index = names.index(region)
        region_attr.data[poly.index].value = names.index(region)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    for mod in list(obj.modifiers):
        bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.select_set(False)
    objects.append(obj)

bpy.ops.object.select_all(action='DESELECT')
for obj in objects: obj.select_set(True)
bpy.context.view_layer.objects.active = bpy.data.objects['Body']
bpy.ops.object.join()
model = bpy.context.object
model.name = 'Cheon_Player'
model.data.name = 'Cheon_Player_Mesh'

# 면과 형상은 유지하고 실제 면에 연결되지 않은 작업 잔여 정점만 정리한다.
bm = bmesh.new()
bm.from_mesh(model.data)
loose = [v for v in bm.verts if not v.link_faces]
loose_count = len(loose)
if loose: bmesh.ops.delete(bm, geom=loose, context='VERTS')
bm.to_mesh(model.data)
bm.free()
model.data.update()
while model.data.uv_layers:
    model.data.uv_layers.remove(model.data.uv_layers[0])
model.data.uv_layers.new(name='UVMap')
model.data.uv_layers.active_index = 0
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.008, area_weight=0.45, correct_aspect=True, scale_to_bounds=False)
bpy.ops.uv.average_islands_scale()
bpy.ops.uv.pack_islands(rotate=True, margin_method='FRACTION', margin=0.0035, shape_method='CONCAVE', scale=True)
bpy.ops.object.mode_set(mode='OBJECT')

# 완성본에는 현재 보이는 파츠만 남기고 원본 파일은 별도로 보존한다.
for obj in list(scene.objects):
    if obj != model: bpy.data.objects.remove(obj, do_unlink=True)
for col in list(bpy.data.collections):
    if col != scene.collection and not col.objects and not col.children:
        bpy.data.collections.remove(col)

scene.render.engine = 'BLENDER_WORKBENCH'
scene.render.resolution_x = 1000
scene.render.resolution_y = 1200
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.world.color = (.11, .12, .15)
sh = scene.display.shading
sh.light = 'STUDIO'
sh.color_type = 'MATERIAL'
sh.show_shadows = True
sh.show_cavity = True
sh.cavity_type = 'BOTH'
sh.curvature_ridge_factor = 0.5
sh.curvature_valley_factor = .7
sh.background_type = 'WORLD'
cam_data = bpy.data.cameras.new('PreviewCamera')
cam = bpy.data.objects.new('PreviewCamera', cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
cam_data.type = 'ORTHO'
cam_data.ortho_scale = 2.4
for filename, position in [('regions_front.png',(0,-6,1.05)),('regions_back.png',(0,6,1.05))]:
    cam.location = position
    cam.rotation_euler = (Vector((0,0,1.05))-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath = str(OUT / filename)
    bpy.ops.render.render(write_still=True)
bpy.data.objects.remove(cam, do_unlink=True)
scene.camera = None
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / 'Cheon_Player_Atlas_Work.blend'))
(OUT / 'atlas_preparation.json').write_text(json.dumps({'vertices':len(model.data.vertices),'faces':len(model.data.polygons),'loose_removed':loose_count,'regions':list(materials),'palette':palette}, indent=2))
print('ATLAS_PREPARED')
