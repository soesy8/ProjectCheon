from pathlib import Path
import json
import sys

import numpy as np

WORK = Path(__file__).resolve().parent
sys.path.insert(0, str(WORK / "Python"))
import soundfile as sf

path = WORK / "Source/cloth_rustle_martian_19291_preview_hq.mp3"
samples, rate = sf.read(path, always_2d=True)
mono = samples.mean(axis=1)
frame = round(rate * 0.05)
levels = []
for start in range(0, len(mono), frame):
    block = mono[start:start + frame]
    levels.append(round(20 * np.log10(max(np.sqrt(np.mean(block ** 2)), 1e-9)), 1))
print(json.dumps({"seconds": len(samples) / rate, "sample_rate": rate, "channels": samples.shape[1],
                  "peak_db": 20 * np.log10(np.max(np.abs(samples))),
                  "frame_seconds": 0.05, "rms_db_per_frame": levels}, indent=2))
