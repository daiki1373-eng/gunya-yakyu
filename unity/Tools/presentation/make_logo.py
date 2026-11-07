# タイトルロゴ(1枚の画像)を作る。フォントは Google Fonts の OFL フォント(Dela Gothic One・Mochiy Pop One)。
# フォントは fonts.googleapis.com/css2?family=<名前>&text=<使う字> の woff2 を ttf に変換して <fonts_dir> に置く(DelaGothicOne.ttf・MochiyPopOne.ttf)。
# 使い方: python make_logo.py <fonts_dir> <out.png>
import sys, math, os
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageChops

FD, OUT = sys.argv[1], sys.argv[2]
W, H = 1500, 660
SS = 2   # 2倍で描いて縮める(縁を滑らかに)

def grad(w, h, stops):
    g = Image.new('RGB', (1, h))
    for y in range(h):
        t = y / max(1, h - 1)
        for i in range(len(stops) - 1):
            t0, c0 = stops[i]; t1, c1 = stops[i + 1]
            if t0 <= t <= t1:
                k = (t - t0) / max(1e-6, t1 - t0)
                g.putpixel((0, y), tuple(int(c0[j] + (c1[j] - c0[j]) * k) for j in range(3)))
                break
    return g.resize((w, h))

def char_mask(ch, font, stroke):
    bb = font.getbbox(ch, stroke_width=stroke)
    m = Image.new('L', (bb[2] - bb[0] + 4, bb[3] - bb[1] + 4), 0)
    ImageDraw.Draw(m).text((-bb[0] + 2, -bb[1] + 2), ch, font=font, fill=255, stroke_width=stroke, stroke_fill=255)
    return m, bb

def line_masks(text, font, strokes, place):
    """place(i) -> (x, y_offset, angle)。strokes ごとに同じ配置のマスクを返す"""
    out = []
    for s in strokes:
        M = Image.new('L', (W * SS, H * SS), 0)
        for i, ch in enumerate(text):
            x, y, ang = place(i)
            m, bb = char_mask(ch, font, s)
            base, _ = char_mask(ch, font, 0)
            # 回転の中心は線なしの字の中心にそろえる
            m = m.rotate(ang, resample=Image.BICUBIC, expand=True)
            cx, cy = x + (bb[0] + bb[2]) / 2, y + (bb[1] + bb[3]) / 2
            M.paste(255, (int(cx - m.width / 2), int(cy - m.height / 2)), m)
        out.append(M)
    return out

def paint(canvas, mask, color_or_img):
    if isinstance(color_or_img, tuple):
        layer = Image.new('RGBA', canvas.size, color_or_img + (255,))
    else:
        layer = color_or_img.convert('RGBA')
    canvas.paste(layer, (0, 0), mask)

def shear(mask, k):
    return mask.transform(mask.size, Image.AFFINE, (1, k, -k * mask.height / 2, 0, 1, 0), resample=Image.BICUBIC)

canvas = Image.new('RGBA', (W * SS, H * SS), (0, 0, 0, 0))

# --- 2段目「ベースボール」: 太い字・金のグラデーション・濃い赤の縁・白の外縁・影 ---
f2 = ImageFont.truetype(os.path.join(FD, 'DelaGothicOne.ttf'), 220 * SS)
t2 = 'ベースボール'
adv = [f2.getlength(c) for c in t2]
tracking = -8 * SS
total = sum(adv) + tracking * (len(t2) - 1)
x0 = (W * SS - total) / 2
xs = []
x = x0
for a in adv: xs.append(x); x += a + tracking
y2 = 250 * SS
def place2(i):
    # 右へ行くほど少し上がる(勢い)
    return xs[i], y2, 0
m_fill, m_dark, m_white = line_masks(t2, f2, [0, 16 * SS, 30 * SS], place2)
m_fill, m_dark, m_white = [shear(m, -0.12) for m in (m_fill, m_dark, m_white)]
bb2 = m_white.getbbox()

# --- 1段目「ぐにゃぐにゃ」: 丸い字が波打つ・赤 ---
f1 = ImageFont.truetype(os.path.join(FD, 'MochiyPopOne.ttf'), 150 * SS)
t1 = 'ぐにゃぐにゃ'
adv1 = [f1.getlength(c) for c in t1]
tr1 = -4 * SS
tot1 = sum(adv1) + tr1 * (len(t1) - 1)
xs1 = []; x = (W * SS - tot1) / 2
for a in adv1: xs1.append(x); x += a + tr1
y1 = 60 * SS
def place1(i):
    return xs1[i], y1 + math.sin(i * 1.15) * 22 * SS, math.cos(i * 1.15) * 9
n_fill, n_dark, n_white = line_masks(t1, f1, [0, 12 * SS, 24 * SS], place1)

# 影(紺・下へずらしてぼかす)
shadow = ImageChops.lighter(m_white, n_white)
shadow = shadow.filter(ImageFilter.GaussianBlur(6 * SS))
sh = Image.new('L', shadow.size, 0); sh.paste(shadow, (0, 16 * SS))
paint(canvas, sh.point(lambda v: int(v * 0.75)), (10, 14, 40))

# 1段目は2段目の後ろ側に置く(同じ1枚の絵の中での重なりは字の組みとして扱う)
paint(canvas, n_white, (255, 255, 255))
paint(canvas, n_dark, (40, 20, 70))
g1 = grad(W * SS, H * SS, [(0, (255, 120, 120)), (0.16, (240, 40, 50)), (0.30, (170, 0, 30)), (1, (170, 0, 30))])
paint(canvas, n_fill, g1)

paint(canvas, m_white, (255, 255, 255))
paint(canvas, m_dark, (110, 0, 18))
top, bot = bb2[1], bb2[3]
def ty(v): return (v * SS - 0) / (H * SS)
g2 = grad(W * SS, H * SS, [(0, (255, 250, 200)), (top / (H * SS) + 0.02, (255, 250, 200)),
                           ((top + bot) / 2 / (H * SS), (255, 200, 40)),
                           (bot / (H * SS), (240, 110, 0)), (1, (240, 110, 0))])
paint(canvas, m_fill, g2)
# 字の上半分に細い光の帯
hl = m_fill.copy()
band = Image.new('L', hl.size, 0)
ImageDraw.Draw(band).rectangle([0, top + int((bot - top) * 0.20), hl.width, top + int((bot - top) * 0.33)], fill=110)
hl = ImageChops.multiply(hl, band)
paint(canvas, hl, (255, 255, 240))

out = canvas.resize((W, H), Image.LANCZOS)
bb = out.getchannel('A').getbbox()
pad = 16
out = out.crop((max(0, bb[0] - pad), max(0, bb[1] - pad), min(W, bb[2] + pad), min(H, bb[3] + pad)))
out.save(OUT)
print(OUT, out.size)
