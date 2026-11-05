# pose_raw.json(自動推定) + rig_manual.txt(手直し) → Assets/Resources/Chara/rig.txt
# 使い方: python build.py <pose_raw.json> <rig_manual.txt> <出力rig.txt>
import sys, json, os
sys.path.insert(0, os.path.dirname(__file__))
from rigio import load_rig, save_rig

TH = 0.30   # これより自信の低い関節は使わない

def mid(a, b): return ((a[0]+b[0])/2, (a[1]+b[1])/2)
def lerp(a, b, t): return (a[0]+(b[0]-a[0])*t, a[1]+(b[1]-a[1])*t)

def auto(d):
    ok = lambda n: n in d and d[n][2] >= TH
    P = lambda n: (d[n][0], d[n][1])
    j = {}
    if not (ok('shoulderL') and ok('shoulderR') and ok('hipL') and ok('hipR')):
        return None
    neck = mid(P('shoulderL'), P('shoulderR'))
    pelvis = mid(P('hipL'), P('hipR'))
    j['neck'] = neck; j['pelvis'] = pelvis
    j['chest'] = lerp(neck, pelvis, 0.35)
    if ok('nose'):
        n = P('nose'); j['head'] = (n[0] + (n[0]-neck[0])*0.8, n[1] + (n[1]-neck[1])*0.8)
    else:
        j['head'] = (neck[0], neck[1] - (pelvis[1]-neck[1])*0.6)
    for s in 'LR':
        j['shoulder'+s] = P('shoulder'+s)
        j['hip'+s] = P('hip'+s)
        if ok('elbow'+s): j['elbow'+s] = P('elbow'+s)
        if ok('elbow'+s) and ok('wrist'+s):
            w = P('wrist'+s)
            if ok('hand'+s): w = lerp(w, P('hand'+s), 0.5)
            j['wrist'+s] = w
        if ok('knee'+s): j['knee'+s] = P('knee'+s)
        if ok('knee'+s) and ok('ankle'+s): j['ankle'+s] = P('ankle'+s)
    # 顔(目・口・眉)。自信が低いキャラは書かない(実行時に頭の骨から推定)
    fc = d.get('face')
    if fc:
        conf = min(fc['eyeA'][4], fc['eyeB'][4], fc['mouth'][4])
        if conf >= FACE_TH:
            ea, eb = fc['eyeA'], fc['eyeB']
            if ea[0] > eb[0]: ea, eb = eb, ea
            ba, bb = fc['browA'], fc['browB']
            if ba[0] > bb[0]: ba, bb = bb, ba
            for nm, g in (('eyeL', ea), ('eyeR', eb), ('mouth', fc['mouth']), ('browL', ba), ('browR', bb)):
                j[nm] = (g[0], g[1])
                j[nm + 'Sz'] = (max(g[2], 4.0), max(g[3], 3.0))
    return j

FACE_TH = 0.5

def main():
    raw = json.load(open(sys.argv[1]))
    manual = load_rig(sys.argv[2]) if os.path.exists(sys.argv[2]) else {}
    rig, bad = {}, []
    for cid, d in raw.items():
        j = auto(d)
        if j is None: bad.append(cid); j = {}
        rig[cid] = {'j': j, 'size': tuple(d['size']), 'box': tuple(d['box'])}
        if cid in manual:
            m = manual[cid]
            if m.get('replace'): rig[cid]['j'] = {}
            rig[cid]['j'].update(m['j'])
            rig[cid]['manual'] = True
            for k, v in m.items():
                if k not in ('j', 'replace'): rig[cid][k] = v
        if rig[cid].get('noface'):
            for nm in ('eyeL', 'eyeR', 'mouth', 'browL', 'browR'):
                rig[cid]['j'].pop(nm, None); rig[cid]['j'].pop(nm + 'Sz', None)
        if not rig[cid]['j']: del rig[cid]
        else:
            # 前に出す腕(打者=バットを振る腕/投手=投げる腕)。バットを握る側、なければ手が高い側
            jj = rig[cid]['j']
            def dist(a, b): return ((a[0]-b[0])**2 + (a[1]-b[1])**2) ** 0.5
            if not any(k in rig[cid] for k in ('frontL', 'frontR')):
                if 'batGrip' in jj and 'wristL' in jj and 'wristR' in jj:
                    fr = 'L' if dist(jj['wristL'], jj['batGrip']) < dist(jj['wristR'], jj['batGrip']) else 'R'
                elif 'wristL' in jj and 'wristR' in jj:
                    fr = 'L' if jj['wristL'][1] < jj['wristR'][1] else 'R'
                else:
                    fr = 'L' if 'wristL' in jj else 'R'
                rig[cid]['front' + fr] = True
    hdr = '# 自動生成(Tools/chara-rig/build.py)。手で直すときは rig_manual.txt を編集して再生成する\n'
    save_rig(sys.argv[3], {k: {kk: vv for kk, vv in v.items() if kk != 'manual'} for k, v in rig.items()}, hdr)
    print('rigged', len(rig), 'auto-failed(needs manual):', [b for b in bad if b not in manual])

if __name__ == '__main__':
    main()
