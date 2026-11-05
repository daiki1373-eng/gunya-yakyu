# 元絵(切り抜きPNG)から関節位置を推定する(DWPose: ~/models/dw-ll_ucoco_384.onnx)。
# 使い方: python pose.py <Cut画像フォルダ> <出力json>
import sys, os, json
import numpy as np, cv2, onnxruntime as ort

MODEL = os.path.expanduser('~/models/dw-ll_ucoco_384.onnx')
W, H = 288, 384
MEAN = np.array([123.675, 116.28, 103.53], np.float32)
STD = np.array([58.395, 57.12, 57.375], np.float32)
NAMES = ['nose','eyeL','eyeR','earL','earR','shoulderL','shoulderR','elbowL','elbowR','wristL','wristR',
         'hipL','hipR','kneeL','kneeR','ankleL','ankleR']

def affine(cx, cy, sw, sh, inv=False):
    src = np.float32([[cx, cy], [cx, cy - sh/2], [cx - sw/2, cy]])
    dst = np.float32([[W/2, H/2], [W/2, 0], [0, H/2]])
    return cv2.getAffineTransform(dst, src) if inv else cv2.getAffineTransform(src, dst)

def run(sess, rgba):
    a = rgba[:, :, 3]
    ys, xs = np.nonzero(a > 20)
    x0, x1, y0, y1 = xs.min(), xs.max(), ys.min(), ys.max()
    rgb = rgba[:, :, :3].astype(np.float32)
    al = (a.astype(np.float32) / 255.0)[..., None]
    img = rgb * al + 255.0 * (1 - al)            # 白背景に合成
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    sw, sh = (x1 - x0) * 1.25, (y1 - y0) * 1.25
    if sw / sh > W / H: sh = sw * H / W
    else: sw = sh * W / H
    M = affine(cx, cy, sw, sh)
    crop = cv2.warpAffine(img, M, (W, H), flags=cv2.INTER_LINEAR, borderValue=(255, 255, 255))
    x = ((crop - MEAN) / STD).transpose(2, 0, 1)[None].astype(np.float32)
    sx, sy = sess.run(None, {sess.get_inputs()[0].name: x})
    kx = sx[0].argmax(1) / 2.0; ky = sy[0].argmax(1) / 2.0
    sc = np.minimum(sx[0].max(1), sy[0].max(1))
    Mi = affine(cx, cy, sw, sh, inv=True)
    pts = np.stack([kx, ky, np.ones_like(kx)], 1) @ Mi.T
    return pts, sc, (int(x0), int(y0), int(x1), int(y1))

def main():
    src, out = sys.argv[1], sys.argv[2]
    sess = ort.InferenceSession(MODEL, providers=['CPUExecutionProvider'])
    res = {}
    for f in sorted(os.listdir(src)):
        if not f.endswith('.png'): continue
        rgba = cv2.imread(os.path.join(src, f), cv2.IMREAD_UNCHANGED)
        rgba = cv2.cvtColor(rgba, cv2.COLOR_BGRA2RGBA)
        pts, sc, box = run(sess, rgba)
        d = {'box': box, 'size': [rgba.shape[1], rgba.shape[0]]}
        for i, n in enumerate(NAMES):
            d[n] = [round(float(pts[i, 0]), 1), round(float(pts[i, 1]), 1), round(float(sc[i]), 2)]
        # 手の中心(手のキーポイント21点の平均)
        for nm, s in (('handL', 91), ('handR', 112)):
            hp = pts[s:s+21]; hs = sc[s:s+21]
            d[nm] = [round(float(hp[:, 0].mean()), 1), round(float(hp[:, 1].mean()), 1), round(float(hs.mean()), 2)]
        res[f[:-4]] = d
        print(f[:-4], ' '.join(f'{n}:{d[n][2]}' for n in ('shoulderL','wristR','hipL','ankleR')), flush=True)
    json.dump(res, open(out, 'w'), indent=1, ensure_ascii=False)

if __name__ == '__main__':
    main()
