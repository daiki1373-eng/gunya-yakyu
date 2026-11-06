# cut.py(isnet)でうまく抜けない絵を、GrabCut で抜き直す。
#   mask: isnet の結果を手がかりに広げる(isnet が一部しか拾わない絵: 電柱・ゾンビの頭)
#   rect: 指定した範囲の中から抜く(isnet がほぼ何も拾わない絵: ただの石)。範囲は画像比 x0 y0 x1 y1
# 使い方: python cut_fix.py   (下の FIX を編集)。結果は ~/cut/<id>.png を上書き
import os, numpy as np, cv2, onnxruntime as ort
from PIL import Image

FIX = {
    'pole': ('mask', None),
    'zombie': ('mask', None),
    'rock': ('rect', (0.10, 0.50, 0.90, 0.90)),
}
src = os.path.expanduser('~/mnt/gunya-yakyu/chara')
out = os.path.expanduser('~/cut')
sess = ort.InferenceSession(os.path.expanduser('~/models/isnet-anime.onnx'), providers=['CPUExecutionProvider'])

def isnet(im):
    x = np.asarray(im.resize((1024, 1024), Image.LANCZOS)).astype(np.float32) / 255.0
    x = (x - np.array([0.485, 0.456, 0.406], np.float32)).transpose(2, 0, 1)[None]
    m = sess.run(None, {'img': x})[0][0, 0]
    return (m - m.min()) / (m.max() - m.min() + 1e-6)

for cid, (mode, rect) in FIX.items():
    f = next(p for p in os.listdir(src) if os.path.splitext(p)[0] == cid and os.path.isfile(os.path.join(src, p)))
    im = Image.open(os.path.join(src, f)).convert('RGB'); W, H = im.size
    s = 0.25; w, h = int(W * s), int(H * s)
    small = cv2.cvtColor(np.asarray(im.resize((w, h))), cv2.COLOR_RGB2BGR)
    bgm = np.zeros((1, 65), np.float64); fgm = np.zeros((1, 65), np.float64)
    ms = cv2.resize(isnet(im), (w, h))
    gm = np.full((h, w), cv2.GC_PR_BGD, np.uint8)
    if mode == 'mask':
        gm[ms > 0.15] = cv2.GC_PR_FGD; gm[ms > 0.6] = cv2.GC_FGD
        b = int(w * 0.06); gm[:b, :] = 0; gm[:, :b] = 0; gm[:, -b:] = 0
    else:
        x0, y0, x1, y1 = int(rect[0] * w), int(rect[1] * h), int(rect[2] * w), int(rect[3] * h)
        gm[:] = cv2.GC_BGD
        gm[y0:y1, x0:x1] = cv2.GC_PR_FGD
        gm[ms > 0.5] = cv2.GC_FGD          # isnet が拾った小物(帽子)は残す
    cv2.grabCut(small, gm, None, bgm, fgm, 8, cv2.GC_INIT_WITH_MASK)
    a = ((gm == 1) | (gm == 3)).astype(np.uint8)
    # 一番大きなかたまり + その 3% 以上のかたまりだけ残す
    nb, lab, st, _ = cv2.connectedComponentsWithStats(a, 8)
    if nb > 1:
        areas = st[1:, cv2.CC_STAT_AREA]; keep = np.zeros(nb, bool); keep[1:] = areas >= areas.max() * 0.03
        a = keep[lab].astype(np.uint8)
    a = cv2.resize(a.astype(np.float32), (W, H), interpolation=cv2.INTER_LINEAR)
    a = cv2.GaussianBlur(a, (5, 5), 0)
    a = np.clip((a - 0.3) / 0.4, 0, 1)
    rgba = np.dstack([np.asarray(im), (a * 255).astype(np.uint8)])
    ys, xs = np.where(a > 0.05)
    y0, y1 = max(0, ys.min() - 4), min(H, ys.max() + 5); x0, x1 = max(0, xs.min() - 4), min(W, xs.max() + 5)
    Image.fromarray(rgba[y0:y1, x0:x1]).save(os.path.join(out, cid + '.png'))
    print(cid, mode, 'fg', round(float((a > 0.5).mean()), 3))
