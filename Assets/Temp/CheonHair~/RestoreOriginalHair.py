import bpy
import hashlib
import json
from pathlib import Path

source_path = Path(bpy.data.filepath)
output_directory = Path(r'D:\project\ProjectCheon\Assets\Temp\CheonHair~')
source_hash = hashlib.sha256(source_path.read_bytes()).hexdigest()
hair = bpy.data.objects['Hair']
hair_origin = bpy.data.objects['HairOrigin']
completed_hair = bpy.data.objects['Hair_Completed']

# 원본 헤어가 속한 컬렉션을 열고, 별도로 보관한 기본 가닥은 숨긴다.
for collection in hair.users_collection:
    collection.hide_viewport = False
    collection.hide_render = False

def enable_hair_collection(layer_collection):
    if layer_collection.collection in tuple(hair.users_collection):
        layer_collection.exclude = False
        layer_collection.hide_viewport = False
    for child in layer_collection.children:
        enable_hair_collection(child)

for view_layer in bpy.context.scene.view_layers:
    enable_hair_collection(view_layer.layer_collection)

hair.hide_viewport = False
hair.hide_render = False
hair.hide_set(False)
hair_origin.hide_viewport = True
hair_origin.hide_render = True
hair_origin.hide_set(True)
completed_hair.hide_viewport = True
completed_hair.hide_render = True
completed_hair.hide_set(True)

if bpy.context.object and bpy.context.object.mode != 'OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.select_all(action='DESELECT')
hair.select_set(True)
bpy.context.view_layer.objects.active = hair
bpy.context.preferences.filepaths.save_version = 0
staged_path = output_directory / 'Cheon_Player_OriginalHair.blend'
bpy.ops.wm.save_as_mainfile(filepath=str(staged_path))
report = {'source': str(source_path), 'source_hash': source_hash,
          'staged': str(staged_path), 'active_object': hair.name,
          'hair_visible': hair.visible_get(),
          'completed_hidden': completed_hair.hide_viewport and completed_hair.hide_render,
          'source_hidden': hair_origin.hide_viewport and hair_origin.hide_render}
assert report['hair_visible'] and report['completed_hidden'] and report['source_hidden']
(output_directory / 'restore_original_hair.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print('RESTORED_HAIR', json.dumps(report))
