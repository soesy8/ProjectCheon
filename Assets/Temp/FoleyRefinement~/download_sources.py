from pathlib import Path
import urllib.request, zipfile, json
root = Path(__file__).resolve().parent
sources = {
 'KenneyRPG': 'https://kenney.nl/media/pages/assets/rpg-audio/8e99002d76-1677590336/kenney_rpg-audio.zip',
 'KenneyImpact': 'https://kenney.nl/media/pages/assets/impact-sounds/87b4ddecda-1677589768/kenney_impact-sounds.zip',
 'TinyWorldsSteps': 'https://opengameart.org/sites/default/files/%5Bkdd%5DDifferentSteps_0.zip'
}
dest = root.parent / 'FoleyRefinementSources'
dest.mkdir(exist_ok=True)
for pack, url in sources.items():
 archive = root / (pack + '.zip')
 if not archive.exists():
  req = urllib.request.Request(url, headers={'User-Agent':'Mozilla/5.0'})
  with urllib.request.urlopen(req, timeout=60) as r: archive.write_bytes(r.read())
 with zipfile.ZipFile(archive) as z:
  names = [n for n in z.namelist() if n.lower().endswith(('.ogg','.wav'))]
  print(pack, json.dumps(names))
  for n in names:
   if pack == 'KenneyRPG' and not any(t in n.lower() for t in ['cloth','creak','chop','leather','footstep']): continue
   if pack == 'KenneyImpact' and not any(t in n.lower() for t in ['wood','soft','gravel','grass','concrete','plank','generic']): continue
   target = dest / (pack + '_' + Path(n).name)
   target.write_bytes(z.read(n))
