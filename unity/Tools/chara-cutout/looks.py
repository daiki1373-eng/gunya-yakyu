import os, json
import numpy as np
from PIL import Image
cut=os.path.expanduser('~/cut')
res={}; meta=[]
def mode_color(px):
    if len(px)<20: return None
    q=(px//24).astype(np.int32)
    key=q[:,0]*10000+q[:,1]*100+q[:,2]
    vals,cnt=np.unique(key,return_counts=True)
    k=vals[cnt.argmax()]
    sel=px[key==k]
    return np.median(sel,axis=0)
for f in sorted(os.listdir(cut)):
    if not f.endswith('.png'): continue
    cid=f[:-4]
    im=np.asarray(Image.open(os.path.join(cut,f)).convert('RGBA')).astype(np.float32)
    H,W=im.shape[:2]
    a=im[...,3]>200
    rgb=im[...,:3]
    lum=rgb.mean(axis=2)
    def band(y0,y1,x0=0.15,x1=0.85):
        r0,r1=int(H*(1-y1)),int(H*(1-y0)); c0,c1=int(W*x0),int(W*x1)
        m=a[r0:r1,c0:c1]&(lum[r0:r1,c0:c1]>28)
        return rgb[r0:r1,c0:c1][m]
    torso=band(0.50,0.68); pants=band(0.20,0.42); sock=band(0.07,0.17); cap=band(0.88,1.0,0.2,0.8)
    out={}
    for n,p in [('jersey',torso),('pants',pants),('sock',sock),('cap',cap)]:
        c=mode_color(p); out[n]=[int(v) for v in c] if c is not None else None
    # 縦じま: 胴が白っぽく、赤い画素がそれなりにある
    t=torso
    white=((t.min(axis=1)>185)).mean() if len(t) else 0
    red=((t[:,0]>150)&(t[:,1]<110)&(t[:,2]<110)).mean() if len(t) else 0
    out['stripe']=bool(white>0.28 and red>0.04)
    out['white']=round(float(white),2); out['red']=round(float(red),2)
    res[cid]=out
json.dump(res,open(os.path.expanduser('~/looks.json'),'w'),indent=0)
for k,v in res.items(): print(k, v['jersey'], v['pants'], v['sock'], v['cap'], 'S' if v['stripe'] else '-', v['white'], v['red'])
