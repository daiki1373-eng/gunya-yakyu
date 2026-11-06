# 演出素材(presentation-materials-v2)をゲーム用の大きさにそろえる。
# 使い方: python prep.py <art-src/presentation> <Assets/Resources/Presentation>
import sys, os
import numpy as np
from PIL import Image

def fit_w(im, w): return im.resize((w, round(im.height * w / im.width)), Image.LANCZOS)

def cut_machine(im):
    # ガチャマシンの背景(暗い紺)を抜く。アニメ絵用の切り抜きモデル(isnet-anime)で前景を出し、GrabCut で縁を整える
    import cv2, onnxruntime as ort
    rgb = im.convert('RGB'); W, H = rgb.size
    sess = ort.InferenceSession(os.path.expanduser('~/models/isnet-anime.onnx'), providers=['CPUExecutionProvider'])
    x = np.asarray(rgb.resize((1024, 1024), Image.LANCZOS)).astype(np.float32) / 255.0
    x = (x - np.array([0.485, 0.456, 0.406], np.float32)).transpose(2, 0, 1)[None]
    m = sess.run(None, {'img': x})[0][0, 0]; m = (m - m.min()) / (m.max() - m.min() + 1e-6)
    s = 0.5; w, h = int(W * s), int(H * s)
    small = cv2.cvtColor(np.asarray(rgb.resize((w, h))), cv2.COLOR_RGB2BGR)
    ms = cv2.resize(m, (w, h))
    gm = np.full((h, w), cv2.GC_PR_BGD, np.uint8)
    gm[ms > 0.2] = cv2.GC_PR_FGD; gm[ms > 0.8] = cv2.GC_FGD
    b = int(w * 0.02); gm[:b, :] = 0; gm[:, :b] = 0; gm[:, -b:] = 0
    bgm = np.zeros((1, 65), np.float64); fgm = np.zeros((1, 65), np.float64)
    cv2.grabCut(small, gm, None, bgm, fgm, 6, cv2.GC_INIT_WITH_MASK)
    a = ((gm == 1) | (gm == 3)).astype(np.float32)
    a = cv2.resize(a, (W, H)); a = cv2.GaussianBlur(a, (5, 5), 0); a = np.clip((a - 0.3) / 0.4, 0, 1)
    res = Image.fromarray(np.dstack([np.asarray(rgb), (a * 255).astype(np.uint8)]), 'RGBA')
    return res.crop(res.getchannel('A').point(lambda v: 255 if v > 20 else 0).getbbox())

def main():
    src, dst = sys.argv[1], sys.argv[2]
    os.makedirs(dst, exist_ok=True)
    O = lambda n: os.path.join(src, n)
    S = lambda im, n: im.save(os.path.join(dst, n), optimize=True)
    # 大型ビジョン: 常時表示は掲示板の画面(横長 3.5:1)に合わせて中央を切る
    logo = Image.open(O('bigscreen_logo.png')).convert('RGB')
    h = round(logo.width / 3.5); y0 = (logo.height - h) // 2
    S(fit_w(logo.crop((0, y0, logo.width, y0 + h)), 1400), 'vision_logo.png')
    S(fit_w(Image.open(O('bigscreen_playball.png')).convert('RGB'), 1024), 'vision_playball.png')
    S(fit_w(Image.open(O('bigscreen_homerun.png')).convert('RGB'), 1024), 'vision_homerun.png')
    S(Image.open(O('title_bg.png')).convert('RGB').resize((1600, 900), Image.LANCZOS), 'title_bg.png')
    S(fit_w(cut_machine(Image.open(O('gacha_machine.png'))), 512), 'gacha_machine.png')
    S(Image.open(O('gacha_dial.png')).convert('RGBA').resize((512, 512), Image.LANCZOS), 'gacha_dial.png')
    for n in ('icon_coin', 'icon_point'):
        im = Image.open(O(n + '.png')).convert('RGBA')
        im = im.crop(im.getchannel('A').getbbox()); im.thumbnail((128, 128), Image.LANCZOS)
        c = Image.new('RGBA', (128, 128), (0, 0, 0, 0)); c.paste(im, ((128 - im.width) // 2, (128 - im.height) // 2))
        S(c, n + '.png')
    im = Image.open(O('ojisan_run_side.png')).convert('RGBA'); im = im.crop(im.getchannel('A').getbbox())
    S(fit_w(im, 768), 'proto_ojisan_run_side.png')
    print('ok', sorted(os.listdir(dst)))

if __name__ == '__main__':
    main()
