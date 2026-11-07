# Aポーズの立ち絵を、胴・左腕・右腕・左脚・右脚の部品に塗り分ける(試合で部品ごとに回すため)。
# 骨の線を種にして、絵の不透明な画素の中だけを少しずつ広げる(透明なすき間は越えない)。
# 腕と胴の間にすき間があるAポーズなら、境目は脇の下の自然な位置になる。
# 使い方: python labels.py <Body画像フォルダ> <rig_body.txt> <出力フォルダ(BodyLabel)>
# 出力: <id>.png グレースケール、半分の大きさ。値 = 部品の番号 × 32 (0=胴 1=左腕 2=右腕 3=左脚 4=右脚。左右は画像の左右)
import sys, os
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(__file__))
from rigio import load_rig

STEP = 2

def lerp(a, b, t): return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)

def seeds(j):
    s = {0: [], 1: [], 2: [], 3: [], 4: []}
    pel, neck = j['pelvis'], j['neck']
    waist = lerp(pel, neck, 0.25)
    s[0] += [(pel, waist), (waist, neck)]
    if 'hipL' in j and 'hipR' in j: s[0].append((j['hipL'], j['hipR']))
    if 'shoulderL' in j and 'shoulderR' in j:
        s[0].append((lerp(j['shoulderL'], j['shoulderR'], 0.1), lerp(j['shoulderL'], j['shoulderR'], 0.9)))
    if 'head' in j: s[0].append((neck, j['head']))
    for side, arm, leg in (('L', 1, 3), ('R', 2, 4)):
        sh, el, wr = j.get('shoulder' + side), j.get('elbow' + side), j.get('wrist' + side)
        if sh and el:
            s[arm].append((lerp(sh, el, 0.35), el))
            if wr: s[arm].append((el, lerp(el, wr, 1.3)))
        hp, kn, an = j.get('hip' + side), j.get('knee' + side), j.get('ankle' + side)
        if hp and kn:
            s[leg].append((lerp(hp, kn, 0.3), kn))
            if an: s[leg].append((kn, lerp(kn, an, 1.2)))
    return s

def draw(lab, a, b, v, w, h):
    n = int(max(abs(b[0] - a[0]), abs(b[1] - a[1])) / STEP) + 2
    for t in np.linspace(0, 1, n):
        x = int((a[0] + (b[0] - a[0]) * t) / STEP); y = int((a[1] + (b[1] - a[1]) * t) / STEP)
        if 0 <= x < w and 0 <= y < h: lab[y, x] = v

def grow(lab, allowed):
    # 未定(-1)の画素を、隣の決まった画素の番号で埋めていく(allowed の中だけ)
    h, w = lab.shape
    for _ in range(2000):
        todo = (lab < 0) & allowed
        if not todo.any(): break
        changed = False
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0), (1, 1), (1, -1), (-1, 1), (-1, -1)):
            src = np.full_like(lab, -1)
            ys0, ys1 = max(0, dy), h + min(0, dy)
            xs0, xs1 = max(0, dx), w + min(0, dx)
            src[ys0:ys1, xs0:xs1] = lab[ys0 - dy:ys1 - dy, xs0 - dx:xs1 - dx]
            m = todo & (src >= 0) & (lab < 0)
            if m.any():
                lab[m] = src[m]; changed = True
        if not changed: break
    return lab

def main():
    src, rigp, out = sys.argv[1:4]
    os.makedirs(out, exist_ok=True)
    rig = load_rig(rigp)
    for cid in sorted(rig):
        j = rig[cid]['j']
        if 'pelvis' not in j or 'neck' not in j: continue
        im = Image.open(os.path.join(src, cid + '.png')).convert('RGBA')
        W, H = im.size
        w, h = (W + STEP - 1) // STEP, (H + STEP - 1) // STEP
        alpha = np.asarray(im.resize((w, h), Image.BILINEAR).getchannel('A')) > 40
        lab = np.full((h, w), -1, np.int16)
        # 胴は面で種をまく(肩と股を結ぶ四角を少し内側へ縮めたもの + 頭の円)。線だけだと腕の部品が胸まで広がる
        from PIL import ImageDraw
        poly = Image.new('L', (w, h), 0)
        dr = ImageDraw.Draw(poly)
        if all(k in j for k in ('shoulderL', 'shoulderR', 'hipL', 'hipR')):
            q = [j['shoulderL'], j['shoulderR'], j['hipR'], j['hipL']]
            cx = sum(p[0] for p in q) / 4; cy = sum(p[1] for p in q) / 4
            dr.polygon([((cx + (p[0] - cx) * 0.85) / STEP, (cy + (p[1] - cy) * 0.85) / STEP) for p in q], fill=1)
        if 'head' in j:
            hc = lerp(j['neck'], j['head'], 0.5)
            r = 0.45 * ((j['head'][0] - j['neck'][0]) ** 2 + (j['head'][1] - j['neck'][1]) ** 2) ** 0.5
            dr.ellipse([(hc[0] - r) / STEP, (hc[1] - r) / STEP, (hc[0] + r) / STEP, (hc[1] + r) / STEP], fill=1)
        lab[np.asarray(poly) > 0] = 0
        for v, segs in seeds(j).items():
            if v >= 3 and rig[cid].get('nolegs'): continue   # 脚がはっきりない絵は下半身も胴
            for a, b in segs: draw(lab, a, b, v, w, h)
        lab[~alpha] = np.where(lab[~alpha] >= 0, -1, -1)   # 種は不透明な画素だけ
        lab = grow(lab, alpha)                              # 1) 絵の中だけで広げる
        lab = grow(lab, np.ones_like(alpha))                # 2) 透明な所は近い部品で埋める
        lab[lab < 0] = 0
        Image.fromarray((lab * 32).astype(np.uint8), 'L').save(os.path.join(out, cid + '.png'), optimize=True)
        counts = [int((lab[alpha] == k).sum()) for k in range(5)]
        print(cid, counts)

if __name__ == '__main__':
    main()
