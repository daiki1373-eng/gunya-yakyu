# 決めポーズ・横向きの走り絵(chara/rig-src/<id>/<id>_<pose>.png)を試合用にそろえる。
# 使い方: python prep_poses.py <chara/rig-src> <Assets/Resources/Chara>
# 出力: Pose/<id>_<pose>.png と pose_meta.txt(1行 = 名前 足の下端の画素 体の高さの画素)
#   構え・振り切り・投げ終わり: 1024x1536 → 512x768(Aポーズの Body と同じ縮尺)
#   走り(2コマ): 1920x1280 → 768x512。2コマは同じ切り方で、位置・大きさのずれをそのまま保つ
# 同じ種類で _v2 などがあれば番号の新しいものを使う(sumo_swing_finish_v1 のような不採用版は名前で除外)
import sys, os, re, time
T0 = time.time(); LIMIT = 150
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from prep_body import bleed

KINDS = {'swing_ready': (512, 768), 'swing_finish': (512, 768), 'pitch_release': (512, 768),
         'run_side': (768, 512), 'run_side_2': (768, 512)}

def pick(folder, cid, kind):
    best, bv = None, -1
    for f in os.listdir(folder):
        m = re.fullmatch(re.escape(cid) + '_' + kind + r'(?:_v(\d+))?\.png', f)
        if m:
            v = int(m.group(1) or 1)
            if f.endswith('_v1.png'): continue        # 明示的な旧版(v1)は不採用
            if v > bv: best, bv = f, v
    return os.path.join(folder, best) if best else None

def main():
    src, dst = sys.argv[1], sys.argv[2]
    out = os.path.join(dst, 'Pose'); os.makedirs(out, exist_ok=True)
    meta, n = [], 0
    for cid in sorted(os.listdir(src)):
        folder = os.path.join(src, cid)
        if not os.path.isdir(folder): continue
        for kind, size in KINDS.items():
            p = pick(folder, cid, kind)
            if not p: continue
            name = cid + '_' + kind
            dp = os.path.join(out, name + '.png')
            fresh = os.path.exists(dp) and os.path.getmtime(dp) >= os.path.getmtime(p)
            im = Image.open(dp if fresh else p).convert('RGBA')
            if not fresh: im = im.resize(size, Image.LANCZOS)
            bb = im.getchannel('A').point(lambda v: 255 if v > 40 else 0).getbbox()
            if bb is None: continue
            if not fresh:
                if time.time() - T0 > LIMIT: print('time up; run again'); break
                bleed(im).save(dp)
            meta.append('%s %d %d' % (name, bb[3], bb[3] - bb[1]))
            n += 1
    if time.time() - T0 > LIMIT: print('incomplete', n); return
    open(os.path.join(dst, 'pose_meta.txt'), 'w').write('\n'.join(meta) + '\n')
    print('poses', n)

if __name__ == '__main__':
    main()
