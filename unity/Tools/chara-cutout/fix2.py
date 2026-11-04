import os, numpy as np, cv2
from PIL import Image
import onnxruntime as ort
src=os.path.expanduser('~/mnt/gunya-yakyu/chara'); out=os.path.expanduser('~/cut')
so=ort.SessionOptions(); so.intra_op_num_threads=4
sess=ort.InferenceSession(os.path.expanduser('~/models/isnet-anime.onnx'), so, providers=['CPUExecutionProvider'])
def matte(im):
    W,H=im.size
    x=np.asarray(im.resize((1024,1024),Image.LANCZOS)).astype(np.float32)/255.0
    x=(x-np.array([0.485,0.456,0.406],np.float32)).transpose(2,0,1)[None]
    m=sess.run(None,{'img':x})[0][0,0]; m=(m-m.min())/(m.max()-m.min()+1e-6)
    return cv2.resize(m,(W,H))
def save(im,a,cid):
    nb,lab,st,_=cv2.connectedComponentsWithStats((a>0.5).astype(np.uint8),8)
    if nb>1:
        areas=st[1:,cv2.CC_STAT_AREA]; big=areas.max(); keep=np.zeros(nb,bool); keep[1:]=areas>=big*0.03
        km=cv2.dilate(keep[lab].astype(np.uint8),np.ones((5,5),np.uint8))>0; a=a*km
    rgba=np.dstack([np.asarray(im),(a*255).astype(np.uint8)])
    ys,xs=np.where(a>0.05); H,W=a.shape
    y0,y1=max(0,ys.min()-4),min(H,ys.max()+5); x0,x1=max(0,xs.min()-4),min(W,xs.max()+5)
    Image.fromarray(rgba[y0:y1,x0:x1]).save(os.path.join(out,cid+'.png'))
# 審判: 3コマの一番下だけ使う
u=Image.open(os.path.join(src,'umpire.png')).convert('RGB'); W,H=u.size
uc=u.crop((0,int(H*0.665),int(W*0.78),H))
a=np.clip((matte(uc)-0.25)/0.5,0,1); save(uc,a,'umpire')
# 幽霊: 半透明なので色(彩度が低い/青い)で抜く
g=Image.open(os.path.join(src,'ghost.png')).convert('RGB'); W,H=g.size
gc=g.crop((int(W*0.08),int(H*0.08),int(W*0.97),int(H*0.90)))
hsv=cv2.cvtColor(np.asarray(gc),cv2.COLOR_RGB2HSV).astype(np.float32)
h,s,v=hsv[...,0]*2,hsv[...,1]/255,hsv[...,2]/255
cool=((h>180)&(h<290)).astype(np.float32)
a=np.clip(((0.42-s)/0.2),0,1)*np.clip((v-0.45)/0.2,0,1)
a=np.maximum(a,cool*np.clip((v-0.4)/0.2,0,1)*np.clip((s-0.05)/0.1,0,1))
a=cv2.GaussianBlur(a,(5,5),0)
# 左のベンチの人は除外(画像の左30%・上60%)
hh,ww=a.shape; a[:int(hh*0.62),:int(ww*0.21)]=0
a=cv2.morphologyEx(a,cv2.MORPH_CLOSE,np.ones((9,9),np.uint8))
save(gc,np.clip(a*1.2,0,1),'ghost')
print('ok')
