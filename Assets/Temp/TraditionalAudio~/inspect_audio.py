import json, struct
from pathlib import Path
import numpy as np

ROOT=Path(__file__).resolve().parent

def read_wav(path):
    raw=Path(path).read_bytes(); pos=12; fmt=None; data=None
    while pos+8<=len(raw):
        tag=raw[pos:pos+4]; size=struct.unpack_from('<I',raw,pos+4)[0]; part=raw[pos+8:pos+8+size]
        if tag==b'fmt ': fmt=part
        if tag==b'data': data=part
        pos+=8+size+(size%2)
    code,channels,rate,_,_,bits=struct.unpack_from('<HHIIHH',fmt)
    if code==65534: code=struct.unpack_from('<H',fmt,24)[0]
    if code==3: samples=np.frombuffer(data,dtype='<f4' if bits==32 else '<f8').astype(np.float32)
    elif bits==24:
        b=np.frombuffer(data,dtype=np.uint8).reshape(-1,3).astype(np.int32)
        v=b[:,0]|b[:,1]<<8|b[:,2]<<16; v=(v^0x800000)-0x800000
        samples=v.astype(np.float32)/8388608
    elif bits==16: samples=np.frombuffer(data,dtype='<i2').astype(np.float32)/32768
    elif bits==32: samples=np.frombuffer(data,dtype='<i4').astype(np.float32)/2147483648
    else: raise ValueError((code,bits))
    return samples.reshape(-1,channels).mean(axis=1),rate

def regions(x,rate):
    hop=int(rate*.02); n=len(x)//hop
    rms=np.sqrt(np.mean(x[:n*hop].reshape(n,hop)**2,axis=1))
    mask=rms>max(.002,float(rms.max())*.035)
    active=np.flatnonzero(mask); result=[]
    if not len(active): return result
    start=prev=int(active[0])
    for frame in active[1:]:
        if frame-prev>8:
            result.append((max(0,(start-1)*.02),min(len(x)/rate,(prev+3)*.02)))
            start=int(frame)
        prev=int(frame)
    result.append((max(0,(start-1)*.02),min(len(x)/rate,(prev+3)*.02)))
    return result

def pitch(x,rate):
    x=x[int(.12*rate):int(.75*rate)]
    n=1<<int(np.ceil(np.log2(len(x)*2)))
    spec=np.abs(np.fft.rfft(x*np.hanning(len(x)),n))
    freq=np.fft.rfftfreq(n,1/rate)
    spec[(freq<100)|(freq>1600)]=0
    f=freq[np.argmax(spec)]
    return round(float(f),2),round(float(69+12*np.log2(f/440)),2)

if __name__=='__main__':
    out={}
    for path in (ROOT/'Sources').glob('*.wav'):
        x,rate=read_wav(path); reg=regions(x,rate)
        entries=[{'start':a,'end':b,'pitch':pitch(x[int(a*rate):int(b*rate)],rate) if b-a>.9 else None} for a,b in reg]
        out[path.stem]={'rate':rate,'duration':round(len(x)/rate,3),'peak':float(np.max(np.abs(x))),'regions':entries}
    (ROOT/'source_analysis.json').write_text(json.dumps(out,indent=2),encoding='utf-8')
    print(json.dumps(out,indent=2))
