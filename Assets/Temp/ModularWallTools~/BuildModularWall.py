import bpy
import bmesh
import math
import random
import json
from pathlib import Path
from mathutils import Vector


# 반복 파츠의 연결면은 X=0, X=2.4이며 끝 파츠의 연결면은 X=0이다.
WORK = Path(r"D:\project\ProjectCheon\Assets\Temp\ModularWallTools~")
LENGTH = 2.4
PITCH = 0.4
random.seed(27)
bpy.context.preferences.filepaths.save_version = 0
if hasattr(bpy.context.preferences.filepaths, "file_preview_type"):
    bpy.context.preferences.filepaths.file_preview_type = "NONE"
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
for block in list(bpy.data.materials):
    bpy.data.materials.remove(block)


def linear(value):
    return value / 12.92 if value < 0.04045 else ((value + 0.055) / 1.055) ** 2.4


def color(hex_value):
    value = hex_value.lstrip("#")
    return tuple(linear(int(value[i:i + 2], 16) / 255.0) for i in (0, 2, 4)) + (1,)


def material(name, palette, scale=5.0, roughness=0.82, bump=0.0):
    result = bpy.data.materials.new(name)
    result.use_nodes = True
    result.diffuse_color = color(palette[len(palette) // 2])
    tree = result.node_tree
    principled = tree.nodes.get("Principled BSDF")
    principled.inputs["Roughness"].default_value = roughness
    principled.inputs["Specular IOR Level"].default_value = 0.25
    geometry = tree.nodes.new("ShaderNodeNewGeometry")
    geometry.location = (-760, 100)
    noise = tree.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = scale * 0.40
    noise.inputs["Detail"].default_value = 0.0
    noise.inputs["Roughness"].default_value = 0.55
    noise.location = (-540, 100)
    # 셀 중심을 샘플링해 둥근 얼룩 대신 큼직하고 각진 색면을 만든다.
    cells = tree.nodes.new("ShaderNodeTexVoronoi")
    cells.location = (-750, -130)
    cells.inputs["Scale"].default_value = scale * 1.45
    cells.inputs["Randomness"].default_value = 0.85
    tree.links.new(geometry.outputs["Position"], cells.inputs["Vector"])
    tree.links.new(cells.outputs["Position"], noise.inputs["Vector"])
    ramp = tree.nodes.new("ShaderNodeValToRGB")
    ramp.location = (-300, 100)
    ramp.color_ramp.interpolation = "CONSTANT"
    for extra in list(ramp.color_ramp.elements)[1:]:
        ramp.color_ramp.elements.remove(extra)
    for i, tone in enumerate(palette):
        pos = 0.16 + i * 0.64 / max(1, len(palette) - 1)
        element = ramp.color_ramp.elements[0] if i == 0 else ramp.color_ramp.elements.new(pos)
        element.position = pos
        element.color = color(tone)
    tree.links.new(noise.outputs["Fac"], ramp.inputs["Fac"])
    tree.links.new(ramp.outputs["Color"], principled.inputs["Base Color"])
    if bump:
        fine = tree.nodes.new("ShaderNodeTexNoise")
        fine.location = (-520, -170)
        fine.inputs["Scale"].default_value = 80.0
        fine.inputs["Detail"].default_value = 1.5
        tree.links.new(geometry.outputs["Position"], fine.inputs["Vector"])
        relief = tree.nodes.new("ShaderNodeBump")
        relief.location = (-190, -150)
        relief.inputs["Strength"].default_value = 0.12
        relief.inputs["Distance"].default_value = bump
        tree.links.new(fine.outputs["Fac"], relief.inputs["Height"])
        tree.links.new(relief.outputs["Normal"], principled.inputs["Normal"])
    return result


stone_mats = [
    material("M_Sandstone_Honey", ["BD9A69", "C9A978", "D5B585", "DEC294", "E6CCA0"], 5.4, bump=0.012),
    material("M_Sandstone_Cream", ["C6A674", "D3B586", "E0C498", "E6CFA9", "EBD6B3"], 5.0, bump=0.012),
    material("M_Sandstone_Warm", ["C29B66", "CDA975", "DCBA88", "E1C291", "E7CDA2"], 4.9, bump=0.012),
    material("M_Sandstone_Pale", ["BFAA87", "CDBA98", "DBC7A6", "E2D1B1", "E8D9BE"], 5.8, bump=0.012),
]
base_mat = material("M_Foundation_WarmGrey", ["8D8270", "A39680", "B2A189", "BEAF95", "C8B99E"], 5.0, bump=0.015)
mortar_mat = material("M_LimeMortar", ["AA9678", "B6A386", "C5B394", "CEBC9C"], 7, bump=0.009)
wood_mat = material("M_Timber_Vermilion", ["803416", "943C19", "A8461E", "B54F23", "BF5A28"], 6.5, roughness=0.78, bump=0.006)
wood_end_mat = material("M_Timber_EndGrain", ["743019", "87371B", "9B421F", "A94B24"], 10)
roof_mats = [
    material("M_Giwa_SlateBlue", ["293343", "303C50", "39475C", "46556B", "8A857A"], 6.5, roughness=0.78, bump=0.003),
    material("M_Giwa_BlueGrey", ["283344", "2F3C50", "37465C", "43546A", "8D877A"], 6.0, roughness=0.79, bump=0.003),
    material("M_Giwa_WarmPatina", ["2B3645", "344154", "3D4C62", "4C5B70", "9B907E"], 6.8, roughness=0.79, bump=0.003),
]
roof_dark = material("M_Giwa_Shadow", ["253140", "2D3A4B", "364457", "404E60"], 8, roughness=0.83)
edge_mat = material("M_Giwa_WornEdge", ["7C7C77", "969084", "ABA08B", "B9AC94"], 12, roughness=0.85)

parts = []


def mesh_object(name, vertices, faces, mat, bevel=0):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    normal_mesh = bmesh.new()
    normal_mesh.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(normal_mesh, faces=list(normal_mesh.faces))
    normal_mesh.to_mesh(mesh)
    normal_mesh.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)
    if bevel:
        modifier = obj.modifiers.new("Soft crafted edges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        modifier.affect = "EDGES"
        apply_modifier(obj, modifier)
    parts.append(obj)
    return obj


def apply_modifier(obj, modifier):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)


def cube(name, center, dimensions, mat, bevel=0.015):
    x, y, z = center
    a, b, c = [dimension / 2 for dimension in dimensions]
    vertices = [(x + sx * a, y + sy * b, z + sz * c)
                for sx, sy, sz in [(-1, -1, -1), (-1, -1, 1), (-1, 1, -1), (-1, 1, 1),
                                   (1, -1, -1), (1, -1, 1), (1, 1, -1), (1, 1, 1)]]
    faces = [(0, 4, 6, 2), (1, 3, 7, 5), (0, 1, 5, 4),
             (2, 6, 7, 3), (0, 2, 3, 1), (4, 5, 7, 6)]
    return mesh_object(name, vertices, faces, mat, bevel)


def clip_x(obj, low, high):
    # 먼저 온전한 블록을 둥글게 만든 뒤 자르므로 복제 경계에 새 모따기가 생기지 않는다.
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    for limit, normal in [(low, (1, 0, 0)), (high, (-1, 0, 0))]:
        result = bmesh.ops.bisect_plane(
            bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
            dist=0.000001, plane_co=(limit, 0, 0), plane_no=normal, clear_inner=True)
        boundary = [item for item in result["geom_cut"] if isinstance(item, bmesh.types.BMEdge) and item.is_boundary]
        if boundary:
            bmesh.ops.holes_fill(bm, edges=boundary, sides=0)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()


def brick(name, center, dimensions, mat, seed, low, high):
    obj = cube(name, center, dimensions, mat, bevel=0)
    rng = random.Random(seed)
    joins_through_center = seed in (400, 422)
    lower_taper, upper_taper = rng.uniform(-0.005, 0.005), rng.uniform(-0.005, 0.005)
    for vertex in obj.data.vertices:
        if joins_through_center:
            # 연결면을 지나는 돌은 앞뒤 대칭을 지켜 끝 파츠를 회전해도 맞물리게 한다.
            taper = lower_taper if vertex.co.z < center[2] else upper_taper
            vertex.co.x += math.copysign(taper, vertex.co.x - center[0])
            vertex.co.y += math.copysign(taper, vertex.co.y)
        else:
            vertex.co.x += rng.uniform(-0.005, 0.005)
            vertex.co.y += rng.uniform(-0.008, 0.008)
            vertex.co.z += rng.uniform(-0.010, 0.010)
        if center[2] < 0.3 and vertex.co.z < center[2]:
            vertex.co.z = 0.0
    bevel = obj.modifiers.new("Hand dressed stone corners", "BEVEL")
    bevel.width = 0.035 if center[2] > 0.3 else 0.028
    bevel.segments = 2
    apply_modifier(obj, bevel)
    clip_x(obj, low, high)
    return obj


def roof_height(t):
    return 2.015 - 0.56 * t + 0.26 * t * t


def roof_slope(t):
    return (-0.56 + 0.52 * t) / 0.61


def tile(name, x_center, side, t_start, t_end, mat, trough=False, radius=0.080, uplift=0.0):
    steps, cross = 5, (8 if not trough else 6)
    vertices = []
    width = 0.4
    for skin in range(2):
        for k in range(steps + 1):
            t = t_start + (t_end - t_start) * k / steps
            slope = roof_slope(t)
            normal = Vector((0, -side * slope, 1)).normalized()
            for j in range(cross + 1):
                u = j / cross
                if trough:
                    offset_x = (u - 0.5) * width
                    offset_z = 0.008 + 0.044 * (2 * u - 1) ** 2 - skin * 0.027
                else:
                    angle = math.pi * u
                    r = radius - skin * 0.019
                    offset_x = r * math.cos(angle)
                    offset_z = r * math.sin(angle) + 0.025
                # 끝 파츠만 살짝 들어 올리고 중앙 모듈의 접합 단면은 일정하게 유지한다.
                base = Vector((x_center + offset_x, side * t * 0.61, roof_height(t) + uplift * t * t))
                point = base + normal * offset_z
                vertices.append(tuple(point))
    row = cross + 1
    layer = (steps + 1) * row
    faces = []
    for skin in range(2):
        for k in range(steps):
            for j in range(cross):
                a = skin * layer + k * row + j
                face = (a, a + 1, a + row + 1, a + row)
                faces.append(face if skin == 0 else face[::-1])
    for k in range(steps):
        for j in [0, cross]:
            a, b = k * row + j, (k + 1) * row + j
            faces.append((a, b, b + layer, a + layer))
    for k in [0, steps]:
        for j in range(cross):
            a = k * row + j
            faces.append((a, a + layer, a + 1 + layer, a + 1))
    obj = mesh_object(name, vertices, faces, mat)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    return obj


def cylinder(name, center, radius, depth, axis, mat, count=16, bevel=0.006):
    bpy.ops.mesh.primitive_cylinder_add(vertices=count, radius=radius, depth=depth, location=center)
    obj = bpy.context.object
    obj.name = name
    if axis == "X":
        obj.rotation_euler[1] = math.pi / 2
    elif axis == "Y":
        obj.rotation_euler[0] = math.pi / 2
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(mat)
    if bevel:
        modifier = obj.modifiers.new("Rounded fired edge", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        apply_modifier(obj, modifier)
    parts.append(obj)
    obj.select_set(False)
    return obj


def ring(name, center, radius, inner, depth, axis, mat, segments=20):
    vertices, faces = [], []
    for along in [-depth / 2, depth / 2]:
        for r in [radius, inner]:
            for i in range(segments):
                theta = 2 * math.pi * i / segments
                a, b = r * math.cos(theta), r * math.sin(theta)
                offset = Vector((along, a, b) if axis == "X" else (a, along, b))
                vertices.append(tuple(Vector(center) + offset))
    for i in range(segments):
        j = (i + 1) % segments
        faces.extend([(i, j, 2 * segments + j, 2 * segments + i),
                      (segments + i, 3 * segments + i, 3 * segments + j, segments + j),
                      (i, segments + i, segments + j, j),
                      (2 * segments + i, 2 * segments + j, 3 * segments + j, 3 * segments + i)])
    return mesh_object(name, vertices, faces, mat, bevel=0.003)


def body(low, high, end=False):
    # 돌 단면과 회반죽 코어의 끝면이 같은 평면에서 겹치지 않게 2 mm 후퇴시킨다.
    cube("Recessed lime mortar", ((low + high) / 2, 0, 0.78), (high - low - 0.004, 0.612, 1.54), mortar_mat, 0)
    courses = [(0.15, 0.292, 0.74, 0.8, 0.0),
               (0.515, 0.420, 0.680, 0.8, 0.4),
               (0.948, 0.420, 0.676, 0.8, 0.0),
               (1.373, 0.402, 0.670, 0.8, 0.4)]
    for row, (z, height, depth, width, shift) in enumerate(courses):
        for i in range(-3, 6):
            x = i * width + shift
            if x + width / 2 < low + 0.001 or x - width / 2 > high - 0.001:
                continue
            phase = i % 3
            mat = base_mat if row == 0 else stone_mats[(phase + row * 2) % len(stone_mats)]
            brick(f"Stone course {row + 1} block {i}", (x, 0, z),
                  (width - 0.014, depth, height), mat, 400 + row * 11 + phase, low, high)
    cube("Continuous vermilion wall plate", ((low + high) / 2, 0, 1.600),
         (high - low + 0.05, 1.100, 0.200), wood_mat, 0.020)
    clip_x(parts[-1], low, high)
    for side in [-1, 1]:
        cube("Upper timber lip", ((low + high) / 2, side * 0.540, 1.695),
             (high - low + 0.05, 0.038, 0.038), wood_mat, 0.007)
        clip_x(parts[-1], low, high)
        for i in range(-2, 8):
            x = 0.2 + PITCH * i
            if low < x < high:
                cube("Exposed short rafter", (x, side * 0.536, 1.620),
                     (0.109, 0.170, 0.190), wood_end_mat, 0.013)


def roof(low, high, end=False):
    for side in [-1, 1]:
        for i in range(-2, 9):
            x = i * PITCH
            if x + 0.2 < low - 0.001 or x - 0.2 > high + 0.001:
                continue
            for section, (a, b) in enumerate([(0.02, 0.535), (0.515, 1.035)]):
                obj = tile("Concave field tile", x, side, a, b, roof_mats[(i + section) % 3], trough=True)
                clip_x(obj, low, high)
            # 처마 끝의 얇은 점토 단면이 기와의 두께를 읽기 쉽게 만든다.
            obj = tile("Worn clay eave edge", x, side, 1.027, 1.038, edge_mat, trough=True)
            clip_x(obj, low, high)
        for i in range(-2, 8):
            x = 0.2 + i * PITCH
            if not low < x < high:
                continue
            for section, (a, b) in enumerate([(0.015, 0.535), (0.505, 1.028)]):
                tile("Convex overlapping cover tile", x, side, a, b, roof_mats[(i + section + 1) % 3])
            t = 1.035
            center = (x, side * (t * 0.61 + 0.010), roof_height(t) + 0.025)
            cylinder("Round eave tile face", center, 0.079, 0.038, "Y", roof_mats[(i + 1) % 3])
            ring("Eave circular rim", (center[0], center[1] + side * 0.023, center[2]),
                 0.075, 0.065, 0.012, "Y", roof_dark)
            cylinder("Inset tile medallion", (center[0], center[1] + side * 0.023, center[2]),
                     0.064, 0.008, "Y", roof_dark, bevel=0.002)
    cube("Ridge dark bedding", ((low + high) / 2, 0, 2.018),
         (high - low + 0.06, 0.250, 0.083), roof_dark, 0.014)
    clip_x(parts[-1], low, high)
    for side in [-1, 1]:
        cube("Fine ridge edge", ((low + high) / 2, side * 0.121, 2.027),
             (high - low + 0.04, 0.010, 0.009), edge_mat, 0.002)
        clip_x(parts[-1], low, high)
    for i in range(-2, 8):
        x = 0.2 + i * PITCH
        if low < x < high:
            cylinder("Segmented rounded ridge", (x, 0, 2.097), 0.106, 0.394, "X", roof_mats[i % 3], 14, 0.008)
    if end:
        x = low + 0.025
        for side in [-1, 1]:
            for section, (a, b) in enumerate([(0.015, 0.535), (0.505, 1.048)]):
                tile("Swept gable edge tile", x, side, a, b, roof_mats[section], radius=0.111, uplift=0.085)
            t = 1.048
            center = (x, side * (t * 0.61 + 0.012), roof_height(t) + 0.025 + 0.085 * t * t)
            cylinder("Large gable eave roundel", center, 0.112, 0.050, "Y", roof_mats[2], 20, 0.008)
            ring("Gable roundel rim", (center[0], center[1] + side * 0.031, center[2]),
                 0.105, 0.087, 0.014, "Y", roof_dark)
            cylinder("Gable roundel inset", (center[0], center[1] + side * 0.032, center[2]),
                     0.086, 0.009, "Y", roof_dark, 20, 0.002)
        cylinder("Raised ridge end disc", (low - 0.012, 0, 2.110), 0.160, 0.085, "X", roof_mats[2], 20, 0.008)
        ring("Ridge terminal concentric rim", (low - 0.062, 0, 2.110),
             0.146, 0.116, 0.018, "X", roof_dark)
        cylinder("Ridge terminal inset", (low - 0.067, 0, 2.110), 0.114, 0.008, "X", roof_dark, 20, 0.003)


def finish(name):
    bpy.ops.object.select_all(action="DESELECT")
    valid_parts = []
    for obj in parts:
        if len(obj.data.polygons):
            obj.select_set(True)
            valid_parts.append(obj)
        else:
            bpy.data.objects.remove(obj, do_unlink=True)
    bpy.context.view_layer.objects.active = valid_parts[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    obj.data.name = name + "_Mesh"
    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    # 부드러운 돌 모서리와 기와의 낮은 면 수를 함께 살린다.
    for poly in obj.data.polygons:
        poly.use_smooth = True
    obj.data.set_sharp_from_angle(angle=math.radians(38))
    weighted = obj.modifiers.new("Weighted corner normals", "WEIGHTED_NORMAL")
    weighted.keep_sharp = True
    weighted.weight = 25
    apply_modifier(obj, weighted)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.008)
    bpy.ops.object.mode_set(mode="OBJECT")
    obj.asset_mark()
    obj.asset_data.author = "Project Cheon"
    obj.asset_data.tags.new("Modular")
    obj.asset_data.tags.new("Korean Wall")
    obj["module_length_m"] = LENGTH if name == "Wall_Middle" else 0.4
    obj["snap_axis"] = "+X"
    obj["connection_origin"] = "Ground center at X=0"
    obj["usage"] = ("Duplicate +2.4 m on X. Keep scale at 1." if name == "Wall_Middle"
                    else "Left end at X=0. Right end: Z rotation 180 degrees, X=2.4 * middle count.")
    obj["materials"] = "Self-contained procedural materials; UV0 included."
    parts.clear()
    return obj


body(0, LENGTH)
roof(0, LENGTH)
middle = finish("Wall_Middle")
middle.select_set(False)
body(-0.4, 0, end=True)
roof(-0.4, 0, end=True)
end = finish("Wall_End")


def look_at(obj, point):
    obj.rotation_euler = (Vector(point) - obj.location).to_track_quat("-Z", "Y").to_euler()


scene = bpy.context.scene
scene.name = "Modular Korean Wall"
scene.unit_settings.system = "METRIC"
scene.unit_settings.length_unit = "METERS"
scene.render.engine = "CYCLES"
scene.cycles.samples = 48
scene.cycles.use_denoising = True
scene.render.resolution_x = 1500
scene.render.resolution_y = 1050
scene.render.resolution_percentage = 100
scene.view_settings.view_transform = "AgX"
scene.view_settings.look = "AgX - Medium High Contrast"
scene.render.image_settings.file_format = "PNG"
scene.world.use_nodes = True
scene.world.node_tree.nodes["Background"].inputs[0].default_value = color("D6E0EF")
scene.world.node_tree.nodes["Background"].inputs[1].default_value = 0.55
ground_mat = material("M_PreviewGround", ["E7DBC1", "E7DBC1"], 1, roughness=0.88)
ground = cube("Preview ground", (2.4, 0, -0.105), (200, 200, 0.2), ground_mat, 0)
parts.clear()
stage_objects = [ground]
for name, location, power, size, tint in [
    ("Key softbox", (-2.5, -4.5, 7), 1200, 5.0, "FFF1D8"),
    ("Cool fill", (5, 2.5, 5.5), 850, 4.0, "DDEAFF"),
    ("Soft front", (4, -5, 3.0), 230, 3.0, "FFF2DF"),
]:
    data = bpy.data.lights.new(name, "AREA")
    data.energy, data.shape, data.size = power, "DISK", size
    data.color = color(tint)[:3]
    obj = bpy.data.objects.new(name, data)
    scene.collection.objects.link(obj)
    obj.location = location
    look_at(obj, (2, 0, 1))
    stage_objects.append(obj)
camera_data = bpy.data.cameras.new("Preview camera")
camera = bpy.data.objects.new("Preview camera", camera_data)
scene.collection.objects.link(camera)
camera.location = (7.4, -9.3, 6.4)
look_at(camera, (2.4, 0, 1.06))
camera_data.type = "ORTHO"
camera_data.ortho_scale = 7.7
scene.camera = camera
stage_objects.append(camera)

second = middle.copy()
second.data = middle.data
scene.collection.objects.link(second)
second.location.x = LENGTH
second.name = "Preview middle repeat"
right = end.copy()
right.data = end.data
scene.collection.objects.link(right)
right.location.x = LENGTH * 2
right.rotation_euler.z = math.pi
right.name = "Preview opposite end"
stage_objects.extend([second, right])

# 렌더링 이미지는 지정된 작업 잔여물 폴더에만 둔다.
scene.render.filepath = str(WORK / "T_Wall_Assembly.png")
bpy.ops.wm.save_as_mainfile(filepath=str(WORK / "Wall_Preview.blend"), compress=True)
bpy.ops.render.render(write_still=True)

second.hide_render = True
right.hide_render = True
end.location.x = -0.52
camera.location = (5.4, -8.4, 5.4)
look_at(camera, (0.85, 0, 1.05))
camera_data.ortho_scale = 4.95
scene.render.filepath = str(WORK / "T_Wall_Parts.png")
bpy.ops.render.render(write_still=True)
end.location.x = 0

for obj in stage_objects:
    bpy.data.objects.remove(obj, do_unlink=True)
scene.camera = None
scene.render.filepath = "//"
bpy.context.preferences.filepaths.save_version = 0
readme = bpy.data.texts.new("사용법_README")
readme.write("""기와 담장 모듈 / Modular Korean Wall

Wall_Middle : 중앙 반복 파츠. 길이 2.4 m, 연결 축 X.
Wall_End    : 끝마감 파츠. 폭 0.4 m, 연결면 원점 X=0.

조립:
1. 중앙 파츠를 (0, 0, 0)에 놓습니다.
2. Shift+D, X, 2.4 로 복제합니다. 2.4 m 간격을 유지합니다.
3. 왼쪽 끝 파츠는 (0, 0, 0), 회전 (0, 0, 0)입니다.
4. 오른쪽 끝 파츠는 (2.4 x 중앙 개수, 0, 0), Z 회전 180도입니다.
   끝 파츠는 앞뒤가 대칭이므로 음수 스케일 없이 양쪽에 사용할 수 있습니다.

모든 파츠는 지면 높이 Z=0이며 스케일이 적용되어 있습니다.
일반 스케일 늘리기보다 복제 또는 Array의 Constant Offset X=2.4를 사용하세요.
앞뒤 모두 모델링되어 있습니다. 외부 이미지 없이 내장 절차형 재질을 사용합니다.
UV0가 포함되어 있습니다. 다른 엔진에 내보낼 경우 절차형 재질은 별도 베이크가 필요합니다.
""")


def setup_view(target):
    bpy.ops.object.select_all(action="DESELECT")
    target.select_set(True)
    bpy.context.view_layer.objects.active = target
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "VIEW_3D":
                space = area.spaces.active
                space.shading.type = "MATERIAL"
                space.shading.studiolight_rotate_z = 0.3
                space.overlay.show_floor = True
                space.clip_end = 200
                region = space.region_3d
                region.view_distance = 4.6 if target == middle else 3.8
                region.view_location = (1.2 if target == middle else -0.2, 0, 1.10)
                region.view_rotation = (Vector((5.5, -8, 5.0))).to_track_quat("Z", "Y")
                region.view_perspective = "ORTHO"


report = {}
for obj in [middle, end]:
    report[obj.name] = {
        "vertices": len(obj.data.vertices),
        "polygons": len(obj.data.polygons),
        "triangles": sum(len(p.vertices) - 2 for p in obj.data.polygons),
        "bounds": [[min(v.co[axis] for v in obj.data.vertices), max(v.co[axis] for v in obj.data.vertices)] for axis in range(3)],
        "uv_layers": list(obj.data.uv_layers.keys()),
        "scale": list(obj.scale),
    }
(WORK / "Wall_GeometryReport.json").write_text(json.dumps(report, indent=2), encoding="utf-8")

# 파일마다 실제 모델 하나만 남기고 저장한다. 다른 모듈 데이터는 다음 저장을 위해 잠시 보관한다.
end_mesh = end.data
end_mesh.use_fake_user = True
end_properties = {key: end[key] for key in end.keys()}
bpy.data.objects.remove(end, do_unlink=True)
setup_view(middle)
repeat = middle.modifiers.new("Repeat length - 2.4 m per section", "ARRAY")
repeat.use_relative_offset = False
repeat.use_constant_offset = True
repeat.constant_offset_displace = (LENGTH, 0, 0)
repeat.count = 1
repeat.use_merge_vertices = False
bpy.ops.wm.save_as_mainfile(filepath=str(WORK / "20_Wall_Middle.blend"), compress=True)
middle_mesh = middle.data
bpy.data.objects.remove(middle, do_unlink=True)
end = bpy.data.objects.new("Wall_End", end_mesh)
scene.collection.objects.link(end)
end_mesh.use_fake_user = False
for key, value in end_properties.items():
    end[key] = value
end.asset_mark()
end.asset_data.author = "Project Cheon"
end.asset_data.tags.new("Modular")
end.asset_data.tags.new("Korean Wall")
setup_view(end)
bpy.ops.wm.save_as_mainfile(filepath=str(WORK / "20_Wall_End.blend"), compress=True)
print("MODULAR_WALL_COMPLETE", json.dumps(report))
