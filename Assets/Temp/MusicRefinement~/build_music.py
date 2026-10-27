"""두 BGM만 새로 편곡한다. 효과음과 원본 국악 녹음은 변경하지 않는다."""
from pathlib import Path
import sys, math, json, wave
import numpy as np
ROOT=Path(__file__).resolve().parent
SOURCES=ROOT.parent/'TraditionalAudio~'
sys.path.insert(0,str(SOURCES))
from inspect_audio import read_wav, regions
OUT=ROOT.parents[1]/'Test/CombatPresentation/Audio'
RATE=44100
sources={}
for name in ['Gayageum','Daegeum','Janggu1','Janggu2','Janggu3','Jwago']:
    sources[name]=read_wav(SOURCES/'Sources'/(name+'.wav'))
onsets={}

def resample(x,ratio):
    return np.interp(np.arange(0,len(x)-1,ratio),np.arange(len(x)),x).astype(np.float32)
def fade(x,attack=.0015,release=.06):
    x=x.copy();a=min(len(x),round(attack*RATE));b=min(len(x),round(release*RATE))
    if a:
        envelope=np.linspace(0,1,a);x[:a]*=envelope[:,None] if x.ndim==2 else envelope
    if b:
        envelope=np.cos(np.linspace(0,np.pi/2,b))**2;x[-b:]*=envelope[:,None] if x.ndim==2 else envelope
    return x
def normalized(x):
    x=x-x.mean();return x/max(float(abs(x).max()),1e-7)
def cut(name,start,end):
    x,rate=sources[name]
    return resample(x[round(start*rate):round(end*rate)],rate/RATE)
def align(x,key):
    # 원음마다 다른 앞쪽 무음을 제거해 '파일 시작'과 실제 발음을 같은 박에 맞춘다.
    width=round(RATE*.001)
    power=np.convolve(x*x,np.ones(width)/width,mode='same')
    head=power[:min(len(power),round(RATE*.4))]
    active=np.flatnonzero(head>head.max()*.0064)
    start=max(0,int(active[0])-round(RATE*.001)) if len(active) else 0
    onsets[key]=round(start/RATE*1000,2)
    return normalized(fade(x[start:]))

gaya_regions=[(.14,3.5,99.59),(7.94,11.3,134.45),(15.48,19,147.69),
 (22.78,26.3,200),(30.5,34.2,221.2),(38.82,42.1,268.16),
 (45.92,49.2,296.3),(52.38,55.7,338.03),(58.36,61.5,400),
 (64.76,68,444.44),(70.66,73.8,539.33),(77.48,79.95,592.59)]
flute_regions=[(.38,7.5,238.81),(11.04,17.6,275.86),(21,30.22,294.48),
 (32.38,40.98,322.15),(46.12,53.66,358.21),(56.88,63.86,400),
 (68.46,75.98,436.36),(80.4,87.28,480),(92.1,101.44,545.45)]
gaya=[(align(cut('Gayageum',a,b),f'gaya_{i}'),f) for i,(a,b,f) in enumerate(gaya_regions)]
flute=[(align(cut('Daegeum',a,b),f'flute_{i}'),f) for i,(a,b,f) in enumerate(flute_regions)]
def note(midi,length,breath=False):
    hz=440*2**((midi-69)/12)
    x,f=min(flute if breath else gaya,key=lambda item:abs(math.log2(hz/item[1])))
    x=resample(x,hz/f)[:round(length*RATE)]
    return fade(x,.018 if breath else .0015,.18 if breath else .09)

drums={}
for name,length in [('Janggu1',.4),('Janggu2',.32),('Janggu3',.18),('Jwago',.46)]:
    x,rate=sources[name];a,b=regions(x,rate)[0]
    drums[name]=fade(align(cut(name,a,b),name)[:round(length*RATE)],.0008,.055)

def render(bpm,bars,events):
    beat_samples=RATE*60/bpm
    length=round(bars*4*beat_samples)
    stems={key:np.zeros((length,2),np.float32) for key in set(e[0] for e in events)}
    schedule=[]
    for instrument,beat,clip,gain,pan in events:
        position=round(beat*beat_samples)
        stereo=clip[:,None]*gain*np.array([math.sqrt((1-pan)/2),math.sqrt((1+pan)/2)])
        # 마지막 마디의 자연스러운 잔향을 첫 마디 앞으로 연결한다.
        first=min(len(stereo),length-position)
        stems[instrument][position:position+first]+=stereo[:first]
        if first<len(stereo):stems[instrument][:len(stereo)-first]+=stereo[first:]
        schedule.append({'instrument':instrument,'beat':beat,'sample':position})
    return stems,schedule
def room(x,amount):
    # 박자로 오인될 만한 긴 반복 딜레이 없이 짧고 작은 공간감만 더한다.
    out=x.copy()
    for delay,gain in [(.011,.35),(.017,.26),(.023,.19),(.031,.12)]:
        out+=np.roll(x[:,::-1],round(delay*RATE),axis=0)*amount*gain
    return out
def write(path,x):
    with wave.open(str(path),'wb') as f:
        f.setnchannels(x.shape[1] if x.ndim>1 else 1);f.setsampwidth(2);f.setframerate(RATE)
        f.writeframes((np.clip(x,-1,1)*32767).astype('<i2').tobytes())
def master(name,stems,schedule,bpm,peak,wet):
    mix=room(sum(stems.values()),wet)
    mix-=mix.mean(axis=0)
    gain=peak/float(abs(mix).max());mix*=gain
    write(OUT/(name+'.wav'),mix)
    # 검증용 스템과 박자표는 Temp 안에서만 보관한다.
    for instrument,data in stems.items():write(ROOT/(name+'_'+instrument+'.wav'),data*gain)
    length=len(mix)
    report={'bpm':bpm,'meter':'4/4','bars':16,'seconds':length/RATE,'samples':length,
      'peakDb':float(20*np.log10(abs(mix).max())), 'rmsDb':float(20*np.log10(np.sqrt(np.mean(mix**2)))),
      'loopBoundaryDelta':float(abs(mix[-1]-mix[0]).max()),
      'maxSchedulingErrorSamples':max(abs(e['sample']-e['beat']*60/bpm*RATE) for e in schedule),
      'stemMeanSquare':{k:float(np.mean((v*gain)**2)) for k,v in stems.items()},'events':schedule}
    return mix,report

# 일반: D장조 오음음계, 모든 발음을 4분음표 정박에 배치한다.
# 가야금의 짧은 문답 뒤에 대금이 응답하며 장구는 사용하지 않는다.
events=[]
phrases=[ [62,66,69,66], [64,66,69,71], [69,66,64,62], [None]*4,
          [66,69,74,71], [69,66,64,66], [64,62,57,62], [None]*4,
          [62,66,69,74], [71,69,66,64], [66,69,66,62], [None]*4,
          [69,71,74,71], [69,66,64,66], [64,62,57,62], [None]*4 ]
for bar,phrase in enumerate(phrases):
    base=bar*4
    root=50 if bar%8<4 else 47 if bar%8<6 else 50
    events.append(('foundation',base,note(root,1.35),.21,-.12))
    events.append(('foundation',base+2,note(57,1.0),.13,.10))
    for beat,midi in enumerate(phrase):
        if midi is not None:events.append(('gayageum',base+beat,note(midi,1.02),.38 if beat==0 else .30,[-.18,.12,-.08,.18][beat]))
    if bar%4==3:
        a,b=[(69,66),(64,62),(71,69),(64,62)][bar//4]
        events.append(('daegeum',base,note(a,1.28,True),.14,.12))
        events.append(('daegeum',base+2,note(b,1.28,True),.14,.12))
calm,calm_report=master('Test_Exploration_GayageumDaegeum',*render(80,16,events),80,.64,.10)

# 전투: 장구의 덩·쿵·덕을 전면에 두고 8분음표 추진력과 4마디 끝의 짧은 16분음표 필인으로 구성한다.
# 대금, 높은 가야금 선율, 징은 빼고 낮은 현악과 좌고가 장구를 받친다.
events=[]
for bar in range(16):
    base=bar*4
    pattern=[(0,'Janggu1',.82),(.5,'Janggu3',.34),(1,'Janggu2',.66),(1.5,'Janggu3',.5),
             (2,'Janggu1',.76),(2.5,'Janggu3',.36),(3,'Janggu2',.66),(3.5,'Janggu3',.53)]
    if bar%4==2:pattern += [(2.75,'Janggu3',.23)]
    if bar%4==3:pattern += [(3.25,'Janggu3',.34),(3.75,'Janggu3',.43)]
    for beat,key,gain in pattern:
        events.append(('janggu',base+beat,drums[key],gain,-.12 if key=='Janggu3' else .1))
    if bar%2==0:events.append(('jwago',base,drums['Jwago'],.17,0))
    root=[50,50,48,45,50,53,48,45][bar%8]
    for beat,pitch in [(0,root),(2,root+7)]:
        events.append(('lowstrings',base+beat,note(pitch,.48),.11,-.2))
battle,battle_report=master('Test_Combat_JangguJwago',*render(144,16,events),144,.76,.025)

write(ROOT/'Test_ExplorationPreview.wav',fade(calm[:RATE*18],.03,.35))
write(ROOT/'Test_CombatPreview.wav',fade(battle[:RATE*16],.015,.25))
report={'exploration':calm_report,'combat':battle_report,'sourceLeadInRemovedMs':onsets}
(ROOT/'MusicBuildReport.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps({k:{a:b for a,b in v.items() if a!='events'} for k,v in report.items() if k!='sourceLeadInRemovedMs'},indent=2))
