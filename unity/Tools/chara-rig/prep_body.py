# 動かす用の画像(chara/rig-src/<id>/)を、試合で使う形にそろえる。
# 使い方: python prep_body.py <chara/rig-src> <Assets/Resources/Chara>
# 出力:
#   Body/<id>.png   Aポーズの立ち絵(512x768。元の1024x1536を半分に)
#   Bat/<id>.png    バット・持ち物(余白を切り、握る側が左。標準のバット=横512px。長さの比は保つ)
#   Bat/<id>_2.png  2本目の持ち物(<id>_bat2.png があれば。打席ごとにどちらかを持つ)
#   Glove/<id>.png  グローブ(長辺256px)
# 同じ種類で _v2, _v3 … があれば一番新しい番号を使う。
import sys, os, re
import numpy as np
from PIL import Image

STD_BAT = 1500.0   # 依頼書の標準バットの長さ(画素)
HALF_LEFT = {'twins'}   # 2人並んだ絵

def pick(folder, cid, kind):
    best, bv = None, -1
    for f in os.listdir(folder):
        m = re.fullmatch(re.escape(cid) + '_' + kind + r'(?:_v(\d+))?\.png', f)
        if m:
            v = int(m.group(1) or 1)
            if v > bv: best, bv = f, v
    return os.path.join(folder, best) if best else None

def bleed(im):
    # 透明な画素の色を、近くの不透明な色で埋める(縮小・ミップマップで縁が黒くならないように)
    a = np.asarray(im, np.float32) / 255.0
    rgb, al = a[..., :3], a[..., 3:4]
    pm, w = rgb * al, al.copy()
    out = rgb.copy()
    hole = (al[..., 0] < 0.02)
    for _ in range(24):
        if not hole.any(): break
        pm = (pm + np.roll(pm, 1, 0) + np.roll(pm, -1, 0) + np.roll(pm, 1, 1) + np.roll(pm, -1, 1)) / 5
        w = (w + np.roll(w, 1, 0) + np.roll(w, -1, 0) + np.roll(w, 1, 1) + np.roll(w, -1, 1)) / 5
        got = hole & (w[..., 0] > 1e-3)
        out[got] = pm[got] / w[got]
        hole &= ~got
    res = np.concatenate([out, al], 2)
    return Image.fromarray((np.clip(res, 0, 1) * 255 + 0.5).astype(np.uint8), 'RGBA')

def trim(im, pad):
    bb = im.getchannel('A').point(lambda v: 255 if v > 8 else 0).getbbox()
    if bb is None: return im
    x0, y0, x1, y1 = bb
    return im.crop((max(0, x0 - pad), max(0, y0 - pad), min(im.width, x1 + pad), min(im.height, y1 + pad)))

def mul4(im):
    w, h = (im.width + 3) // 4 * 4, (im.height + 3) // 4 * 4
    if (w, h) == im.size: return im
    c = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    c.paste(im, ((w - im.width) // 2, (h - im.height) // 2))
    return c

def main():
    src, dst = sys.argv[1], sys.argv[2]
    for k in ('Body', 'Bat', 'Glove'): os.makedirs(os.path.join(dst, k), exist_ok=True)
    done = {'Body': [], 'Bat': [], 'Glove': []}
    for cid in sorted(os.listdir(src)):
        folder = os.path.join(src, cid)
        if not os.path.isdir(folder): continue
        p = pick(folder, cid, 'apose')
        if p:
            im = Image.open(p).convert('RGBA')
            if cid in HALF_LEFT:
                # 2人並んだ絵は左の1人だけを中央に置き直す(試合では1人ずつ動かす)
                half = im.crop((0, 0, im.width // 2, im.height))
                bb = half.getchannel('A').getbbox()
                c = Image.new('RGBA', im.size, (0, 0, 0, 0))
                if bb:
                    piece = half.crop(bb)
                    c.paste(piece, ((im.width - piece.width) // 2, bb[1]))
                im = c
            im = im.resize((512, 768), Image.LANCZOS)
            bleed(im).save(os.path.join(dst, 'Body', cid + '.png'), optimize=True)
            done['Body'].append(cid)
        p = pick(folder, cid, 'bat')
        if p:
            im = trim(Image.open(p).convert('RGBA'), 6)
            k = 512.0 / STD_BAT
            im = im.resize((max(8, round(im.width * k)), max(8, round(im.height * k))), Image.LANCZOS)
            mul4(bleed(im)).save(os.path.join(dst, 'Bat', cid + '.png'), optimize=True)
            done['Bat'].append(cid)
        p = pick(folder, cid, 'bat2')
        if p:
            # 2本目の持ち物(双子の金バットなど)。試合では打席ごとにどちらかを持つ
            im = trim(Image.open(p).convert('RGBA'), 6)
            k = 512.0 / STD_BAT
            im = im.resize((max(8, round(im.width * k)), max(8, round(im.height * k))), Image.LANCZOS)
            mul4(bleed(im)).save(os.path.join(dst, 'Bat', cid + '_2.png'), optimize=True)
            done['Bat'].append(cid + '_2')
        p = pick(folder, cid, 'glove')
        if p:
            im = trim(Image.open(p).convert('RGBA'), 6)
            im.thumbnail((256, 256), Image.LANCZOS)
            mul4(bleed(im)).save(os.path.join(dst, 'Glove', cid + '.png'), optimize=True)
            done['Glove'].append(cid)
    for k, v in done.items(): print(k, len(v), ' '.join(v))

if __name__ == '__main__':
    main()
