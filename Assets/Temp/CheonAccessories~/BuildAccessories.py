import bpy
import bmesh
import hashlib
import json
import math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

OUT=Path(r'D:\project\ProjectCheon\Assets\Temp\CheonAccessories~')
source_path=Path(bpy.data.filepath)
source_hash=hashlib.sha256(source_path.read_bytes()).hexdigest()
scene=bpy.context.scene
deps=bpy.context.evaluated_depsgraph_get()

def object_fingerprint(obj):
    data={'matrix':[list(row) for row in obj.matrix_world],
          'vertices':[list(v.co) for v in obj.data.vertices] if obj.type=='MESH' else [],
          'faces':[list(f.vertices) for f in obj.data.polygons] if obj.type=='MESH' else [],
          'modifiers':[(m.name,m.type) for m in obj.modifiers],
          'materials':[m.name if m else None for m in obj.data.materials] if obj.type=='MESH' else []}
    return hashlib.sha256(json.dumps(data,sort_keys=True).encode()).hexdigest()

preserved={obj.name:object_fingerprint(obj) for obj in scene.objects if not obj.name.startswith('Hair')}

def make_bvh(name):
    obj=bpy.data.objects[name].evaluated_get(deps)
    mesh=obj.to_mesh()
    result=BVHTree.FromPolygons([obj.matrix_world@v.co for v in mesh.vertices],
                               [list(f.vertices) for f in mesh.polygons])
    obj.to_mesh_clear()
    return result

body_bvh=make_bvh('Body')
hat_bvh=make_bvh('Gat')

def body_surface(x,z,front=True):
    origin=Vector((x,-2 if front else 2,z))
    direction=Vector((0,1 if front else -1,0))
    hit=body_bvh.ray_cast(origin,direction)[0]
    if hit is None:
        raise ValueError('Body surface missed at %s'%((x,z,front),))
    return hit

# 세 파츠의 월드 좌표를 보존한 채 현재 Hair를 기준으로 합친다.
hair_parts=[obj for obj in scene.objects if obj.type=='MESH' and obj.name.startswith('Hair')]
hair_vertex_count=sum(len(obj.data.vertices) for obj in hair_parts)
hair_face_count=sum(len(obj.data.polygons) for obj in hair_parts)
hair_collections=set(collection for obj in hair_parts for collection in obj.users_collection)
if bpy.context.object and bpy.context.object.mode!='OBJECT':
    bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.select_all(action='DESELECT')
for obj in hair_parts:
    obj.hide_set(False)
    obj.hide_viewport=False
    obj.select_set(True)
bpy.context.view_layer.objects.active=bpy.data.objects['Hair']
bpy.ops.object.join()
hair=bpy.context.object
hair.name='Hair'
hair.data.name='HairMesh'
bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
for collection in hair_collections:
    if collection in hair.users_collection and collection.name=='Hair_WorkSource':
        collection.name='Hair'
    elif not collection.objects and not collection.children:
        bpy.data.collections.remove(collection)
assert len(hair.data.vertices)==hair_vertex_count
assert len(hair.data.polygons)==hair_face_count

accessories=bpy.data.collections.new('Straps')
scene.collection.children.link(accessories)

def new_builder():
    return {'vertices':[],'faces':[],'groups':{}}

def add_ribbon(builder,name,centers,width_axes,normals,widths,thickness,bevel=.0008,support_modes=None,support_clearance=.0055):
    vertices=builder['vertices']
    faces=builder['faces']
    first=len(vertices)
    for row,(center,axis,normal,width) in enumerate(zip(centers,width_axes,normals,widths)):
        center,axis,normal=Vector(center),Vector(axis).normalized(),Vector(normal).normalized()
        half_width=width/2
        half_depth=thickness/2
        edge=min(bevel,half_depth*.6)
        profile=[(-half_width+edge,-half_depth),(half_width-edge,-half_depth),
                 (half_width,-half_depth+edge),(half_width,half_depth-edge),
                 (half_width-edge,half_depth),(-half_width+edge,half_depth),
                 (-half_width,half_depth-edge),(-half_width,-half_depth+edge)]
        for u,v in profile:
            base=center+axis*u
            mode=support_modes[row] if support_modes else None
            if mode in ['front','back']:
                hit=body_bvh.ray_cast(Vector((base.x,-2 if mode=='front' else 2,base.z)),
                                       Vector((0,1 if mode=='front' else -1,0)))[0]
                if hit is not None:
                    if mode=='front': base.y=min(base.y,hit.y-support_clearance)
                    else: base.y=max(base.y,hit.y+support_clearance)
            elif mode=='top':
                hit=body_bvh.ray_cast(Vector((base.x,base.y,1.445)),Vector((0,0,-1)))[0]
                if hit is not None and hit.z>1.29:
                    base.z=hit.z+.0055
            vertices.append(tuple(base+normal*v))
    for row in range(len(centers)-1):
        for column in range(8):
            a=first+row*8+column
            b=first+row*8+(column+1)%8
            faces.append([a,b,b+8,a+8])
    faces.append([first+i for i in reversed(range(8))])
    last=first+(len(centers)-1)*8
    faces.append([last+i for i in range(8)])
    builder['groups'][name]=list(range(first,len(vertices)))

def directions(centers,hints):
    axes=[]
    normals=[]
    for index,point in enumerate(centers):
        a=Vector(centers[max(0,index-1)])
        b=Vector(centers[min(len(centers)-1,index+1)])
        tangent=(b-a).normalized()
        normal=Vector(hints[index]).normalized()
        axis=tangent.cross(normal).normalized()
        normal=axis.cross(tangent).normalized()
        axes.append(axis)
        normals.append(normal)
    return axes,normals

def build_object(name,builder):
    mesh=bpy.data.meshes.new(name+'Mesh')
    mesh.from_pydata(builder['vertices'],[],builder['faces'])
    mesh.update()
    bm=bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    obj=bpy.data.objects.new(name,mesh)
    accessories.objects.link(obj)
    for group_name,indices in builder['groups'].items():
        group=obj.vertex_groups.new(name=group_name)
        group.add(indices,1,'REPLACE')
    return obj

# 어깨끈은 파우치 안쪽 부착점에서 앞가슴, 어깨, 등과 옆구리를 거쳐 반대 부착점까지 잇는다.
pouch=bpy.data.objects['Pouch']
front_anchor=pouch.matrix_world@Vector((-.93,0,.80))
back_anchor=pouch.matrix_world@Vector((.93,0,.80))
front_rows=[]
for i in range(29):
    t=i/28
    z=.997+(1.347-.997)*t
    x=.054+(-.177-.054)*t
    hit=body_surface(x,z,True)
    front_rows.append(Vector((x,hit.y-.008,z)))
# 벨트 아래의 접힘과 파우치 사이에는 자연스러운 여유를 둔다.
front_rows[0].y=min(front_rows[0].y,-.145)
front_rows[1].y=min(front_rows[1].y,-.137)
strap_centers=[front_anchor,Vector((.060,-.164,.977))]+front_rows
strap_hints=[Vector((0,-1,0)) for _ in strap_centers]
strap_supports=[None,None]+['front']*len(front_rows)
shoulder=[(-.180,-.020,1.369),(-.180,.002,1.377),(-.180,.024,1.386),
          (-.180,.046,1.379),(-.180,.067,1.369),(-.177,.087,1.347)]
shoulder_start=len(strap_centers)
shoulder_axes=[]
for index,point in enumerate(shoulder):
    point=Vector(point)
    heights=[]
    for offset in [-.0205,0,.0205]:
        hit=body_bvh.ray_cast(Vector((point.x+offset,point.y,1.445)),Vector((0,0,-1)))[0]
        heights.append(hit.z if hit is not None and hit.z>1.29 else point.z-.006)
    slope=(heights[2]-heights[0])/.041
    width_axis=Vector((1,0,slope)).normalized()
    point.z=max(height-width_axis.z*offset for height,offset in zip(heights,[-.0205,0,.0205]))+.006
    shoulder_axes.append(width_axis)
    strap_centers.append(point)
    angle=math.pi*index/(len(shoulder)-1)
    strap_hints.append(Vector((0,-math.cos(angle),math.sin(angle))))
    strap_supports.append(None)
for i in range(1,30):
    t=i/29
    x=-.177+(.146+.177)*t
    z=1.344+(.994-1.344)*t
    hit=body_surface(x,z,False)
    strap_centers.append(Vector((x,hit.y+.008,z)))
    strap_hints.append(Vector((0,1,0)))
    strap_supports.append('back')
for point,normal in [((.170,.052,.985),(1,.6,0)),((.190,.020,.979),(1,0,0)),
                     ((.202,-.020,.973),(1,0,0)),((.204,-.060,.966),(1,-.4,0)),
                     (back_anchor,(1,-.6,0))]:
    strap_centers.append(Vector(point))
    strap_hints.append(Vector(normal))
    strap_supports.append(None)
strap_axes,strap_normals=directions(strap_centers,strap_hints)
for index,axis in enumerate(shoulder_axes):
    row=shoulder_start+index
    tangent=(strap_centers[row+1]-strap_centers[row-1]).normalized()
    strap_axes[row]=axis
    strap_normals[row]=axis.cross(tangent).normalized()
strap_widths=[.034 if i in [0,len(strap_centers)-1] else .041 for i in range(len(strap_centers))]
strap_builder=new_builder()
add_ribbon(strap_builder,'ShoulderLoop',strap_centers,strap_axes,strap_normals,strap_widths,.005,.001,strap_supports)
strap=build_object('PouchStrap',strap_builder)
strap['Description']='Reference crossbody strap: front, over shoulder, back, both pouch attachment points'
strap['StrapWidth']=.041

# 갓 안쪽에서 시작해 귀 앞과 턱선을 따라 내려오는 양쪽 천 끈을 만든다.
chin_builder=new_builder()
for sign,label in [(-1,'Left'),(1,'Right')]:
    anchor=hat_bvh.ray_cast(Vector((sign*.155,-.015,1.5)),Vector((0,0,1)))[0]
    anchor.z-=.002
    points=[anchor,Vector((sign*.155,-.045,1.765))]
    controls=[Vector((sign*.153,-.052,1.705)),Vector((sign*.165,-.030,1.600)),
              Vector((sign*.090,-.090,1.490)),Vector((0,-.140,1.446))]
    for index in range(13):
        t=index/12
        a,b,c,d=controls
        points.append((1-t)**3*a+3*(1-t)**2*t*b+3*(1-t)*t*t*c+t**3*d)
    axes,normals=directions(points,[Vector((0,-1,0))]*len(points))
    add_ribbon(chin_builder,label+'ChinBand',points,axes,normals,[.0155]*len(points),.0035,.00065,
               [None,None]+['front']*13,support_clearance=.014)

def folded_ribbon(name,points,widths):
    axes,normals=directions(points,[Vector((0,-1,0))]*len(points))
    add_ribbon(chin_builder,name,points,axes,normals,widths,.0038,.0007)

# 참고 이미지의 턱 아래 매듭, 짧은 나비 리본과 길이가 다른 두 끝단을 반영한다.
folded_ribbon('BowLeft',[(0,-.141,1.443),(-.024,-.132,1.430),(-.035,-.137,1.403),(-.014,-.147,1.419)],
              [.014,.018,.014,.010])
folded_ribbon('BowRight',[(0,-.141,1.443),(.025,-.132,1.429),(.033,-.139,1.402),(.012,-.148,1.420)],
              [.014,.018,.014,.010])
folded_ribbon('RibbonTailLeft',[(-.006,-.142,1.439),(-.010,-.132,1.410),(-.011,-.124,1.368),
                                 (-.014,-.128,1.321),(-.020,-.134,1.290)],
              [.017,.020,.020,.019,.017])
folded_ribbon('RibbonTailRight',[(.007,-.139,1.439),(.012,-.126,1.406),(.017,-.122,1.365),
                                  (.022,-.127,1.329),(.026,-.131,1.307)],
              [.016,.018,.018,.017,.014])

# 가운데 매듭은 낮은 면 수의 납작한 팔각형으로 잡는다.
knot_centers=[Vector((0,-.147,1.454)),Vector((0,-.151,1.447)),Vector((0,-.150,1.436))]
add_ribbon(chin_builder,'Knot',knot_centers,[Vector((1,0,0))]*3,[Vector((0,-1,0))]*3,
           [.014,.024,.017],.013,.0025)
chin=build_object('GatChinStrap',chin_builder)
chin['Description']='Reference cloth chin straps with throat knot, bow folds and two hanging tails'
chin['StrapWidth']=.0155

for obj in [strap,chin]:
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.015)
    bpy.ops.object.mode_set(mode='OBJECT')

validation={}
for obj in [hair,strap,chin]:
    bm=bmesh.new()
    bm.from_mesh(obj.data)
    validation[obj.name]={'vertices':len(bm.verts),'polygons':len(bm.faces),
                          'nonmanifold_edges':sum(not edge.is_manifold for edge in bm.edges),
                          'zero_area_faces':sum(face.calc_area()<1e-10 for face in bm.faces)}
    bm.free()
for name,fingerprint in preserved.items():
    assert object_fingerprint(bpy.data.objects[name])==fingerprint,name+' changed unexpectedly'
assert len([obj for obj in scene.objects if obj.name.startswith('Hair')])==1
assert all(validation[name]['nonmanifold_edges']==0 for name in ['PouchStrap','GatChinStrap'])
assert all(item['zero_area_faces']==0 for item in validation.values())

bpy.ops.object.select_all(action='DESELECT')
strap.select_set(True)
chin.select_set(True)
bpy.context.view_layer.objects.active=strap
bpy.context.preferences.filepaths.save_version=0
staged_path=OUT/'Cheon_Player_Accessories_Work.blend'
bpy.ops.wm.save_as_mainfile(filepath=str(staged_path))
report={'source':str(source_path),'source_hash':source_hash,'staged':str(staged_path),
        'preserved':preserved,'validation':validation,'hair_parts_before':len(hair_parts),
        'hair_vertices_before':hair_vertex_count,'hair_faces_before':hair_face_count}
(OUT/'accessories_validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print('ACCESSORIES_VALIDATION',json.dumps(validation))

# 확인용 색상과 카메라는 저장된 작업 파일을 변경하지 않는다.
scene.render.engine='BLENDER_WORKBENCH'
scene.render.resolution_x=1200
scene.render.resolution_y=1200
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.world.color=(.11,.11,.11)
sh=scene.display.shading
sh.light='STUDIO'
sh.studio_light='paint.sl'
sh.color_type='OBJECT'
sh.show_shadows=True
sh.show_cavity=True
sh.cavity_type='BOTH'
sh.curvature_ridge_factor=.5
sh.curvature_valley_factor=.45
sh.background_type='WORLD'
for obj in scene.objects:
    obj.color=(.60,.60,.60,1)
    if obj.name=='Hair': obj.color=(.17,.17,.17,1)
    if obj.name in ['Pouch','PouchStrap']: obj.color=(.22,.115,.055,1)
    if obj.name=='GatChinStrap': obj.color=(.028,.032,.04,1)
cam_data=bpy.data.cameras.new('PreviewCamera')
cam=bpy.data.objects.new('PreviewCamera',cam_data)
scene.collection.objects.link(cam)
scene.camera=cam
cam_data.type='ORTHO'
def render(name,pos,target,scale):
    cam.location=pos
    cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
    cam_data.ortho_scale=scale
    scene.render.filepath=str(OUT/name)
    bpy.ops.render.render(write_still=True)
render('accessories_front.png',(0,-5,1.18),(0,0,1.18),2.00)
render('accessories_threequarter.png',(3,-5,2.0),(0,0,1.18),2.00)
render('accessories_side.png',(5,0,1.18),(0,0,1.18),2.00)
render('accessories_back.png',(0,5,1.18),(0,0,1.18),2.00)
render('chin_front.png',(0,-5,1.56),(0,-.03,1.56),.63)
render('chin_side.png',(5,-1,1.56),(0,-.03,1.56),.65)
render('strap_front.png',(0,-5,1.16),(0,-.03,1.16),.93)
render('strap_back.png',(0,5,1.16),(0,.03,1.16),.93)
