import bpy
import hashlib
import json
import math
from pathlib import Path
from mathutils import Quaternion


WORK_FOLDER = Path(r"D:\project\ProjectCheon\Assets\Temp\BlenderReferenceAlignment")
SOURCE_BLEND = Path(r"E:\C.J.I\Blender\Cheon_Player.blend")
FRONT_IMAGE = Path(r"E:\C.J.I\ReferenceImage\PlayerCharacter\T_PlayerCharacter_Front.png")
SIDE_IMAGE = WORK_FOLDER / "T_PlayerCharacter_Side.png"


def get_body_signature():
    """사용자가 작업한 메시, UV, 재질 연결과 오브젝트 변환을 보존했는지 확인한다."""
    body = bpy.data.objects["Body"]
    mesh = body.data
    values = {
        "vertices": [list(vertex.co) for vertex in mesh.vertices],
        "faces": [list(face.vertices) for face in mesh.polygons],
        "material_indices": [face.material_index for face in mesh.polygons],
        "materials": [material.name if material else None for material in mesh.materials],
        "uv_layers": [[list(item.uv) for item in layer.data] for layer in mesh.uv_layers],
        "matrix": [list(row) for row in body.matrix_world],
    }
    return hashlib.sha256(json.dumps(values, sort_keys=True).encode()).hexdigest()


bpy.ops.wm.open_mainfile(filepath=str(SOURCE_BLEND), load_ui=True, use_scripts=False)
original_body_signature = get_body_signature()
front = bpy.data.objects["Ref_Front"]
side = bpy.data.objects["Ref_Side"]
previous_front = front.data
previous_side = side.data

# 정면 배치와 축척은 기존 모델 작업의 기준으로 유지하고, 측면을 같은 기준에 맞춘다.
front_image = bpy.data.images.load(str(FRONT_IMAGE), check_existing=False)
front_image.name = "T_PlayerCharacter_Front.png"
side_image = bpy.data.images.load(str(SIDE_IMAGE), check_existing=False)
side_image.name = "T_PlayerCharacter_Side_Aligned.png"
assert tuple(front_image.size) == (1024, 1024)
assert tuple(side_image.size) == (1024, 1024)
front.data = front_image
side.data = side_image
for previous_image in (previous_front, previous_side):
    if previous_image and previous_image.users == 0:
        bpy.data.images.remove(previous_image)
front_image.name = "T_PlayerCharacter_Front.png"
side_image.name = "T_PlayerCharacter_Side_Aligned.png"
front.rotation_euler = (math.pi / 2, 0, 0)
side.rotation_euler = (math.pi / 2, 0, -math.pi / 2)
side.scale = front.scale.copy()
side.empty_display_size = front.empty_display_size
side.empty_image_offset = front.empty_image_offset
side.location.x = 0
side.location.z = front.location.z

# 두 이미지의 갓 중심이 같은 세로축을 통과하도록 측면의 수평 원점을 정한다.
world_image_size = front.empty_display_size * front.scale.x
side_axis_pixel = 509.0
side.location.y = (side_axis_pixel / 1024 - 0.5) * world_image_size

for reference in (front, side):
    reference.show_empty_image_orthographic = True
    reference.show_empty_image_perspective = False
    reference.show_empty_image_only_axis_aligned = True
    reference.empty_image_depth = "BACK"
    reference.empty_image_side = "DOUBLE_SIDED"

# 이미지 파일과 함께 이동해도 레퍼런스가 끊어지지 않게 블렌드 파일에도 포함한다.
front_image.pack()
side_image.pack()
front_image.filepath = "//../ReferenceImage/PlayerCharacter/T_PlayerCharacter_Front.png"
side_image.filepath = "//../ReferenceImage/PlayerCharacter/T_PlayerCharacter_Side.png"

for screen in bpy.data.screens:
    if screen.name == "Layout":
        for area in screen.areas:
            if area.type == "VIEW_3D":
                region = area.spaces.active.region_3d
                region.view_rotation = Quaternion((1, 0, 0), math.pi / 2)
                region.view_perspective = "ORTHO"
                region.view_location = (0, 0, front.location.z)
                region.view_distance = 3.1

assert get_body_signature() == original_body_signature, "Body mesh or transform changed"
bpy.context.preferences.filepaths.save_version = 0
destination = WORK_FOLDER / "Cheon_Player.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(destination), relative_remap=False, check_existing=False)
report = {
    "source": str(SOURCE_BLEND),
    "staged_blend": str(destination),
    "body_signature": original_body_signature,
    "body_preserved": get_body_signature() == original_body_signature,
    "front_image_size": list(front_image.size),
    "side_image_size": list(side_image.size),
    "packed_references": [front_image.name, side_image.name],
    "world_image_size": world_image_size,
    "world_bottom_z": front.location.z + (0.5 - 988 / 1024) * world_image_size,
    "pixel_verification": json.loads((WORK_FOLDER / "PixelVerification.json").read_text(encoding="utf-8")),
    "reference_objects": [{
        "name": obj.name,
        "location": list(obj.location),
        "rotation": list(obj.rotation_euler),
        "scale": list(obj.scale),
        "display_size": obj.empty_display_size,
    } for obj in (front, side)],
}
(WORK_FOLDER / "AlignmentReport.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print("ALIGNMENT_REPORT", json.dumps(report, ensure_ascii=False))
