import hashlib
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[3]
WORK=Path(__file__).resolve().parent
before=json.loads((WORK/'BeforeMeshes.json').read_text(encoding='utf-8-sig'))
protected={'Assets/Arts/Environment/CheonNoiseMap/GraniteBoulder.asset', 'Assets/Arts/Environment/SelfModeling/Models/Wall_End.fbx', 'Assets/Arts/Environment/SelfModeling/Models/Wall_Middle.fbx', 'Assets/Test/CheonMountainMap/Models/Test_CheonMountainMap.fbx'}
paths={row['path'] for row in before['meshes'] if row['path'].endswith('.asset') and row['readable'] and row['path'] not in protected}
paths.update(row['path'] for row in before['models'] if row['readable'] and row['path'] not in protected)
records=[]
for relative in sorted(paths):
    path=ROOT/relative
    data=path.read_bytes()
    meta=Path(str(path)+'.meta').read_bytes()
    backup=WORK/'ReadWriteBackup'/relative
    backup.parent.mkdir(parents=True,exist_ok=True)
    assert not backup.exists()
    backup.write_bytes(data)
    Path(str(backup)+'.meta').write_bytes(meta)
    records.append({'path':relative,'sourceHash':hashlib.sha256(data).hexdigest(),'metaHash':hashlib.sha256(meta).hexdigest()})
(WORK/'ReadWritePlan.json').write_text(json.dumps(records,indent=2),encoding='utf-8')
print(json.dumps({'targets':len(records),'protected':sorted(protected)}))
