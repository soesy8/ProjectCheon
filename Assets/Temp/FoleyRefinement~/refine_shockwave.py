from pathlib import Path
import numpy as np
import sys, wave, shutil, json
ROOT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT.parent/'TraditionalAudio~'))
from inspect_audio import read_wav
OUT=ROOT.parents[1]/'Test/CombatPresentation/Audio'
RATE=48000
rng=np.random.default_rng(271027)
backup=ROOT/'BeforeShockwave';backup.mkdir(exist_ok=True)

def normalized(x,peak=1): return x*peak/max(float(abs(x).max()),1e-8)
def filtered(x,low,high):
    n=1<<int(np.ceil(np.log2(len(x)*2)))
    frequency=np.fft.rfftfreq(n,1/RATE)
    response=1/(1+(frequency/high)**4)
    if low: response*=1-1/(1+(frequency/low)**4)
    return np.fft.irfft(np.fft.rfft(x,n)*response,n)[:len(x)]
def pressure(length=.55,weight=1):
    t=np.arange(round(length*RATE))/RATE
    onset=1-np.exp(-t/.0012)
    # 着지 충격의 급격한 압력 변화와 공기 파열을 따로 만들고 짧게 감쇠한다.
    phase=2*np.pi*(52*t+108*.022*(1-np.exp(-t/.022)))
    body=np.sin(phase)*np.exp(-t/.105)*onset
    rumble=normalized(filtered(rng.normal(size=len(t)),0,230))*np.exp(-t/.14)*onset
    air=normalized(filtered(rng.normal(size=len(t)),350,6500))*np.exp(-t/.065)*onset
    crack=normalized(filtered(rng.normal(size=len(t)),900,11000))*np.exp(-t/.012)*onset
    result=(body*.65+rumble*.38+air*.36+crack*.2)*weight
    result[-min(len(result),2400):]*=np.linspace(1,0,min(len(result),2400))**2
    return result
def add(x,layer,at,gain):
    start=round(at*RATE);size=min(len(layer),len(x)-start)
    if size>0:x[start:start+size]+=layer[:size]*gain
def write(path,x):
    with wave.open(str(path),'wb') as f:
        f.setnchannels(1);f.setsampwidth(2);f.setframerate(RATE)
        f.writeframes((np.clip(x,-1,1)*32767).astype('<i2').tobytes())

report={}
recipes={
 'Test_GuardianSlam':(.62,[(0,.92)],.88),
 'Test_SpiritAwaken':(.7,[(.4,.62),(.86,.29)],.79),
 'Test_CueHeavy':(.85,[(.23,.23)],.65),
 'Test_CueCrack':(.73,[(0,.35),(.19,.2)],.7),
 'Test_CueJump':(.88,[(0,.14)],.59)
}
for name,(earth_gain,layers,peak) in recipes.items():
    path=OUT/(name+'.wav');base=backup/path.name
    if not base.exists():shutil.copy2(path,base)
    before,rate=read_wav(base);assert rate==RATE
    after=before*earth_gain
    for at,gain in layers:add(after,pressure(),at,gain)
    after-=after.mean()
    after[:48]*=np.linspace(0,1,48);after[-1920:]*=np.linspace(1,0,1920)**2
    after=normalized(after,peak);write(path,after)
    report[name]={'seconds':len(after)/RATE,'peak':float(abs(after).max()),
       'rmsBefore':float(np.sqrt(np.mean(before**2))),'rmsAfter':float(np.sqrt(np.mean(after**2)))}

# 직전 흙 중심 타격 → 수정 타격 → 전조와 타격 → 균열 → 소환 순서.
preview=np.zeros(RATE*12)
old,_=read_wav(backup/'Test_GuardianSlam.wav');add(preview,old,.25,.9)
for name,at in [('Test_GuardianSlam',1.8),('Test_CueJump',3.3),('Test_GuardianSlam',3.95),
                ('Test_CueHeavy',5.4),('Test_GuardianSlam',6.2),('Test_CueCrack',7.5),
                ('Test_GuardianSlam',8.25),('Test_SpiritAwaken',9.7)]:
    x,_=read_wav(OUT/(name+'.wav'));add(preview,x,at,.9)
write(ROOT/'Test_ShockwavePreview.wav',preview)

preview=np.zeros(RATE*20)
def place(name,time,gain=1):
    x,_=read_wav(OUT/(name+'.wav'));add(preview,x,time,gain)
for i in range(4):place(f'Test_FootstepStone_{i+1:02d}',.2+i*.42,.78)
for i in range(4):place(f'Test_FootstepDirt_{i+1:02d}',2.3+i*.42,.78)
for i in range(4):place(f'Test_FootstepDirt_{i+1:02d}',4.5+i*.28,.92)
for name,at in [('Test_DodgeCloth',6.2),('Test_WoodImpact',7.6),('Test_WindImpact',8.4),
 ('Test_CueJump',9.5),('Test_GuardianSlam',10.2),('Test_CueCrack',11.5),('Test_SpiritAwaken',12.5),('Test_ShrineJing',15.7)]:place(name,at)
write(ROOT/'Test_FoleyPreview.wav',preview*.9)
(ROOT/'ShockwaveReport.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
