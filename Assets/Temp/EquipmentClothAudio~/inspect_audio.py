from importlib.util import find_spec
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).parent / "Python"))

print({name: find_spec(name) is not None for name in ("numpy", "soundfile", "audioread", "pydub", "imageio_ffmpeg")})
try:
    import soundfile as sf
    import numpy as np
    for path in sorted(Path(__file__).parent.glob("Source/OGG/cloth*.ogg")):
        samples, rate = sf.read(path, always_2d=True)
        print(path.name, rate, samples.shape, len(samples) / rate,
              "peak", np.max(np.abs(samples)), "rms", np.sqrt(np.mean(samples ** 2)))
except ImportError as error:
    print(error)
