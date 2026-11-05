# リグ(関節)を元絵に重ねた確認用の一覧画像を作る。
# 使い方: python preview.py <Cut画像フォルダ> <rig.txt> <出力prefix>
import sys, os
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.dirname(__file__))
from rigio import load_rig, BONES

def main():
    src, rigp, out = sys.argv[1:4]
    rig = load_rig(rigp)
    ids = sorted(rig)
    W, H = 300, 330
    per = 25
    for part in range((len(ids) + per - 1) // per):
        sub = ids[part*per:(part+1)*per]
        sheet = Image.new('RGB', (W*5, H*((len(sub)+4)//5)), (200, 200, 200))
        dr = ImageDraw.Draw(sheet)
        for i, cid in enumerate(sub):
            im = Image.open(os.path.join(src, cid + '.png')).convert('RGBA')
            s = (W - 6) / max(im.size)
            im = im.resize((int(im.size[0]*s), int(im.size[1]*s)))
            bg = Image.new('RGBA', im.size, (240, 240, 240, 255)); bg.alpha_composite(im)
            x0, y0 = (i % 5)*W + 3, (i//5)*H + 3
            sheet.paste(bg.convert('RGB'), (x0, y0))
            j = rig[cid]['j']
            P = lambda n: (x0 + j[n][0]*s, y0 + j[n][1]*s)
            for b, (a, c, col) in BONES.items():
                if a in j and c in j:
                    dr.line([P(a), P(c)], fill=col, width=3)
            for n in j:
                p = P(n); dr.ellipse([p[0]-3, p[1]-3, p[0]+3, p[1]+3], fill=(0, 0, 0))
            dr.text((x0 + 2, y0 + H - 24), cid + (' *' if rig[cid].get('manual') else ''), fill=(0, 0, 0))
        sheet.save(f'{out}{part}.jpg', quality=88)

if __name__ == '__main__':
    main()
