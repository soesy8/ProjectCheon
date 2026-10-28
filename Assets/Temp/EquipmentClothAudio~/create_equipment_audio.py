"""CC0 천 원음을 짧은 발도·납도 폴리로 편집한다."""
from pathlib import Path
import json
import sys

import numpy as np

WORK = Path(__file__).resolve().parent
sys.path.insert(0, str(WORK / "Python"))
import soundfile as sf

ROOT = WORK.parents[2]
OUTPUT = ROOT / "Assets/Arts/Audio/MountainPass/Presentation"
RATE = 44100


def read_cloth(name, speed=1.0):
    samples, rate = sf.read(WORK / "Source/OGG" / name, always_2d=True)
    samples = samples.mean(axis=1)
    # 무음만 정리하고 작은 옷감 마찰의 시작과 끝은 남긴다.
    window = max(1, round(rate * 0.005))
    energy = np.sqrt(np.convolve(samples ** 2, np.ones(window) / window, "same"))
    active = np.flatnonzero(energy > energy.max() * 0.025)
    samples = samples[max(0, active[0] - window):min(len(samples), active[-1] + window)]
    positions = np.arange(round(len(samples) * RATE / rate / speed)) * rate * speed / RATE
    return np.interp(positions, np.arange(len(samples)), samples)


def mix(duration, layers):
    result = np.zeros(round(duration * RATE))
    for name, speed, offset, gain in layers:
        samples = read_cloth(name, speed)
        start = round(offset * RATE)
        count = min(len(samples), len(result) - start)
        result[start:start + count] += samples[:count] * gain
    # 옷을 쥐는 둔탁한 접촉음은 빼고 얇은 천의 가벼운 마찰을 남긴다.
    padding = 4096
    padded = np.pad(result, (padding, padding))
    frequencies = np.fft.rfftfreq(len(padded), 1 / RATE)
    highpass = 1 / np.sqrt(1 + (620 / np.maximum(frequencies, 1)) ** 8)
    lowpass = 1 / np.sqrt(1 + (frequencies / 6200) ** 8)
    body_dip = 1 - 0.3 * np.exp(-0.5 * (np.log2(np.maximum(frequencies, 1) / 1050) / 0.6) ** 2)
    result = np.fft.irfft(np.fft.rfft(padded) * highpass * lowpass * body_dip, n=len(padded))[padding:-padding]
    # 빠른 접촉 피크만 완만하게 줄인다. 포화 처리는 거친 마찰감을 더하므로 쓰지 않는다.
    fast_window = round(0.003 * RATE)
    slow_window = round(0.028 * RATE)
    fast = np.sqrt(np.convolve(result ** 2, np.ones(fast_window) / fast_window, "same"))
    slow = np.sqrt(np.convolve(result ** 2, np.ones(slow_window) / slow_window, "same"))
    gain = np.minimum(1, 1.15 * slow / np.maximum(fast, 1e-8))
    gain = np.convolve(np.pad(gain, (fast_window, fast_window), mode="edge"),
                       np.ones(fast_window) / fast_window, "same")[fast_window:-fast_window]
    result *= gain
    # 원음의 짧은 딱딱한 접촉이 음량 기준을 잡지 않도록 앞뒤를 보며 피크를 감쇠한다.
    peak_window = round(0.004 * RATE) | 1
    padded_peaks = np.pad(np.abs(result), (peak_window // 2, peak_window // 2), mode="edge")
    peaks = np.lib.stride_tricks.sliding_window_view(padded_peaks, peak_window).max(axis=1)
    threshold = np.sqrt(np.mean(result ** 2)) * 2.8
    peak_gain = np.minimum(1, threshold / np.maximum(peaks, 1e-8))
    peak_gain = np.convolve(np.pad(peak_gain, (fast_window, fast_window), mode="edge"),
                            np.ones(fast_window) / fast_window, "same")[fast_window:-fast_window]
    result *= peak_gain
    result *= min(0.049 / max(np.sqrt(np.mean(result ** 2)), 1e-8),
                  0.30 / max(np.max(np.abs(result)), 1e-8))
    fade_in = round(0.028 * RATE)
    fade_out = round(0.095 * RATE)
    result[:fade_in] *= np.sin(np.linspace(0, np.pi / 2, fade_in)) ** 2
    result[-fade_out:] *= np.cos(np.linspace(0, np.pi / 2, fade_out)) ** 2
    result[0] = result[-1] = 0
    return result


def describe(path):
    samples, rate = sf.read(path, always_2d=True)
    return {
        "file": str(path.relative_to(ROOT)),
        "seconds": round(len(samples) / rate, 4),
        "sample_rate": rate,
        "channels": samples.shape[1],
        "peak_dbfs": round(20 * np.log10(max(np.max(np.abs(samples)), 1e-12)), 2),
        "rms_dbfs": round(20 * np.log10(max(np.sqrt(np.mean(samples ** 2)), 1e-12)), 2),
        "clipped_samples": int(np.count_nonzero(np.abs(samples) >= 0.9999)),
        "edge_samples": [float(samples[0, 0]), float(samples[-1, 0])],
    }


clips = {
    "ClothDraw.wav": mix(0.43, [
        ("cloth4.ogg", 1.28, 0.0, 1.0),
        ("cloth2.ogg", 1.30, 0.035, 0.20),
    ]),
    "ClothSheathe.wav": mix(0.52, [
        ("cloth4.ogg", 1.12, 0.0, 1.0),
        ("cloth3.ogg", 1.20, 0.075, 0.22),
    ]),
}
for name, samples in clips.items():
    sf.write(OUTPUT / name, samples, RATE, subtype="PCM_16")

report = [describe(OUTPUT / name) for name in clips]
report.append(describe(OUTPUT / "FanEquip.wav"))
(WORK / "audio_report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=False, indent=2))
