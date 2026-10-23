import bpy
import numpy as np
import json
from pathlib import Path

out=Path(r'D:\project\ProjectCheon\Assets\Temp\CheonPlayerTexture~')
mesh=bpy.data.objects['Cheon_Player'].data
mesh.calc_loop_triangles()
size=2048
uv=np.array([v.uv[:] for v in mesh.uv_layers.active.data],np.float64)
owner=np.full((size,size),-1,np.int32)
pairs={}
for ti,tri in enumerate(mesh.loop_triangles):
    coords=uv[list(tri.loops)]*size-.5
    minimum=np.maximum(np.floor(coords.min(axis=0)).astype(int),0)
    maximum=np.minimum(np.ceil(coords.max(axis=0)).astype(int),size-1)
    if np.any(maximum<minimum):continue
    xx,yy=np.meshgrid(np.arange(minimum[0],maximum[0]+1),np.arange(minimum[1],maximum[1]+1))
    a,b,c=coords
    denom=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
    if abs(denom)<1e-10:continue
    w0=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/denom
    w1=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/denom
    w2=1-w0-w1
    inside=(w0>.0001)&(w1>.0001)&(w2>.0001)
    py,px=np.nonzero(inside)
    gx,gy=px+minimum[0],py+minimum[1]
    previous=owner[gy,gx]
    for other,count in zip(*np.unique(previous[previous>=0],return_counts=True)):
        key=tuple(sorted((mesh.loop_triangles[int(other)].polygon_index,tri.polygon_index)))
        pairs[key]=pairs.get(key,0)+int(count)
    owner[gy,gx]=ti
bad=sorted(set(p for pair in pairs for p in pair))
report={'overlap_pixels':sum(pairs.values()),'pairs':[[list(pair),count] for pair,count in sorted(pairs.items(),key=lambda item:-item[1])], 'bad_faces':bad,
        'faces':[{'index':i,'center':list(mesh.polygons[i].center),'material':mesh.materials[mesh.polygons[i].material_index].name} for i in bad]}
(out/'uv_overlap_report.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report)[:10000])
