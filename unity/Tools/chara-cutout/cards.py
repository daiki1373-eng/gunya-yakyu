# カードの絵(chara/ の元画像)から、一覧用(Thumb 240x360)と拡大用(Full 横648)を書き出す。まだないキャラだけ作る。
#   Thumb: 横幅いっぱいで縦横2:3に切り、上から35%の位置で切り出す(顔が入るように)
#   Full : 横648に縮小(縦は比のまま)
# 使い方: python cards.py <chara フォルダ> <Assets/Resources/Chara>
import sys, os
from PIL import Image

def main():
    src, dst = sys.argv[1], sys.argv[2]
    for k in ('Thumb', 'Full'): os.makedirs(os.path.join(dst, k), exist_ok=True)
    n = 0
    for f in sorted(os.listdir(src)):
        p = os.path.join(src, f)
        if not os.path.isfile(p) or not f.lower().endswith(('.png', '.webp', '.jpg', '.jpeg')): continue
        cid = os.path.splitext(f)[0]
        th, fu = os.path.join(dst, 'Thumb', cid + '.png'), os.path.join(dst, 'Full', cid + '.png')
        if os.path.exists(th) and os.path.exists(fu): continue
        im = Image.open(p).convert('RGB'); W, H = im.size
        if not os.path.exists(fu):
            im.resize((648, round(H * 648 / W)), Image.LANCZOS).save(fu, optimize=True)
        if not os.path.exists(th):
            ch = min(H, round(W * 1.5)); cw = round(ch / 1.5)
            ox = (W - cw) // 2; oy = round((H - ch) * 0.35)
            im.crop((ox, oy, ox + cw, oy + ch)).resize((240, 360), Image.LANCZOS).save(th, optimize=True)
        n += 1
    print('made', n)

if __name__ == '__main__':
    main()
