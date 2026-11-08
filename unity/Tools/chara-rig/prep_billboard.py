# 遠景用ビルボード絵(1024x1536、キー抜き済み)を、試合で使う 512x512 の絵にする。
# 体の範囲(不透明部分)で切り抜き、高さ480pxに縮めて、足の裏が下から16pxの位置に来るよう中央に置く。
# 2の累乗(512x512)にするのは、ブラウザ版でミップマップ付きのまま ASTC に圧縮できるようにするため。
# 体の高さ(px)は Chara/billboard_meta.txt に「id 高さ」で書く(ゲームはこれで身長を合わせる)。
# 使い方: python3 prep_billboard.py <元の絵のフォルダ> <出力フォルダ(Assets/Resources/Chara/Billboard)>
import os, sys
from PIL import Image

SIZE, CONTENT_H, FOOT = 512, 480, 16

def main(src, dst):
    os.makedirs(dst, exist_ok=True)
    meta_path = os.path.join(os.path.dirname(dst.rstrip('/')), 'billboard_meta.txt')
    meta = {}
    if os.path.exists(meta_path):
        for line in open(meta_path):
            p = line.split()
            if len(p) == 2: meta[p[0]] = p[1]
    n = 0
    for f in sorted(os.listdir(src)):
        if not f.lower().endswith('.png'):
            continue
        im = Image.open(os.path.join(src, f)).convert('RGBA')
        box = im.getchannel('A').point(lambda v: 255 if v > 8 else 0).getbbox()
        if box is None:
            print('skip (empty)', f); continue
        body = im.crop(box)
        k = CONTENT_H / body.height
        w = max(1, round(body.width * k))
        if w > SIZE - 8:                      # 横に広い絵は幅で合わせる
            k = (SIZE - 8) / body.width
            w = SIZE - 8
        h = max(1, round(body.height * k))
        body = body.resize((w, h), Image.LANCZOS)
        out = Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))
        out.alpha_composite(body, ((SIZE - w) // 2, SIZE - FOOT - h))
        out.save(os.path.join(dst, f), optimize=True)
        meta[os.path.splitext(f)[0]] = str(h)
        n += 1
    with open(meta_path, 'w') as fo:
        for k in sorted(meta): fo.write(k + ' ' + meta[k] + '\n')
    print('wrote', n)

if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
