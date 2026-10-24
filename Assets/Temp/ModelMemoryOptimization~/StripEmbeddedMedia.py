"""FBX의 Video/Content만 제거하고 나머지 속성 바이트를 그대로 보존한다."""
import hashlib
import importlib
import json
from pathlib import Path
import struct
import sys
import types

ROOT = Path(__file__).resolve().parents[3]
WORK = Path(__file__).resolve().parent
MAGIC = b'Kaydara FBX Binary  \x00\x1a\x00'
FOOT_ID = bytes.fromhex('fabcab09d0c8d466b176fb831cf7267e')
TAIL_ID = bytes.fromhex('f85a8c6adef5d97eece90ce3758f290b')

def sha(data):
    return hashlib.sha256(data).hexdigest()

class Document:
    def __init__(self, data):
        assert data[:23] == MAGIC
        self.data = data
        self.version = struct.unpack_from('<I', data, 23)[0]
        self.fmt = '<QQQB' if self.version >= 7500 else '<IIIB'
        self.size = struct.calcsize(self.fmt)
        self.zero = bytes(self.size)
        self.nodes, offset = self.children(27, len(data))
        assert data[offset:offset+self.size] == self.zero
        self.footer = data[offset+self.size:]
        self.media = []

    def validate_footer(self):
        assert self.footer[:16] == FOOT_ID
        assert self.footer[16:20] == bytes(4)
        assert self.footer[-16:] == TAIL_ID
        assert self.footer[-136:-16] == bytes(120)
        assert struct.unpack('<I', self.footer[-140:-136])[0] == self.version
        assert not any(self.footer[20:-140])

    def children(self, offset, limit):
        result = []
        while offset + self.size <= limit and self.data[offset:offset+self.size] != self.zero:
            end, count, length, name_len = struct.unpack_from(self.fmt, self.data, offset)
            assert offset < end <= limit
            start = offset+self.size
            name = self.data[start:start+name_len]
            prop_start = start+name_len
            prop_end = prop_start+length
            assert prop_end <= end
            props = self.data[prop_start:prop_end]
            if prop_end == end:
                children, sentinel = [], False
            else:
                children, child_end = self.children(prop_end, end)
                assert self.data[child_end:end] == self.zero
                sentinel = True
            result.append((name, count, props, children, sentinel))
            offset = end
        return result, offset

    def semantic_hash(self):
        digest = hashlib.sha256()
        def visit(nodes, parent=b''):
            for name, count, props, children, sentinel in nodes:
                if parent == b'Video' and name == b'Content':
                    continue
                digest.update(struct.pack('<Q', len(name))+name)
                digest.update(struct.pack('<QQ', count, len(props))+props)
                visit(children, name)
                digest.update(b'END_NODE')
        visit(self.nodes)
        return digest.hexdigest()

    def rewrite(self):
        output = bytearray(self.data[:27])
        def emit(nodes, parent=b''):
            for name, count, props, children, sentinel in nodes:
                if parent == b'Video' and name == b'Content':
                    assert count == 1 and props[0:1] == b'R'
                    length = struct.unpack_from('<I', props, 1)[0]
                    payload = props[5:]
                    assert len(payload) == length and payload.startswith(b'\x89PNG\r\n\x1a\n')
                    self.media.append({'bytes': length, 'hash': sha(payload)})
                    continue
                offset = len(output)
                output.extend(self.zero)
                output.extend(name)
                output.extend(props)
                emit(children, name)
                if sentinel:
                    output.extend(self.zero)
                struct.pack_into(self.fmt, output, offset, len(output), count, len(props), len(name))
        emit(self.nodes)
        if not self.media:
            return self.data
        self.validate_footer()
        output.extend(self.zero)
        output.extend(self.footer[:20])
        padding = ((len(output)+15) & ~15)-len(output)
        output.extend(bytes(padding or 16))
        output.extend(self.footer[-140:])
        return bytes(output)

def independent_parser():
    directory = Path(r'C:\Program Files\Blender Foundation\Blender 5.1\5.1\scripts\addons_core\io_scene_fbx')
    package = types.ModuleType('fbx_audit_standalone')
    package.__path__ = [str(directory)]
    sys.modules[package.__name__] = package
    return importlib.import_module(package.__name__+'.parse_fbx')

def prepare():
    parser = independent_parser()
    inventory = json.loads((ROOT/'Assets/Temp/TextureSourceResize~/Before.json').read_text(encoding='utf-8-sig'))
    # 과거 원본 및 현재 외부 텍스처와 일치하는 내장 이미지만 제거한다.
    def collect_hashes(value):
        result = set()
        if isinstance(value, dict):
            for key, item in value.items():
                if 'hash' in key.lower() and isinstance(item, str) and len(item) == 64:
                    result.add(item.lower())
                result.update(collect_hashes(item))
        elif isinstance(value, list):
            for item in value:
                result.update(collect_hashes(item))
        return result
    known = collect_hashes(inventory)
    known.update(sha(path.read_bytes()) for path in (ROOT/'Assets').rglob('*.png') if 'Temp' not in path.parts)
    records = []
    for path in sorted((ROOT/'Assets').rglob('*')):
        if path.suffix.lower() != '.fbx' or 'Temp' in path.parts:
            continue
        original = path.read_bytes()
        document = Document(original)
        rewritten = document.rewrite()
        if not document.media:
            continue
        assert all(media['hash'] in known for media in document.media), str(path)
        after = Document(rewritten)
        after.rewrite()
        assert not after.media
        assert after.semantic_hash() == document.semantic_hash()
        relative = path.relative_to(ROOT).as_posix()
        backup = WORK/'Backup'/relative
        stage = WORK/'Staged'/relative
        backup.parent.mkdir(parents=True, exist_ok=True)
        stage.parent.mkdir(parents=True, exist_ok=True)
        assert not backup.exists() or backup.read_bytes() == original
        assert not stage.exists() or stage.read_bytes() == rewritten
        backup.write_bytes(original)
        stage.write_bytes(rewritten)
        parser.parse(str(stage))
        records.append({'path': relative, 'beforeBytes': len(original), 'afterBytes': len(rewritten),
                        'beforeHash': sha(original), 'afterHash': sha(rewritten),
                        'metaHash': sha(Path(str(path)+'.meta').read_bytes()),
                        'semanticHash': document.semantic_hash(), 'media': document.media})
    assert len(records) == 11 and sum(len(row['media']) for row in records) == 63
    (WORK/'EmbeddedMediaPlan.json').write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({'files': len(records), 'images': sum(len(row['media']) for row in records),
                      'savedBytes': sum(row['beforeBytes']-row['afterBytes'] for row in records)}))

def apply():
    records = json.loads((WORK/'EmbeddedMediaPlan.json').read_text(encoding='utf-8'))
    for row in records:
        path = ROOT/row['path']
        assert sha(path.read_bytes()) == row['beforeHash']
        assert sha(Path(str(path)+'.meta').read_bytes()) == row['metaHash']
        assert sha((WORK/'Staged'/row['path']).read_bytes()) == row['afterHash']
    for row in records:
        (ROOT/row['path']).write_bytes((WORK/'Staged'/row['path']).read_bytes())
    print(json.dumps({'applied': len(records)}))

if __name__ == '__main__':
    {'prepare': prepare, 'apply': apply}[sys.argv[1]]()
