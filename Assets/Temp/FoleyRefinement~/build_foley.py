from pathlib import Path
import sys, wave, json, shutil
import numpy as np
ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT.parent/'TraditionalAudio~'))
from inspect_audio import read_wav
OUT = ROOT.parents[1]/'Test/CombatPresentation/Audio'
RATE = 48000
rng = np.random.default_rng(271026)
made = {}

def speed(x, ratio):
    return np.interp(np.arange(0,len(x),ratio), np.arange(len(x)), x)
def fade(x, attack=.002, release=.035):
    x=x.copy(); a=min(len(x),int(attack*RATE)); b=min(len(x),int(release*RATE))
    if a: x[:a] *= np.linspace(0,1,a)**.7
    if b: x[-b:] *= np.cos(np.linspace(0,np.pi/2,b))**2
    return x
def norm(x, peak=1):
    return x * (peak/max(float(np.max(np.abs(x))),1e-8))
def source(name):
    x,rate=read_wav(ROOT/'Decoded'/(name+'.wav'))
    if rate != RATE: x=speed(x,rate/RATE)
    active=np.flatnonzero(np.abs(x)>np.max(np.abs(x))*.012)
    if len(active): x=x[max(0,active[0]-120):min(len(x),active[-1]+1200)]
    return norm(fade(x),.85)
def filt(x, low=0, high=20000):
    n=1<<int(np.ceil(np.log2(max(len(x)*2,2))))
    f=np.fft.rfftfreq(n,1/RATE)
    response=1/(1+(f/max(high,1))**4)
    if low: response*=1-1/(1+(f/low)**4)
    return np.fft.irfft(np.fft.rfft(x,n)*response,n)[:len(x)]
def mix(length, *layers):
    x=np.zeros(round(length*RATE))
    for sample,offset,gain in layers:
        start=round(offset*RATE); size=min(len(sample),len(x)-start)
        if size>0: x[start:start+size]+=sample[:size]*gain
    return x
def boom(length=.4):
    t=np.arange(round(length*RATE))/RATE
    return norm(filt(rng.normal(size=len(t)),high=170))*np.exp(-t*15)*np.minimum(t/.004,1)
def save(name,x,peak=.78):
    path=OUT/(name+'.wav')
    old=ROOT/'Before'/path.name
    if path.exists() and not old.exists(): shutil.copy2(path,old)
    x=norm(fade(x, .001, .04),peak)
    write(path,x)
    made[name]=x
def write(path,x):
    with wave.open(str(path),'wb') as f:
        f.setnchannels(1);f.setsampwidth(2);f.setframerate(RATE)
        f.writeframes((np.clip(x,-1,1)*32767).astype('<i2').tobytes())

gravel=source('TinyWorldsSteps_gravel')
stone=source('TinyWorldsSteps_stone01')
cloth1=source('KenneyRPG_cloth1');cloth2=source('KenneyRPG_cloth3')
creak=source('KenneyRPG_creak3')
chop=source('KenneyRPG_chop')
for i in range(4):
    concrete=source(f'KenneyImpact_footstep_concrete_{i:03d}')
    grass=source(f'KenneyImpact_footstep_grass_{i:03d}')
    # 굳은 바닥의 짧은 접촉음과 표면의 마찰을 분리해 변주한다.
    save(f'Test_FootstepStone_{i+1:02d}',mix(.29,(concrete,0,.72),(speed(stone,1.06+i*.035),.008,.32)),.67)
    dirt=mix(.31,(filt(speed(grass,1.4),high=5500),0,.68),(speed(gravel,1.8+i*.07),.012,.45),
             (filt(concrete,high=650),0,.22))
    save(f'Test_FootstepDirt_{i+1:02d}',dirt,.67)
save('Test_Footstep',made['Test_FootstepDirt_01'],.67)

# 몸이 땅에 닿는 중간 구간에 흙 마찰을 모으고, 시작과 끝에는 옷자락을 둔다.
roll=mix(.78,(cloth1,0,.6),(speed(gravel,1.05),.12,.65),
         (filt(source('KenneyImpact_impactSoft_medium_001'),high=1200),.18,.28),
         (speed(cloth2,1.15),.38,.42))
save('Test_DodgeCloth',roll,.79)

# 길게 울리는 나무 타악기 대신 짧은 균열과 여러 개의 작은 파절을 겹친다.
fracture=mix(.46,(filt(chop,low=180,high=11000),0,.75),
             (speed(filt(creak,low=400,high=9500),1.6),.009,.48),
             (source('KenneyImpact_impactWood_light_003'),.031,.45),
             (speed(source('KenneyImpact_impactPlank_medium_002'),1.45),.061,.3),
             (speed(gravel,2.1),.09,.19))
save('Test_WoodImpact',fracture,.86)
save('Test_WindImpact',mix(.59,(fracture,0,.87),(filt(cloth2,high=5000),.025,.25)),.8)

def earth(duration, weight=1, seed=0):
    dry=source(f'KenneyImpact_impactSoft_heavy_{seed%5:03d}')
    grain=mix(duration,(speed(gravel,.73),.015,.67),(speed(gravel,1.4),.13,.34),
              (filt(dry,high=1600),0,.45),(boom(),0,.10*weight))
    for j in range(4):
        particle=source(f'KenneyImpact_footstep_grass_{(seed+j)%5:03d}')
        grain+=mix(duration,(speed(particle,1.8+j*.3),.10+j*.09,.13/(1+j*.2)))
    return grain
save('Test_GuardianSlam',earth(.98,1),.88)
save('Test_CueJump',mix(.48,(speed(gravel,1.55),0,.8),(filt(cloth2,high=2400),.03,.25),(boom(.25),0,.035)),.59)
save('Test_CueHeavy',mix(.68,(speed(gravel,1.2),0,.7),(earth(.4,.4,2),.23,.4)),.65)
save('Test_CueCrack',mix(.76,(earth(.4,.25,1),0,.65),(earth(.4,.3,3),.19,.38),(speed(gravel,1.4),.3,.3)),.65)
save('Test_SpiritAwaken',mix(1.55,(speed(gravel,.55),0,.45),(earth(.8,.5,4),.4,.7),(earth(.6,.35,2),.86,.3)),.75)

# 원래 징의 속도와 음높이를 보존하고 뒤쪽 절반만 덜어 낸다.
jing,rate=read_wav(ROOT/'Before/Test_ShrineJing.wav')
if rate!=RATE: jing=speed(jing,rate/RATE)
jing=jing[:len(jing)//2].copy(); jing=fade(jing,0,.65)
write(OUT/'Test_ShrineJing.wav',jing);made['Test_ShrineJing']=jing

preview=np.zeros(RATE*20)
def place(name,time,gain=1):
    global preview
    preview+=mix(20,(made[name],time,gain))
for i in range(4): place(f'Test_FootstepStone_{i+1:02d}',.2+i*.42,.78)
for i in range(4): place(f'Test_FootstepDirt_{i+1:02d}',2.3+i*.42,.78)
for i in range(4): place(f'Test_FootstepDirt_{i+1:02d}',4.5+i*.28,.92)
place('Test_DodgeCloth',6.2)
place('Test_WoodImpact',7.6);place('Test_WindImpact',8.4)
place('Test_CueJump',9.5);place('Test_GuardianSlam',10.2)
place('Test_CueCrack',11.5);place('Test_SpiritAwaken',12.5)
place('Test_ShrineJing',15.7)
write(ROOT/'Test_FoleyPreview.wav',preview*.9)
report={n:{'seconds':round(len(x)/RATE,4),'peak':round(float(abs(x).max()),4),
 'rms':round(float(np.sqrt(np.mean(x*x))),4),'end':float(x[-1])} for n,x in made.items()}
(ROOT/'AudioBuildReport.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
