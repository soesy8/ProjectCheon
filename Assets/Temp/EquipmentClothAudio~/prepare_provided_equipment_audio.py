"""사용자가 제공한 MP3에서 발도·납도에 맞는 천 마찰 구간을 추출한다."""
from pathlib import Path
import json
import sys

import numpy as np

WORK = Path(__file__).resolve().parent
sys.path.insert(0, str(WORK / "Python"))
import soundfile as sf

ROOT = WORK.parents[2]
SOURCE = WORK / "Source/cloth_rustle_martian_19291_preview_hq.mp3"
OUTPUT = ROOT / "Assets/Arts/Audio/MountainPass/Presentation"
samples, rate = sf.read(SOURCE, always_2d=True)
samples = samples.mean(axis=1)


def extract(start, end):
    clip = samples[round(start * rate):round(end * rate)].copy()
    # 원본 음색과 속도를 유지하고 아주 낮은 녹음 잡음만 정리한다.
    clip -= clip.mean()
    padded = np.pad(clip, (4096, 4096))
    frequencies = np.fft.rfftfreq(len(padded), 1 / rate)
    response = frequencies / np.sqrt(frequencies ** 2 + 75 ** 2)
    clip = np.fft.irfft(np.fft.rfft(padded) * response, n=len(padded))[4096:-4096]
    fade_in = round(0.014 * rate)
    fade_out = round(0.060 * rate)
    clip[:fade_in] *= np.sin(np.linspace(0, np.pi / 2, fade_in)) ** 2
    clip[-fade_out:] *= np.cos(np.linspace(0, np.pi / 2, fade_out)) ** 2
    # 두 동작의 평균 음량을 맞추되 원음의 자연스러운 강약은 그대로 둔다.
    rms = np.sqrt(np.mean(clip ** 2))
    gain = min(0.047 / max(rms, 1e-9), 0.28 / max(np.max(np.abs(clip)), 1e-9))
    clip *= gain
    clip[0] = clip[-1] = 0
    return clip


report = []
for filename, start, end in (("ClothDraw.wav", 0.49, 0.92), ("ClothSheathe.wav", 1.09, 1.61)):
    clip = extract(start, end)
    destination = OUTPUT / filename
    sf.write(destination, clip, rate, subtype="PCM_16")
    decoded, decoded_rate = sf.read(destination)
    report.append({
        "file": str(destination.relative_to(ROOT)),
        "source": SOURCE.name,
        "source_start_seconds": start,
        "source_end_seconds": end,
        "seconds": len(decoded) / decoded_rate,
        "sample_rate": decoded_rate,
        "peak_dbfs": 20 * np.log10(np.max(np.abs(decoded))),
        "rms_dbfs": 20 * np.log10(np.sqrt(np.mean(decoded ** 2))),
        "clipped_samples": int(np.count_nonzero(np.abs(decoded) >= 0.9999)),
        "edge_samples": [float(decoded[0]), float(decoded[-1])],
    })

(WORK / "provided_audio_report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=False, indent=2))
