"""국립국악원 단음으로 테스트용 음악과 효과음을 편곡한다. 원본 파일은 수정하지 않는다."""
import json, math, wave
from pathlib import Path
import numpy as np
from inspect_audio import read_wav, regions

ROOT=Path(__file__).resolve().parent
PROJECT=ROOT.parents[2]
DEST=PROJECT/'Assets/Test/CombatPresentation/Audio'
DEST.mkdir(parents=True,exist_ok=True)
RATE=44100
RNG=np.random.default_rng(10427)
report={}

def resample(x,ratio):
    return np.interp(np.arange(0,len(x)-1,ratio),np.arange(len(x)),x).astype(np.float32)

def edge(x,attack=.003,release=.06):
    x=x.copy(); a=min(len(x),int(attack*RATE)); b=min(len(x),int(release*RATE))
    if a: x[:a]*=np.linspace(0,1,a)
    if b: x[-b:]*=np.linspace(1,0,b)
    return x

def normal(x,peak=.8):
    x=x-np.mean(x)
    return x*(peak/max(.00001,float(np.max(np.abs(x)))))

sources={}
for name in ['Bak','Janggu1','Janggu2','Janggu3','Jwago','Jing','Gayageum','Daegeum']:
    x,rate=read_wav(ROOT/'Sources'/(name+'.wav'))
    sources[name]=(x,rate)

def cut(name,start,end):
    x,rate=sources[name]; x=x[int(start*rate):int(end*rate)]
    return normal(resample(x,rate/RATE))

drums={}
for name in ['Bak','Janggu1','Janggu2','Janggu3','Jwago','Jing']:
    x,rate=sources[name]; a,b=regions(x,rate)[0]
    drums[name]=edge(cut(name,max(0,a-.015),min(len(x)/rate,b+.32)),.001,.12)

# 음계 전체 녹음에서 개별 발음과 자연스러운 잔향을 추출한다.
gaya_regions=[(.14,3.5,99.59),(7.94,11.3,134.45),(15.48,19,147.69),
              (22.78,26.3,200),(30.5,34.2,221.2),(38.82,42.1,268.16),
              (45.92,49.2,296.3),(52.38,55.7,338.03),(58.36,61.5,400),
              (64.76,68,444.44),(70.66,73.8,539.33),(77.48,79.95,592.59)]
flute_regions=[(.38,7.5,238.81),(11.04,17.6,275.86),(21,30.22,294.48),
               (32.38,40.98,322.15),(46.12,53.66,358.21),(56.88,63.86,400),
               (68.46,75.98,436.36),(80.4,87.28,480),(92.1,101.44,545.45)]
gaya_bank=[(cut('Gayageum',a,b),f) for a,b,f in gaya_regions]
flute_bank=[(cut('Daegeum',a,b),f) for a,b,f in flute_regions]

def note(midi,flute=False,duration=2):
    hz=440*2**((midi-69)/12)
    bank=flute_bank if flute else gaya_bank
    x,f=min(bank,key=lambda item:abs(math.log2(hz/item[1])))
    y=resample(x,hz/f)
    if flute: return edge(y[:int(duration*RATE)],.12,.35)
    return edge(y,.002,.18)

def mix(duration,events,stereo=False,loop=False):
    length=round(duration*RATE)
    output=np.zeros((length,2) if stereo else length,dtype=np.float32)
    for time,clip,gain,pan in events:
        data=clip*gain
        if stereo: data=data[:,None]*np.array([math.sqrt((1-pan)/2),math.sqrt((1+pan)/2)])
        start=round(time*RATE)
        if loop:
            for offset in range(0,len(data),length):
                block=data[offset:offset+length]; index=(start+offset)%length
                first=min(len(block),length-index)
                output[index:index+first]+=block[:first]
                if first<len(block): output[:len(block)-first]+=block[first:]
        elif start<length:
            count=min(len(data),length-start); output[start:start+count]+=data[:count]
    return output

def room(x,loop=False,wet=.12):
    result=x.copy()
    for delay,level in [(.071,.5),(.113,.35),(.179,.24),(.283,.17),(.421,.1)]:
        shift=round(delay*RATE)
        tail=np.roll(x[:,::-1] if x.ndim==2 else x,shift,axis=0)
        if not loop: tail[:shift]=0
        result+=tail*(wet*level)
    return result

def save(name,x,peak=.75,loop=False):
    x=x-np.mean(x,axis=0)
    x=x*(peak/max(.0001,float(np.max(np.abs(x)))))
    if loop:
        # 마지막 8ms만 첫 샘플로 수렴시켜 반복 경계의 클릭을 억제한다.
        n=int(.008*RATE); ramp=np.linspace(0,1,n,dtype=np.float32)
        if x.ndim==2: ramp=ramp[:,None]
        x[-n:]=x[-n:]*(1-ramp)+x[0]*ramp
    else:
        n=min(len(x),int(.012*RATE)); ramp=np.linspace(1,0,n)
        x[-n:]*=ramp[:,None] if x.ndim==2 else ramp
    path=DEST/(name+'.wav')
    with wave.open(str(path),'wb') as w:
        w.setnchannels(2 if x.ndim==2 else 1);w.setsampwidth(2);w.setframerate(RATE)
        w.writeframes((np.clip(x,-1,1)*32767).astype('<i2').tobytes())
    report[name]={'seconds':round(len(x)/RATE,3),'peakDb':round(float(20*np.log10(np.max(np.abs(x)))),2),
                  'rmsDb':round(float(20*np.log10(np.sqrt(np.mean(x*x)))),2),'channels':2 if x.ndim==2 else 1,
                  'loopBoundaryDelta':float(np.max(np.abs(x[-1]-x[0]))) if loop else None}
    return x

def original(name):
    x,r=read_wav(PROJECT/'Assets/Arts/Audio/MountainPass'/(name+'.wav'))
    return resample(x,r/RATE)

# 산길: 6/8의 느린 호흡, 8마디 문답 두 번. 대금은 빈 마디 사이만 들어온다.
events=[]; pulse=60/144; bar=6*pulse
phrases=[[60,67,70,67],[63,65,67],[58,65,67,65],[60,63,67],
         [65,70,72,70],[63,67,70],[58,65,67],[60,67,60]]
for i in range(16):
    phrase=phrases[i%8]; base=i*bar
    events.append((base,note(48 if i%4!=2 else 46),.27,-.2))
    for j,midi in enumerate(phrase):
        time=base+[0,1.5,3,4.5][j]*pulse
        if i>=8 and j==1: time+=.25*pulse
        events.append((time,note(midi),.26 if j else .36,(-.28 if j%2==0 else .25)))
    if i in [1,5,9,13]:
        events.append((base+.5*pulse,note([67,70,65,67][[1,5,9,13].index(i)],True,2.2),.18,.12))
    if i%4==0: events.append((base,drums['Janggu2'],.10,-.05))
exploration=save('Test_Exploration_GayageumDaegeum',room(mix(16*bar,events,True,True),True,.22),.62,True)

# 전투: 12박 순환을 응용한 현대 게임용 리듬. 경고음과 충돌음을 위한 여백을 남긴다.
events=[]; pulse=60/216; bar=12*pulse
for i in range(12):
    base=i*bar
    for beat,key,gain in [(0,'Janggu1',.47),(3,'Janggu3',.27),(5,'Janggu3',.14),
                          (6,'Janggu2',.42),(9,'Janggu3',.33),(10.5,'Janggu3',.16)]:
        events.append((base+beat*pulse,drums[key],gain*(-.04+RNG.random()*.08+1),-.12 if key=='Janggu3' else .1))
    events.extend([(base,drums['Jwago'],.38,0),(base+6*pulse,drums['Jwago'],.26,0)])
    root=[48,48,51,46][i%4]
    for beat,midi,gain in [(0,root,.38),(2,root+12,.21),(4.5,root+7,.30),
                           (6,root+12,.31),(8,root+19,.21),(10,root+12,.24)]:
        events.append((base+beat*pulse,note(midi),gain,-.24))
    if i%4==3:
        for beat in [10,10.5,11,11.5]: events.append((base+beat*pulse,drums['Janggu3'],.13+(beat-10)*.025,.2))
    if i in [2,6,10]: events.append((base+3*pulse,note(67 if i!=6 else 70,True,1.5),.15,.24))
    if i%4==0: events.append((base,drums['Jing'],.055,.1))
combat=save('Test_Combat_JangguJwago',room(mix(12*bar,events,True,True),True,.10),.76,True)

def effect(name,duration,events,peak=.78,wet=.08):
    return save(name,room(mix(duration,events),False,wet),peak)

effect('Test_FanSwing',.55,[(0,original('FanWhoosh'),.8,0),(.025,note(79),.055,0)],.62)
effect('Test_WoodImpact',.5,[(0,drums['Bak'],.65,0),(0,drums['Janggu3'],.38,0),(.008,original('WoodStrike'),.22,0)],.84)
effect('Test_WindImpact',.8,[(0,original('WindHit'),.55,0),(.015,drums['Janggu2'],.20,0),(.01,note(72),.18,0)],.73)
effect('Test_PlayerHurt',.6,[(0,drums['Janggu2'],.42,0),(0,original('PlayerHurt'),.40,0)],.75)
effect('Test_GuardianSlam',1.7,[(0,drums['Jwago'],.85,0),(.012,drums['Bak'],.24,0),(.025,drums['Jing'],.16,0),(0,original('GuardianSlam'),.18,0)],.9)
effect('Test_ShrineJing',5.5,[(0,drums['Jing'],.4,0),(.15,note(72),.20,0),(.45,note(79),.14,0)],.68,.2)
effect('Test_DodgeCloth',.65,[(0,original('Dodge'),.8,0),(.015,drums['Bak'],.045,0)],.55)
effect('Test_GourdRestore',1.55,[(0,original('GourdDrink'),.6,0),(.18,note(72),.23,0),(.40,note(79),.19,0)],.65,.14)
effect('Test_MenuGayageum',.48,[(0,note(79),.6,0),(.085,note(84),.20,0)],.58,.06)
effect('Test_SpiritAwaken',3.0,[(0,drums['Jwago'],.55,0),(.16,drums['Jing'],.24,0),(.2,note(58,True,2.4),.20,0)],.78,.2)
effect('Test_Victory',4.2,[(0,drums['Janggu1'],.24,0),(0,note(60),.36,0),(.25,note(67),.34,0),(.50,note(70),.30,0),(.85,note(72),.42,0),(1.15,note(79),.25,0),(1,note(72,True,2.5),.14,0)],.70,.24)
effect('Test_Defeat',3.7,[(0,drums['Jwago'],.3,0),(.1,note(63),.4,0),(.65,note(58),.36,0),(1.2,note(48),.40,0)],.65,.16)
effect('Test_FanEquip',.45,[(0,original('FanEquip'),.55,0),(.01,drums['Bak'],.12,0),(.035,note(72),.12,0)],.55)
effect('Test_Footstep',.27,[(0,original('Footstep'),.92,0),(.005,drums['Janggu3'],.025,0)],.4,0)
# 기존 산바람의 음색은 유지하되 저음 드론 성분을 줄인다.
wind=original('MountainWind'); spectrum=np.fft.rfft(wind); frequencies=np.fft.rfftfreq(len(wind),1/RATE)
spectrum*=np.minimum(1,(frequencies/180)**2)*np.minimum(1,1600/np.maximum(1,frequencies))
save('Test_MountainAir',np.fft.irfft(spectrum,len(wind)).astype(np.float32),.25,True)
effect('Test_CueJump',.5,[(0,drums['Janggu3'],.38,0),(.16,drums['Janggu3'],.52,0)],.57,0)
effect('Test_CueHeavy',.85,[(0,drums['Jwago'],.35,0),(.22,drums['Janggu1'],.38,0),(.46,drums['Bak'],.22,0)],.62,.06)
effect('Test_CueCrack',.75,[(0,drums['Janggu3'],.24,0),(.11,drums['Janggu3'],.29,0),(.22,drums['Janggu3'],.36,0),(.34,drums['Bak'],.32,0)],.59,0)

(ROOT/'audio_build_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
# 대화에서 바로 들어볼 수 있는 24초 미리듣기. 실제 게임 믹스와는 별도 편집본이다.
preview=np.concatenate([exploration[:RATE*10]*.66,np.zeros((RATE,2)),combat[:RATE*12]*.66],axis=0)
for t,name in [(12.5,'Test_CueJump'),(14,'Test_GuardianSlam'),(17,'Test_WoodImpact'),(19,'Test_GourdRestore')]:
    x,r=read_wav(DEST/(name+'.wav')); start=int(t*RATE); end=min(len(preview),start+len(x))
    preview[start:end]+=x[:end-start,None]*.34
preview*=.92/max(.92,float(np.max(np.abs(preview))))
with wave.open(str(ROOT/'Test_TraditionalAudioPreview.wav'),'wb') as w:
    w.setnchannels(2);w.setsampwidth(2);w.setframerate(RATE);w.writeframes((preview*32767).astype('<i2').tobytes())
print(json.dumps(report,ensure_ascii=False,indent=2))
