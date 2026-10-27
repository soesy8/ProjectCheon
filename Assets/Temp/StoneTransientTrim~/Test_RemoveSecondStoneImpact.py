from pathlib import Path
import sys, json, wave, hashlib, shutil
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
PROJECT = ROOT.parents[2]
AUDIO = PROJECT / 'Assets/Test/CombatPresentation/Audio'
DECODED = ROOT.parent / 'FoleyRefinement~/Decoded'
sys.path.insert(0, str(ROOT.parent / 'TraditionalAudio~'))
from inspect_audio import read_wav

RATE = 48000
LENGTH = round(.29 * RATE)
APPLY = '--apply' in sys.argv

def speed(x, ratio):
    return np.interp(np.arange(0, len(x), ratio), np.arange(len(x)), x)

def fade(x, attack=.002, release=.035):
    x = x.copy()
    a = min(len(x), int(attack * RATE)); b = min(len(x), int(release * RATE))
    if a: x[:a] *= np.linspace(0, 1, a) ** .7
    if b: x[-b:] *= np.cos(np.linspace(0, np.pi / 2, b)) ** 2
    return x

def source(name):
    x, rate = read_wav(DECODED / (name + '.wav'))
    if rate != RATE: x = speed(x, rate / RATE)
    active = np.flatnonzero(abs(x) > abs(x).max() * .012)
    if len(active): x = x[max(0, active[0] - 120):min(len(x), active[-1] + 1200)]
    x = fade(x)
    return x * (.85 / max(float(abs(x).max()), 1e-8))

def layer(x, offset, gain):
    result = np.zeros(LENGTH)
    start = round(offset * RATE); size = min(len(x), LENGTH - start)
    result[start:start + size] = x[:size] * gain
    return result

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def write(path, samples):
    with wave.open(str(path), 'wb') as stream:
        stream.setnchannels(1); stream.setsampwidth(2); stream.setframerate(RATE)
        stream.writeframes(samples.astype('<i2').tobytes())

before_hashes = {p.name: digest(p) for p in AUDIO.iterdir() if p.is_file()}
stone = source('TinyWorldsSteps_stone01')
report = []; prepared = []
for i in range(4):
    path = AUDIO / f'Test_FootstepStone_{i + 1:02d}.wav'
    with wave.open(str(path), 'rb') as stream:
        assert (stream.getnchannels(), stream.getsampwidth(), stream.getframerate(), stream.getnframes()) == (1, 2, RATE, LENGTH)
        original = np.frombuffer(stream.readframes(LENGTH), dtype='<i2').copy()
    concrete = layer(source(f'KenneyImpact_footstep_concrete_{i:03d}'), 0, .72)
    second = layer(speed(stone, 1.06 + i * .035), .008, .32)
    mixed = fade(concrete + second, .001, .04)
    gain = .67 / max(float(abs(mixed).max()), 1e-8)
    reconstructed = (mixed * gain * 32767).astype('<i2')
    # 원본 생성 결과와 일치할 때만 레이어를 분리한다. 사용자 편집본은 덮어쓰지 않는다.
    assert np.array_equal(reconstructed, original), f'Source reconstruction mismatch: {path.name}'
    # 첫 40ms는 보존하고 6ms 동안 두 번째 돌 레이어만 서서히 없앤다.
    # 콘크리트 접촉음 전체와 그 잔향은 그대로 남는다. 필터·속도·정규화는 적용하지 않는다.
    start = round(.040 * RATE); end = round(.046 * RATE)
    removal = np.ones(LENGTH); removal[:start] = 0
    removal[start:end] = np.sin(np.linspace(0, np.pi / 2, end - start)) ** 2
    removed = fade(second, .001, .04) * gain * 32767 * removal
    edited = np.rint(original.astype(float) - removed).astype('<i2')
    assert np.array_equal(edited[:start], original[:start])
    remaining = fade(concrete, .001, .04) * gain * 32767
    assert np.max(abs(edited[end:].astype(float) - remaining[end:])) <= 1.5
    assert abs(int(edited[-1])) <= 1 and max(abs(edited.astype(int))) <= 32767
    # 두 번째 피크의 감소량을 기록한다.
    region = slice(round(.048 * RATE), round(.070 * RATE))
    report.append({'file': path.name, 'first_40ms_bit_identical': True,
                   'source_reconstruction_identical': True, 'seconds': LENGTH / RATE,
                   'second_peak_before': float(abs(original[region].astype(float)).max() / 32768),
                   'second_peak_after': float(abs(edited[region].astype(float)).max() / 32768),
                   'meta_sha256': before_hashes[path.name + '.meta']})
    prepared.append((path, original, edited))

if APPLY:
    backup = ROOT / 'Before'; backup.mkdir(exist_ok=True)
    for path, original, edited in prepared:
        assert not (backup / path.name).exists(), 'Existing backup: do not apply twice'
        shutil.copy2(path, backup / path.name)
        shutil.copy2(str(path) + '.meta', backup / (path.name + '.meta'))
    for path, original, edited in prepared: write(path, edited)
    changed = [name for name, sha in before_hashes.items() if digest(AUDIO / name) != sha]
    assert sorted(changed) == sorted(path.name for path, _, _ in prepared)
    (ROOT / 'Test_EditReport.json').write_text(json.dumps({'clips': report, 'changed_files': changed}, indent=2), encoding='utf-8')
    # 01~04를 한 번씩 듣는 미리듣기. 재생 템포를 바꾸지 않는다.
    preview = np.zeros(round(2.4 * RATE), dtype='<i2')
    for i, (_, _, edited) in enumerate(prepared):
        at = round((.15 + .55 * i) * RATE); preview[at:at + LENGTH] = edited
    write(ROOT / 'Test_StoneStepsPreview.wav', preview)

# 같은 축척으로 두 번째 피크 제거를 확인하는 파형 그림.
image = Image.new('RGB', (1000, 680), '#17212b'); draw = ImageDraw.Draw(image)
font = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 18)
small = ImageFont.truetype('C:/Windows/Fonts/arial.ttf', 15)
draw.text((25, 15), 'Stone footsteps: original (orange) / edited (teal)', font=font, fill='#f5f6f8')
for row, (path, original, edited) in enumerate(prepared):
    top = 60 + row * 150; baseline = top + 70
    draw.text((25, top), path.stem, font=small, fill='#dbe7ef')
    for ms in range(0, 301, 50):
        x = 210 + ms / 290 * 750
        if x <= 960:
            draw.line((x, top + 25, x, top + 115), fill='#344450')
            draw.text((x - 8, top + 118), str(ms), font=small, fill='#9bafb9')
    draw.line((210, baseline, 960, baseline), fill='#657987')
    for values, color in [(original, '#efaa58'), (edited, '#53d7c1')]:
        for x in range(750):
            segment = values[x * LENGTH // 750:(x + 1) * LENGTH // 750].astype(float) / 32768
            draw.line((210 + x, baseline - segment.max() * 62, 210 + x, baseline - segment.min() * 62), fill=color)
draw.text((918, 657), 'ms', font=small, fill='#9bafb9')
image.save(ROOT / 'Test_StoneWaveformComparison.png')
print(json.dumps({'applied': APPLY, 'clips': report}, indent=2))
