import bpy
import json
import math
from pathlib import Path

out=Path(r'D:\project\ProjectCheon\Assets\Temp\CheonPlayerTexture~')
mesh=bpy.data.objects['Cheon_Player'].data
mesh.calc_loop_triangles()
uv=mesh.uv_layers.active.data
triangles=[]
bins={}

def cross(a,b):return a[0]*b[1]-a[1]*b[0]
def sub(a,b):return (a[0]-b[0],a[1]-b[1])
def area(poly):return abs(sum(cross(a,b) for a,b in zip(poly,poly[1:]+poly[:1])))*.5
def intersection_area(subject,clip):
    result=subject[:]
    orientation=1 if cross(sub(clip[1],clip[0]),sub(clip[2],clip[0]))>0 else -1
    for a,b in zip(clip,clip[1:]+clip[:1]):
        points=result
        result=[]
        if not points:return 0
        edge=sub(b,a)
        prev=points[-1]
        prev_dist=cross(edge,sub(prev,a))*orientation
        for current in points:
            dist=cross(edge,sub(current,a))*orientation
            if (dist>=0)!=(prev_dist>=0):
                t=prev_dist/(prev_dist-dist)
                result.append((prev[0]+t*(current[0]-prev[0]),prev[1]+t*(current[1]-prev[1])))
            if dist>=0:result.append(current)
            prev,prev_dist=current,dist
    return area(result) if len(result)>2 else 0

for ti,tri in enumerate(mesh.loop_triangles):
    points=[tuple(uv[i].uv) for i in tri.loops]
    low=tuple(min(p[k] for p in points) for k in (0,1))
    high=tuple(max(p[k] for p in points) for k in (0,1))
    triangles.append((points,low,high,tri.polygon_index))
    for x in range(math.floor(low[0]*64),math.floor(high[0]*64)+1):
        for y in range(math.floor(low[1]*64),math.floor(high[1]*64)+1):bins.setdefault((x,y),[]).append(ti)
seen=set()
pairs={}
for indices in bins.values():
    for ai,i in enumerate(indices):
        a,al,ah,af=triangles[i]
        for j in indices[ai+1:]:
            key=(i,j)
            if key in seen:continue
            seen.add(key)
            b,bl,bh,bf=triangles[j]
            if ah[0]<=bl[0] or bh[0]<=al[0] or ah[1]<=bl[1] or bh[1]<=al[1]:continue
            overlap=intersection_area(a,b)
            if overlap>1e-12:
                pair=tuple(sorted((af,bf)))
                pairs[pair]=pairs.get(pair,0)+overlap
bad=sorted(set(f for pair in pairs for f in pair))
report={'overlap_area':sum(pairs.values()),'pairs':[[list(pair),value] for pair,value in pairs.items()],'bad_faces':bad}
(out/'uv_overlap_report.json').write_text(json.dumps(report,indent=2))
print('EXACT_UV_CHECK',json.dumps(report),flush=True)
