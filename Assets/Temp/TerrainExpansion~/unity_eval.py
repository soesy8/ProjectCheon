import json
import pathlib
import subprocess
import sys

code = pathlib.Path(sys.argv[1]).read_text(encoding='utf-8-sig')
result = subprocess.run([
    r'C:\Users\MBC\AppData\Local\Unity\bin\unity.exe',
    'command', 'eval', '--caller', 'plugin', '--skill', 'unity-scene-setup',
    '--project-path', r'D:\project\ProjectCheon', '--format', 'json', '--non-interactive',
    '--timeout', '180', '--', '--code', code, '--timeout', '180000',
], capture_output=True, text=True, encoding='utf-8', errors='replace')
try:
    envelope = json.loads(result.stdout)
    if isinstance(envelope.get('data'), dict):
        envelope['data'].pop('parameters', None)
    sys.stdout.buffer.write((json.dumps(envelope, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
except json.JSONDecodeError:
    sys.stdout.buffer.write(result.stdout.encode('utf-8'))
    raise
if result.stderr:
    sys.stderr.buffer.write(result.stderr.encode('utf-8'))
if result.returncode != 0:
    sys.exit(result.returncode)
if not envelope.get('success') or not envelope.get('data', {}).get('result', {}).get('success', True):
    sys.exit(1)
