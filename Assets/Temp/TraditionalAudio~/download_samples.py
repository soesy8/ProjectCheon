"""사용자가 승인한 이용정보를 공식 다운로드 양식에 제출한다."""
import http.cookiejar
import json
from pathlib import Path
import urllib.parse
import urllib.request

ROOT = Path(__file__).resolve().parent
BASE = 'https://api.gugak.go.kr'
PAGE = BASE + '/digitaleum/front/monotone/list.do'
SAMPLES = [('Bak',2258),('Janggu1',2283),('Janggu2',2289),('Janggu3',2295),
           ('Jwago',2305),('Jing',2335),('Gayageum',2518),('Daegeum',2557)]
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
opener.addheaders = [('User-Agent','Mozilla/5.0'),('Referer',PAGE)]
with opener.open(PAGE, timeout=40) as response:
    (ROOT / 'download_page.html').write_bytes(response.read())
records = []
for name, identifier in SAMPLES:
    target = ROOT / 'Sources' / (name+'.wav')
    if target.exists() and target.stat().st_size > 44:
        print(name, 'already downloaded', target.stat().st_size, flush=True)
        continue
    fields = {'arrId':'','id':str(identifier),'usePurposeGb':'비상업용',
              'usePurpose':'기타','usePurposeDtl':'개인 개발 게임의 배경음악 및 효과음 제작',
              'companyName':'개인(소속 없음)'}
    body = urllib.parse.urlencode(fields).encode('utf-8')
    request = urllib.request.Request(BASE+'/digitaleum/cmmn/file/monotone/download.do', data=body)
    request.add_header('Content-Type','application/x-www-form-urlencoded; charset=UTF-8')
    with opener.open(request, timeout=60) as response:
        payload = response.read()
        content_type = response.headers.get('Content-Type','')
    if len(payload) < 44 or payload[:4] not in (b'RIFF',b'RF64'):
        raise RuntimeError(f'{name}: audio download failed: {len(payload)} bytes, {content_type}, {payload[:100]!r}')
    target.write_bytes(payload)
    records.append({'sample':name,'id':identifier,'bytes':len(payload),'form':fields})
    (ROOT/'download_receipts.json').write_text(json.dumps(records,ensure_ascii=False,indent=2),encoding='utf-8')
    print(name, identifier, len(payload), content_type, flush=True)
