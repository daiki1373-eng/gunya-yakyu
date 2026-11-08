# ホーム画面の下のメニュー・所持数のアイコンを描く(白い記号 + 紺の縁 + 影)。
# 使い方: python3 make_nav_icons.py <出力フォルダ>
import math, sys
from PIL import Image, ImageDraw, ImageFilter, ImageChops

N = 512            # 描く大きさ(最後に 128 へ縮める)
OUT = 128
NAVY = (14, 20, 70)


def blank():
    return Image.new("L", (N, N), 0)


def sub(a, b):
    return ImageChops.subtract(a, b)


def grow(m, px):
    return m.filter(ImageFilter.MaxFilter(px * 2 + 1)) if px > 0 else m


def rot_rect(cx, cy, w, h, deg, r):
    """角の丸い長方形を回転して描いたマスク"""
    big = Image.new("L", (N, N), 0)
    d = ImageDraw.Draw(big)
    d.rounded_rectangle([cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2], radius=r, fill=255)
    return big.rotate(deg, resample=Image.BICUBIC, center=(cx, cy))


def icon_home():
    m = blank(); d = ImageDraw.Draw(m)
    d.polygon([(256, 62), (470, 252), (412, 252), (412, 446), (100, 446), (100, 252), (42, 252)], fill=255)
    cut = blank(); ImageDraw.Draw(cut).rounded_rectangle([214, 318, 298, 446], radius=18, fill=255)
    m = sub(m, cut)
    ch = blank(); ImageDraw.Draw(ch).rectangle([330, 90, 380, 190], fill=255)
    return ImageChops.lighter(m, ch)


def person(cx, top, s):
    m = blank(); d = ImageDraw.Draw(m)
    hr = 62 * s
    d.ellipse([cx - hr, top, cx + hr, top + hr * 2], fill=255)
    bw, bh = 150 * s, 150 * s
    by = top + hr * 2 + 14 * s
    d.rounded_rectangle([cx - bw, by, cx + bw, by + bh * 1.6], radius=int(110 * s), fill=255)
    return m


def icon_team():
    front = person(300, 120, 1.0)
    back = person(170, 92, 0.86)
    back = sub(back, grow(front, 16))
    m = ImageChops.lighter(front, back)
    clip = blank(); ImageDraw.Draw(clip).rectangle([0, 0, N, 446], fill=255)
    return ImageChops.multiply(m, clip)


def star(cx, cy, r):
    pts = []
    for i in range(10):
        a = -math.pi / 2 + i * math.pi / 5
        rr = r if i % 2 == 0 else r * 0.45
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    m = blank(); ImageDraw.Draw(m).polygon(pts, fill=255)
    return m


def icon_cards():
    back = rot_rect(210, 260, 210, 300, 14, 30)
    front = rot_rect(300, 270, 210, 300, -8, 30)
    back = sub(back, grow(front, 16))
    front = sub(front, star(300, 270, 66))
    return ImageChops.lighter(front, back)


def icon_gacha():
    """ガチャ: リボンの付いた箱"""
    m = blank(); d = ImageDraw.Draw(m)
    d.rounded_rectangle([96, 236, 416, 446], radius=22, fill=255)     # 箱
    d.rounded_rectangle([70, 168, 442, 240], radius=20, fill=255)     # ふた
    # 蝶結び
    d.ellipse([120, 66, 262, 176], fill=255)
    d.ellipse([250, 66, 392, 176], fill=255)
    cut = blank(); c = ImageDraw.Draw(cut)
    c.ellipse([160, 98, 236, 150], fill=255)
    c.ellipse([276, 98, 352, 150], fill=255)
    c.rectangle([60, 236, 452, 254], fill=255)                        # ふたと箱の間
    c.rectangle([226, 168, 286, 446], fill=255)                       # リボンの帯(抜いて描き直す)
    m = sub(m, cut)
    rb = blank(); r = ImageDraw.Draw(rb)
    r.rectangle([238, 168, 274, 236], fill=255)
    r.rectangle([238, 254, 274, 446], fill=255)
    r.ellipse([226, 120, 286, 172], fill=255)
    return ImageChops.lighter(m, rb)

def icon_online():
    m = blank(); d = ImageDraw.Draw(m)
    d.ellipse([70, 70, 442, 442], fill=255)
    cut = blank(); c = ImageDraw.Draw(cut)
    w = 18
    c.ellipse([180, 70, 332, 442], outline=255, width=w)     # 経線
    c.line([(256, 70), (256, 442)], fill=255, width=w)
    c.line([(70, 256), (442, 256)], fill=255, width=w)       # 赤道
    for y in (160, 352):                                     # 緯線
        half = math.sqrt(186 ** 2 - (y - 256) ** 2)
        c.line([(256 - half, y), (256 + half, y)], fill=255, width=w)
    return sub(m, cut)


def finish(mask, color_layers=None, outline=22):
    """白い記号に紺の縁と影を付けて 128px にする"""
    edge = grow(mask, outline // 2).filter(ImageFilter.GaussianBlur(2))
    shadow = edge.filter(ImageFilter.GaussianBlur(10))
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    sh = Image.new("RGBA", (N, N), NAVY + (0,)); sh.putalpha(shadow.point(lambda v: int(v * 0.55)))
    img.alpha_composite(sh, (0, 14))
    ol = Image.new("RGBA", (N, N), NAVY + (255,)); ol.putalpha(edge)
    img.alpha_composite(ol)
    body = Image.new("RGBA", (N, N), (255, 255, 255, 255))
    # 上が白、下が少し青みの灰(つや)
    grad = Image.linear_gradient("L").resize((N, N))
    tint = Image.new("RGBA", (N, N), (196, 210, 255, 255))
    body = Image.composite(tint, body, grad.point(lambda v: int(v * 0.55)))
    body.putalpha(mask.filter(ImageFilter.GaussianBlur(1)))
    img.alpha_composite(body)
    if color_layers:
        for layer in color_layers:
            img.alpha_composite(layer)
    return img.resize((OUT, OUT), Image.LANCZOS)


def icon_ball():
    """試合ボタンの野球ボール(白 + 赤い縫い目)"""
    m = blank(); ImageDraw.Draw(m).ellipse([60, 60, 452, 452], fill=255)
    seams = Image.new("RGBA", (N, N), (0, 0, 0, 0)); s = ImageDraw.Draw(seams)
    red = (214, 30, 36, 255)
    for side in (-1, 1):
        cx = 256 + side * 330
        r = 250
        box = [cx - r, 256 - r, cx + r, 256 + r]
        a0, a1 = (-38, 38) if side < 0 else (142, 218)
        s.arc(box, a0, a1, fill=red, width=14)
        for i in range(7):
            a = math.radians(a0 + (a1 - a0) * (i + 0.5) / 7)
            px, py = cx + r * math.cos(a), 256 + r * math.sin(a)
            nx, ny = math.cos(a), math.sin(a)
            tx, ty = -ny, nx
            for sgn in (-1, 1):
                s.line([(px + nx * 6 * sgn, py + ny * 6 * sgn), (px + nx * 24 * sgn + tx * 12, py + ny * 24 * sgn + ty * 12)], fill=red, width=9)
    clip = m.filter(ImageFilter.MinFilter(25))
    seams.putalpha(ImageChops.multiply(seams.getchannel("A"), clip))
    return finish(m, [seams], outline=26)


def main():
    out = sys.argv[1]
    for name, fn in [("home", icon_home), ("team", icon_team), ("cards", icon_cards),
                     ("gacha", icon_gacha), ("online", icon_online)]:
        finish(fn()).save(f"{out}/icon_nav_{name}.png")
    icon_ball().save(f"{out}/icon_nav_ball.png")


if __name__ == "__main__":
    main()
