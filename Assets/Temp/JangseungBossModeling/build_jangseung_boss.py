"""장승 보스 모델과 Unity용 FBX를 생성하는 작업 보조 스크립트."""
import bpy
import bmesh
import json
import math
from pathlib import Path
from mathutils import Vector

PROJECT = Path(r"D:\mbcAcademy\ProjectCheon")
ART = PROJECT / "Assets/Arts/Characters/JangseungBoss"
TEMP = PROJECT / "Assets/Temp/JangseungBossModeling"
for folder in (ART / "Models", ART / "Materials", ART / "Source~", TEMP):
    folder.mkdir(parents=True, exist_ok=True)
HEAD_HEIGHT = 2.0
BODY_HEIGHT = HEAD_HEIGHT * 1.35
HEAD_TOP = BODY_HEIGHT + HEAD_HEIGHT
palette = {
    "M_JangseungWood": "A66A32", "M_JangseungCutWood": "C38A4C",
    "M_JangseungDarkWood": "624023", "M_JangseungCavity": "241810",
    "M_JangseungEye": "DFBE7B", "M_JangseungBlack": "100F0D",
    "M_JangseungRed": "B5331D", "M_JangseungTeeth": "D9B675",
    "M_JangseungHat": "25373B",
}
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
for material in list(bpy.data.materials):
    bpy.data.materials.remove(material)
bpy.context.scene.unit_settings.system = "METRIC"
bpy.context.scene.unit_settings.scale_length = 1
bpy.context.preferences.filepaths.save_version = 0

def rgba(value):
    values = [int(value[i:i+2], 16) / 255 for i in (0, 2, 4)]
    return tuple(v / 12.92 if v <= .04045 else ((v + .055) / 1.055) ** 2.4 for v in values) + (1,)

materials = {}
for name, color in palette.items():
    material = bpy.data.materials.new(name)
    material.diffuse_color = rgba(color)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    diffuse = nodes.new("ShaderNodeBsdfDiffuse")
    diffuse.inputs["Color"].default_value = (.8, .8, .8, 1)
    shader_rgb = nodes.new("ShaderNodeShaderToRGB")
    ramp = nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"
    ramp.color_ramp.elements[0].position = .13
    ramp.color_ramp.elements[0].color = (.34, .29, .24, 1)
    ramp.color_ramp.elements[1].position = .60
    ramp.color_ramp.elements[1].color = (1, 1, 1, 1)
    ramp.color_ramp.elements.new(.32).color = (.70, .66, .60, 1)
    multiply = nodes.new("ShaderNodeMixRGB")
    multiply.blend_type = "MULTIPLY"
    multiply.inputs[0].default_value = 1
    multiply.inputs[2].default_value = rgba(color)
    emission = nodes.new("ShaderNodeEmission")
    links = material.node_tree.links
    for output_socket, input_socket in (
        (diffuse.outputs[0], shader_rgb.inputs[0]), (shader_rgb.outputs[0], ramp.inputs[0]),
        (ramp.outputs[0], multiply.inputs[1]), (multiply.outputs[0], emission.inputs[0]),
        (emission.outputs[0], output.inputs[0])):
        links.new(output_socket, input_socket)
    materials[name] = material

collection = bpy.data.collections.new("JangseungBoss_Model")
bpy.context.scene.collection.children.link(collection)
parts = {name: [] for name in ("Body", "Head", "Ear_L", "Ear_R", "Hat")}

def register(obj, group):
    for previous in list(obj.users_collection):
        previous.objects.unlink(obj)
    collection.objects.link(obj)
    parts[group].append(obj)
    return obj

def mesh_object(name, vertices, faces, material, group, indices=None):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    for key in material if isinstance(material, list) else [material]:
        mesh.materials.append(materials[key])
    if indices:
        for polygon, index in zip(mesh.polygons, indices):
            polygon.material_index = index
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    # 입 안쪽은 외피의 반대 방향을 바라보아야 Unity의 뒷면 제거에서도 보인다.
    if name == "Mouth_DeepCarvedCavity":
        bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    parts[group].append(obj)
    return obj

def bevel(obj, width):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    modifier = obj.modifiers.new("CarvedBevel", "BEVEL")
    modifier.width, modifier.segments = width, 1
    if obj.name.startswith("Brow_"):
        modifier.material = 1
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    return obj

def prism(name, outline, front, back, material, group, width=0):
    count = len(outline)
    vertices = [(x, front, z) for x, z in outline] + [(x, back, z) for x, z in outline]
    faces = [tuple(reversed(range(count))), tuple(range(count, 2 * count))]
    faces += [(i, (i+1) % count, (i+1) % count + count, i+count) for i in range(count)]
    obj = mesh_object(name, vertices, faces, material, group)
    if name.startswith("Brow_"):
        obj.data.materials.append(materials["M_JangseungCutWood"])
    return bevel(obj, width) if width else obj

def box(name, center, size, material, group, width=.01, angle=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    obj = bpy.context.object
    obj.name, obj.dimensions = name, size
    obj.rotation_euler[1] = angle
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(materials[material])
    register(obj, group)
    return bevel(obj, width) if width else obj

def ellipsoid(name, center, size, material, segments=12, rings=6):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1, location=center)
    obj = bpy.context.object
    obj.name, obj.scale = name, size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(materials[material])
    return register(obj, "Head")

def loft(name, section, levels, material, group, open_front=False):
    count = len(section)
    vertices = [(x*sx, y*sy+offset, z) for z, sx, sy, offset in levels for x, y in section]
    faces = [tuple(reversed(range(count)))]
    for level in range(len(levels)-1):
        for i in range(count):
            if open_front and i == 0:
                continue
            a, b = level*count+i, level*count+(i+1) % count
            faces.append((a, b, b+count, a+count))
    faces.append(tuple(range((len(levels)-1)*count, len(levels)*count)))
    return mesh_object(name, vertices, faces, material, group)

# 나무 머리 2m, 턱 아래 몸통 2.7m로 실제 지오메트리 비율을 고정한다.
body_section = [(-.38,-.35),(0,-.39),(.38,-.35),(.53,-.21),(.53,.24),
                (.37,.38),(-.37,.38),(-.53,.24),(-.53,-.21)]
loft("Body_TallInsetPillar", body_section,
     [(0,1,1,.04),(BODY_HEIGHT,1,1,.04)],
     "M_JangseungWood","Body")
head_section = [(-.50,-.50),(.50,-.50),(.66,-.30),(.66,.22),
                (.50,.38),(-.50,.38),(-.66,.22),(-.66,-.30)]
loft("Head_CarvedTimber", head_section,
     [(BODY_HEIGHT,1,1,0),(HEAD_TOP-.08,1,1,0),(HEAD_TOP,.94,.94,0)],
     "M_JangseungWood","Head",True)

# 얼굴 앞면에 입 구멍을 남기고 안쪽으로 이어지는 면을 만들어 실제 깊이를 준다.
mouth = [(-.43,2.94),(.43,2.94),(.46,3.10),(.47,3.46),
         (.37,3.54),(-.37,3.55),(-.47,3.46),(-.46,3.10)]
outer = [(-.50,2.80),(.50,2.80),(.50,3.10),(.50,3.46),
         (.50,HEAD_TOP),(-.50,HEAD_TOP),(-.50,3.46),(-.50,3.10)]
vertices = [(x,-.50,z) for x,z in outer] + [(x,-.50,z) for x,z in mouth]
faces = [(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)]
mesh_object("Face_MouthSurround",vertices,faces,"M_JangseungWood","Head")
vertices = [(x,-.50,z) for x,z in mouth] + [(x*.87,-.12,3.24+(z-3.24)*.88) for x,z in mouth]
faces = [(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(8)] + [tuple(range(8,16))]
mesh_object("Mouth_DeepCarvedCavity",vertices,faces,
            ["M_JangseungDarkWood","M_JangseungCavity"],"Head",[0]*8+[1])
jaw = [(-.59,2.94),(-.44,3.04),(0,3.00),(.44,3.04),(.59,2.94),
       (.54,2.78),(.36,BODY_HEIGHT),(-.36,BODY_HEIGHT),(-.54,2.78)]
prism("Jaw_ProtrudingChin",jaw,-.77,-.24,"M_JangseungWood","Head",.025)

for side in (-1,1):
    label = "L" if side < 0 else "R"
    x = side*.292
    ellipsoid("EyeSocket_"+label,(x,-.49,4.055),(.278,.104,.325),"M_JangseungDarkWood")
    ellipsoid("EyeWoodRim_"+label,(x,-.554,4.05),(.238,.124,.279),"M_JangseungEye")
    ellipsoid("EyeBlack_"+label,(side*.278,-.666,4.07),(.135,.034,.170),"M_JangseungBlack")
    ellipsoid("EyeRedPupil_"+label,(side*.278,-.702,4.018),(.039,.012,.040),"M_JangseungRed",10,4)
    brow = [(-.65,4.57),(-.19,4.36),(-.045,4.17),(-.18,4.005),(-.63,4.285)]
    if side > 0:
        brow = [(-x,z) for x,z in reversed(brow)]
    prism("Brow_HeavyAngled_"+label,brow,-.775,-.46,"M_JangseungDarkWood","Head",.024)
nose_section = [(-.70,-.40),(0,-1),(.70,-.40),(1,.20),(.65,1),(-.65,1),(-1,.20)]
loft("Nose_BroadFaceted",nose_section,
     [(3.55,.19,.20,-.77),(3.68,.165,.26,-.76),(4.16,.066,.09,-.59)],
     "M_JangseungCutWood","Head")
for side in (-1,1):
    prism("Nostril_"+str(side),[(side*.055,3.568),(side*.13,3.55),(side*.16,3.58)],
          -.916,-.89,"M_JangseungDarkWood","Head")
upper = [(-.325,3.425,.150,.200,-.10),(-.115,3.385,.157,.250,.04),
         (.108,3.420,.145,.194,-.08),(.322,3.414,.147,.221,.09)]
lower = [(-.31,3.035,.145,.170,.12),(-.105,3.020,.148,.150,-.10),
         (.125,3.083,.145,.275,-.07),(.325,3.040,.131,.180,.12)]
for label, teeth in (("Upper",upper),("Lower",lower)):
    for i,(x,z,width,height,angle) in enumerate(teeth):
        box("Tooth_"+label+"_"+str(i),(x,-.54,z),(width,.17,height),
            "M_JangseungTeeth","Head",.016,angle)

ear_outline = [(.61,4.30),(.74,4.45),(.93,4.48),(1.075,4.39),(1.10,4.22),
               (1.025,4.04),(.975,3.97),(.965,3.28),(.86,3.15),(.71,3.19),
               (.65,3.31),(.66,3.99),(.60,4.13)]
for side in (-1,1):
    group = "Ear_L" if side < 0 else "Ear_R"
    outline = [(side*x,z) for x,z in ear_outline]
    if side < 0:
        outline.reverse()
    count = len(outline)
    vertices, faces, indices = [], [tuple(reversed(range(count)))], [0]
    for sx,sz,y in [(1,1,-.10),(1,1,-.32),(.92,.94,-.46),(.67,.79,-.46),(.58,.72,-.285)]:
        vertices += [(side*.84+(x-side*.84)*sx,y,3.82+(z-3.82)*sz) for x,z in outline]
    for ring in range(4):
        for i in range(count):
            faces.append((ring*count+i,ring*count+(i+1)%count,
                          (ring+1)*count+(i+1)%count,(ring+1)*count+i))
            indices.append([0,1,0,2][ring])
    faces.append(tuple(range(4*count,5*count)))
    indices.append(0)
    mesh_object(group+"_CarvedLongEar",vertices,faces,
                ["M_JangseungWood","M_JangseungCutWood","M_JangseungDarkWood"],group,indices)

hat_section = [(-.47,-.52),(.47,-.52),(.66,-.33),(.66,.26),
               (.47,.45),(-.47,.45),(-.66,.26),(-.66,-.33)]
loft("Hat_MainCrown",hat_section,[(4.66,1.02,1,0),(4.73,1.02,1,0),(5.10,.95,.96,0),(5.16,.86,.86,0)],
     "M_JangseungHat","Hat")
loft("Hat_RaisedTop",hat_section,[(5.12,.72,.74,0),(5.38,.72,.74,0),(5.43,.65,.67,0)],
     "M_JangseungHat","Hat")
for side in (-1,1):
    box("Hat_Wing_"+str(side),(side*1.02,.02,4.88),(.98,.25,.33),"M_JangseungHat","Hat",.085)
for i,outline in enumerate([
    [(-.255,.01),(-.235,.75),(-.24,1.21),(-.207,1.83),(-.215,.71),(-.224,.01)],
    [(.19,.01),(.17,1.0),(.19,1.65),(.176,2.69),(.163,1.59),(.15,1.01),(.16,.01)]]):
    vertices = [(x,-.314-.04*(1-abs(x)/.38),z) for x,z in outline]
    mesh_object("Body_WoodSplit_"+str(i),vertices,[tuple(range(len(vertices)))],"M_JangseungDarkWood","Body")
prism("Forehead_WoodSplit",[(.315,4.70),(.297,4.37),(.32,4.19),(.321,4.40),(.34,4.70)],
      -.505,-.494,"M_JangseungDarkWood","Head")

root = bpy.data.objects.new("JangseungBoss",None)
collection.objects.link(root)
root["wooden_head_height_m"], root["body_height_m"] = HEAD_HEIGHT, BODY_HEIGHT
root["head_body_ratio"] = "1 : 1.35 (hat excluded)"
head_pivot = bpy.data.objects.new("HeadPivot",None)
collection.objects.link(head_pivot)
head_pivot.location = (0,0,BODY_HEIGHT)
head_pivot.parent = root
bpy.context.view_layer.update()
joined = []
for group, objects in parts.items():
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = group
    # 머리 밑부분을 피벗으로 삼아 추후 움직임을 적용하기 쉽게 한다.
    bpy.context.scene.cursor.location = (0,0,0 if group == "Body" else BODY_HEIGHT)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    matrix = obj.matrix_world.copy()
    obj.parent = root if group == "Body" else head_pivot
    obj.matrix_world = matrix
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.025)
    bpy.ops.object.mode_set(mode="OBJECT")
    joined.append(obj)
bpy.context.view_layer.update()
bounds, triangles, vertex_count = {}, 0, 0
for obj in joined:
    obj.data.calc_loop_triangles()
    triangles += len(obj.data.loop_triangles)
    vertex_count += len(obj.data.vertices)
    coordinates = [obj.matrix_world @ Vector(v) for v in obj.bound_box]
    bounds[obj.name] = {"min":[min(v[i] for v in coordinates) for i in range(3)],
                        "max":[max(v[i] for v in coordinates) for i in range(3)]}
assert abs(bounds["Body"]["max"][2]-bounds["Body"]["min"][2]-BODY_HEIGHT) < 1e-5
assert abs(bounds["Head"]["max"][2]-bounds["Head"]["min"][2]-HEAD_HEIGHT) < .002
assert abs(BODY_HEIGHT/HEAD_HEIGHT-1.35) < 1e-8

bpy.ops.object.select_all(action="DESELECT")
for obj in [root,head_pivot]+joined:
    obj.select_set(True)
bpy.context.view_layer.objects.active = root
bpy.ops.export_scene.fbx(filepath=str(ART/"Models/JangseungBoss.fbx"),use_selection=True,
    object_types={"MESH","EMPTY"},global_scale=1,apply_unit_scale=True,apply_scale_options="FBX_SCALE_UNITS",
    axis_forward="-Z",axis_up="Y",use_mesh_modifiers=True,mesh_smooth_type="FACE",
    use_custom_props=True,bake_anim=False,add_leaf_bones=False,path_mode="AUTO",embed_textures=False)
manifest = {"woodenHeadHeight":HEAD_HEIGHT,"bodyHeight":BODY_HEIGHT,"bodyToHeadRatio":1.35,
            "hatExcludedFromRatio":True,"triangles":triangles,"vertices":vertex_count,
            "meshObjects":len(joined),"materialColors":palette,"bounds":bounds}
(TEMP/"build_manifest.json").write_text(json.dumps(manifest,indent=2),encoding="utf-8")

# 촬영 세트는 FBX에 포함하지 않고 Blender 원본에만 분리하여 둔다.
studio = bpy.data.collections.new("Preview_Studio_NotExported")
bpy.context.scene.collection.children.link(studio)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.012))
floor = bpy.context.object
for previous in list(floor.users_collection):
    previous.objects.unlink(floor)
studio.objects.link(floor)
floor.name = "Preview_Ground"
ground = bpy.data.materials.new("Preview_GroundMaterial")
ground.use_nodes = True
ground.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value = (.44,.43,.40,1)
ground.node_tree.nodes.get("Principled BSDF").inputs["Roughness"].default_value = 1
floor.data.materials.append(ground)
for name,location,energy,size in [("Key",(-3,-4,8),1050,4),("Fill",(4,-1,5),300,5),("Rim",(0,4,7),600,3)]:
    data = bpy.data.lights.new("Preview_"+name,"AREA")
    data.energy, data.shape, data.size = energy,"DISK",size
    obj = bpy.data.objects.new(data.name,data)
    studio.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector((0,0,2.7))-obj.location).to_track_quat("-Z","Y").to_euler()
camera_data = bpy.data.cameras.new("Preview_Camera")
camera = bpy.data.objects.new("Preview_Camera",camera_data)
studio.objects.link(camera)
scene = bpy.context.scene
scene.camera = camera
camera_data.type, camera_data.ortho_scale = "ORTHO",6.1
scene.render.engine = "BLENDER_EEVEE"
scene.render.resolution_x = scene.render.resolution_y = 1024
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.world.use_nodes = True
scene.world.node_tree.nodes.get("Background").inputs["Color"].default_value = (.44,.43,.40,1)
scene.world.node_tree.nodes.get("Background").inputs["Strength"].default_value = .7
scene.view_settings.view_transform = "Standard"
scene.view_settings.look = "None"
def view(location):
    camera.location = location
    camera.rotation_euler = (Vector((0,-.02,2.7))-camera.location).to_track_quat("-Z","Y").to_euler()
view((7,-13,6.6))
bpy.ops.wm.save_as_mainfile(filepath=str(ART/"Source~/JangseungBoss.blend"))
for label,location in [("Hero",(7,-13,6.6)),("Front",(0,-15,2.7)),("Side",(15,0,2.7))]:
    view(location)
    scene.render.filepath = str(TEMP/("Preview_"+label+".png"))
    bpy.ops.render.render(write_still=True)
print("JANGSEUNG_BUILD_COMPLETE "+json.dumps({"triangles":triangles,"vertices":vertex_count,"ratio":1.35}))

