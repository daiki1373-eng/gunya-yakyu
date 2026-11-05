# rig.txt の読み書き。1行 = "id 関節名 x y"(画像の画素座標、左上原点)。
# 関節名: head neck chest pelvis shoulderL/R elbowL/R wristL/R hipL/R kneeL/R ankleL/R batGrip batTip
# (L/R は画像の左/右。なくてよい関節は書かない → その部分は親の骨に付いて動く)
BONES = {
    # 骨名: (始点, 終点, 色)
    'spine':  ('pelvis', 'chest', (255, 200, 0)),
    'neck':   ('chest', 'neck', (255, 200, 0)),
    'head':   ('neck', 'head', (255, 120, 0)),
    'uarmL':  ('shoulderL', 'elbowL', (0, 120, 255)),
    'farmL':  ('elbowL', 'wristL', (0, 200, 255)),
    'uarmR':  ('shoulderR', 'elbowR', (255, 0, 120)),
    'farmR':  ('elbowR', 'wristR', (255, 90, 200)),
    'thighL': ('hipL', 'kneeL', (0, 160, 0)),
    'shinL':  ('kneeL', 'ankleL', (120, 220, 0)),
    'thighR': ('hipR', 'kneeR', (160, 0, 200)),
    'shinR':  ('kneeR', 'ankleR', (210, 120, 255)),
    'bat':    ('batGrip', 'batTip', (120, 70, 20)),
}

def load_rig(path):
    rig = {}
    for line in open(path, encoding='utf-8'):
        line = line.split('#')[0].strip()
        if not line: continue
        p = line.split()
        if p[1] in ('size', 'box', 'scale'):
            rig.setdefault(p[0], {'j': {}})[p[1]] = tuple(float(v) for v in p[2:])
            continue
        if p[1] == 'flag':
            rig.setdefault(p[0], {'j': {}})[p[2]] = True
            continue
        rig.setdefault(p[0], {'j': {}})['j'][p[1]] = (float(p[2]), float(p[3]))
    return rig

def save_rig(path, rig, header=''):
    with open(path, 'w', encoding='utf-8') as f:
        if header: f.write(header)
        for cid in sorted(rig):
            for k, v in rig[cid].items():
                if k != 'j' and v is True: f.write(f'{cid} flag {k}\n')
                if k in ('size', 'box'): f.write(f'{cid} {k} ' + ' '.join(f'{x:.0f}' for x in v) + '\n')
                if k == 'scale': f.write(f'{cid} scale {v[0]:.2f}\n')
            for n, (x, y) in rig[cid]['j'].items():
                f.write(f'{cid} {n} {x:.0f} {y:.0f}\n')
