import os, time, sys
import numpy as np, cv2
import onnxruntime as ort
from PIL import Image
src=os.path.expanduser('~/mnt/gunya-yakyu/chara')
out=os.path.expanduser('~/cut')
t0=time.time()
so=ort.SessionOptions(); so.intra_op_num_threads=4
sess=ort.InferenceSession(os.path.expanduser('~/models/isnet-anime.onnx'), so, providers=['CPUExecutionProvider'])
n=0
for f in sorted(os.listdir(src)):
    if not f.lower().endswith(('.png','.webp','.jpg','.jpeg')) or not os.path.isfile(os.path.join(src,f)): continue   # rig-src などは対象外
    cid=os.path.splitext(f)[0]
    if os.path.exists(os.path.join(out,cid+'.png')): continue
    if time.time()-t0>30: break
    im=Image.open(os.path.join(src,f)).convert('RGB')
    W,H=im.size
    x=np.asarray(im.resize((1024,1024),Image.LANCZOS)).astype(np.float32)/255.0
    x=(x-np.array([0.485,0.456,0.406],np.float32))
    x=x.transpose(2,0,1)[None]
    m=sess.run(None,{'img':x})[0][0,0]
    m=(m-m.min())/(m.max()-m.min()+1e-6)
    m=cv2.resize(m,(W,H),interpolation=cv2.INTER_LINEAR)
    a=np.clip((m-0.25)/0.5,0,1)
    # 小さな粒(花火・きらきら)を落とす
    nb,lab,st,_=cv2.connectedComponentsWithStats((a>0.5).astype(np.uint8),8)
    if nb>1:
        areas=st[1:,cv2.CC_STAT_AREA]; big=areas.max()
        keep=np.zeros(nb,bool); keep[1:]=areas>=big*0.03
        km=keep[lab]
        km=cv2.dilate(km.astype(np.uint8),np.ones((5,5),np.uint8))>0
        a=a*km
    rgba=np.dstack([np.asarray(im),(a*255).astype(np.uint8)])
    ys,xs=np.where(a>0.05)
    y0,y1=max(0,ys.min()-4),min(H,ys.max()+5); x0,x1=max(0,xs.min()-4),min(W,xs.max()+5)
    Image.fromarray(rgba[y0:y1,x0:x1]).save(os.path.join(out,cid+'.png'))
    n+=1
print('done',n,'total',len(os.listdir(out)), round(time.time()-t0,1))
