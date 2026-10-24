import hashlib
import json
from pathlib import Path
import sys

ROOT=Path(__file__).resolve().parents[3]
WORK=Path(__file__).resolve().parent
def read(name):
    return json.loads((WORK/name).read_text(encoding='utf-8-sig'))
def sha(data):
    return hashlib.sha256(data).hexdigest()
def normalize(data):
    return data.replace(b'\r\n',b'\n')

failures=[]
media={row['path']:row for row in read('EmbeddedMediaPlan.json')}
plan=read('ReadWritePlan.json')
for row in plan:
    relative=row['path']
    current=(ROOT/relative).read_bytes()
    original=(WORK/'ReadWriteBackup'/relative).read_bytes()
    meta=Path(str(ROOT/relative)+'.meta').read_bytes()
    old_meta=Path(str(WORK/'ReadWriteBackup'/relative)+'.meta').read_bytes()
    if relative.endswith('.asset'):
        expected=normalize(original).replace(b'  m_IsReadable: 1\n',b'  m_IsReadable: 0\n')
        if normalize(current)!=expected:
            failures.append(relative+': native source changed beyond readability')
        if meta!=old_meta:
            failures.append(relative+': native meta changed')
    else:
        expected_hash=media[relative]['afterHash'] if relative in media else row['sourceHash']
        if sha(current)!=expected_hash:
            failures.append(relative+': FBX source mismatch')
        expected=normalize(old_meta).replace(b'  isReadable: 1\n',b'  isReadable: 0\n')
        if normalize(meta)!=expected:
            failures.append(relative+': importer changed beyond readability')
for relative,row in media.items():
    if sha((ROOT/relative).read_bytes())!=row['afterHash']:
        failures.append(relative+': embedded media source hash mismatch')

before=read('BeforeMeshes.json')
after=read('AfterMeshes.json')
bm={row['key']:row for row in before['meshes']}
am={row['key']:row for row in after['meshes']}
if bm.keys()!=am.keys():
    failures.append('Mesh GUID/local file IDs changed')
for key,row in bm.items():
    if key not in am: continue
    for field in ('path','name','vertices','subMeshes','blendShapes','bufferBytes','geometryHash'):
        if row[field]!=am[key][field]: failures.append(row['path']+': '+field+' changed')
targets={row['path'] for row in plan}
for key,row in am.items():
    if row['path'] in targets and row['readable']:
        failures.append(row['path']+': remains readable')
    elif row['path'] not in targets and row['readable']!=bm[key]['readable']:
        failures.append(row['path']+': protected readability changed')
before_models={row['path']:row for row in before['models']}
for row in after['models']:
    for field in ('shapeHash','materials'):
        if row[field]!=before_models[row['path']][field]: failures.append(row['path']+': '+field+' changed')
report={'passed':not failures,'failures':failures,'embeddedFbxFiles':len(media),
        'embeddedImagesRemoved':sum(len(row['media']) for row in media.values()),
        'sourceBytesSaved':sum(row['beforeBytes']-row['afterBytes'] for row in media.values()),
        'nativeMeshesChanged':sum(row['path'].endswith('.asset') for row in plan),
        'modelImportersChanged':sum(row['path'].endswith('.fbx') for row in plan),
        'verifiedMeshes':len(am),'verifiedModels':len(after['models']),
        'cpuBufferBytesRemoved':sum(row['bufferBytes'] for row in before['meshes'] if row['path'] in targets),
        'editorLoadedMeshBytesBefore':before['loadedMeshBytes'],'editorLoadedMeshBytesAfter':after['loadedMeshBytes']}
(WORK/'Validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
sys.exit(0 if report['passed'] else 1)
