import os, json
from PIL import Image
cut=os.path.expanduser('~/cut'); dst=os.path.expanduser('~/mnt/gunya-yakyu/unity/Assets/Resources/Chara')
os.makedirs(dst+'/Cut',exist_ok=True); os.makedirs(dst+'/Head',exist_ok=True)
H=json.load(open(os.path.expanduser('~/heads_manual.json')))
for cid,(cx,cy,s) in H.items():
    im=Image.open(os.path.join(cut,cid+'.png')).convert('RGBA')
    W,Hh=im.size
    # 立ち姿(縦720まで。幅・高さは4の倍数に)
    # 512x512 の透明な台紙の下中央に置く(2の累乗にして圧縮とミップマップを効かせる)
    sc=min(512/Hh, 512/W)
    w=int(W*sc); h=int(Hh*sc)
    canvas=Image.new('RGBA',(512,512),(0,0,0,0))
    canvas.paste(im.resize((w,h),Image.LANCZOS),((512-w)//2,512-h))
    canvas.save(os.path.join(dst,'Cut',cid+'.png'))
    # 頭(正方形256)。はみ出す分は透明で埋める
    side=s*W
    box=(int(cx*W-side/2),int(cy*Hh-side/2),int(cx*W+side/2),int(cy*Hh+side/2))
    head=Image.new('RGBA',(box[2]-box[0],box[3]-box[1]),(0,0,0,0))
    head.paste(im.crop((max(0,box[0]),max(0,box[1]),min(W,box[2]),min(Hh,box[3]))),(max(0,-box[0]),max(0,-box[1])))
    head=head.resize((256,256),Image.LANCZOS)
    # 角のごちゃつき(肩・腕)を消すため、円の外をなめらかに透明にする
    import numpy as np
    a=np.asarray(head).astype(np.float32)
    yy,xx=np.mgrid[0:256,0:256]; r=np.sqrt((xx-127.5)**2+(yy-127.5)**2)/128.0
    m=np.clip((1.0-r)/0.10,0,1)
    a[...,3]*=m
    Image.fromarray(a.astype(np.uint8)).save(os.path.join(dst,'Head',cid+'.png'))
print('ok',len(H))

# 立ち姿の中身の範囲(x0 x1 高さ, 512台紙のピクセル)を書き出す。試合で体を上下・左右に分けて動かすのに使う
import numpy as np
lines=[]
for f in sorted(os.listdir(os.path.join(dst,'Cut'))):
    if not f.endswith('.png'): continue
    a=np.asarray(Image.open(os.path.join(dst,'Cut',f)).convert('RGBA'))[...,3]
    ys,xs=np.where(a>40)
    lines.append('%s %d %d %d'%(f[:-4],xs.min(),xs.max()+1,a.shape[0]-ys.min()))
open(os.path.join(dst,'cut_meta.txt'),'w').write('\n'.join(lines)+'\n')
