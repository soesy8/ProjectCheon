import bpy
import bmesh
import math
import json
from mathutils import Vector, Quaternion
from pathlib import Path

OUT = Path(r'D:\project\ProjectCheon\Assets\Temp\CheonHair~')
scene = bpy.context.scene
source = bpy.data.objects['HairOrigin']
original_hair = bpy.data.objects['Hair']
source_points = [source.matrix_world @ vertex.co - Vector((source.location.x, 0, 0)) for vertex in source.data.vertices]
source_faces = [list(face.vertices) for face in source.data.polygons]
used = sorted(set(index for face in source_faces for index in face))
remap = {old: new for new, old in enumerate(used)}
source_points = [source_points[index] for index in used]
source_faces = [[remap[index] for index in face] for face in source_faces]
z_top = max(p.z for p in source_points)
z_bottom = min(p.z for p in source_points)
root = max(source_points, key=lambda p: p.z)
tip = min(source_points, key=lambda p: p.z)

# 원본 한 가닥의 면 구성과 두께를 유지하며, 머리 표면을 따르는 경로로 변형한다.
vertices, faces, face_materials, groups = [], [], [], {}
center = Vector((0, 0.006, 1.670))
radius = Vector((0.197, 0.209, 0.235))

def surface(theta, phi):
    return center + Vector((radius.x*math.sin(phi)*math.sin(theta),
                            -radius.y*math.sin(phi)*math.cos(theta),
                            radius.z*math.cos(phi)))

def normal_at(theta, phi):
    return Vector((math.sin(phi)*math.sin(theta)/radius.x,
                   -math.sin(phi)*math.cos(theta)/radius.y,
                   math.cos(phi)/radius.z)).normalized()

def bezier(a, b, c, t):
    return (1-t)**2*a + 2*(1-t)*t*b + t*t*c

def add_lock(name, path, theta_start, theta_end, width, depth, material=0):
    first = len(vertices)
    a,b,c = [Vector(point) for point in path]
    for point in source_points:
        t = (z_top-point.z)/(z_top-z_bottom)
        theta = theta_start*(1-t)+theta_end*t
        lateral = Vector((math.cos(theta), math.sin(theta), 0))
        direction = (b-a)*(1-t)+(c-b)*t
        outward = lateral.cross(direction).normalized()
        expected = Vector((math.sin(theta), -math.cos(theta), 0.35))
        if outward.dot(expected) < 0:
            outward.negate()
        axis_x = root.x*(1-t)+tip.x*t
        axis_y = -0.096-0.078*math.sin(math.pi*t/2)-0.016*math.sin(math.pi*t)
        position = bezier(a,b,c,t)
        position += lateral*(point.x-axis_x)*width/0.132
        position += outward*(-point.y+axis_y)*depth/0.090
        vertices.append(tuple(position))
    groups[name] = list(range(first,len(vertices)))
    for face in source_faces:
        faces.append([first+i for i in face])
        face_materials.append(material)

def cap_bottom(theta):
    angle = abs((theta+math.pi)%(2*math.pi)-math.pi)
    keys = [(0,1.748),(.65,1.718),(1.12,1.660),(1.60,1.641),(1.95,1.594),(2.40,1.553),(math.pi,1.540)]
    for (a,z),(b,w) in zip(keys,keys[1:]):
        if a<=angle<=b:
            return z+(w-z)*(angle-a)/(b-a)
    return keys[-1][1]

# 겹친 가닥 사이로 두피가 드러나지 않도록 닫힌 내부 헤어 캡을 만든다.
segments, rings = 32, 7
vertices.append(tuple(surface(0,0)))
for row in range(1,rings+1):
    for column in range(segments):
        theta = 2*math.pi*column/segments
        final_phi = math.acos((cap_bottom(theta)-center.z)/radius.z)
        phi = final_phi*row/rings
        vertices.append(tuple(surface(theta,phi)))
for column in range(segments):
    faces.append([0,1+column,1+(column+1)%segments])
    face_materials.append(2)
for row in range(rings-1):
    for column in range(segments):
        a=1+row*segments+column
        b=1+row*segments+(column+1)%segments
        c=1+(row+1)*segments+(column+1)%segments
        d=1+(row+1)*segments+column
        faces.append([a,b,c,d])
        face_materials.append(2)
bottom_center=len(vertices)
vertices.append((0,.025,1.68))
for column in range(segments):
    faces.append([bottom_center,1+(rings-1)*segments+(column+1)%segments,1+(rings-1)*segments+column])
    face_materials.append(2)
groups['Scalp'] = list(range(len(vertices)))

# 아래쪽 레이어는 귀 뒤와 목덜미를 감싸고 끝이 서로 어긋나도록 배치한다.
for index, degrees in enumerate([83,110,137,164,190,217,244,272]):
    theta = math.radians(degrees)
    end_theta = theta+math.radians(7 if degrees<180 else -6)
    end_z = 1.580 if degrees in [83,272] else 1.515+(index%3)*.009
    phi_end=math.acos((end_z-center.z)/radius.z)
    path=[surface(theta,.98),surface(theta+math.radians(3),1.57)+normal_at(theta,1.57)*.008,
          surface(end_theta,phi_end)]
    add_lock('Nape_%02d'%(index+1),path,theta,end_theta,.106,.043,0 if index%3 else 1)

# 윗 레이어는 뒤통수 볼륨과 좌우의 흐름을 연결한다.
for index, degrees in enumerate([66,94,122,150,179,207,235,263,291]):
    theta=math.radians(degrees)
    sweep=math.radians(11 if degrees<180 else -10)
    phi_end = 1.69+(index%3)*.075
    path=[surface(theta-sweep*.4,.48),surface(theta,1.22)+normal_at(theta,1.22)*.011,
          surface(theta+sweep,phi_end)]
    add_lock('SideBack_%02d'%(index+1),path,theta-sweep*.4,theta+sweep,.135,.050,0 if index%4 else 1)

# 정수리는 큰 가닥의 뿌리를 짧은 가닥으로 덮어 자연스럽게 이어 준다.
for index,degrees in enumerate([72,144,216,288,350]):
    theta=math.radians(degrees)
    end_theta=theta+math.radians(18)
    path=[surface(theta,.16),surface(theta+.12,.59)+normal_at(theta,.59)*.009,
          surface(end_theta,.98+(index%2)*.06)]
    add_lock('Crown_%02d'%(index+1),path,theta,end_theta,.112,.031,0)

# 앞머리는 정중앙을 조금 비켜 흐르게 하여 참고 이미지의 비대칭 실루엣을 따른다.
front_locks=[
    ('Fringe_LeftOuter', [(-.067,-.048,1.874),(-.153,-.131,1.798),(-.192,-.101,1.708)],-.45,-.96,.112,.043,0),
    ('Fringe_RightOuter',[(.079,-.037,1.874),(.169,-.110,1.797),(.206,-.057,1.705)], .5,1.05,.117,.044,0),
    ('Fringe_LeftSweep', [(-.018,-.083,1.884),(-.102,-.187,1.790),(-.119,-.172,1.713)],-.15,-.56,.130,.048,1),
    ('Fringe_RightSweep',[(.038,-.072,1.883),(.106,-.187,1.792),(.157,-.137,1.712)],.2,.65,.130,.047,0),
    ('Fringe_Center',    [(-.014,-.085,1.891),(-.027,-.194,1.802),(.015,-.191,1.685)],0,.08,.114,.050,0),
]
for name,path,start,end,width,depth,material in front_locks:
    add_lock(name,path,start,end,width,depth,material)

for sign in [-1,1]:
    theta=sign*1.12
    path=[surface(theta,.99),Vector((sign*.188,-.076,1.701)),Vector((sign*.168,-.044,1.614))]
    add_lock('Temple_Left' if sign<0 else 'Temple_Right',path,theta,theta,.065,.030,2)

# 갓의 기울어진 챙 위에서는 크라운의 안쪽에 들어가도록 상부 볼륨을 정리한다.
hat=bpy.data.objects['Gat']
hat_inverse=hat.matrix_world.inverted()
for index,position in enumerate(vertices):
    local=hat_inverse@Vector(position)
    if local.z > -1.12:
        distance=math.hypot(local.x,local.y)
        limit=(1.300835-.12455*(local.z+.9447))*math.cos(math.pi/16)-.04
        if distance>limit:
            blend=min(1.0,(local.z+1.12)/.16)
            factor=(distance-(distance-limit)*blend)/distance
            local.x*=factor
            local.y*=factor
            vertices[index]=tuple(hat.matrix_world@local)

mesh=bpy.data.meshes.new('Hair_CompletedMesh')
mesh.from_pydata(vertices,[],faces)
mesh.update()
hair=bpy.data.objects.new('Hair_Completed',mesh)
hair_collection=bpy.data.collections.new('Hair_Completed')
scene.collection.children.link(hair_collection)
hair_collection.objects.link(hair)

colors=[(.135,.066,.034,1),(.157,.080,.042,1),(.096,.043,.022,1)]
for name,color in zip(['M_HairChestnut','M_HairChestnutLight','M_HairChestnutShadow'], colors):
    material=bpy.data.materials.new(name)
    material.diffuse_color=color
    material.use_nodes=True
    shader=material.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value=color
    shader.inputs['Roughness'].default_value=.79
    mesh.materials.append(material)
for polygon,index in zip(mesh.polygons,face_materials):
    polygon.material_index=index
    polygon.use_smooth=False
for name,indices in groups.items():
    group=hair.vertex_groups.new(name=name)
    group.add(indices,1.0,'REPLACE')

bm=bmesh.new()
bm.from_mesh(mesh)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bm.to_mesh(mesh)
bm.free()
hair['SourceObject']='HairOrigin'
hair['Design']='Faceted chestnut hair: crown, layered fringe, temples, sides and nape'
hair['LockCount']=len(groups)-1
hair['Editing']='Vertex groups identify each lock. Hair_WorkSource retains the source meshes.'

archive=bpy.data.collections.new('Hair_WorkSource')
scene.collection.children.link(archive)
for obj in [source,original_hair]:
    for collection in list(obj.users_collection):
        collection.objects.unlink(obj)
    archive.objects.link(obj)
archive.hide_viewport=True
archive.hide_render=True

bpy.ops.object.select_all(action='DESELECT')
hair.select_set(True)
bpy.context.view_layer.objects.active=hair
bpy.ops.object.mode_set(mode='EDIT')
bpy.ops.mesh.select_all(action='SELECT')
bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.015)
bpy.ops.object.mode_set(mode='OBJECT')

# 파일을 열면 헤어를 바로 점검할 수 있도록 기존 작업 공간의 시점을 머리로 맞춘다.
view_rotation=Vector((3,-6,2)).to_track_quat('Z','Y')
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.type='SOLID'
            area.spaces.active.shading.color_type='MATERIAL'
            area.spaces.active.overlay.show_floor=False
            area.spaces.active.overlay.show_axis_x=False
            area.spaces.active.overlay.show_axis_y=False
            area.spaces.active.region_3d.view_rotation=view_rotation
            area.spaces.active.region_3d.view_location=(0,0,1.72)
            area.spaces.active.region_3d.view_distance=1.35
            area.spaces.active.region_3d.view_perspective='ORTHO'

bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Cheon_Player_Hair_Work.blend'))

# 미리보기용 카메라는 완성 파일에 포함하지 않는다.
scene.render.engine='BLENDER_WORKBENCH'
scene.render.resolution_x=1100
scene.render.resolution_y=1100
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.world.color=(.105,.105,.105)
sh=scene.display.shading
sh.light='STUDIO'
sh.studio_light='paint.sl'
sh.color_type='MATERIAL'
sh.show_shadows=True
sh.show_cavity=True
sh.cavity_type='BOTH'
sh.curvature_ridge_factor=.7
sh.curvature_valley_factor=.55
sh.background_type='WORLD'
sh.show_specular_highlight=True
camera_data=bpy.data.cameras.new('PreviewCamera')
camera=bpy.data.objects.new('PreviewCamera',camera_data)
scene.collection.objects.link(camera)
scene.camera=camera
camera_data.type='ORTHO'

def render(name,pos,target,scale):
    camera.location=pos
    camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
    camera_data.ortho_scale=scale
    scene.render.filepath=str(OUT/name)
    bpy.ops.render.render(write_still=True)

bpy.data.objects['Pouch'].hide_render=True
render('hair_full.png',(0,-5,1.05),(0,0,1.05),2.35)
render('hair_hat_front.png',(0,-5,1.84),(0,0,1.84),1.12)
render('hair_hat_threequarter.png',(3,-5,3.0),(0,0,1.77),1.12)
bpy.data.objects['Gat'].hide_render=True
render('hair_front.png',(0,-5,1.72),(0,0,1.72),.64)
render('hair_threequarter.png',(3,-5,2.6),(0,0,1.72),.67)
render('hair_side.png',(5,0,1.72),(0,0,1.72),.65)
render('hair_back.png',(0,5,1.72),(0,0,1.72),.65)

bm=bmesh.new()
bm.from_mesh(mesh)
report={'vertices':len(mesh.vertices),'polygons':len(mesh.polygons),'triangles':sum(len(f.vertices)-2 for f in mesh.polygons),
        'locks':len(groups)-1,'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges),
        'loose_vertices':sum(not v.link_faces for v in bm.verts),'zero_area_faces':sum(f.calc_area()<1e-10 for f in bm.faces)}
bm.free()
(OUT/'hair_validation.json').write_text(json.dumps(report,indent=2))
print('HAIR_VALIDATION',json.dumps(report))
