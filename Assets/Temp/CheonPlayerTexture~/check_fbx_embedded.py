import hashlib
import json
from pathlib import Path
from io_scene_fbx import parse_fbx

out=Path(r'D:\project\ProjectCheon\Assets\Temp\CheonPlayerTexture~')
root,version=parse_fbx.parse(str(out/'Cheon_Player_Textured.fbx'))
report=[]
for section in root.elems:
    if section.id!=b'Objects':continue
    for element in section.elems:
        if element.id!=b'Video':continue
        children={child.id:child for child in element.elems}
        filename=children[b'Filename'].props[0].decode()
        relative=children[b'RelativeFilename'].props[0].decode()
        content=children[b'Content'].props[0]
        texture=out/Path(filename).name
        digest=hashlib.sha256(content).hexdigest()
        assert digest==hashlib.sha256(texture.read_bytes()).hexdigest()
        report.append({'filename':filename,'relative':relative,'embedded_bytes':len(content),'sha256':digest})
assert len(report)==3
(out/'embedded_texture_validation.json').write_text(json.dumps(report,indent=2))
print('EMBEDDED_TEXTURES_VERIFIED',json.dumps(report))
