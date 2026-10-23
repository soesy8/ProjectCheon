import bpy
import json
import math
import numpy as np
from pathlib import Path

OUT = Path(r'D:\project\ProjectCheon\Assets\Temp\CheonPlayerTexture~')
SIZE = 4096
model = bpy.data.objects['Cheon_Player']
mesh = model.data
info = json.loads((OUT / 'atlas_preparation.json').read_text())
regions = info['regions']
palette = {k: np.array(v, dtype=np.float32) for k,v in info['palette'].items()}
palette.update(skin=np.array([.98,.704,.49]), hair=np.array([.28,.145,.075]), robe=np.array([.045,.56,.65]),
               leather=np.array([.43,.225,.095]), sleeve=np.array([.94,.918,.86]))

def smooth(a, b, value):
    v=np.clip((value-a)/(b-a),0,1)
    return v*v*(3-2*v)

def paint(position, region, normal):
    x,y,z=position.T
    ax=np.abs(x)
    count=len(x)
    color=np.tile(palette[region],(count,1)).astype(np.float32)
    rough=np.full(count,.72,np.float32)
    metal=np.zeros(count,np.float32)
    wave=(np.sin(x*37.1+z*13.3)*np.cos(y*43.9-z*19.7)+.45*np.sin(x*113.3+y*79.1+z*83.7))/1.45
    def mix(target, amount):
        nonlocal color
        a=np.clip(amount,0,1)[:,None]
        color=color*(1-a)+np.asarray(target)*a
    def set_color(mask, key):
        color[mask]=palette[key]
    if region in ('robe','sleeve','collar','blue','gold','pants','ribbon'):
        weave=np.sin(x*2400+z*370)*np.sin(z*2200+y*410)
        color*=1+.012*wave[:,None]+.008*weave[:,None]
    if region=='robe':
        # 한복의 깃, 겹섶, 소매는 같은 아틀라스에 직접 그려 재질 슬롯을 추가하지 않는다.
        front=y<.015
        upper=(z>1.13)&(z<1.47)
        collar_line=.49*(z-1.135)
        inner=front&upper&(ax<collar_line-.012)
        trim=front&upper&(np.abs(ax-collar_line)<.024)
        mix(palette['collar'],(front&upper).astype(np.float32)*(1-smooth(collar_line-.013,collar_line-.011,ax)))
        mix(palette['blue'],(front&upper).astype(np.float32)*(1-smooth(.023,.025,np.abs(ax-collar_line))))
        back_collar=(y>=.015)&(z>1.385)&(ax<.13)
        set_color(back_collar,'blue')
        set_color(back_collar&(z>1.43),'collar')
        radius=.132+(1.02-z)*.33
        seam=(z<1.01)&(z>.34)&(y<-.04)&(np.abs(ax-radius*.65)<.011)
        set_color(seam,'blue')
        # 옷자락에는 얇은 봉제선만 두어 기존 실루엣과 저해상도 조형을 보존한다.
        hem=(z<.367)&(z>.35)
        color[hem]*=.91
        rough[:]=.82
    elif region=='sleeve':
        # 소매 끝의 경사진 단면에 평행하도록 파란 끝동 폭을 정의한다.
        cuff_distance=z-(.64+.57*ax)
        cuff=cuff_distance<.115
        mix(palette['blue'],1-smooth(.114,.116,cuff_distance))
        stripe=(cuff_distance>.105)&(cuff_distance<.11)
        color[stripe]*=1.18
        rough[:]=.82
    elif region=='gold':
        color*=1+.045*np.sin(z*7)[:,None]
        weave=np.sin(z*3400)*np.sin(x*2700+y*390)
        color*=1+.012*weave[:,None]
        rough[:]=.71
    elif region=='skin':
        rough[:]=.63
        # 좌우 볼, 귓바퀴, 코의 은은한 색 변화도 UV 텍스처에 기록한다.
        front=smooth(-.085,-.12,y)
        cheek=np.exp(-((ax-.115)/.040)**2-((z-1.557)/.024)**2)*front
        mix([.94,.43,.34],cheek*.23)
        ear=np.exp(-((ax-.195)/.025)**2-((z-1.60)/.046)**2)
        mix([.88,.49,.35],ear*.20)
        nose=np.exp(-(x/.022)**2-((z-1.539)/.016)**2)*front
        mix([.88,.40,.27],nose*.17)
        mouth_z=1.505+4.4*x*x
        line=np.exp(-((z-mouth_z)/.0018)**2)*(1-smooth(.025,.037,ax))*front
        mix([.43,.16,.078],line*.92)
        lip=np.exp(-((z-(mouth_z-.004))/.0022)**2)*(1-smooth(.018,.028,ax))*front
        mix([1,.78,.58],lip*.3)
        color*=1+.008*wave[:,None]
    elif region=='eyes':
        color[:]=[.105,.049,.023]
        rough[:]=.27
        eye_shine=np.exp(-((ax-.074)/.0045)**2-((z-1.624)/.005)**2)
        eye_shine*=smooth(-.129,-.14,y)
        mix([1,.98,.9],eye_shine*.98)
        second=np.exp(-((ax-.086)/.002)**2-((z-1.588)/.002)**2)*smooth(-.128,-.14,y)
        mix([.70,.45,.24],second*.5)
    elif region in ('hair','brows'):
        rough[:]=.71
        broad=.10*np.sin(x*32+y*28+z*13)+.035*wave
        color*=1+broad[:,None]
        if region=='hair':
            tip=.86+.14*smooth(1.53,1.88,z)
            color*=tip[:,None]
            strand=np.sin(x*370+y*310+z*19)
            color*=1+.018*strand[:,None]
    elif region=='hat':
        rough[:]=.66
        color[:]=[.065,.077,.092]
        crown=z>1.89
        angle=np.arctan2(y-.015,x)
        grain=np.sin(angle*330)*np.sin(z*2200)
        color*=1+.026*grain[:,None]+.035*wave[:,None]
        band=(z>1.902)&(z<1.924)&crown
        color[band]*=.72
        radius=np.sqrt(x*x+(y-.015)**2)
        edge=(radius>.475)
        color[edge]*=1.18
    elif region=='ribbon':
        rough[:]=.76
        color[:]=[.052,.063,.078]
        color*=1+.025*wave[:,None]
    elif region=='leather':
        rough[:]=.69
        color*=1+.035*wave[:,None]
        grain=np.sin(x*1427+y*977+z*1733)*np.sin(x*1883-y*831+z*1291)
        color*=1+.009*grain[:,None]
        # 가방의 정면 덮개 가장자리에 얇은 박음질을 텍스처로 추가한다.
        local_x=x-.14
        pouch_front=(y<-.174)&(z>.77)&(z<.998)
        stitch_vertical=(np.abs(np.abs(local_x)-.075)<.0011)&(z>.795)&(z<.945)
        stitch_bottom=(np.abs(z-.795)<.0011)&(np.abs(local_x)<.075)
        dash=(np.sin(z*930)>-.12)&stitch_vertical | ((np.sin(x*930)>-.12)&stitch_bottom)
        mix([.70,.44,.21],(pouch_front&dash).astype(np.float32)*.62)
    elif region=='button':
        color[:]=[.86,.59,.21]
        rough[:]=.35
        metal[:]=.72
    elif region=='pants':
        rough[:]=.85
        ankle=(z>.115)&(z<.18)
        color[ankle]*=.73
    elif region=='shoes':
        rough[:]=.60
        sole=z<-.014
        set_color(sole,'collar')
        ankle=(z>.105)&(z<.133)
        set_color(ankle,'collar')
    return np.clip(color,0,1),rough,metal

mesh.calc_loop_triangles()
positions=np.array([v.co[:] for v in mesh.vertices],np.float32)
uv=np.array([v.uv[:] for v in mesh.uv_layers.active.data],np.float32)
base=np.zeros((SIZE,SIZE,4),np.float32)
roughness=np.zeros((SIZE,SIZE,4),np.float32)
metallic=np.zeros((SIZE,SIZE,4),np.float32)
occupied=np.zeros((SIZE,SIZE),bool)
overlap_pixels=0
names=[m.name.removeprefix('Preview_') for m in mesh.materials]
for index,tri in enumerate(mesh.loop_triangles):
    coords=uv[list(tri.loops)]*SIZE-.5
    minimum=np.maximum(np.floor(coords.min(axis=0)).astype(int),0)
    maximum=np.minimum(np.ceil(coords.max(axis=0)).astype(int),SIZE-1)
    if np.any(maximum<minimum):continue
    xx,yy=np.meshgrid(np.arange(minimum[0],maximum[0]+1,dtype=np.float32),np.arange(minimum[1],maximum[1]+1,dtype=np.float32))
    a,b,c=coords
    denom=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
    if abs(denom)<1e-8:continue
    w0=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/denom
    w1=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/denom
    w2=1-w0-w1
    inside=(w0>=0)&(w1>=0)&(w2>=0)
    py,px=np.nonzero(inside)
    if not len(py):continue
    gx,gy=px+minimum[0],py+minimum[1]
    overlap_pixels+=int(occupied[gy,gx].sum())
    points=w0[inside,None]*positions[tri.vertices[0]]+w1[inside,None]*positions[tri.vertices[1]]+w2[inside,None]*positions[tri.vertices[2]]
    region=names[tri.material_index]
    color,r,m=paint(points,region,tri.normal)
    base[gy,gx,:3]=color
    roughness[gy,gx,:3]=r[:,None]
    metallic[gy,gx,:3]=m[:,None]
    occupied[gy,gx]=True
    if index%2000==0:print('RASTER',index,'/',len(mesh.loop_triangles),flush=True)

coverage=float(occupied.mean())
# UV 아이랜드 바깥으로 12 픽셀을 확장해 밉맵과 경계 샘플링의 번짐을 줄인다.
grown=occupied.copy()
for step in range(12):
    previous=grown.copy()
    for dy,dx in ((1,0),(-1,0),(0,1),(0,-1),(1,1),(-1,-1),(1,-1),(-1,1)):
        valid=np.roll(previous,(dy,dx),(0,1))&~grown
        if dy>0:valid[:dy,:]=False
        if dy<0:valid[dy:,:]=False
        if dx>0:valid[:,:dx]=False
        if dx<0:valid[:,dx:]=False
        ty,tx=np.nonzero(valid)
        for buffer in (base,roughness,metallic):
            buffer[ty,tx,:3]=buffer[ty-dy,tx-dx,:3]
        grown[valid]=True
for buffer in (base,roughness,metallic):buffer[:,:,3]=1
base[~grown,:3]=[.06,.075,.09]
roughness[~grown,:3]=.8

def save_texture(name,buffer,srgb):
    image=bpy.data.images.new(name,width=SIZE,height=SIZE,alpha=True,float_buffer=False)
    image.colorspace_settings.name='sRGB' if srgb else 'Non-Color'
    # 바이트 이미지의 픽셀 배열에는 해당 이미지 색공간의 값을 그대로 기록한다.
    image.pixels.foreach_set(buffer.ravel())
    image.update()
    image.filepath_raw=str(OUT/(name+'.png'))
    image.file_format='PNG'
    image.save()
    image.pack()
    return image

color_image=save_texture('T_Cheon_Player_BaseColor',base,True)
rough_image=save_texture('T_Cheon_Player_Roughness',roughness,False)
metal_image=save_texture('T_Cheon_Player_Metallic',metallic,False)
material=bpy.data.materials.new('M_Cheon_Player')
material.use_nodes=True
material.diffuse_color=(.035,.28,.36,1)
tree=material.node_tree
tree.nodes.clear()
output=tree.nodes.new('ShaderNodeOutputMaterial')
output.location=(650,60)
shader=tree.nodes.new('ShaderNodeBsdfPrincipled')
shader.location=(320,60)
tree.links.new(shader.outputs['BSDF'],output.inputs['Surface'])
uv_node=tree.nodes.new('ShaderNodeUVMap')
uv_node.uv_map='UVMap'
uv_node.location=(-620,80)
for index,(im,socket) in enumerate([(color_image,'Base Color'),(rough_image,'Roughness'),(metal_image,'Metallic')]):
    node=tree.nodes.new('ShaderNodeTexImage')
    node.image=im
    node.label=socket
    node.location=(-360,300-index*270)
    node.interpolation='Linear'
    node.extension='EXTEND'
    tree.links.new(uv_node.outputs['UV'],node.inputs['Vector'])
    tree.links.new(node.outputs['Color'],shader.inputs[socket])
    if index==0:tree.nodes.active=node
mesh.materials.clear()
mesh.materials.append(material)
for face in mesh.polygons:face.material_index=0
for attr in list(mesh.color_attributes):mesh.color_attributes.remove(attr)
attr=mesh.attributes.get('surface_region')
if attr:mesh.attributes.remove(attr)
for mat in list(bpy.data.materials):
    if mat!=material:bpy.data.materials.remove(mat)
for image in list(bpy.data.images):
    if image not in (color_image,rough_image,metal_image) and image.name not in ('Render Result','Viewer Node'):
        bpy.data.images.remove(image)
scene=bpy.context.scene
scene.unit_settings.system='METRIC'
scene.unit_settings.scale_length=1.0
scene.render.engine='CYCLES'
scene.cycles.samples=32
scene.view_settings.view_transform='AgX'
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.type='MATERIAL'
            area.spaces.active.overlay.show_overlays=False
            area.spaces.active.region_3d.view_distance=3.6
            area.spaces.active.region_3d.view_location=(0,0,1.05)
model['texture_workflow']='One mesh / One material / One non-overlapping UVMap / Image textures / No vertex colors'
model['source_parts']='Body, Hair, Gat, GatStrap, GatRibbon, GatRibbonParts, GatStrapTail, BeltTail, Pouch'
model['texture_resolution']=SIZE
bpy.context.view_layer.objects.active=model
model.select_set(True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Cheon_Player_Textured.blend'))
report={'vertices':len(mesh.vertices),'polygons':len(mesh.polygons),'triangles':len(mesh.loop_triangles),'materials':len(mesh.materials),'uv_maps':len(mesh.uv_layers),'color_attributes':len(mesh.color_attributes),'atlas_resolution':SIZE,'uv_coverage':coverage,'overlap_pixel_count':overlap_pixels,'image_names':[i.name for i in (color_image,rough_image,metal_image)]}
(OUT/'texture_report.json').write_text(json.dumps(report,indent=2))
print('TEXTURE_COMPLETE',json.dumps(report),flush=True)
