import bpy
import bmesh
import math
import os
import json
import struct
import zlib
import numpy as np
from mathutils import Vector, Quaternion


class PlayerFanModel:
    """레퍼런스의 윤곽과 문양을 사용해 편집 가능한 부채를 만든다."""

    def __init__(self):
        self.directory = os.path.dirname(os.path.abspath(__file__))
        self.references = r'E:\C.J.I\ReferenceImage\PlayerFan'
        self.scale = 0.00052
        self.pivot = np.array((511.0, 674.0))
        self.outer = np.array([
            (55, 396), (88, 304), (142, 260), (202, 187),
            (275, 161), (354, 111), (422, 109), (512, 82),
            (600, 108), (669, 112), (747, 161), (821, 187),
            (879, 259), (935, 304), (974, 395),
        ], dtype=float)
        self.inner = self.pivot + (self.outer - self.pivot) / np.linalg.norm(
            self.outer - self.pivot, axis=1)[:, None] * 236.0
        self.materials = {}
        self.meshes = []
        self.stats = {}

    def collection(self, name):
        collection = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(collection)
        return collection

    def point(self, image_point, depth=0.0):
        return ((image_point[0] - self.pivot[0]) * self.scale,
                depth, (self.pivot[1] - image_point[1]) * self.scale)

    def material(self, name, color, metallic=0, roughness=.55):
        material = bpy.data.materials.new(name)
        material.diffuse_color = (*color, 1)
        material.use_nodes = True
        shader = material.node_tree.nodes.get('Principled BSDF')
        shader.inputs['Base Color'].default_value = (*color, 1)
        shader.inputs['Metallic'].default_value = metallic
        shader.inputs['Roughness'].default_value = roughness
        shader.inputs['Specular IOR Level'].default_value = .22
        self.materials[name] = material
        return material

    @staticmethod
    def read_png(path):
        # 원본 PNG를 직접 읽어 색상 변환 없이 인쇄용 문양을 추출한다.
        with open(path, 'rb') as source:
            data = source.read()
        offset, chunks = 8, []
        while offset < len(data):
            length = struct.unpack('>I', data[offset:offset + 4])[0]
            kind = data[offset + 4:offset + 8]
            content = data[offset + 8:offset + 8 + length]
            if kind == b'IHDR':
                width, height, bits, color, _, _, interlace = struct.unpack('>2I5B', content)
            elif kind == b'IDAT':
                chunks.append(content)
            offset += length + 12
        assert bits == 8 and color in (2, 6) and interlace == 0
        channels = 3 if color == 2 else 4
        raw = np.frombuffer(zlib.decompress(b''.join(chunks)), dtype=np.uint8)
        raw = raw.reshape(height, width * channels + 1)
        decoded = np.zeros((height, width * channels), dtype=np.uint8)
        for row in range(height):
            mode, current = int(raw[row, 0]), raw[row, 1:].astype(np.int32)
            previous = decoded[row - 1] if row else np.zeros(width * channels, dtype=np.uint8)
            for column in range(width * channels):
                left = int(decoded[row, column - channels]) if column >= channels else 0
                above = int(previous[column])
                corner = int(previous[column - channels]) if column >= channels else 0
                if mode == 0:
                    predictor = 0
                elif mode == 1:
                    predictor = left
                elif mode == 2:
                    predictor = above
                elif mode == 3:
                    predictor = (left + above) // 2
                else:
                    prediction = left + above - corner
                    distances = (abs(prediction - left), abs(prediction - above), abs(prediction - corner))
                    predictor = (left, above, corner)[distances.index(min(distances))]
                decoded[row, column] = (int(current[column]) + predictor) & 255
        return decoded.reshape(height, width, channels)[:, :, :3].astype(float) / 255

    @staticmethod
    def save_png(path, pixels):
        height, width = pixels.shape[:2]
        data = np.uint8(np.clip(pixels * 255 + .5, 0, 255))
        def chunk(kind, payload):
            return struct.pack('>I', len(payload)) + kind + payload + struct.pack('>I', zlib.crc32(kind + payload) & 0xffffffff)
        payload = b''.join(b'\x00' + row.tobytes() for row in data)
        with open(path, 'wb') as target:
            target.write(b'\x89PNG\r\n\x1a\n')
            target.write(chunk(b'IHDR', struct.pack('>2I5B', width, height, 8, 2, 0, 0, 0)))
            target.write(chunk(b'IDAT', zlib.compress(payload)))
            target.write(chunk(b'IEND', b''))

    def create_paper_material(self):
        source = self.read_png(os.path.join(self.references, 'T_PlayerFan_Front.png'))
        height, width = source.shape[:2]
        yy, xx = np.mgrid[:height, :width]
        points = np.stack((xx, yy), axis=-1)
        delta = points - self.pivot
        angle = np.arctan2(delta[:, :, 0], -delta[:, :, 1])
        radius = np.linalg.norm(delta, axis=2)
        rays = self.outer - self.pivot
        angles = np.arctan2(rays[:, 0], -rays[:, 1])
        cream = np.array((.956, .919, .825))
        output = np.empty_like(source)
        output[:] = cream
        for index in range(len(angles) - 1):
            along = (angle - angles[index]) / (angles[index + 1] - angles[index])
            # 각 면의 종이색을 근사해 원본의 조명/주름 음영이 두 번 적용되지 않게 한다.
            outer_radius = np.linalg.norm(rays[index]) * (1 - along) + np.linalg.norm(rays[index + 1]) * along
            radial = (radius - 236) / np.maximum(outer_radius - 236, 1)
            mask = (along >= 0) & (along <= 1) & (radial >= -.04) & (radial <= 1.01)
            red, green, blue = source[:, :, 0], source[:, :, 1], source[:, :, 2]
            blank = mask & (radial > .05) & (radial < .93) & (red > .67) & (green > .64) & (blue > .52)
            blank &= (red - green > .004) & (red - green < .095) & (red - blue < .225)
            local_along, local_radial = along[mask], radial[mask]
            features = np.stack((np.ones_like(along), along, radial, along * radial, radial ** 2), axis=-1)
            coefficients = np.linalg.lstsq(features[blank][::4], source[blank][::4], rcond=None)[0]
            illumination = np.clip(features[mask] @ coefficients, .60, 1.05)
            output[mask] = np.clip(source[mask] / illumination * cream, 0, 1)
        # 얇은 안쪽 종이 테두리에 원본 부챗살/배경이 섞이지 않도록 여백을 정리한다.
        output[radius < 249] = cream
        # 스튜디오 조명에서도 수묵의 짙은 청록색과 금색 해가 원본처럼 읽히게 한다.
        teal_ink = np.clip((output[:, :, 1] - output[:, :, 0]) / .16, 0, 1)
        output *= 1 - teal_ink[:, :, None] * np.array((.43, .32, .30))
        gold_ink = np.clip((output[:, :, 0] - output[:, :, 1] - .10) / .19, 0, 1)
        output *= 1 - gold_ink[:, :, None] * np.array((.01, .13, .32))
        texture_path = os.path.join(self.directory, 'T_PlayerFan_PaperBaseColor.png')
        self.save_png(texture_path, output)
        image = bpy.data.images.load(texture_path, check_existing=False)
        image.name = 'T_PlayerFan_PaperBaseColor'
        image.pack()
        material = self.material('M_PlayerFan_PaintedPaper', (.956, .919, .825), roughness=.77)
        tree = material.node_tree
        texture = tree.nodes.new('ShaderNodeTexImage')
        texture.name = 'T_PlayerFan_PaperBaseColor'
        texture.label = '레퍼런스 산수화 · 조명 보정'
        texture.image = image
        texture.location = (-520, 170)
        shader = tree.nodes.get('Principled BSDF')
        tree.links.new(texture.outputs['Color'], shader.inputs['Base Color'])
        noise = tree.nodes.new('ShaderNodeTexNoise')
        noise.inputs['Scale'].default_value = 2200
        noise.inputs['Roughness'].default_value = .72
        noise.location = (-530, -170)
        bump = tree.nodes.new('ShaderNodeBump')
        bump.inputs['Strength'].default_value = .15
        bump.inputs['Distance'].default_value = .000065
        bump.location = (-230, -100)
        tree.links.new(noise.outputs['Fac'], bump.inputs['Height'])
        tree.links.new(bump.outputs['Normal'], shader.inputs['Normal'])
        return material

    def mesh(self, name, vertices, faces, materials, collection, uv=None, material_indices=None):
        mesh = bpy.data.meshes.new(name + '_Mesh')
        mesh.from_pydata(vertices, [], faces)
        mesh.update()
        normals = bmesh.new()
        normals.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(normals, faces=normals.faces)
        normals.to_mesh(mesh)
        normals.free()
        obj = bpy.data.objects.new(name, mesh)
        collection.objects.link(obj)
        for material in materials:
            mesh.materials.append(material)
        if material_indices:
            for polygon, index in zip(mesh.polygons, material_indices):
                polygon.material_index = index
        if uv:
            layer = mesh.uv_layers.new(name='UVMap')
            for polygon in mesh.polygons:
                for loop_index in polygon.loop_indices:
                    layer.data[loop_index].uv = uv[mesh.loops[loop_index].vertex_index]
        else:
            # 금속/목재 부품에도 기본 UV를 만들어 이후 텍스처 작업을 할 수 있게 한다.
            layer = mesh.uv_layers.new(name='UVMap')
            for polygon in mesh.polygons:
                normal = polygon.normal
                axis = max(range(3), key=lambda a: abs(normal[a]))
                axes = [a for a in range(3) if a != axis]
                for loop_index in polygon.loop_indices:
                    co = mesh.vertices[mesh.loops[loop_index].vertex_index].co
                    layer.data[loop_index].uv = (co[axes[0]] * 4 + .5, co[axes[1]] * 4 + .5)
        self.meshes.append(obj)
        obj.parent = self.root
        return obj

    def bevel(self, obj, width=.0007, segments=1):
        bevel = obj.modifiers.new('Edge bevel', 'BEVEL')
        bevel.width = width
        bevel.segments = segments
        bevel.affect = 'EDGES'
        bevel.profile = .5
        return obj

    def prism(self, name, outline, depth, thickness, material, collection, bevel=0):
        vertices = [(x, y, z) for y in (depth - thickness / 2, depth + thickness / 2) for x, z in outline]
        count = len(outline)
        faces = [tuple(reversed(range(count))), tuple(range(count, 2 * count))]
        faces += [(index, (index + 1) % count, (index + 1) % count + count, index + count) for index in range(count)]
        obj = self.mesh(name, vertices, faces, [material], collection)
        # 입력 윤곽 방향에 관계없이 모든 부품의 법선을 바깥으로 정렬한다.
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(obj.data)
        bm.free()
        if bevel:
            self.bevel(obj, bevel)
        return obj

    def cylinder(self, name, center, radius, depth, material, collection, sides=12, axis='Z'):
        bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius, depth=depth, location=center,
                                          rotation=(math.pi / 2, 0, 0) if axis == 'Y' else (0, 0, 0))
        obj = bpy.context.object
        obj.name = name
        obj.data.name = name + '_Mesh'
        for previous in list(obj.users_collection):
            previous.objects.unlink(obj)
        collection.objects.link(obj)
        obj.data.materials.append(material)
        obj.parent = self.root
        self.meshes.append(obj)
        return obj

    def tube(self, name, points, radius, material, collection, sides=8):
        points = [Vector(p) for p in points]
        vertices, faces = [], []
        for index, point in enumerate(points):
            direction = points[min(index + 1, len(points) - 1)] - points[max(0, index - 1)]
            side = direction.cross(Vector((1, 0, 0))).normalized()
            across = direction.normalized().cross(side).normalized()
            for step in range(sides):
                angle = step * math.tau / sides
                vertices.append(point + radius * (math.cos(angle) * side + math.sin(angle) * across))
        for row in range(len(points) - 1):
            for column in range(sides):
                a = row * sides + column
                b = row * sides + (column + 1) % sides
                faces.append((a, b, b + sides, a + sides))
        faces += [tuple(reversed(range(sides))), tuple((len(points) - 1) * sides + i for i in range(sides))]
        return self.mesh(name, vertices, faces, [material], collection)

    def create_paper(self, collection, paper, cream, gold):
        # 산/골을 번갈아 배치해 위·옆에서도 주름이 보이는 실제 두께의 종이를 만든다.
        vertices, uv = [], []
        for back in (False, True):
            for index, (inner, outer) in enumerate(zip(self.inner, self.outer)):
                peak = -.013 if index % 2 else .009
                for image_point, taper in ((inner, .28), (outer, 1.0)):
                    vertices.append(self.point(image_point, peak * taper + (.00055 if back else 0)))
                    uv.append((image_point[0] / 1024, 1 - image_point[1] / 1024))
        row = len(self.outer) * 2
        faces, indices = [], []
        for index in range(len(self.outer) - 1):
            a = 2 * index
            faces.append((a, a + 2, a + 3, a + 1))
            indices.append(0)
            faces.append((a + row, a + 1 + row, a + 3 + row, a + 2 + row))
            indices.append(1)
            faces.append((a + 1, a + 3, a + 3 + row, a + 1 + row))
            indices.append(1)
            faces.append((a + 2, a, a + row, a + 2 + row))
            indices.append(1)
        faces += [(0, 1, row + 1, row), (row - 1, row - 2, 2 * row - 2, 2 * row - 1)]
        indices += [1, 1]
        sheet = self.mesh('Fan_PleatedPaper', vertices, faces, [paper, cream], collection, uv, indices)
        sheet['description'] = '14개 면 / 7개 산 주름 / 실제 두께 0.55mm'
        sheet['front'] = '전면 -Y, 레퍼런스 투영 UV'

        for index in range(len(self.outer) - 1):
            # 금박은 종이 위에 붙이는 얇은 띠로, 양면과 상단 두께를 모두 감싼다.
            a, b = self.outer[index], self.outer[index + 1]
            inner_a = a + (self.pivot - a) / np.linalg.norm(self.pivot - a) * 5.4
            inner_b = b + (self.pivot - b) / np.linalg.norm(self.pivot - b) * 5.4
            da = -.013 if index % 2 else .009
            db = -.013 if (index + 1) % 2 else .009
            front = [self.point(inner_a, da * .992 - .00013), self.point(a, da - .00013),
                     self.point(b, db - .00013), self.point(inner_b, db * .992 - .00013)]
            back = [(x, y + .00082, z) for x, y, z in front]
            border_faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
                            (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
            self.mesh('Fan_GoldRim_%02d' % (index + 1), front + back, border_faces, [gold], collection)
        return sheet

    def create_ribs(self, collection, wood, wood_light, wood_dark, gold):
        for index, degrees in enumerate((-59, -45, -30, -15, 0, 15, 30, 45, 59)):
            angle = math.radians(degrees)
            radial = Vector((math.sin(angle), math.cos(angle)))
            perpendicular = Vector((math.cos(angle), -math.sin(angle)))
            # 살 끝은 종이 안쪽으로 조금 숨겨 이음부가 벌어져 보이지 않게 한다.
            length = .1265
            bottom_width, top_width = .007, .0135
            outline = []
            for distance, offset in ((-.003, -bottom_width / 2), (length, -top_width / 2),
                                     (length + .002, top_width / 2), (-.003, bottom_width / 2)):
                point = radial * distance + perpendicular * offset
                outline.append(tuple(point))
            # 여러 겹의 살을 축에 적층하되 전체 두께는 손에 쥘 수 있는 범위로 유지한다.
            depth = .007 + (index - 4) * .00045
            rib = self.prism('Fan_WoodRib_%02d' % (index + 1), outline, depth, .0042,
                             wood if index % 3 else wood_light, collection, .00055)
            rib['angle_degrees'] = degrees

        for side, name in ((-1, 'L'), (1, 'R')):
            # 외곽 장살은 레퍼런스의 굵은 테이퍼 실루엣을 직접 따른다.
            image_outline = [(501, 670), (299, 552), (52, 389), (39, 398),
                             (16, 456), (291, 591), (508, 692)]
            if side == 1:
                image_outline = [(1022 - x, z) for x, z in image_outline]
            outline = [(self.point(point)[0], self.point(point)[2]) for point in image_outline]
            guard = self.prism('Fan_OuterGuard_' + name, outline, -.003, .013,
                               wood, collection, .0015)
            guard['description'] = '굵은 목재 장살 / 금색 끝띠'
            # 넓은 전면에 작은 색 차이를 주어 원목의 절삭면을 표현한다.
            inset = [(494, 667), (296, 558), (51, 401), (29, 451), (289, 581)]
            if side == 1:
                inset = [(1022 - x, z) for x, z in inset]
            surface = [(self.point(point)[0], self.point(point)[2]) for point in inset]
            self.prism('Fan_GuardFacet_' + name, surface, -.0097, .00035,
                       wood_light, collection)
            band = [(43, 404), (54, 410), (34, 463), (23, 458)]
            if side == 1:
                band = [(1022 - x, z) for x, z in band]
            outline = [(self.point(point)[0], self.point(point)[2]) for point in band]
            self.prism('Fan_GuardGoldBand_' + name, outline, -.003, .0146,
                       gold, collection, .00045)

        hub = [(472, 650), (501, 640), (530, 646), (553, 681),
               (538, 702), (519, 711), (493, 708), (465, 687)]
        hub_outline = [(self.point(point)[0], self.point(point)[2]) for point in hub]
        self.prism('Fan_PivotWoodHousing', hub_outline, -.001, .017, wood_dark, collection, .0011)
        front_hub = [(479, 653), (502, 647), (527, 653), (544, 680),
                     (534, 697), (518, 704), (496, 701), (475, 686)]
        outline = [(self.point(point)[0], self.point(point)[2]) for point in front_hub]
        self.prism('Fan_PivotFace', outline, -.0101, .0012, wood, collection, .0004)

        self.bevel(self.cylinder('Fan_PivotAxle', (0, 0, 0), .0065, .031,
                                 gold, collection, 12, 'Y'), .0007)
        for side, name in ((-1, 'Front'), (1, 'Back')):
            # 낮은 다면체 리벳으로 정면과 측면의 금속 고정부를 표현한다.
            vertices = [(0, side * .0195, .0005)]
            count = 8
            for radius, depth in ((.0071, .0165), (.0078, .0139)):
                for step in range(count):
                    angle = math.tau * step / count + math.pi / 8
                    vertices.append((radius * math.cos(angle), side * depth, radius * math.sin(angle) + .0005))
            faces = [(0, 1 + i, 1 + (i + 1) % count) for i in range(count)]
            faces += [(1 + i, 9 + i, 9 + (i + 1) % count, 1 + (i + 1) % count) for i in range(count)]
            faces.append(tuple(reversed(range(9, 17))))
            self.mesh('Fan_PivotGoldCap_' + name, vertices, faces, [gold], collection)

    def create_pendant(self, collection, jade, teal, gold, ivory, ivory_shadow):
        # 두 가닥의 청록색 고리를 리벳 아래로 통과시킨다.
        for side, name in ((-1, 'L'), (1, 'R')):
            points = [(side * .0047, -.0144, -.004),
                      (side * .0046, -.0149, -.014),
                      (side * .0030, -.014, -.027),
                      (side * .0012, -.012, -.034)]
            self.tube('Fan_TealCord_' + name, points, .00165, teal, collection)
        self.cylinder('Fan_JadeTopGoldCap', (0, -.0119, -.034), .0065, .0035, gold, collection, 8)

        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=.0142, location=(0, -.0119, -.050))
        bead = bpy.context.object
        bead.name = 'Fan_FacetedJadeBead'
        bead.scale = (1, .87, 1.03)
        for previous in list(bead.users_collection):
            previous.objects.unlink(bead)
        collection.objects.link(bead)
        bead.parent = self.root
        bead.data.materials.append(jade)
        lighter = self.material('M_PlayerFan_JadeLight', (.42, .72, .51), roughness=.46)
        darker = self.material('M_PlayerFan_JadeShade', (.25, .52, .34), roughness=.48)
        bead.data.materials.append(lighter)
        bead.data.materials.append(darker)
        for polygon in bead.data.polygons:
            polygon.material_index = 1 if polygon.index % 7 == 1 else (2 if polygon.index % 11 == 2 else 0)
        self.meshes.append(bead)
        self.cylinder('Fan_JadeBottomGoldCap', (0, -.0119, -.0655), .0060, .0035, gold, collection, 8)

        # 술은 가느다란 실 대신 넓은 여덟 면으로 만들어 원본의 로우폴리 느낌을 유지한다.
        count = 8
        vertices, faces = [], []
        levels = [(-.0685, .0046), (-.0745, .0090), (-.1355, .0215), (-.1400, .0140)]
        for row, (height, radius) in enumerate(levels):
            for step in range(count * 2):
                angle = math.tau * step / (count * 2) + math.pi / 8
                current_radius = radius * (1 if step % 2 == 0 else (.68 if row >= 2 else .94))
                z = height + (.004 if row == 2 and step % 2 else 0)
                vertices.append((current_radius * math.sin(angle), -.0119 + current_radius * .70 * math.cos(angle), z))
        indices = []
        for row in range(len(levels) - 1):
            for step in range(count * 2):
                a = row * count * 2 + step
                b = row * count * 2 + (step + 1) % (count * 2)
                faces.append((a, b, b + count * 2, a + count * 2))
                indices.append(step % 2)
        faces += [tuple(reversed(range(count * 2))), tuple(range(3 * count * 2, 4 * count * 2))]
        indices += [0, 1]
        tassel = self.mesh('Fan_IvoryPleatedTassel', vertices, faces, [ivory, ivory_shadow], collection,
                           material_indices=indices)
        bm = bmesh.new()
        bm.from_mesh(tassel.data)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(tassel.data)
        bm.free()

    def setup_studio(self, collection):
        scene = bpy.context.scene
        scene.render.engine = 'CYCLES'
        scene.cycles.samples = 48
        scene.cycles.use_denoising = True
        scene.render.resolution_x = 1200
        scene.render.resolution_y = 1200
        scene.render.resolution_percentage = 100
        scene.render.image_settings.file_format = 'PNG'
        scene.render.film_transparent = False
        if scene.world is None:
            scene.world = bpy.data.worlds.new('PlayerFan_StudioWorld')
        scene.world.color = (.28, .28, .28)
        scene.world.use_nodes = True
        scene.world.node_tree.nodes['Background'].inputs['Color'].default_value = (.66, .70, .73, 1)
        scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value = .32
        scene.view_settings.view_transform = 'AgX'
        scene.view_settings.look = 'AgX - Medium High Contrast'
        scene.render.image_settings.color_mode = 'RGBA'
        scene.unit_settings.system = 'METRIC'
        scene.unit_settings.length_unit = 'METERS'

        def camera(name, location, target, scale):
            data = bpy.data.cameras.new(name)
            obj = bpy.data.objects.new(name, data)
            collection.objects.link(obj)
            obj.location = location
            obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()
            data.type = 'ORTHO'
            data.ortho_scale = scale
            data.lens = 55
            return obj

        target = (0, 0, .086)
        self.cameras = {
            'Isometric': camera('Camera_Isometric', (.36, -1.15, .35), target, .575),
            'Front': camera('Camera_Front', (0, -1.3, .086), target, .575),
            'Side': camera('Camera_Side', (1.3, 0, .086), target, .54),
            'Top': camera('Camera_Top', (0, 0, 1.3), (0, 0, .09), .575),
            'Back': camera('Camera_Back', (0, 1.3, .086), target, .575),
        }
        scene.camera = self.cameras['Isometric']

        def area(name, location, power, size, color):
            light = bpy.data.lights.new(name, 'AREA')
            light.energy = power
            light.shape = 'DISK'
            light.size = size
            light.color = color
            obj = bpy.data.objects.new(name, light)
            collection.objects.link(obj)
            obj.location = location
            obj.rotation_euler = (Vector(target) - obj.location).to_track_quat('-Z', 'Y').to_euler()
        area('Studio_Key', (-.35, -.55, .70), 13, .55, (1, .91, .79))
        area('Studio_Fill', (.48, -.30, .24), 6, .48, (.77, .90, 1))
        area('Studio_Rim', (.10, .38, .57), 17, .44, (1, .91, .76))

        # 배경은 모델과 분리해 두고 기본 작업 화면에서는 숨긴다.
        background = bpy.data.materials.new('M_PlayerFan_StudioBackground')
        background.use_nodes = True
        shader = background.node_tree.nodes.get('Principled BSDF')
        shader.inputs['Base Color'].default_value = (.61, .59, .55, 1)
        shader.inputs['Roughness'].default_value = 1
        # 뒷면 카메라에서도 모델이 배경판에 가려지지 않도록 배경판의 뒷면을 투명하게 한다.
        nodes = background.node_tree.nodes
        links = background.node_tree.links
        geometry = nodes.new('ShaderNodeNewGeometry')
        transparent = nodes.new('ShaderNodeBsdfTransparent')
        mix = nodes.new('ShaderNodeMixShader')
        links.new(geometry.outputs['Backfacing'], mix.inputs[0])
        links.new(shader.outputs['BSDF'], mix.inputs[1])
        links.new(transparent.outputs['BSDF'], mix.inputs[2])
        links.new(mix.outputs[0], nodes.get('Material Output').inputs['Surface'])
        mesh = bpy.data.meshes.new('Studio_Backdrop_Mesh')
        mesh.from_pydata([(-20, .12, -20), (20, .12, -20), (20, .12, 20), (-20, .12, 20)], [], [(0, 1, 2, 3)])
        backdrop = bpy.data.objects.new('Studio_Backdrop', mesh)
        collection.objects.link(backdrop)
        mesh.materials.append(background)
        for obj in collection.objects:
            obj.hide_set(True)

    def setup_references(self, collection):
        for index, view in enumerate(('Front', 'Side', 'Top', 'Isometric')):
            image = bpy.data.images.load(os.path.join(self.references, 'T_PlayerFan_' + view + '.png'), check_existing=True)
            image.name = 'Reference_PlayerFan_' + view
            image.pack()
            reference = bpy.data.objects.new('REF_PlayerFan_' + view, None)
            reference.empty_display_type = 'IMAGE'
            reference.data = image
            reference.empty_display_size = 1024 * self.scale
            reference.color[3] = .45
            reference.empty_image_depth = 'BACK'
            collection.objects.link(reference)
            reference.location = (0, .05, (674 - 512) * self.scale)
            reference.rotation_euler = (math.pi / 2, 0, 0)
            reference.hide_render = True
            reference.hide_set(True)
        collection.hide_render = True

    def validate(self):
        total_vertices, total_triangles = 0, 0
        failures = []
        for obj in self.meshes:
            mesh = obj.data
            mesh.calc_loop_triangles()
            total_vertices += len(mesh.vertices)
            total_triangles += len(mesh.loop_triangles)
            bm = bmesh.new()
            bm.from_mesh(mesh)
            if any(not vertex.link_faces for vertex in bm.verts):
                failures.append(obj.name + ': loose vertices')
            if any(not edge.is_manifold for edge in bm.edges):
                failures.append(obj.name + ': non-manifold edges')
            if any(polygon.area < 1e-12 for polygon in mesh.polygons):
                failures.append(obj.name + ': zero-area faces')
            bm.free()
        assert not failures, failures
        assert bpy.data.images['T_PlayerFan_PaperBaseColor'].packed_file
        self.stats = {'mesh_objects': len(self.meshes), 'base_vertices': total_vertices,
                      'base_triangles': total_triangles, 'paper_facets': 14,
                      'paper_folds': 7, 'reference_images_packed': 4,
                      'artwork_texture_packed': True, 'manifold_meshes': True,
                      'dimensions_m': {'width': .52, 'height': .448},
                      'origin': 'fan pivot', 'front_axis': '-Y', 'unit': 'meter'}
        with open(os.path.join(self.directory, 'PlayerFanModel_Audit.json'), 'w', encoding='utf-8') as target:
            json.dump(self.stats, target, ensure_ascii=False, indent=2)
        print('MODEL_VALIDATION_PASS', json.dumps(self.stats), flush=True)

    def build(self):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.context.preferences.filepaths.save_version = 0
        self.root = bpy.data.objects.new('PlayerFan_Root', None)
        self.root.empty_display_type = 'PLAIN_AXES'
        self.root.empty_display_size = .025
        root_collection = self.collection('PLAYER_FAN')
        root_collection.objects.link(self.root)
        self.root['description'] = '청운 산수 접부채 · Player fan'
        self.root['reference_directory'] = self.references
        self.root['scale_note'] = '실물 기준 약 52cm 폭, 피벗 원점 / Z up / 정면 -Y'
        paper_collection = self.collection('01_PleatedPaper_And_GoldRim')
        ribs_collection = self.collection('02_WoodRibs_And_Pivot')
        pendant_collection = self.collection('03_Jade_Cord_And_Tassel')
        studio_collection = self.collection('90_Studio_Cameras_And_Lights')
        references_collection = self.collection('99_References_Packed_Hidden')
        paper = self.create_paper_material()
        cream = self.material('M_PlayerFan_PaperReverse', (.905, .842, .692), roughness=.80)
        wood = self.material('M_PlayerFan_Walnut', (.140, .066, .026), roughness=.55)
        wood_light = self.material('M_PlayerFan_WalnutFacet', (.168, .083, .036), roughness=.56)
        wood_dark = self.material('M_PlayerFan_WalnutEndgrain', (.092, .039, .013), roughness=.62)
        gold = self.material('M_PlayerFan_AntiqueGold', (.83, .487, .126), metallic=.70, roughness=.35)
        jade = self.material('M_PlayerFan_Jade', (.34, .64, .43), roughness=.43)
        teal = self.material('M_PlayerFan_TealCord', (.020, .28, .30), roughness=.52)
        ivory = self.material('M_PlayerFan_IvoryTassel', (.94, .865, .688), roughness=.76)
        ivory_shadow = self.material('M_PlayerFan_IvoryTasselShade', (.80, .703, .522), roughness=.80)
        self.create_paper(paper_collection, paper, cream, gold)
        self.create_ribs(ribs_collection, wood, wood_light, wood_dark, gold)
        self.create_pendant(pendant_collection, jade, teal, gold, ivory, ivory_shadow)
        self.setup_studio(studio_collection)
        self.setup_references(references_collection)
        self.validate()

        # 파일을 열면 완성 모델이 재질과 함께 보이고, 각 부품을 바로 선택할 수 있다.
        bpy.ops.object.select_all(action='DESELECT')
        self.root.select_set(True)
        bpy.context.view_layer.objects.active = self.root
        orientation = self.cameras['Isometric'].rotation_euler.to_quaternion()
        for screen in bpy.data.screens:
            for area in screen.areas:
                if area.type == 'VIEW_3D':
                    space = area.spaces.active
                    space.shading.type = 'MATERIAL'
                    space.shading.studiolight_rotate_z = .25
                    space.overlay.show_floor = False
                    space.overlay.show_axis_x = False
                    space.overlay.show_axis_y = False
                    space.region_3d.view_distance = .85
                    space.region_3d.view_location = (0, 0, .083)
                    space.region_3d.view_rotation = orientation
                    space.region_3d.view_perspective = 'ORTHO'
        note = bpy.data.texts.new('PlayerFan_README')
        note.write('PLAYER FAN / 청운 산수 접부채\n\n'
                   '원본: E:\\C.J.I\\ReferenceImage\\PlayerFan의 Front / Side / Top / Isometric.\n'
                   '산수화 텍스처와 네 장의 레퍼런스는 이 .blend 안에 모두 포함되어 있습니다.\n'
                   '01: 주름 종이 + 금박 테두리 / 02: 목재 부챗살 + 금속 축 / 03: 끈 + 옥 + 술.\n'
                   '90: 렌더용 카메라/조명 / 99: 숨겨진 레퍼런스 이미지.\n'
                   '정면 -Y, 위 Z, 단위 meter, 원점은 부채 회전축입니다.\n'
                   '7개의 주름을 실제 입체 메시로 구현했습니다. 뒷면은 무지 아이보리입니다.\n'
                   '목재의 Bevel은 수정 가능한 상태로 유지했습니다.\n')
        path = os.path.join(self.directory, 'Cheon_PlayerFan.blend')
        bpy.context.scene.render.filepath = os.path.join(self.directory, 'T_PlayerFan_Preview_Isometric.png')
        bpy.ops.wm.save_as_mainfile(filepath=path, compress=True)
        print('BLEND_SAVED', path, flush=True)
        for view in ('Isometric', 'Front', 'Top', 'Side', 'Back'):
            bpy.context.scene.camera = self.cameras[view]
            bpy.context.scene.render.resolution_x = 1000 if view in ('Isometric', 'Front') else 700
            bpy.context.scene.render.resolution_y = bpy.context.scene.render.resolution_x
            bpy.context.scene.cycles.samples = 40 if view in ('Isometric', 'Front') else 24
            bpy.context.scene.render.filepath = os.path.join(self.directory, 'T_PlayerFan_Preview_' + view + '.png')
            bpy.ops.render.render(write_still=True)
            print('PREVIEW_DONE', view, flush=True)


if __name__ == '__main__':
    model = PlayerFanModel()
    model.build()
