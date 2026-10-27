from pathlib import Path
import sys, json, wave
import numpy as np
ROOT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT.parent/'TraditionalAudio~'))
from inspect_audio import read_wav
build=json.loads((ROOT/'MusicBuildReport.json').read_text())
report={}
for theme,name,instrument in [('exploration','Test_Exploration_GayageumDaegeum','gayageum'),('combat','Test_Combat_JangguJwago','janggu')]:
    x,rate=read_wav(ROOT/(name+'_'+instrument+'.wav'))
    # 실제 렌더링된 파형의 스펙트럼 상승점을 찾아 의도한 박자와 대조한다.
    size=512;hop=128
    padded=np.pad(x,(size//2,size//2))
    windows=np.lib.stride_tricks.sliding_window_view(padded,size)[::hop]
    spectrum=abs(np.fft.rfft(windows*np.hanning(size),axis=1))
    spectrum[:,np.fft.rfftfreq(size,1/rate)<180]=0
    flux=np.maximum(0,np.diff(spectrum,axis=0,prepend=spectrum[:1])).sum(axis=1)
    times=np.arange(len(flux))*hop/rate
    errors=[];misses=0
    for e in build[theme]['events']:
        if e['instrument']!=instrument or e['beat']==0:continue
        expected=e['beat']*60/build[theme]['bpm']
        local=np.flatnonzero((times>=expected-.012)&(times<=expected+.060))
        index=local[np.argmax(flux[local])]
        if flux[index]<.01:misses+=1
        errors.append((times[index]-expected)*1000)
    report[theme]={'detectedAttacks':len(errors),'missingAttacks':misses,
        'medianAttackOffsetMs':round(float(np.median(errors)),2),
        'minimumAttackOffsetMs':round(float(min(errors)),2),
        'maximumAttackOffsetMs':round(float(max(errors)),2),
        'attackSpreadMs':round(float(max(errors)-min(errors)),2),
        'passed':bool(misses==0 and max(abs(v) for v in errors)<35)}
(ROOT/'RhythmVerification.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps(report,indent=2))
