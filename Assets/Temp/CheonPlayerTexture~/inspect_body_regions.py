import bpy
import json
from pathlib import Path

obj=bpy.data.objects['Body']
mesh=obj.data
data={'vertices':[list(obj.matrix_world@v.co) for v in mesh.vertices], 'faces':[]}
for p in mesh.polygons:
    data['faces'].append({'id':p.index,'v':list(p.vertices),'c':list(obj.matrix_world@p.center),'n':list(p.normal)})
Path(r'D:\project\ProjectCheon\Assets\Temp\CheonPlayerTexture~\body_geometry.json').write_text(json.dumps(data))
for start in range(0,500,20):
    fs=mesh.polygons[start:start+20]
    print(start, 'to', start+19, 'bounds', [[round(min((obj.matrix_world@mesh.vertices[v].co)[k] for p in fs for v in p.vertices),4),round(max((obj.matrix_world@mesh.vertices[v].co)[k] for p in fs for v in p.vertices),4)] for k in range(3)])
print('SEAM_COUNT',sum(e.use_seam for e in mesh.edges))
