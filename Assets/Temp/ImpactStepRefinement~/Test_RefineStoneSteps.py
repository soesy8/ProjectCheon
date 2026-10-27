from pathlib import Path
import json, wave, shutil
import numpy as np

ROOT = Path(__file__).resolve().parent
AUDIO = ROOT.parents[1] / 'Test/CombatPresentation/Audio'
SOURCES = ROOT.parent / 'FoleyRefinement~/Decoded'
RATE = 48000

def read(path):
    with wave.open(str(path), 'rb') as file:
        assert file.getsampwidth() == 2
        rate = file.getframerate()
        audio = np.frombuffer(file.readframes(file.getnframes()), dtype='<i2').astype(float) / 32768
        audio = audio.reshape(-1, file.getnchannels()).mean(axis=1)
    if rate != RATE:
        audio = np.interp(np.arange(0, len(audio), rate / RATE), np.arange(len(audio)), audio)
    return audio

def write(path, audio):
    with wave.open(str(path), 'wb') as file:
        file.setnchannels(1); file.setsampwidth(2); file.setframerate(RATE)
        file.writeframes((np.clip(audio, -1, 1) * 32767).astype('<i2').tobytes())

def stats(audio):
    spectrum = abs(np.fft.rfft(audio)) ** 2
    frequencies = np.fft.rfftfreq(len(audio), 1 / RATE)
    total = max(float(spectrum.sum()), 1e-12)
    return {'seconds': len(audio) / RATE, 'peak': float(abs(audio).max()),
            'rms': float(np.sqrt(np.mean(audio * audio))),
            'above_2000_hz_energy': float(spectrum[frequencies > 2000].sum() / total),
            'spectral_centroid_hz': float((frequencies * spectrum).sum() / total)}

report = {}
created = []
for i in range(4):
    path = AUDIO / f'Test_FootstepStone_{i+1:02d}.wav'
    backup = ROOT / 'Before' / path.name
    if not backup.exists(): shutil.copy2(path, backup)
    before = read(backup)
    # 쇠처럼 들릴 수 있는 stone01 마찰 레이어를 빼고 기존 콘크리트 접촉음만 쓴다.
    audio = read(SOURCES / f'KenneyImpact_footstep_concrete_{i:03d}.wav')
    active = np.flatnonzero(abs(audio) > max(abs(audio)) * .012)
    if len(active): audio = audio[max(0, active[0] - 120):]
    ratio = .86 + i * .018
    audio = np.interp(np.arange(0, len(audio), ratio), np.arange(len(audio)), audio)
    duration = .24
    length = round(RATE * duration)
    audio = np.pad(audio, (0, max(0, length - len(audio))))[:length]
    n = 1 << int(np.ceil(np.log2(len(audio) * 4)))
    frequencies = np.fft.rfftfreq(n, 1 / RATE)
    # 저역 충격은 남기고, 고음과 저주파 울림을 함께 정리한다.
    response = (1 - 1 / (1 + (frequencies / 55) ** 4)) / (1 + (frequencies / 680) ** 8)
    audio = np.fft.irfft(np.fft.rfft(audio, n) * response, n)[:length]
    time = np.arange(length) / RATE
    audio *= np.exp(-np.maximum(time - .045, 0) / .065)
    attack = round(.003 * RATE); tail = round(.045 * RATE)
    audio[:attack] *= np.linspace(0, 1, attack)
    audio[-tail:] *= np.cos(np.linspace(0, np.pi / 2, tail)) ** 2
    scale = min(.60 / max(abs(audio).max(), 1e-9), .043 / max(np.sqrt(np.mean(audio*audio)), 1e-9))
    audio *= scale
    write(path, audio)
    after = stats(read(path))
    assert after['above_2000_hz_energy'] < .001
    assert after['peak'] <= .601 and after['rms'] <= .044
    report[path.name] = {'before': stats(before), 'after': after}
    created.append(audio)

# 걷기 4걸음 뒤 달리기 8걸음: 배경음악 없이 두 속도를 비교할 수 있는 미리듣기.
preview = np.zeros(RATE * 6)
for step in range(4):
    at = round((.25 + step * .42) * RATE)
    preview[at:at+len(created[step])] += created[step] * .7
for step in range(8):
    at = round((2.4 + step * .28) * RATE)
    sample = created[step % 4]
    preview[at:at+len(sample)] += sample * .95
write(ROOT / 'Test_StoneStepsPreview.wav', preview)
(ROOT / 'Test_StoneStepReport.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(json.dumps(report, indent=2))
