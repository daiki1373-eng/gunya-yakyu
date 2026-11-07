# chara/ の元絵の選び方: <id>.png/.webp と、作り直し版 <id>_v2.png ... があれば番号の一番新しいものを採用版にする
import os, re
def pick_sources(src):
    best = {}
    for f in os.listdir(src):
        p = os.path.join(src, f)
        if not os.path.isfile(p) or not f.lower().endswith(('.png', '.webp', '.jpg', '.jpeg')): continue
        stem = os.path.splitext(f)[0]
        m = re.fullmatch(r'(.+)_v(\d+)', stem)
        cid, v = (m.group(1), int(m.group(2))) if m else (stem, 1)
        if cid not in best or v > best[cid][0]: best[cid] = (v, f)
    return {cid: f for cid, (v, f) in best.items()}
