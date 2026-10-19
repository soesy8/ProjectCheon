import bpy
import bmesh
import json
import math
import random
import numpy as np
from pathlib import Path
from mathutils import Vector, Matrix

ROOT = Path(r'D:\project\ProjectCheon\Assets\Test\Cheon\Arts\ReferenceEnvironment')
MODELS = ROOT / 'Models'
TEXTURES = ROOT / 'Textures'
random.seed(20261019)
TAU = math.tau
MATERIALS = {}
CATALOG = []
ACTIVE_ROOT = None
ACTIVE_COLLECTION = None


def color(value):
    return tuple(int(value[i:i + 2], 16) / 255 for i in (0, 2, 4))


def material(key, tint, kind='surface', texture=None, mode=0, opacity=1, emission=0):
    name = 'Test_M_Ref' + key
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color(tint), 1)
    mat.use_nodes = True
    shader = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    shader.inputs['Base Color'].default_value = (*color(tint), 1)
    shader.inputs['Roughness'].default_value = .82
    if texture:
        node = mat.node_tree.nodes.new('ShaderNodeTexImage')
        node.image = bpy.data.images.load(str(TEXTURES / texture))
        mat.node_tree.links.new(node.outputs['Color'], shader.inputs['Base Color'])
    MATERIALS[name] = dict(name=name, color=tint, kind=kind, texture=texture, mode=mode, opacity=opacity, emission=emission)
    return mat


def adopt(obj, name, mat=None):
    obj.name = name
    for collection in list(obj.users_collection):
        collection.objects.unlink(obj)
    ACTIVE_COLLECTION.objects.link(obj)
    obj.parent = ACTIVE_ROOT
    if mat:
        obj.data.materials.clear()
        obj.data.materials.append(mat)
    return obj


def mesh(name, vertices, faces, mat, uv=None):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    ACTIVE_COLLECTION.objects.link(obj)
    obj.parent = ACTIVE_ROOT
    if mat:
        data.materials.append(mat)
    layer = data.uv_layers.new(name='UVMap')
    for polygon in data.polygons:
        normal = polygon.normal
        axis = max(range(3), key=lambda i: abs(normal[i]))
        for loop in polygon.loop_indices:
            index = data.loops[loop].vertex_index
            point = data.vertices[index].co
            layer.data[loop].uv = uv[index] if uv else ((point.y, point.z) if axis == 0 else (point.x, point.z) if axis == 1 else (point.x, point.y))
    return obj


def bevel(obj, width=.035, segments=2):
    modifier = obj.modifiers.new('Soft broad edges', 'BEVEL')
    modifier.width = width
    modifier.segments = segments
    return obj


def box(name, position, size, mat, edge=.025, rotation=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=position)
    obj = adopt(bpy.context.object, name, mat)
    obj.scale = size
    obj.rotation_euler.z = rotation
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return bevel(obj, edge)


def ellipsoid(name, position, size, mat, subdivisions=2, smooth=True):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdivisions, radius=1, location=position)
    obj = adopt(bpy.context.object, name, mat)
    obj.scale = size
    for face in obj.data.polygons:
        face.use_smooth = smooth
    return obj


def cylinder(name, position, radius, depth, mat, vertices=32):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=position)
    return bevel(adopt(bpy.context.object, name, mat), min(.025, depth * .12))


def tube(name, points, radii, mat, sides=10):
    points = [Vector(p) for p in points]
    vertices = []
    for i, point in enumerate(points):
        tangent = points[min(i + 1, len(points) - 1)] - points[max(0, i - 1)]
        tangent.normalize()
        reference = Vector((0, 1, 0)) if abs(tangent.y) < .9 else Vector((1, 0, 0))
        right = tangent.cross(reference).normalized()
        up = tangent.cross(right).normalized()
        radius = radii[i] if isinstance(radii, (list, tuple)) else radii
        for j in range(sides):
            angle = j * TAU / sides
            vertices.append(tuple(point + right * math.cos(angle) * radius + up * math.sin(angle) * radius))
    faces = [tuple(reversed(range(sides)))]
    for i in range(len(points) - 1):
        for j in range(sides):
            a = i * sides + j
            b = i * sides + (j + 1) % sides
            faces.append((a, b, b + sides, a + sides))
    faces.append(tuple((len(points) - 1) * sides + j for j in range(sides)))
    return mesh(name, vertices, faces, mat)


def torus(name, position, major, minor, mat, rotation=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_segments=48, minor_segments=8, location=position, rotation=rotation, major_radius=major, minor_radius=minor)
    return adopt(bpy.context.object, name, mat)


def stone(name, position, size, seed=0, mat=None):
    rng = random.Random(seed)
    count = 11
    angular = [rng.uniform(.88, 1.12) for _ in range(count)]
    vertices = []
    for level, (radius, height) in enumerate(((.86, 0), (1, .16), (.94, .65), (.7, 1))):
        for j in range(count):
            angle = j * TAU / count + .09 * level
            width = angular[j] * radius
            shoulder = .07*math.sin(angle*2+seed) if level else 0
            vertices.append((position[0] + math.cos(angle) * size[0] * .5 * width + level/3*size[0]*.08*math.sin(seed),
                             position[1] + math.sin(angle) * size[1] * .5 * width + level/3*size[1]*.07*math.cos(seed),
                             position[2] + size[2] * (height + shoulder + (rng.uniform(-.025, .025) if level else 0))))
    faces = [tuple(reversed(range(count)))]
    for level in range(3):
        for j in range(count):
            a = level * count + j
            b = level * count + (j + 1) % count
            faces.append((a, b, b + count, a + count))
    faces.append(tuple(3 * count + j for j in range(count)))
    return bevel(mesh(name, vertices, faces, mat or STONE), min(size) * .022)


def cloth(name, top, width, length, mat, phase=0, depth=.12):
    vertices, uv, faces = [], [], []
    rows, columns = 12, 4
    for row in range(rows + 1):
        t = row / rows
        for column in range(columns + 1):
            u = column / columns - .5
            twist = math.sin(t * 5 + phase) * .45
            vertices.append((top[0] + u * width * (1 + .12 * t) + math.sin(t * 6 + phase) * .06,
                             top[1] + math.sin(t * TAU + phase) * depth + u * width * twist,
                             top[2] - t * length + math.sin(u * 3 + t * 4) * .025))
            uv.append((column / columns, 1 - t))
    for row in range(rows):
        for column in range(columns):
            a = row * (columns + 1) + column
            faces.append((a, a + columns + 1, a + columns + 2, a + 1))
    obj = mesh(name, vertices, faces, mat, uv)
    modifier = obj.modifiers.new('Thin cloth thickness', 'SOLIDIFY')
    modifier.thickness = .008
    return obj


def plane(name, width, depth, position, mat):
    x, y, z = position
    return mesh(name, [(x-width/2,y-depth/2,z),(x+width/2,y-depth/2,z),(x+width/2,y+depth/2,z),(x-width/2,y+depth/2,z)], [(0,1,2,3)], mat, [(0,0),(1,0),(1,1),(0,1)])


def disk(name, radius, z, mat, depth=.1):
    return cylinder(name, (0, 0, z-depth/2), radius, depth, mat, 64)


def rope(name, points, radius=.055):
    # 두 가닥의 완만한 꼬임만 표현해 축소 화면의 실루엣을 유지한다.
    points = [Vector(p) for p in points]
    for strand in range(2):
        strand_points = []
        for i, point in enumerate(points):
            angle = i * .95 + strand * math.pi
            strand_points.append(tuple(point + Vector((math.cos(angle)*radius*.42, 0, math.sin(angle)*radius*.42))))
        tube(name + '_Twist' + str(strand), strand_points, radius*.72, ROPE, 6)


def ribbon_line(start, end, count=9):
    a, b = Vector(start), Vector(end)
    points = [tuple(a.lerp(b, t/48) + Vector((0,0,-.55*math.sin(t/48*math.pi)))) for t in range(49)]
    rope('MainSwayingRope', points)
    colors = [IVORY, IVORY, CLOTH_JADE, IVORY, CLOTH_RED, IVORY, CLOTH_BLUE, CLOTH_OCHRE]
    for index in range(count):
        t = (index+.5)/count
        point = a.lerp(b,t) + Vector((0,0,-.55*math.sin(t*math.pi)))
        torus('ClothKnot', tuple(point), .075, .032, colors[index % len(colors)], (math.pi/2,0,0))
        cloth('WishSilk_' + str(index), tuple(point), .22 + .06*(index%3), .85 + .2*(index%4), colors[index%len(colors)], index*.7)


def pine(variant):
    bend = -1 if variant == 1 else 1
    trunk = [(0,0,0),(.18*bend,.04,.7),(.55*bend,.02,1.5),(.25*bend,.06,2.3),(-.12*bend,.13,3.2),(.24*bend,.16,4.1),(.1*bend,.12,4.9)]
    if variant == 2:
        trunk = [(x*.8,y+.15*math.sin(z),z*.9) for x,y,z in trunk]
    tube('CurvedPineTrunk', trunk, [.47,.41,.35,.3,.24,.17,.07], BARK, 12)
    for angle in (0,.9,2.1,3.3,4.5,5.5):
        end = (math.cos(angle)*1.1,math.sin(angle)*.8,.03)
        tube('GroundedPineRoot', [(0,0,.48),(end[0]*.55,end[1]*.55,.16),end], [.23,.18,.035], BARK, 8)
    pads = [(-1.5,-.1,1.95,1.18),(1.65,.2,2.6,1.3),(-1.55,.26,3.3,1.2),(.95,-.08,4.12,1.1),(-.4,.12,4.95,.95),(-.9,-.55,2.5,.88),(1,.8,3.5,.88)]
    for index, (x,y,z,radius) in enumerate(pads):
        if variant == 2:
            x*=.87; y+=.25*math.sin(index*2); z*=.9
        x *= bend
        attach = Vector(trunk[min(len(trunk)-2, max(1,int(z)))] )
        end = Vector((x,y,z-.12))
        tube('SupportingPineBranch', [tuple(attach),tuple(attach.lerp(end,.55)+Vector((0,0,-.12))),tuple(end)], [.18,.13,.055], BARK, 8)
        for lobe in range(5):
            angle = lobe*TAU/5
            size = (radius*(.53 if lobe else .7),radius*(.42 if lobe else .55),radius*.3)
            location = (x+math.cos(angle)*radius*.47,y+math.sin(angle)*radius*.34,z+(.12 if lobe%2 else 0))
            obj = ellipsoid('VolumePineCanopy', location, size, FOLIAGE, 2, True)
            for vertex in obj.data.vertices:
                vertex.co *= 1 + .05*math.sin(vertex.co.x*13+index)


def paper_talisman(position=(0,0,1.2), scale=1, phase=0):
    return cloth('CurvedHanjiTalisman', position, .34*scale, .95*scale, TALISMAN, phase, .07*scale)


def sealstone():
    stone('SealStoneBody',(0,0,0),(2.3,1.8,1.5),18)
    points = [(math.cos(i*TAU/72)*1.13,math.sin(i*TAU/72)*.88,1.02) for i in range(73)]
    rope('SacredStoneRope', points, .065)
    paper_talisman((0,-.91,1.02),.68)
    for x in (-.7,.7):
        cloth('PlainPaperCharm',(x,-.81,1.02),.2,.46,IVORY,depth=.025)
    for x in (-.47,.48):
        for j in range(4):
            tube('ShortRopeTassel',[(x+j*.015,-.87,1.01),(x+j*.022,-.9,.63)],[.025,.012],ROPE,5)


def constellation():
    disk('ConstellationGardenGround',3.9,.02,PALE_STONE,.08)
    stone('CentralConstellationStone',(0,0,.02),(1.5,1.4,1.25),21)
    for i in range(6):
        angle = i*TAU/6
        stone('OuterStarStone',(math.cos(angle)*2.7,math.sin(angle)*2.7,.02),(1, .9, .92),31+i)
    plane('ConstellationInlay',8,8,(0,0,.028),CONSTELLATION)


def circle(kind):
    radius = 2.4
    disk('SharedRitualStoneFloor',radius,.03,PALE_STONE,.11)
    for i in range(24):
        angle = i*TAU/24
        box('LowRimStone',(math.cos(angle)*radius,math.sin(angle)*radius,.035),(.56,.32,.14),STONE,.035,angle+math.pi/2)
    plane('FlatRitualInlay',radius*2,radius*2,(0,0,.04),SEAL_PATTERN if kind=='Seal' else HEAL_PATTERN)


def jade_rings():
    for i in range(12):
        a=i*TAU/12
        stone('LowJadeRingFoundation',(math.cos(a)*1.8,math.sin(a)*1.8,0),(.95,.8,.55),60+i)
    for index,(radius,rot) in enumerate(((1.65,(math.pi/2,0,0)),(1.42,(.12,.2,0)),(1.1,(math.pi/2,0,math.pi/2)))):
        torus('IndependentJadeRing_'+str(index),(0,0,2.55),radius,.14,JADE,rot)
    ellipsoid('JadeCenterOrb',(0,0,2.55),(.46,.46,.46),PEARL,3)


def bird(position, scale=1):
    x,y,z = position
    ellipsoid('WoodenDuckBody',(x,y,z+.24*scale),(.43*scale,.2*scale,.26*scale),BARK,2,False)
    tube('WoodenDuckNeck',[(x-.24*scale,y,z+.25*scale),(x-.39*scale,y,z+.59*scale),(x-.43*scale,y,z+.69*scale)],[.16*scale,.12*scale,.15*scale],BARK,9)
    ellipsoid('WoodenDuckHead',(x-.43*scale,y,z+.7*scale),(.18*scale,.14*scale,.17*scale),BARK,2,False)
    box('WideWoodenBill',(x-.66*scale,y,z+.65*scale),(.38*scale,.18*scale,.075*scale),BARK,.025*scale)
    tube('UpliftedDuckTail',[(x+.25*scale,y,z+.28*scale),(x+.56*scale,y,z+.44*scale)],[.15*scale,.025*scale],BARK,7)
    for side in (-1,1):
        ellipsoid('CarvedWoodenWing',(x+.05*scale,y+side*.17*scale,z+.29*scale),(.28*scale,.055*scale,.12*scale),WOOD_LIGHT,2,False)
        ellipsoid('SmallCarvedEye',(x-.47*scale,y+side*.134*scale,z+.74*scale),(.023*scale,.018*scale,.023*scale),DARK,1)


def sotdae(group):
    entries=[(0,0,3.8,1)] if not group else [(-.85,0,3.3,.83),(.2,.3,4.2,1),(.95,-.2,2.5,.72)]
    for index,(x,y,h,s) in enumerate(entries):
        tube('TaperedSotdaePole',[(x,y,0),(x+.08,y+.02,h*.4),(x-.08,y,h*.72),(x,y,h)],[.16,.14,.12,.09],BARK,9)
        bird((x,y,h),s)
        for dz in (0,.09): torus('PoleRopeKnot',(x,y,h-.08-dz),.12,.033,ROPE)
        for i in range(3): stone('GroundingSotdaeRock',(x+math.cos(i*2.1)*.28,y+math.sin(i*2.1)*.23,0),(.6,.5,.36),90+index*3+i)


def wish_ribbons():
    for x in (-2.6,2.6):
        h=3.5 if x<0 else 2.95
        tube('WishRopeWoodPost',[(x,0,0),(x+.08,0,h*.55),(x,0,h)],[.23,.19,.15],BARK,10)
        for i in range(3): stone('WishPostFootStone',(x+math.cos(i*2.1)*.35,math.sin(i*2.1)*.25,0),(.8,.7,.55),101+i)
    ribbon_line((-2.6,0,3.2),(2.6,0,2.7),11)


def offering(kind, position=(0,0,0)):
    x,y,z=position
    if kind=='RiceBowl' or kind=='WaterCup':
        radius=.22 if kind=='RiceBowl' else .115
        # 안쪽 벽과 바닥이 있는 얕은 그릇이다.
        vertices=[]
        profiles=[(.55,0),(1,.2),(1,.25),(.84,.22),(.49,.055)]
        for factor,h in profiles:
            for i in range(24): vertices.append((x+math.cos(i*TAU/24)*radius*factor,y+math.sin(i*TAU/24)*radius*factor,z+h))
        faces=[]
        for r in range(4):
            for i in range(24): a=r*24+i;b=r*24+(i+1)%24;faces.append((a,b,b+24,a+24))
        faces.append(tuple(reversed(range(24))));faces.append(tuple(4*24+i for i in range(24)))
        mesh('Ceramic'+kind,vertices,faces,CERAMIC)
        if kind=='RiceBowl': ellipsoid('SimpleRiceMound',(x,y,z+.23),(.19,.19,.075),IVORY,2)
        else: cylinder('CupWaterSurface',(x,y,z+.18),radius*.81,.008,WATER,24)
    else:
        box('OfferingWoodenTray',(x,y,z+.05),(.8,.48,.07),WOOD_LIGHT,.035)
        for sx,sy in ((-.19,-.08),(.16,-.06),(0,.1)):
            ellipsoid('OfferingPersimmon',(x+sx,y+sy,z+.2),(.15,.145,.14),FRUIT,2)
            tube('FruitStem',[(x+sx,y+sy,z+.32),(x+sx,y+sy,z+.35)],.018,BARK,5)


def altar():
    for i,(p,s) in enumerate((((0,.75,0),(2.8,1.5,1.5)),((-1.2,.1,0),(1.6,1.5,1.1)),((1.15,.15,0),(1.4,1.3,.95)),((0,-.8,0),(3,1.2,.5)))):
        stone('OpenAltarStone',p,s,130+i)
    stone('BroadFrontOfferingSlab',(0,-1.1,.48),(2.8,1.35,.28),139)
    tube('AltarForkedTree',[(-1.5,.8,0),(-1.4,.75,1.2),(-1.15,.7,2.6),(-1.4,.65,3.4)],[.23,.2,.14,.07],BARK)
    tube('AltarTreeFork',[(-1.15,.7,2.6),(-.8,.8,3.2),(-.6,.8,3.5)],[.14,.09,.035],BARK)
    tube('AltarShortRopePost',[(1.5,.7,0),(1.4,.7,2.3)],[.16,.09],BARK)
    ribbon_line((-1.3,.65,2.7),(1.4,.65,2.15),4)
    offering('RiceBowl',(-.55,-1.1,.79));offering('WaterCup',(0,-1.2,.79));offering('FruitTray',(.65,-1.1,.79))


def lily(position, flower=False):
    x,y,z=position
    verts=[(x,y,z)] + [(x+math.cos(.13+i*(TAU-.26)/22)*.28,y+math.sin(.13+i*(TAU-.26)/22)*.28,z+.01*math.sin(i)) for i in range(23)]
    mesh('NotchedLilyPad',verts,[(0,i,i+1) for i in range(1,23)],LILY)
    if flower:
        for i in range(7):
            a=i*TAU/7
            obj=ellipsoid('WaterLilyPetal',(x+math.cos(a)*.11,y+math.sin(a)*.11,z+.065),(.15,.07,.035),IVORY,2)
            obj.rotation_euler.z=a
        ellipsoid('LilyFlowerCenter',(x,y,z+.09),(.055,.055,.025),OCHRE,2)


def pond():
    floor=disk('NaturalPondBasin',1,-.03,DARK_STONE,.2);floor.scale=(3.4,2.6,1)
    water=disk('ThinPondWaterSurface',1,.075,WATER,.014);water.scale=(3.25,2.45,1)
    for i in range(19):
        a=i*TAU/19
        stone('PondShoreBoulder',(math.cos(a)*3.35,math.sin(a)*2.55,-.05),(1.1,.95,.55+(i%3)*.2),160+i)
    for i in range(6):
        a=i*2.4
        lily((math.cos(a)*2.1,math.sin(a)*1.65,.093),i%2==0)


def waterfall():
    # 얇게 휜 낙수 면 두 개와 하단 수면을 분리해 흐름 셰이더를 적용한다.
    for tier in range(2):
        origin_y=-tier*6.5
        origin_z=-tier*3.3
        profile=[(3.8,.1),(0,.1),(-2.4,.08),(-2.6,-.2),(-3.4,-.8),(-4.1,-1.8),(-4.9,-2.9),(-5.4,-3.25),(-6.5,-3.27)] if tier==0 else [(0,.1),(-.7,.08),(-1.1,-.2),(-1.3,-.8),(-1.4,-1.8),(-1.55,-2.9),(-2,-3.25),(-3.9,-3.27)]
        vertices=[];uv=[]
        for j,(y,z) in enumerate(profile):
            for side in (-1,1):
                vertices.append((side*(1.05+.12*math.sin(j)),origin_y+y,origin_z+z));uv.append(((side+1)/2,j/(len(profile)-1)))
        mesh('CurvedWaterfallSheet',vertices,[(i*2,i*2+1,i*2+3,i*2+2) for i in range(len(profile)-1)],WATERFALL,uv)
        for side in (-1,1):
            stone('WaterfallBankRock',(side*1.45,origin_y-(3.8 if tier==0 else 1.3),origin_z-3.4),(1.5,2.3,3.7),180+tier*2+side)
        for i in range(6):
            ellipsoid('LimitedFoamCrest',((i-2.5)*.34,origin_y-(5.5 if tier==0 else 2.03),origin_z-3.12),(.3,.4,.09),FOAM,2)


def path_tile(kind):
    for x in (-.49,.49):
        for y in (-.49,.49): box('BroadPathSlab',(x,y,.055),(.955,.955,.11),PALE_STONE,.045)
    if kind in ('Border','Straight'):
        for side in ((-1,1) if kind=='Straight' else (1,)):
            for y in (-.5,.5): box('LowPathEdging',(side*1.02,y,.12),(.24,.97,.24),STONE,.04)


def stairs():
    for i in range(4):
        top=(i+1)*.19
        box('WideStoneStep',(0,.43*i,top/2),(2.6,.46,top),PALE_STONE,.035)
        for side in (-1,1):box('StairSideStone',(side*1.38,.43*i,top+.035),(.24,.48,.27),STONE,.035)


def battle_floor():
    radius=6
    disk('QuietBattleFloorCenter',radius,.07,PALE_STONE,.16)
    for ring in range(3):
        r=radius-.2-ring*.55
        # 얇은 평면 문양은 별도 셰이더 레이어로 둔다.
        for i in range(32):
            a=(i+.5)*TAU/32
            if ring==0:box('BattleFloorRimSegment',(math.cos(a)*r,math.sin(a)*r,.045),(.99,.42,.14),STONE,.035,a+math.pi/2)
    plane('SubtleBattleFloorInlay',radius*2,radius*2,(0,0,.078),BATTLE_PATTERN)


def cairn(variant):
    profiles=[[(1.5,1.3,.65),(1,.85,.55)],[(1.6,1.4,.55),(1.35,1.15,.44),(1.05,.95,.37),(.75,.7,.32),(.43,.4,.3)],[(1.4,1.1,.5),(1.1,.85,.65),(1.25,.8,.4),(.67,.6,.46)]]
    z=0
    for i,(sx,sy,sz) in enumerate(profiles[variant]):
        stone('BalancedCairnStone',(.08*math.sin(i*1.9),.04*math.cos(i),z),(sx,sy,sz),210+variant*9+i)
        z+=sz*.94


def mountain(variant):
    vertices=[];faces=[]
    nx,ny=32,16
    width=28 if variant<2 else 40
    depth=18 if variant<2 else 22
    for j in range(ny+1):
        y=(j/ny-.5)*depth
        for i in range(nx+1):
            x=(i/nx-.5)*width
            edge=math.sin(math.pi*i/nx)**.6*math.sin(math.pi*j/ny)**.7
            ridge=(8+13*math.exp(-((x+5)/4)**2)+21*math.exp(-((x-3)/3.8)**2)+9*math.exp(-((x-10)/3)**2))
            height=edge*ridge*(.88+.12*math.cos(y*.4+x*.3+variant))
            vertices.append((x,y,height))
    for j in range(ny):
        for i in range(nx):
            a=j*(nx+1)+i
            faces.append((a,a+1,a+nx+2,a+nx+1))
    edge_indices=list(range(nx+1))+[j*(nx+1)+nx for j in range(1,ny+1)]+list(reversed([ny*(nx+1)+i for i in range(nx)]))+[j*(nx+1) for j in reversed(range(1,ny))]
    faces.append(tuple(reversed(edge_indices)))
    mesh('ConnectedMountainRidge',vertices,faces,[MOUNTAIN_NEAR,MOUNTAIN_MID,MOUNTAIN_FAR][variant])


def mountain_foundation():
    outline=[(-20,-49.5),(20,-49.5),(24,-43),(24,-13.1),(31.2,-13.1),(31.2,48),(26,54.5),(-26,54.5),(-31.2,48),(-31.2,-13.1),(-24,-13.1),(-24,-43)]
    polygon=[Vector((x,-y)) for x,y in outline]
    vertices=[];faces=[];nx=52;ny=78
    def distance_to_map(x,y):
        point=Vector((x,y));inside=False;distance=1e6
        for index,a in enumerate(polygon):
            b=polygon[(index+1)%len(polygon)];delta=b-a
            t=max(0,min(1,(point-a).dot(delta)/delta.length_squared))
            distance=min(distance,(point-a-delta*t).length)
            if (a.y>y)!=(b.y>y) and x<(b.x-a.x)*(y-a.y)/(b.y-a.y)+a.x:inside=not inside
        return 0 if inside else distance
    for j in range(ny+1):
        y=-160+j/ny*315
        for i in range(nx+1):
            x=-105+i/nx*210;distance=distance_to_map(x,y)
            ridges=1+.12*math.sin(math.atan2(y,x)*13)*min(1,distance/10)
            height=-.25-distance*.9*ridges
            vertices.append((x,y,height))
    for j in range(ny):
        for i in range(nx):
            a=j*(nx+1)+i;faces.append((a,a+1,a+nx+2,a+nx+1))
    edge=list(range(nx+1))+[j*(nx+1)+nx for j in range(1,ny+1)]+list(reversed([ny*(nx+1)+i for i in range(nx)]))+[j*(nx+1) for j in reversed(range(1,ny))]
    bottom=[]
    for index in edge:
        x,y,_=vertices[index];bottom.append(len(vertices));vertices.append((x,y,-125))
    for index,a in enumerate(edge):
        n=(index+1)%len(edge);faces.append((a,bottom[index],bottom[n],edge[n]))
    faces.append(tuple(reversed(bottom)))
    mesh('BroadGroundedMountainBase',vertices,faces,STONE)


def ruined_pavilion():
    path=r'E:\C.J.I\Blender\06_Bell_Pavilion\Cheon_BellPavilion.blend'
    with bpy.data.libraries.load(path,link=False) as (source,destination):
        destination.objects=[name for name in source.objects if name.startswith('Pavilion_')]
    def original_world(obj):
        return original_world(obj.parent) @ obj.matrix_parent_inverse @ obj.matrix_basis if obj.parent else obj.matrix_basis.copy()
    matrices={obj:original_world(obj) for obj in destination.objects if obj.type=='MESH'}
    for obj in destination.objects:
        if obj.type!='MESH':continue
        ACTIVE_COLLECTION.objects.link(obj)
        obj.parent=ACTIVE_ROOT;obj.matrix_world=matrices[obj]
        # 한 기둥과 난간의 큰 부분만 끊어 폐허 실루엣을 만든다.
        if 'Pillar_Post' in obj.name and obj.location.x>0 and obj.location.y<0:
            obj.scale.z*=.38;obj.location.z-=.7
        if 'SideRail' in obj.name and obj.location.x>0:
            obj.scale.y*=.55
        for index,old in enumerate(obj.data.materials):
            key=old.name.lower()
            obj.data.materials[index]=STONE if 'stone' in key else OCHRE if 'gold' in key else JADE if 'dancheong' in key else BARK
    for side in (-1,1):
        for column in range(19):
            x=(column-9)*.235
            points=[]
            for row in range(9):
                t=row/8
                y=side*(.08+1.9*t)
                z=4.15-1.2*t+.34*t**4
                points.append((x,y,z))
            damaged=side==-1 and column>=12
            # 손실 구역에는 지붕판을 채우지 않고 서까래만 드러낸다.
            if column%2==0:tube('ExposedRoofRafter',[(x,side*.1,4.03),(x,side*1.9,3.12)],.055,BARK,6)
            if damaged:points=points[:3]
            tube('BroadIndigoRoofTile',points,.107,ROOF,8)
        tube('CurvedRoofEave',[(-2.2,side*2,3.35),(0,side*2,3.24),(2.2,side*2,3.35)],.11,ROOF,8)
    tube('RoofRidge',[(-2.25,0,4.29),(0,0,4.16),(2.25,0,4.29)],.14,ROOF,10)
    for i in range(6):box('LimitedFallenTile',(.6+i*.18,-.5+(i%3)*.3,.4),(.28,.23,.08),ROOF,.025,i*.7)


def export_asset(key,label,reference,build):
    global ACTIVE_ROOT,ACTIVE_COLLECTION
    ACTIVE_COLLECTION=bpy.data.collections.new('Test_'+key)
    bpy.context.scene.collection.children.link(ACTIVE_COLLECTION)
    ACTIVE_ROOT=bpy.data.objects.new('Test_CheonRef_'+key,None)
    ACTIVE_COLLECTION.objects.link(ACTIVE_ROOT)
    build()
    bpy.context.view_layer.update()
    objects=[obj for obj in ACTIVE_COLLECTION.objects if obj.type=='MESH']
    depsgraph=bpy.context.evaluated_depsgraph_get()
    bounds=[]
    vertices=triangles=0
    for obj in objects:
        if obj.modifiers:
            data=bpy.data.meshes.new_from_object(obj.evaluated_get(depsgraph),depsgraph=depsgraph)
            obj.modifiers.clear();obj.data=data
        data=obj.data
        topology=bmesh.new();topology.from_mesh(data)
        if all(len(edge.link_faces)==2 for edge in topology.edges):
            bmesh.ops.recalc_face_normals(topology,faces=list(topology.faces));topology.to_mesh(data)
        topology.free()
        data.validate();data.update();data.calc_loop_triangles()
        vertices+=len(data.vertices);triangles+=len(data.loop_triangles)
        if not data.uv_layers: data.uv_layers.new(name='UVMap')
        tint=data.color_attributes.get('Color') or data.color_attributes.new(name='Color',type='BYTE_COLOR',domain='CORNER')
        for polygon in data.polygons:
            value=.94+random.random()*.09
            for loop in polygon.loop_indices:tint.data[loop].color=(value,value,value,1)
        for corner in obj.bound_box:bounds.append(obj.matrix_world@Vector(corner))
    bpy.ops.object.select_all(action='DESELECT')
    for obj in list(ACTIVE_COLLECTION.objects):obj.select_set(True)
    bpy.context.view_layer.objects.active=ACTIVE_ROOT
    target=MODELS/('Test_CheonRef_'+key+'.fbx')
    bpy.ops.export_scene.fbx(filepath=str(target),use_selection=True,object_types={'MESH','EMPTY'},apply_unit_scale=True,axis_forward='-Z',axis_up='Y',bake_anim=False,add_leaf_bones=False,mesh_smooth_type='FACE',use_mesh_modifiers=True,path_mode='AUTO')
    minimum=[min(v[i] for v in bounds) for i in range(3)]
    maximum=[max(v[i] for v in bounds) for i in range(3)]
    CATALOG.append(dict(key=key,label=label,reference=reference,path=str(target.relative_to(ROOT)).replace('\\','/'),vertices=vertices,triangles=triangles,parts=len(objects),dimensions=[maximum[i]-minimum[i] for i in range(3)],materials=sorted({mat.name for obj in objects for mat in obj.data.materials if mat})))
    print('EXPORTED',key,triangles,flush=True)


def save_texture(name,pixels):
    height,width=pixels.shape[:2]
    image=bpy.data.images.new(name,width=width,height=height,alpha=True)
    image.pixels.foreach_set(pixels.astype(np.float32).ravel())
    image.filepath_raw=str(TEXTURES/name);image.file_format='PNG';image.save()


def pattern_textures():
    size=512
    y,x=np.mgrid[0:size,0:size].astype(float)/(size-1)
    px=(x-.5)*2;py=(y-.5)*2;r=np.sqrt(px*px+py*py)
    ring=lambda radius,width: np.clip((width-np.abs(r-radius))/max(width*.35,.0001),0,1)
    def output(name,mask,tint):
        pixels=np.zeros((size,size,4));pixels[:,:,:3]=color(tint);pixels[:,:,3]=mask;save_texture(name,pixels)
    mask=ring(.77,.012)+ring(.73,.006)
    for i in range(3):
        a=i*TAU/3+math.pi/2
        distance=np.sqrt((px-math.cos(a)*.75)**2+(py-math.sin(a)*.75)**2)
        mask+=np.clip((.016-np.abs(distance-.105))/.008,0,1)
    output('Test_T_SealingPattern.png',np.clip(mask,0,1),'B69B64')
    leaves=np.zeros_like(r)
    for i in range(3):
        a=i*TAU/3+math.pi/2
        u=px*math.cos(a)+py*math.sin(a);v=-px*math.sin(a)+py*math.cos(a)
        shape=((u-.52)/.3)**2+(v/.13)**2
        leaves=np.maximum(leaves,np.clip((1-shape)*8,0,1)*(u>.24))
    output('Test_T_HealingPattern.png',np.clip(leaves*.7+ring(.76,.01),0,1),'79A38F')
    constellation=ring(.92,.006)+ring(.85,.004)+ring(.6,.004)
    for i in range(6):
        a=i*TAU/6
        u=px*math.cos(a)+py*math.sin(a);v=-px*math.sin(a)+py*math.cos(a)
        constellation+=((abs(v)<.004)&(u>.05)&(u<.72))*.55
        constellation+=np.exp(-((u-.55)**2+v*v)/.0009)
    output('Test_T_ConstellationPattern.png',np.clip(constellation,0,1)*.65,'70BFC0')
    battle=ring(.8,.008)+ring(.48,.007)
    for i in range(4):
        a=i*TAU/4
        distance=np.sqrt((px-math.cos(a)*.68)**2+(py-math.sin(a)*.68)**2)
        battle+=np.clip((.01-abs(distance-.075))/.006,0,1)
    output('Test_T_BattleFloorPattern.png',np.clip(battle,0,1)*.55,'B69D6F')
    width,height=256,768
    y,x=np.mgrid[0:height,0:width].astype(float);x/=width-1;y/=height-1
    pixels=np.ones((height,width,4));grain=.025*np.sin(x*21+y*13)*np.sin(y*39-x*7)
    pixels[:,:,:3]=np.array(color('F1DEAE'))+grain[:,:,None]
    sun=np.abs(np.sqrt((x-.5)**2+((y-.77)*.5)**2)-.12)<.018
    mark=np.zeros((height,width),dtype=bool)
    for phase in (-1,0,1):
        path=.5+phase*.16+np.sin(y*8+phase)*.08
        mark|=(np.abs(x-path)<.016+.009*np.sin(y*5)**2)&(y>.15)&(y<.68)
    mark|=sun
    pixels[mark,:3]=color('B94E27');save_texture('Test_T_HanjiTalisman.png',np.clip(pixels,0,1))


class Test_BuildReferenceModels:
    @staticmethod
    def main():
        global STONE,PALE_STONE,DARK_STONE,BARK,WOOD_LIGHT,FOLIAGE,ROPE,IVORY,CLOTH_JADE,CLOTH_RED,CLOTH_BLUE,CLOTH_OCHRE,TALISMAN,JADE,PEARL,DARK,CERAMIC,WATER,WATERFALL,FOAM,LILY,OCHRE,FRUIT,ROOF,SEAL_PATTERN,HEAL_PATTERN,CONSTELLATION,BATTLE_PATTERN,MOUNTAIN_NEAR,MOUNTAIN_MID,MOUNTAIN_FAR,CLOUD,FLAME
        MODELS.mkdir(parents=True,exist_ok=True);TEXTURES.mkdir(parents=True,exist_ok=True)
        bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
        pattern_textures()
        STONE=material('Granite','C6BBA0');PALE_STONE=material('Paving','D9CCAC');DARK_STONE=material('Basin','778F82')
        BARK=material('PineWood','A05228');WOOD_LIGHT=material('CarvedWood','BB6B36');FOLIAGE=material('PineNeedles','315E50')
        ROPE=material('StrawRope','B3945D');IVORY=material('IvoryCloth','E9DEBE');CLOTH_JADE=material('JadeCloth','6B9C92')
        CLOTH_RED=material('RedCloth','AF6651');CLOTH_BLUE=material('BlueCloth','516A7C');CLOTH_OCHRE=material('OchreCloth','C6A45D')
        TALISMAN=material('HanjiTalisman','FFFFFF',texture='Test_T_HanjiTalisman.png')
        JADE=material('Jade','8DB5A2');PEARL=material('Pearl','D6E5BE',emission=.15);DARK=material('DarkWood','392F24')
        CERAMIC=material('Ceramic','C7D2C4');WATER=material('Water','459F9D',mode=1);WATERFALL=material('Waterfall','4EADBA',mode=2)
        FOAM=material('Foam','D8EDE2');LILY=material('Lily','7E9350');OCHRE=material('Ochre','B7974E');FRUIT=material('Persimmon','BB6336');ROOF=material('RoofTile','354653')
        SEAL_PATTERN=material('SealPattern','FFFFFF','effect','Test_T_SealingPattern.png')
        HEAL_PATTERN=material('HealPattern','FFFFFF','effect','Test_T_HealingPattern.png')
        CONSTELLATION=material('Constellation','FFFFFF','effect','Test_T_ConstellationPattern.png')
        BATTLE_PATTERN=material('BattlePattern','FFFFFF','effect','Test_T_BattleFloorPattern.png')
        MOUNTAIN_NEAR=material('MountainNear','869E8D');MOUNTAIN_MID=material('MountainMid','7F9FAD');MOUNTAIN_FAR=material('MountainFar','AAC0CD')
        CLOUD=material('CloudMist','E8E7D5','effect',mode=2,opacity=.65);FLAME=material('SpiritFlame','69DDDC','effect',mode=1,opacity=.85)
        for variant in range(3):export_asset('Pine'+chr(65+variant),'소나무 '+chr(65+variant),'03_Pine',lambda v=variant:pine(v))
        for variant in range(3):export_asset('Rock'+chr(65+variant),'자연 바위 '+chr(65+variant),'04_Rock',lambda v=variant:stone('BroadGraniteRock',(0,0,0),[(2.7,2.1,2.25),(2.3,1.8,1.6),(1.6,1.5,2.1)][v],10+v))
        export_asset('FlatRock','납작한 바위','04_Rock',lambda:stone('FlatGraniteStone',(0,0,0),(1.5,1.2,.42),17))
        export_asset('Sealstone','봉인석','12_Sealstone_Constellation',sealstone)
        export_asset('ConstellationGarden','별자리 정원','12_Sealstone_Constellation',constellation)
        export_asset('Talisman','전투 부적','13_Talismans_Seal_Heal',paper_talisman)
        def bundle():
            for i in range(4):paper_talisman((i*.025,i*.024,1.1+i*.012),1,i*.2)
            torus('TalismanBundleKnot',(0,-.13,.67),.11,.025,CLOTH_RED,(math.pi/2,0,0));ellipsoid('KnotJadeBead',(0,-.15,.67),(.055,.04,.055),JADE,2)
        export_asset('TalismanBundle','부적 묶음','13_Talismans_Seal_Heal',bundle)
        export_asset('SealingCircle','봉인진','13_Talismans_Seal_Heal',lambda:circle('Seal'))
        export_asset('HealingCircle','치유진','13_Talismans_Seal_Heal',lambda:circle('Heal'))
        export_asset('JadeRings','공중 옥고리와 중심 구슬','14_Jade_Rings',jade_rings)
        export_asset('Sotdae','솟대','15_Sotdae',lambda:sotdae(False));export_asset('SotdaeGroup','솟대와 나무새 군집','15_Sotdae',lambda:sotdae(True))
        export_asset('WishRibbon','소원천 단품','16_Wish_Ribbons',lambda:cloth('WishSilk',(0,0,1.2),.35,1.15,IVORY,.5))
        export_asset('WishRibbons','소원천과 줄 장식','16_Wish_Ribbons',wish_ribbons)
        export_asset('OutdoorAltar','야외 서낭제단과 공물','17_Outdoor_Seonang_Altar',altar)
        for key,label in [('RiceBowl','밥그릇'),('WaterCup','물잔'),('FruitTray','과일과 목재 쟁반')]:export_asset(key,label,'17_Outdoor_Seonang_Altar',lambda k=key:offering(k))
        export_asset('Pond','연못과 물가','18_Pond_Waterfall',pond);export_asset('WaterfallStream','폭포와 계류','18_Pond_Waterfall',waterfall)
        for kind in ('Plain','Border','Straight'):export_asset('Path'+kind,'돌길 모듈 '+kind,'19_Path_Stairs_Battlefloor',lambda k=kind:path_tile(k))
        export_asset('BoundaryStone','연속 경계석','19_Path_Stairs_Battlefloor',lambda:box('PathBoundaryStone',(0,0,.14),(2,.3,.28),STONE,.045))
        export_asset('StoneStairs','돌계단','19_Path_Stairs_Battlefloor',stairs);export_asset('BattleFloor','전투장 원형 바닥','19_Path_Stairs_Battlefloor',battle_floor)
        for variant in range(3):export_asset('Cairn'+chr(65+variant),'돌탑 '+chr(65+variant),'20_Stone_Cairns',lambda v=variant:cairn(v))
        export_asset('RuinedPavilion','부서진 정자와 파손 모듈','21_Ruined_Pavilion',ruined_pavilion)
        for variant,key in enumerate(('MountainNear','MountainMid','MountainFar')):export_asset(key,'배경 산맥 '+key,'22_Mountain_Cloud_Background',lambda v=variant:mountain(v))
        export_asset('GroundedMountainBase','지면으로 이어지는 산정상 암반','22_Mountain_Cloud_Background',mountain_foundation)
        export_asset('CloudLayer','구름바다와 산허리 안개','22_Mountain_Cloud_Background',lambda:plane('SoftCloudShaderLayer',24,16,(0,0,0),CLOUD))
        def flame():
            for i in range(3):
                obj=mesh('SeparateSpiritFlameCard',[(-.22,0,0),(.22,0,0),(.22,0,.7),(-.22,0,.7)],[(0,1,2,3)],FLAME,[(0,0),(1,0),(1,1),(0,1)])
                obj.rotation_euler.z=i*math.pi/3
        export_asset('SpiritFlame','셰이더 영혼불','12_Sealstone_Constellation',flame)
        manifest=dict(reference_root=r'E:\C.J.I\ReferenceImage\Cheonjiin_AI_3D_Reference_Pack',materials=MATERIALS,models=CATALOG)
        (ROOT/'Test_ReferenceModelCatalog.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
        sources=ROOT/'Sources~';sources.mkdir(exist_ok=True)
        for collection in bpy.context.scene.collection.children:
            collection.hide_render=collection.name!='Test_PineA'
            collection.hide_viewport=collection.name!='Test_PineA'
        bpy.ops.file.pack_all()
        bpy.context.preferences.filepaths.save_version=0
        if hasattr(bpy.context.preferences.filepaths,'file_preview_type'):
            bpy.context.preferences.filepaths.file_preview_type='NONE'
        bpy.ops.wm.save_as_mainfile(filepath=str(sources/'Test_CheonReferenceLibrary.blend'))
        print('REFERENCE_LIBRARY_READY',len(CATALOG),flush=True)


Test_BuildReferenceModels.main()
