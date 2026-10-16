import bpy, bmesh, math, os, json, struct, zlib
import numpy as np
from mathutils import Vector

OUT = r'E:\C.J.I\Blender'
TEMP = os.path.dirname(os.path.abspath(__file__))
os.makedirs(os.path.join(OUT, 'Textures'), exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.context.preferences.filepaths.save_version = 0

# 하나의 아틀라스 안에 얼굴 그림과 파츠별 단색 영역을 함께 둔다.
N = 1024
pixels = np.ones((N, N, 4), dtype=np.float32)
pixels[:, :, :3] = (.24, .26, .28)
palette = {
 'teal': '#229DAE', 'teal_light':'#2AA9B8', 'teal_dark':'#208797',
 'blue':'#245DAB', 'blue_dark':'#234D89', 'ivory':'#F4EBDC',
 'ivory_shadow':'#E6D9C5', 'gold':'#E8AC48', 'gold_light':'#F5BD5C',
 'skin':'#F4B783', 'hair':'#583923', 'hair_light':'#704B30',
 'hair_dark':'#422B20', 'hat':'#28282D', 'hat_light':'#323237',
 'pants':'#35353B', 'pants_light':'#414148', 'strap':'#8D5028',
 'bag':'#87502C', 'bag_light':'#A26435', 'sole':'#E4D5BD',
 'black':'#28282B', 'lining':'#185C70'
}
def rgb(hexcolor):
 return np.array([int(hexcolor[i:i+2], 16)/255 for i in (1,3,5)])
swatches = {}
for i,(name,col) in enumerate(palette.items()):
 x,y = i%8, i//8
 u0,v0=.025+x*.12,.025+y*.12
 pixels[int(v0*N):int((v0+.105)*N),int(u0*N):int((u0+.105)*N),:3]=rgb(col)
 swatches[name]=(u0+.0525,v0+.0525)

# 얼굴 UV는 정면 투영한다. 눈, 눈썹, 입은 픽셀로만 표현한다.
u,v=np.meshgrid((np.arange(N)+.5)/N,(np.arange(N)+.5)/N)
X=(u-.02)/.46*.60-.30
Z=(v-.51)/.47*.49+1.57
region=(u>=.02)&(u<=.48)&(v>=.51)&(v<=.98)
skin=rgb('#F4B783')
pixels[region,:3]=skin
def paint(mask,color):
 a=np.clip(mask,0,1)*region
 pixels[:,:,:3]=pixels[:,:,:3]*(1-a[:,:,None])+np.array(color)*a[:,:,None]
def ellipse(cx,cz,rx,rz,col):
 d=np.sqrt(((X-cx)/rx)**2+((Z-cz)/rz)**2)
 paint(np.clip((1-d)*35,0,1),rgb(col))
def stroke(points,width,col):
 d=np.full_like(X,100.)
 for a,b in zip(points[:-1],points[1:]):
  dx,dz=b[0]-a[0],b[1]-a[1]
  t=np.clip(((X-a[0])*dx+(Z-a[1])*dz)/(dx*dx+dz*dz),0,1)
  d=np.minimum(d,np.sqrt((X-a[0]-t*dx)**2+(Z-a[1]-t*dz)**2))
 paint(np.clip((width-d)*1600,0,1),rgb(col))
for s in (-1,1):
 paint(.12*np.exp(-((X-s*.139)/.045)**2-((Z-1.72)/.025)**2),rgb('#D97D5C'))
 ellipse(s*.085,1.793,.0235,.038,'#493024')
 ellipse(s*.085-.005,1.807,.0057,.007,'#FFF7E9')
 stroke([(s*.052,1.862),(s*.087,1.871),(s*.123,1.860)],.012,'#583624')
smile=[(x,1.704+.012*(x/.039)**2) for x in np.linspace(-.039,.039,17)]
stroke(smile,.0027,'#A25236')
stroke([(-.022,1.701),(0,1.698),(.022,1.701)],.0018,'#FFD5A4')
paint(.18*np.exp(-(X/.016)**2-((Z-1.735)/.008)**2),rgb('#BB7451'))
def png_save(path,arr):
 data=np.uint8(np.clip(arr[::-1]*255+.5,0,255))
 def chunk(t,b):return struct.pack('!I',len(b))+t+b+struct.pack('!I',zlib.crc32(t+b)&0xffffffff)
 raw=b''.join(b'\x00'+row.tobytes() for row in data)
 with open(path,'wb') as f:
  f.write(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('!2I5B',N,N,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(raw))+chunk(b'IEND',b''))
texpath=os.path.join(OUT,'Textures','T_CheonPlayer_BaseColor.png')
png_save(texpath,pixels)

V=[]; F=[]; UV=[]; SM=[]; groups={}
def vertex(p,group):
 i=len(V); V.append(tuple(p)); groups.setdefault(group,[]).append(i); return i
def face(ids,color='teal',smooth=False,faceuv=False):
 F.append(ids); SM.append(smooth)
 if faceuv:
  UV.append([(.02+(V[i][0]+.30)/.60*.46,.51+(V[i][2]-1.57)/.49*.47) for i in ids])
 else:
  u0,v0=swatches[color]; coords=[(-1,-1),(1,-1),(1,1),(-1,1)]
  UV.append([(u0+coords[k%4][0]*.025,v0+coords[k%4][1]*.025) for k in range(len(ids))])
def rings(coords,group,color,cap=True,smooth=False,variation=False,face_texture=False):
 ids=[[vertex(p,group) for p in row] for row in coords]; n=len(ids[0])
 for j in range(len(ids)-1):
  for k in range(n):
   ix=[ids[j][k],ids[j][(k+1)%n],ids[j+1][(k+1)%n],ids[j+1][k]]
   c=color
   if variation and k%5==2:c=color+'_light' if color+'_light' in swatches else color
   useface=face_texture and sum(V[i][1] for i in ix)/4<-.065
   face(ix,c,smooth,useface)
 if cap:
  face(list(reversed(ids[0])),color,smooth)
  face(ids[-1],color,smooth)
 return ids
def ellipse_rings(spec,group,color,n=16,center=(0,0),cap=True,smooth=False,variation=False):
 return rings([[(center[0]+rx*math.sin(2*math.pi*k/n),center[1]-ry*math.cos(2*math.pi*k/n),z) for k in range(n)] for z,rx,ry in spec],group,color,cap,smooth,variation)
def ribbon(points,width,group,color):
 # 정면 XZ 평면에서 일정 폭의 띠를 만든다.
 rows=[]
 for i,p in enumerate(points):
  d=Vector(points[min(i+1,len(points)-1)])-Vector(points[max(0,i-1)])
  t=Vector((d.z,0,-d.x)).normalized()*width/2
  rows.append([vertex(Vector(p)-t,group),vertex(Vector(p)+t,group)])
 for a,b in zip(rows[:-1],rows[1:]):face([a[0],a[1],b[1],b[0]],color)
def patch(points,group,color):face([vertex(p,group) for p in points],color)
def extrude_outline(outline,front,back,group,color):
 # outline is an XZ contour. Front points toward -Y.
 return rings([[(x,y,z) for x,z in outline] for y in (front,back)],group,color,True)
def bevel_box(cx,cy,cz,sx,sy,sz,bevel,group,color):
 outline=[(-sx/2+bevel,-sy/2),(sx/2-bevel,-sy/2),(sx/2,-sy/2+bevel),(sx/2,sy/2-bevel),(sx/2-bevel,sy/2),(-sx/2+bevel,sy/2),(-sx/2,sy/2-bevel),(-sx/2,-sy/2+bevel)]
 rows=[]
 for z,scale in ((cz-sz/2,.83),(cz-sz/2+bevel,1),(cz+sz/2-bevel,1),(cz+sz/2,.88)):
  rows.append([(cx+x*scale,cy+y*scale,z) for x,y in outline])
 return rings(rows,group,color,True,False,True)

# 몸통과 치마를 연속된 링으로 구성해 불필요한 분할을 줄인다.
robe_spec=[(.43,.42,.235),(.49,.405,.226),(.75,.327,.19),(1.04,.245,.157),(1.22,.19,.125),(1.31,.212,.136),(1.49,.225,.14),(1.535,.205,.119),(1.585,.09,.075)]
angles=sorted([2*math.pi*k/20 for k in range(20)]+[.54,2*math.pi-.54])
robe_rows=[[(rx*math.sin(a),-ry*math.cos(a),z) for a in angles] for z,rx,ry in robe_spec]
robe_ids=[[vertex(p,'Robe') for p in row] for row in robe_rows]
for j in range(len(robe_ids)-1):
 for k,a in enumerate(angles):
  c='teal_light' if k%6==2 else 'teal'
  if j<4 and (.53<a<.55 or 5.65<a<5.66):c='blue'
  face([robe_ids[j][k],robe_ids[j][(k+1)%len(angles)],robe_ids[j+1][(k+1)%len(angles)],robe_ids[j+1][k]],c)
ellipse_rings([(.43,.42,.235),(.438,.403,.219),(.49,.392,.214)],'RobeHem','lining',20,cap=False)

def robe_front(x,z,offset=.009):
 for i in range(len(robe_spec)-1):
  z0,rx0,ry0=robe_spec[i];z1,rx1,ry1=robe_spec[i+1]
  if z<=z1:
   t=max(0,min(1,(z-z0)/(z1-z0)));rx=rx0+(rx1-rx0)*t;ry=ry0+(ry1-ry0)*t;break
 else:rx,ry=.09,.075
 return -ry*math.sqrt(max(.02,1-(x/rx)**2))-offset
def shirt_triangle(a,b,c,depth=2):
 if depth:
  ab=((a[0]+b[0])/2,(a[1]+b[1])/2);bc=((b[0]+c[0])/2,(b[1]+c[1])/2);ca=((c[0]+a[0])/2,(c[1]+a[1])/2)
  for tri in ((a,ab,ca),(ab,b,bc),(ca,bc,c),(ab,bc,ca)):shirt_triangle(*tri,depth-1)
 else:patch([(x,robe_front(x,z),z) for x,z in (a,b,c)],'Shirt','ivory')

# 안쪽 저고리와 V자 깃.
shirt_outline=[(-.083,1.595),(.083,1.595),(.126,1.49),(0,1.355),(-.126,1.49)]
for a,b in zip(shirt_outline,shirt_outline[1:]+shirt_outline[:1]):shirt_triangle((0,1.49),a,b)
for s in (-1,1):
 ribbon([(s*.100,0,1.585),(s*.143,0,1.49),(s*.075,0,1.41),(0,0,1.33)],.034,'Collar','blue')
ribbon([(0,0,1.33),(-.075,0,1.225)],.042,'Collar','blue')
for i in groups['Collar']:
 x,y,z=V[i];V[i]=(x,robe_front(x,z,.015),z)
ellipse_rings([(1.51,.07,.064),(1.67,.076,.066)],'Neck','skin',12,smooth=True)

# 넓은 소매와 낮은 면 수의 손. 손가락은 한 손 외곽 안에서 연결된다.
for s,side in ((-1,'R'),(1,'L')):
 centers=[(s*.148,0,1.455),(s*.275,-.005,1.355),(s*.405,-.015,1.17),(s*.477,-.026,1.055)]
 direction=Vector(centers[-1])-Vector(centers[0]); direction.normalize()
 across=Vector((direction.z,0,-direction.x)).normalized()
 rows=[]
 for p,rad,depth in zip(centers,(.072,.115,.16,.17),(.072,.115,.135,.14)):
  rows.append([Vector(p)+across*(rad*math.cos(2*math.pi*k/12))+Vector((0,depth*math.sin(2*math.pi*k/12),0)) for k in range(12)])
 sleeve_ids=rings(rows,'Sleeve_'+side,'ivory',False)
 face(list(reversed(sleeve_ids[0])),'ivory')
 cuffstart=Vector(centers[-1])-direction*.025
 cuffend=Vector(centers[-1])+direction*.060
 rings([[p+across*(.173*math.cos(2*math.pi*k/12))+Vector((0,.143*math.sin(2*math.pi*k/12),0)) for k in range(12)] for p in (cuffstart,cuffend)],'Cuff_'+side,'blue',False,variation=True)
 rings([[cuffend+across*(r*math.cos(2*math.pi*k/12))+Vector((0,d*math.sin(2*math.pi*k/12),0)) for k in range(12)] for r,d in ((.173,.143),(.145,.115))],'Cuff_'+side,'blue_dark',False)
 # 손목은 소매 안에서 손까지 이어져 떠 보이지 않게 한다.
 ellipse_rings([(.91,.046,.038),(1.045,.045,.04)],'Hand_'+side,'skin',8,center=(s*.511,-.025))
 outline=[(.480,.98),(.536,.981),(.568,.927),(.573,.875),(.559,.835),(.539,.817),(.516,.834),(.518,.855),(.538,.888),(.533,.917),(.516,.904),(.514,.870),(.493,.865),(.481,.881)]
 extrude_outline([(s*x,z) for x,z in outline],-.077,.012,'Hand_'+side,'skin')

for s,side in ((-1,'R'),(1,'L')):
 ellipse_rings([(.135,.054,.055),(.195,.060,.062),(.218,.06,.065),(.255,.102,.085),(.315,.12,.10),(.47,.102,.094),(.59,.09,.086)],'Trousers_'+side,'pants',12,center=(s*.15,.015),variation=True)
 ellipse_rings([(.177,.065,.067),(.215,.065,.067)],'Ankle_'+side,'pants',12,center=(s*.15,.015))
 ellipse_rings([(.112,.052,.052),(.14,.052,.052)],'Sock_'+side,'ivory',12,center=(s*.15,.006))
 # 사각 앞코와 얇은 밑창.
 outline=[(-.07,-.20),(.07,-.20),(.088,-.16),(.076,.076),(.051,.104),(-.055,.104),(-.078,.070),(-.088,-.16)]
 rings([[(s*.15+x,y,z) for x,y in outline] for z in (.012,.037)],'ShoeSole_'+side,'sole')
 rings([[(s*.15+x,y,.037) for x,y in outline],[(s*.15+x*.96,y*.97,.080) for x,y in outline],[(s*.15+x*.64,y*.46+.015,.15) for x,y in outline]],'Shoe_'+side,'black')

# 허리띠와 끝자락.
ellipse_rings([(1.155,.222,.148),(1.225,.207,.144)],'Belt','gold',20)
ribbon([(.080,-.151,1.19),(.081,-.177,.98),(.087,-.197,.79)],.066,'BeltTails','gold_light')
ribbon([(.126,-.153,1.19),(.140,-.176,.97),(.160,-.205,.65)],.071,'BeltTails','gold')

# 피부만 smooth shading. 코는 얼굴 표면에 포함되며 눈, 입, 눈썹 기하는 없다.
headspec=[(1.62,.044,.065),(1.637,.100,.108),(1.665,.150,.14),(1.70,.177,.159),(1.728,.188,.169),(1.745,.193,.173),(1.762,.198,.177),(1.79,.202,.18),(1.835,.202,.18),(1.875,.194,.174),(1.925,.18,.16),(1.97,.146,.127),(2.005,.09,.084),(2.02,.025,.03)]
headrows=[]
for z,rx,ry in headspec:
 row=[]
 for k in range(32):
  a=2*math.pi*k/32; c=math.cos(a); x=rx*math.sin(a)
  y=-ry*(abs(c)**.57)*(1 if c>=0 else -1)
  if c>0:
   y-=.043*math.exp(-(x/.023)**2-((z-1.745)/.024)**2)
  row.append((x,y,z))
 headrows.append(row)
rings(headrows,'Head','skin',True,True,False,True)
for s,side in ((-1,'R'),(1,'L')):
 outline=[(.178,1.80),(.214,1.813),(.237,1.785),(.244,1.748),(.224,1.705),(.196,1.700),(.177,1.724)]
 extrude_outline([(s*x,z) for x,z in outline],-.02,.048,'Ear_'+side,'skin')

# 큼직한 머리 덩어리와 앞머리 쐐기.
hairrows=[]
for t in (0,.45,1):
 row=[]
 for k in range(20):
  a=2*math.pi*k/20; c=math.cos(a); bottom=1.78+.135*max(c,0)
  z=bottom+(2.055-bottom)*t; rx=(.219,.211,.175)[(0,.45,1).index(t)]
  ry=(.200,.200,.160)[(0,.45,1).index(t)]
  row.append((rx*math.sin(a),-ry*c+.019,z))
 hairrows.append(row)
rings(hairrows,'Hair','hair',True,False,True)
def lock(points,peak):
 ids=[vertex(p,'Hair') for p in points]; p=vertex(peak,'Hair')
 inset=[vertex(Vector(q)*.60+Vector(peak)*.40,'Hair') for q in points]
 for j in range(len(ids)):
  k=(j+1)%len(ids);col=('hair','hair_light','hair','hair')[j%4]
  face([ids[j],ids[k],inset[k],inset[j]],col)
  face([inset[j],inset[k],p],col)
 face(list(reversed(ids)),'hair_dark')
lock([(-.079,-.178,2.025),(.049,-.184,2.023),(.022,-.212,1.824),(-.054,-.201,1.914)],(-.025,-.241,1.958))
lock([(-.072,-.180,2.018),(-.151,-.146,2.012),(-.226,-.160,1.835),(-.127,-.210,1.874)],(-.131,-.228,1.943))
lock([(.050,-.180,2.020),(.151,-.147,2.009),(.231,-.152,1.837),(.139,-.202,1.865)],(.130,-.225,1.944))
for s in (-1,1):
 lock([(s*.158,-.104,1.994),(s*.207,-.064,1.964),(s*.251,-.055,1.875),(s*.215,-.146,1.861)],(s*.222,-.13,1.93))

# 갓은 링 위주의 얇은 챙과 16면 원통으로 구성.
ellipse_rings([(2.052,.17,.156),(1.995,.48,.43),(2.012,.53,.477),(2.040,.50,.45),(2.080,.17,.156)],'GatBrim','hat',32,cap=False,variation=True)
ellipse_rings([(2.059,.178,.16),(2.24,.155,.141),(2.275,.147,.134),(2.286,.11,.100)],'GatCrown','hat',16,cap=True,variation=True)
for s in (-1,1):
 ribbon([(s*.185,-.065,2.038),(s*.188,-.133,1.845),(s*.177,-.140,1.75),(s*.147,-.138,1.69),(s*.087,-.116,1.637),(0,-.087,1.615)],.012,'HatTies','black')
 ribbon([(0,-.113,1.606),(s*.045,-.132,1.55)],.025,'HatKnot','black')
ribbon([(-.007,-.12,1.59),(-.010,-.16,1.53),(-.014,-.18,1.46),(-.018,-.186,1.39)],.035,'HatTails','hat')
ribbon([(.010,-.125,1.59),(.016,-.17,1.53),(.022,-.19,1.46),(.028,-.194,1.37)],.028,'HatTails','hat_light')
for group in ('HatTails','HatKnot'):
 for i in groups[group]:
  x,y,z=V[i];V[i]=(x,min(y,robe_front(x,z,.033)),z)

# 레퍼런스처럼 바짓단과 신발의 좌우 간격을 확보한다.
for side,s in (('R',-1),('L',1)):
 for prefix in ('Trousers_','Ankle_','Sock_','Shoe_','ShoeSole_'):
  for i in groups[prefix+side]:
   x,y,z=V[i]
   if prefix.startswith('Shoe'):x=s*.15+(x-s*.15)*1.13
   V[i]=(x+s*.037,y,z)

# 가방은 작은 모따기 상자, 어깨끈은 연속 띠.
bevel_box(.276,-.193,.99,.164,.117,.242,.023,'Satchel','bag')
extrude_outline([(.194,1.103),(.348,1.103),(.361,1.002),(.339,.980),(.224,.980)],-.260,-.248,'SatchelFlap','bag_light')
ellipse_rings([(1.004,.012,.008),(1.033,.012,.008)],'SatchelClasp','gold',8,center=(.295,-.271))
ribbon([(-.187,-.083,1.535),(-.142,-.158,1.466),(-.035,-.181,1.31),(.099,-.179,1.145),(.209,-.263,1.066)],.053,'ShoulderStrap','strap')
ribbon([(-.187,-.083,1.535),(-.187,.065,1.535),(-.13,.151,1.43),(.041,.16,1.23),(.222,.093,1.057),(.276,-.13,1.045)],.053,'ShoulderStrap','strap')

mesh=bpy.data.meshes.new('Cheon_Player_Mesh')
mesh.from_pydata(V,[],F); mesh.update()
character=bpy.data.objects.new('Cheon_Player',mesh)
collection=bpy.data.collections.new('CHARACTER'); bpy.context.scene.collection.children.link(collection); collection.objects.link(character)
for i,p in enumerate(mesh.polygons):p.use_smooth=SM[i]
uvlayer=mesh.uv_layers.new(name='UV_Atlas')
for poly,uvs in zip(mesh.polygons,UV):
 for li,coord in zip(poly.loop_indices,uvs):uvlayer.data[li].uv=coord
for name,indices in groups.items():character.vertex_groups.new(name=name).add(indices,1,'REPLACE')
bm=bmesh.new(); bm.from_mesh(mesh); bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=.000001); bmesh.ops.recalc_face_normals(bm,faces=bm.faces); bm.to_mesh(mesh); bm.free(); mesh.update()

mat=bpy.data.materials.new('M_CheonPlayer'); mat.use_nodes=True
nodes=mat.node_tree.nodes; bs=nodes.get('Principled BSDF'); bs.inputs['Roughness'].default_value=.83
bs.inputs['Specular IOR Level'].default_value=.22
im=bpy.data.images.load(texpath,check_existing=False); im.pack()
tex=nodes.new('ShaderNodeTexImage'); tex.name='Painted Face and Color Atlas'; tex.image=im; tex.interpolation='Linear'
mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color']); mesh.materials.append(mat)
for unused in list(bpy.data.materials):
 if unused.users==0:bpy.data.materials.remove(unused)
character['Reference']='T_PlayerCharacter_LowPoly2x_SmoothFace_Front.png'
character['Face features']='Eyes, eyebrows and mouth are painted in T_CheonPlayer_BaseColor. No separate feature geometry.'
character['Topology']='Single mesh object; clothing/accessory islands; edit by vertex group. No armature.'
character['Units']='Meters; Z up; character forward -Y; feet at ground.'

# 렌더 보조 요소는 별도 컬렉션에 두고 FBX에는 캐릭터만 내보낸다.
studio=bpy.data.collections.new('STUDIO'); bpy.context.scene.collection.children.link(studio)
def point(obj,at):obj.rotation_euler=(Vector(at)-obj.location).to_track_quat('-Z','Y').to_euler()
def camera(name,location,at,scale):
 data=bpy.data.cameras.new(name); ob=bpy.data.objects.new(name,data); studio.objects.link(ob)
 ob.location=location; point(ob,at); data.type='ORTHO'; data.ortho_scale=scale; data.lens=55; return ob
front=camera('Camera_Front',(0,-7,1.15),(0,0,1.15),2.55)
iso=camera('Camera_ThreeQuarter',(3.4,-6,3.0),(0,0,1.13),2.65)
for name,loc,energy,size in [('Key',(-3,-4,6),450,4),('Fill',(3,-2,3),240,3),('Rim',(1,3,5),500,3)]:
 data=bpy.data.lights.new(name,'AREA'); data.energy=energy; data.shape='DISK'; data.size=size
 ob=bpy.data.objects.new(name,data);studio.objects.link(ob);ob.location=loc;point(ob,(0,0,1.2))
scene=bpy.context.scene; scene.camera=front
scene.world.color=(.35,.35,.35); scene.world.use_nodes=True
bg=scene.world.node_tree.nodes.get('Background'); bg.inputs['Color'].default_value=(.42,.44,.47,1);bg.inputs['Strength'].default_value=.55
scene.render.engine='CYCLES';scene.cycles.samples=40;scene.cycles.use_denoising=True
scene.render.resolution_x=1100;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
scene.view_settings.view_transform='Standard';scene.view_settings.look='Medium High Contrast' if 'Medium High Contrast' in [x.name for x in scene.bl_rna.properties] else 'None'
scene.view_settings.exposure=-.15;scene.view_settings.gamma=1
scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
bpy.context.view_layer.objects.active=character;character.select_set(True)
for ob in bpy.context.selected_objects:
 if ob!=character:ob.select_set(False)
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   area.spaces.active.shading.type='MATERIAL'
   area.spaces.active.region_3d.view_distance=3.3
   area.spaces.active.region_3d.view_location=(0,0,1.15)
   from mathutils import Quaternion
   area.spaces.active.region_3d.view_rotation=Quaternion((1,0,0),math.pi/2)

mesh.calc_loop_triangles()
stats={'objects':1,'vertices':len(mesh.vertices),'faces':len(mesh.polygons),'triangles':len(mesh.loop_triangles),'materials':len(mesh.materials),'texture_size':[N,N],'uv_layers':len(mesh.uv_layers),'zero_area_faces':sum(p.area<1e-10 for p in mesh.polygons),'dimensions_m':list(character.dimensions),'vertex_groups':list(groups),'rigged':False}
with open(os.path.join(TEMP,'model_stats.json'),'w',encoding='utf-8') as f:json.dump(stats,f,ensure_ascii=False,indent=2)
print('MODEL_STATS',json.dumps(stats),flush=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'Cheon_Player.blend'))
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'Cheon_Player.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,path_mode='COPY',embed_textures=True,mesh_smooth_type='FACE',use_mesh_modifiers=True)
for cam,name in ((front,'Front'),(iso,'ThreeQuarter')):
 scene.camera=cam;scene.render.filepath=os.path.join(TEMP,'Preview_'+name+'.png')
 bpy.ops.render.render(write_still=True)
scene.camera=front
im.filepath='//Textures/T_CheonPlayer_BaseColor.png'
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'Cheon_Player.blend'))
print('CHARACTER_BUILD_COMPLETE',flush=True)
