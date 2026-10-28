from PIL import Image, ImageChops, ImageStat
import json
from pathlib import Path

root = Path(__file__).parent
with_cloud = Image.open(root / 'ClimbingFinal.png').convert('RGB')
without_cloud = Image.open(root / 'ClimbingWithoutCloud.png').convert('RGB')
difference = ImageChops.difference(with_cloud, without_cloud)
areas = {'center': (410, 230, 614, 346), 'gourd_opaque_icon': (49, 504, 58, 526),
         'health_fill': (100, 38, 240, 43), 'left_cloud': (10, 170, 190, 260)}
results = {}
for name, box in areas.items():
    stat = ImageStat.Stat(difference.crop(box))
    results[name] = {'mean_rgb_difference': stat.mean, 'maximum_rgb_difference': [x[1] for x in stat.extrema]}
(root / 'PixelComparison.json').write_text(json.dumps(results, indent=2), encoding='utf-8')
print(json.dumps(results, indent=2))
