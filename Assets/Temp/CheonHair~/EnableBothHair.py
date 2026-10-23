import bpy
import hashlib
import json
from pathlib import Path

source_path = Path(bpy.data.filepath)
source_hash = hashlib.sha256(source_path.read_bytes()).hexdigest()
output = Path(r'D:\project\ProjectCheon\Assets\Temp\CheonHair~')
hair_objects = [bpy.data.objects['Hair'], bpy.data.objects['Hair_Completed']]
hair_collections = {collection for obj in hair_objects for collection in obj.users_collection}

def enable_collections(layer_collection):
    if layer_collection.collection in hair_collections:
        layer_collection.exclude = False
        layer_collection.hide_viewport = False
    for child in layer_collection.children:
        enable_collections(child)

for collection in hair_collections:
    collection.hide_viewport = False
    collection.hide_render = False
for view_layer in bpy.context.scene.view_layers:
    enable_collections(view_layer.layer_collection)
    for obj in hair_objects:
        obj.hide_set(False, view_layer=view_layer)
for obj in hair_objects:
    obj.hide_viewport = False
    obj.hide_render = False

bpy.context.preferences.filepaths.save_version = 0
staged_path = output / 'Cheon_Player_BothHair.blend'
bpy.ops.wm.save_as_mainfile(filepath=str(staged_path))
assert all(obj.visible_get() and not obj.hide_render for obj in hair_objects)
report = {'source': str(source_path), 'source_hash': source_hash, 'staged': str(staged_path)}
(output / 'enable_both_hair.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print('VERIFIED: Hair and Hair_Completed are both visible in viewport and render')
