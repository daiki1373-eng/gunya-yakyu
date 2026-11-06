# 新しいキャラの顔の位置(heads_manual.json の 中心x, 中心y, 幅 を切り抜き画像比で)を骨格推定で求めて足す。
# 既にあるキャラは変えない。人の顔が見つからない絵は「上のほう」を顔とみなす(確認用の一覧で目視して必要なら手で直す)。
# 使い方: python heads_auto.py <切り抜きフォルダ(~/cut)> <heads_manual.json> <確認画像の出力パス>
import sys, os, json
import numpy as np, cv2, onnxruntime as ort
from PIL import Image, ImageDraw
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'chara-rig'))
import pose as P

def main():
    src, jp, out = sys.argv[1:4]
    H = json.load(open(jp))
    sess = ort.InferenceSession(P.MODEL, providers=['CPUExecutionProvider'])
    added = {}
    for f in sorted(os.listdir(src)):
        cid = f[:-4]
        if not f.endswith('.png') or cid in H: continue
        rgba = cv2.cvtColor(cv2.imread(os.path.join(src, f), cv2.IMREAD_UNCHANGED), cv2.COLOR_BGRA2RGBA)
        h, w = rgba.shape[:2]
        pts, sc, box = P.run(sess, rgba)
        face = pts[23:91]; fs = sc[23:91]
        eyes = np.concatenate([face[36:48]]); es = fs[36:48]
        if es.mean() >= 0.45:
            cx, cy = face[:, 0].mean(), face[:, 1].mean()
            span = max(face[:, 0].max() - face[:, 0].min(), face[:, 1].max() - face[:, 1].min())
            size = span * 2.0
            how = 'face'
        else:
            # 顔が分からない絵: 不透明な部分の上から 22% の高さ、幅の半分ほどを顔とみなす
            a = rgba[:, :, 3] > 40
            ys, xs = np.nonzero(a)
            top, bot = ys.min(), ys.max()
            cy = top + (bot - top) * 0.20
            row = xs[(ys > cy - 10) & (ys < cy + 10)]
            cx = row.mean() if len(row) else xs.mean()
            size = (bot - top) * 0.40
            how = 'top'
        added[cid] = [round(float(cx / w), 3), round(float(cy / h), 3), round(float(min(size / w, 1.2)), 3)]
        print(cid, how, added[cid], flush=True)
    H.update(added)
    with open(jp, 'w') as fo:
        fo.write('{' + ',\n'.join('"%s":%s' % (k, json.dumps(v)) for k, v in H.items()) + '}\n')
    # 確認用: 追加分の顔の円を重ねた一覧
    ids = sorted(added)
    cw = 200
    sheet = Image.new('RGB', (cw * 9, cw * ((len(ids) + 8) // 9)), (40, 40, 40))
    d = ImageDraw.Draw(sheet)
    for i, cid in enumerate(ids):
        im = Image.open(os.path.join(src, cid + '.png')).convert('RGBA')
        W, Hh = im.size; cx, cy, s = added[cid]
        side = s * W
        crop = im.crop((int(cx * W - side / 2), int(cy * Hh - side / 2), int(cx * W + side / 2), int(cy * Hh + side / 2))).resize((cw, cw))
        bg = Image.new('RGBA', (cw, cw), (90, 90, 90, 255)); bg.alpha_composite(crop)
        sheet.paste(bg.convert('RGB'), ((i % 9) * cw, (i // 9) * cw))
        d.text(((i % 9) * cw + 4, (i // 9) * cw + 4), cid, fill=(255, 255, 0))
    sheet.save(out, quality=85)

if __name__ == '__main__':
    main()
