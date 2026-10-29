#!/usr/bin/env python3
"""Generate 32x32 pixel-art UI icons (stdlib only).

Style: dark outline + parchment body + gold accent, matching UITheme.
Output: Tools/Art/out/icons/*.png + contact sheet.
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from GenerateBackground import Img, write_png  # noqa: E402

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", "icons")
os.makedirs(OUT, exist_ok=True)

S = 32
OUTLINE = (11, 14, 20, 255)
BODY = (217, 210, 194, 255)      # parchment
SHADE = (150, 144, 130, 255)
GOLD = (201, 168, 106, 255)      # UITheme.gold
GOLD_HI = (231, 211, 160, 255)
BLOOD = (140, 47, 52, 255)
TEAL = (95, 143, 138, 255)
INK = (24, 30, 40, 255)


def blank():
    return Img(S, S)


def px(img, x, y, c):
    if 0 <= x < S and 0 <= y < S:
        img.set(x, y, c)


def rect(img, x0, y0, w, h, c):
    for y in range(y0, y0 + h):
        for x in range(x0, x0 + w):
            px(img, x, y, c)


def circle(img, cx, cy, r, c, filled=True, thickness=1.0):
    for y in range(int(cy - r - 1), int(cy + r + 2)):
        for x in range(int(cx - r - 1), int(cx + r + 2)):
            d = math.hypot(x - cx, y - cy)
            if filled and d <= r:
                px(img, x, y, c)
            elif not filled and abs(d - r) <= thickness * 0.5:
                px(img, x, y, c)


def line(img, x0, y0, x1, y1, c, w=1):
    n = int(max(abs(x1 - x0), abs(y1 - y0)) * 2) + 1
    for i in range(n + 1):
        t = i / n
        x = x0 + (x1 - x0) * t
        y = y0 + (y1 - y0) * t
        for ox in range(w):
            for oy in range(w):
                px(img, int(x) + ox, int(y) + oy, c)


def poly(img, pts, c):
    ys = [p[1] for p in pts]
    for y in range(int(min(ys)), int(max(ys)) + 1):
        xs = []
        for i in range(len(pts)):
            x0, y0 = pts[i]
            x1, y1 = pts[(i + 1) % len(pts)]
            if (y0 <= y < y1) or (y1 <= y < y0):
                t = (y - y0) / (y1 - y0)
                xs.append(x0 + (x1 - x0) * t)
        xs.sort()
        for i in range(0, len(xs) - 1, 2):
            for x in range(int(xs[i]), int(xs[i + 1]) + 1):
                px(img, x, y, c)


def outlined(img, color):
    """Add a 1px dark outline around every opaque pixel."""
    src = Img(S, S)
    src.d = bytearray(img.d)
    for y in range(S):
        for x in range(S):
            if img.get(x, y)[3] != 0:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < S and 0 <= ny < S and src.get(nx, ny)[3] > 0:
                    img.set(x, y, color)
                    break
    return img


# ---------------- icons ----------------
def icon_sword():
    img = blank()
    # blade with a clear point
    poly(img, [(16, 2), (19, 7), (19, 19), (13, 19), (13, 7)], BODY)
    line(img, 14, 8, 14, 18, GOLD_HI)   # edge highlight
    line(img, 17, 7, 17, 18, SHADE)
    # crossguard
    rect(img, 9, 19, 14, 3, GOLD)
    # grip
    rect(img, 14, 22, 4, 6, (96, 70, 46, 255))
    # pommel
    rect(img, 13, 28, 6, 2, GOLD)
    return outlined(img, OUTLINE)


def icon_crit():
    img = blank()
    circle(img, 16, 16, 9, GOLD, filled=False, thickness=2)
    circle(img, 16, 16, 3, BODY)
    for a in range(0, 360, 90):
        r = math.radians(a)
        line(img, 16 + math.cos(r) * 6, 16 + math.sin(r) * 6,
             16 + math.cos(r) * 12, 16 + math.sin(r) * 12, BODY, 2)
    for a in range(45, 360, 90):
        r = math.radians(a)
        line(img, 16 + math.cos(r) * 7, 16 + math.sin(r) * 7,
             16 + math.cos(r) * 10, 16 + math.sin(r) * 10, GOLD)
    return outlined(img, OUTLINE)


def icon_flame():
    img = blank()
    poly(img, [(16, 3), (23, 13), (23, 22), (16, 28), (9, 22), (9, 13)], BLOOD)
    poly(img, [(16, 10), (20, 17), (20, 23), (16, 26), (12, 23), (12, 17)], GOLD)
    poly(img, [(16, 16), (18, 20), (18, 23), (16, 25), (14, 23), (14, 20)], GOLD_HI)
    return outlined(img, OUTLINE)


def icon_boot():
    img = blank()
    # shaft
    poly(img, [(12, 5), (19, 5), (19, 19), (12, 19)], BODY)
    # foot + toe
    poly(img, [(12, 19), (25, 19), (26, 24), (12, 24)], BODY)
    # shaft shading
    rect(img, 17, 6, 2, 12, SHADE)
    # sole
    rect(img, 10, 24, 17, 3, INK)
    rect(img, 10, 24, 17, 1, SHADE)
    # laces
    for y in (8, 11, 14):
        line(img, 13, y, 17, y, GOLD)
    # speed chevrons
    for x in (4, 8):
        line(img, x, 10, x + 3, 13, GOLD)
        line(img, x + 3, 13, x, 16, GOLD)
    return outlined(img, OUTLINE)


def icon_heart():
    img = blank()
    circle(img, 12, 12, 6, BLOOD)
    circle(img, 20, 12, 6, BLOOD)
    poly(img, [(6, 13), (26, 13), (16, 27)], BLOOD)
    circle(img, 11, 10, 2, (190, 90, 95, 255))
    return outlined(img, OUTLINE)


def icon_magnet():
    img = blank()
    # U shape
    circle(img, 16, 18, 9, GOLD, filled=False, thickness=5)
    rect(img, 7, 8, 6, 12, GOLD)
    rect(img, 19, 8, 6, 12, GOLD)
    # poles
    rect(img, 6, 5, 8, 5, BODY)
    rect(img, 18, 5, 8, 5, BODY)
    # inner hole
    circle(img, 16, 18, 6, (0, 0, 0, 0))
    # clear above the arc
    for y in range(0, 12):
        for x in range(S):
            d = math.hypot(x - 16, y - 18)
            if 5.5 < d < 14 and y < 13 and not (5 <= x <= 12 or 19 <= x <= 26):
                if y < 14:
                    img.set(x, y, (0, 0, 0, 0))
    return outlined(img, OUTLINE)


def icon_shield():
    img = blank()
    poly(img, [(16, 3), (27, 8), (27, 17), (16, 29), (5, 17), (5, 8)], BODY)
    poly(img, [(16, 7), (23, 10), (23, 16), (16, 24), (9, 16), (9, 10)], SHADE)
    line(img, 16, 9, 16, 21, GOLD, 2)
    line(img, 11, 14, 21, 14, GOLD, 2)
    return outlined(img, OUTLINE)


def icon_star():
    img = blank()
    pts = []
    for i in range(10):
        a = -math.pi / 2 + i * math.pi / 5
        r = 13 if i % 2 == 0 else 5.5
        pts.append((16 + math.cos(a) * r, 16 + math.sin(a) * r))
    poly(img, pts, GOLD)
    poly(img, [(16, 8), (18, 14), (16, 12), (14, 14)], GOLD_HI)
    return outlined(img, OUTLINE)


def icon_ring():
    img = blank()
    circle(img, 16, 16, 11, GOLD, filled=False, thickness=3)
    circle(img, 16, 16, 5, BODY, filled=False, thickness=2)
    for a in range(0, 360, 45):
        r = math.radians(a)
        px(img, int(16 + math.cos(r) * 8), int(16 + math.sin(r) * 8), GOLD_HI)
    return outlined(img, OUTLINE)


def icon_hourglass():
    img = blank()
    poly(img, [(8, 4), (24, 4), (24, 8), (18, 16), (24, 24), (24, 28), (8, 28), (8, 24), (14, 16), (8, 8)], BODY)
    poly(img, [(13, 22), (19, 22), (16, 26)], GOLD)
    poly(img, [(14, 10), (18, 10), (16, 14)], GOLD)
    rect(img, 7, 3, 18, 2, INK)
    rect(img, 7, 27, 18, 2, INK)
    return outlined(img, OUTLINE)


def icon_dagger():
    img = blank()
    poly(img, [(16, 4), (19, 10), (19, 18), (16, 18), (16, 10)], BODY)
    line(img, 17, 7, 17, 17, SHADE)
    rect(img, 12, 18, 9, 2, GOLD)
    rect(img, 15, 20, 3, 5, (96, 70, 46, 255))
    rect(img, 14, 25, 5, 2, GOLD)
    # second small dagger
    poly(img, [(9, 12), (11, 16), (11, 21), (9, 21), (9, 16)], SHADE)
    return outlined(img, OUTLINE)


def icon_multi():
    img = blank()
    for ox in (5, 13, 21):
        poly(img, [(ox + 3, 6), (ox + 6, 12), (ox + 6, 24), (ox, 24), (ox, 12)], BODY)
        rect(img, ox - 1, 24, 8, 2, GOLD)
    return outlined(img, OUTLINE)


def icon_swirl():
    img = blank()
    cx = cy = 16
    for k in range(3):
        base = k * 2.1
        for i in range(28):
            t = i / 27
            a = base + t * 4.2
            r = 2.5 + t * 10
            x = int(cx + math.cos(a) * r)
            y = int(cy + math.sin(a) * r)
            c = GOLD if k == 0 else (GOLD_HI if k == 1 else SHADE)
            px(img, x, y, c)
            px(img, x + 1, y, c)
    return outlined(img, OUTLINE)


def icon_clock():
    img = blank()
    circle(img, 16, 17, 11, BODY)
    circle(img, 16, 17, 8, SHADE)
    line(img, 16, 17, 16, 10, INK, 2)
    line(img, 16, 17, 21, 19, GOLD, 2)
    rect(img, 12, 3, 8, 2, GOLD)
    return outlined(img, OUTLINE)


def icon_orb():
    img = blank()
    circle(img, 16, 16, 8, TEAL)
    circle(img, 13, 13, 3, (150, 200, 195, 255))
    for a in range(0, 360, 60):
        r = math.radians(a)
        px(img, int(16 + math.cos(r) * 12), int(16 + math.sin(r) * 12), GOLD)
    return outlined(img, OUTLINE)


def icon_arrow():
    img = blank()
    # shaft
    rect(img, 7, 15, 16, 3, (96, 70, 46, 255))
    rect(img, 7, 15, 16, 1, (128, 96, 64, 255))
    # head
    poly(img, [(19, 9), (29, 16), (19, 23)], GOLD)
    poly(img, [(21, 13), (26, 16), (21, 19)], GOLD_HI)
    # fletching
    poly(img, [(4, 10), (10, 15), (4, 16)], BODY)
    poly(img, [(4, 22), (10, 17), (4, 16)], SHADE)
    line(img, 8, 16, 18, 16, SHADE)
    return outlined(img, OUTLINE)


def icon_spear():
    img = blank()
    line(img, 5, 26, 22, 9, (96, 70, 46, 255), 2)
    poly(img, [(20, 11), (27, 4), (28, 12), (21, 13)], GOLD)
    rect(img, 4, 24, 4, 4, GOLD)
    return outlined(img, OUTLINE)


def icon_lightning():
    img = blank()
    poly(img, [(18, 3), (9, 18), (15, 18), (12, 29), (23, 13), (17, 13)], GOLD)
    poly(img, [(17, 6), (12, 15), (16, 15), (15, 23), (20, 14), (15, 14)], GOLD_HI)
    return outlined(img, OUTLINE)


def icon_nova():
    img = blank()
    circle(img, 16, 16, 4, GOLD_HI)
    circle(img, 16, 16, 7, BODY, filled=False, thickness=1)
    for a in range(0, 360, 30):
        r = math.radians(a)
        x0, y0 = 16 + math.cos(r) * 8, 16 + math.sin(r) * 8
        x1, y1 = 16 + math.cos(r) * 13, 16 + math.sin(r) * 13
        line(img, x0, y0, x1, y1, GOLD)
    return outlined(img, OUTLINE)


def icon_boomerang():
    img = blank()
    for i in range(26):
        t = i / 25
        a = -math.pi * 0.15 + t * math.pi * 1.1
        r = 11
        x = 16 + math.cos(a) * r
        y = 16 + math.sin(a) * r
        for ox in range(3):
            for oy in range(3):
                px(img, int(x) + ox, int(y) + oy, BODY)
    for i in range(26):
        t = i / 25
        a = -math.pi * 0.15 + t * math.pi * 1.1
        r = 11
        px(img, int(16 + math.cos(a) * r) + 1, int(16 + math.sin(a) * r) + 1, SHADE)
    return outlined(img, OUTLINE)


def icon_boss_core():
    img = blank()
    circle(img, 16, 16, 10, BLOOD)
    circle(img, 16, 16, 6, GOLD)
    circle(img, 16, 16, 3, GOLD_HI)
    for a in range(0, 360, 45):
        r = math.radians(a)
        line(img, 16 + math.cos(r) * 10, 16 + math.sin(r) * 10,
             16 + math.cos(r) * 14, 16 + math.sin(r) * 14, BLOOD)
    return outlined(img, OUTLINE)



def icon_coin():
    img = blank()
    circle(img, 16, 17, 11, GOLD)
    circle(img, 16, 17, 8, GOLD_HI)
    circle(img, 16, 17, 8, SHADE, filled=False, thickness=1)
    rect(img, 14, 12, 4, 10, GOLD)
    rect(img, 12, 15, 8, 4, GOLD)
    # stack shadow
    rect(img, 7, 27, 18, 2, (120, 96, 60, 255))
    return outlined(img, OUTLINE)


def icon_skull():
    img = blank()
    circle(img, 16, 13, 9, BODY)
    poly(img, [(10, 18), (22, 18), (21, 27), (11, 27)], BODY)
    # eye sockets
    circle(img, 12, 12, 3, (18, 22, 30, 255))
    circle(img, 20, 12, 3, (18, 22, 30, 255))
    # nose
    poly(img, [(16, 15), (18, 19), (14, 19)], (18, 22, 30, 255))
    # teeth
    for x in (13, 16, 19):
        line(img, x, 22, x, 25, (18, 22, 30, 255), 1)
    return outlined(img, OUTLINE)


def icon_time():
    img = blank()
    circle(img, 16, 17, 11, BODY)
    circle(img, 16, 17, 9, SHADE)
    # hands
    line(img, 16, 17, 16, 9, INK, 2)
    line(img, 16, 17, 22, 20, GOLD, 2)
    circle(img, 16, 17, 2, GOLD_HI)
    # top button
    rect(img, 13, 3, 6, 3, GOLD)
    return outlined(img, OUTLINE)


def icon_diff_normal():
    img = blank()
    poly(img, [(16, 3), (27, 8), (27, 17), (16, 29), (5, 17), (5, 8)], TEAL)
    poly(img, [(16, 7), (23, 10), (23, 16), (16, 24), (9, 16), (9, 10)], (150, 190, 185, 255))
    return outlined(img, OUTLINE)


def icon_diff_hard():
    img = blank()
    poly(img, [(16, 3), (27, 8), (27, 17), (16, 29), (5, 17), (5, 8)], GOLD)
    # 裂纹/尖刺
    line(img, 11, 9, 14, 20, (110, 80, 40, 255), 2)
    line(img, 21, 10, 18, 21, (110, 80, 40, 255), 2)
    poly(img, [(16, 9), (19, 15), (16, 14), (13, 15)], (110, 80, 40, 255))
    return outlined(img, OUTLINE)


def icon_diff_nightmare():
    img = blank()
    poly(img, [(16, 3), (27, 8), (27, 17), (16, 29), (5, 17), (5, 8)], BLOOD)
    # 双眼 + 獠牙
    circle(img, 11, 13, 2, GOLD_HI)
    circle(img, 21, 13, 2, GOLD_HI)
    for x in (10, 14, 18, 22):
        poly(img, [(x, 19), (x + 2, 19), (x + 1, 24)], GOLD_HI)
    return outlined(img, OUTLINE)


ICONS = {
    "ic_damage": icon_sword,
    "ic_crit": icon_crit,
    "ic_critdmg": icon_flame,
    "ic_movespeed": icon_boot,
    "ic_health": icon_heart,
    "ic_pickup": icon_magnet,
    "ic_armor": icon_shield,
    "ic_exp": icon_star,
    "ic_area": icon_ring,
    "ic_cooldown": icon_hourglass,
    "ic_weapondmg": icon_dagger,
    "ic_weaponcount": icon_multi,
    "ic_weaponspeed": icon_swirl,
    "ic_interval": icon_clock,
    "ic_w_orb": icon_orb,
    "ic_w_projectile": icon_arrow,
    "ic_w_pierce": icon_spear,
    "ic_w_lightning": icon_lightning,
    "ic_w_nova": icon_nova,
    "ic_w_boomerang": icon_boomerang,
    "ic_evo_bosscore": icon_boss_core,
    "ic_coin": icon_coin,
    "ic_skull": icon_skull,
    "ic_time": icon_time,
    "ic_diff_normal": icon_diff_normal,
    "ic_diff_hard": icon_diff_hard,
    "ic_diff_nightmare": icon_diff_nightmare,
}


def main():
    imgs = {}
    for name, fn in ICONS.items():
        im = fn()
        imgs[name] = im
        write_png(os.path.join(OUT, name + ".png"), im.w, im.h, im.d)

    # contact sheet, 6 columns, 3x scale on dark panel
    scale, cell, cols = 3, 120, 7
    rows = (len(imgs) + cols - 1) // cols
    sheet = Img(cell * cols, cell * rows, (15, 21, 29, 255))
    for i, (name, im) in enumerate(imgs.items()):
        ox = (i % cols) * cell + (cell - im.w * scale) // 2
        oy = (i // cols) * cell + (cell - im.h * scale) // 2
        for y in range(im.h * scale):
            for x in range(im.w * scale):
                c = im.get(x // scale, y // scale)
                if c[3]:
                    sheet.blend(ox + x, oy + y, c)
    write_png(os.path.join(OUT, "icons_preview.png"), sheet.w, sheet.h, sheet.d)
    print("wrote", len(imgs) + 1, "files to", OUT)


if __name__ == "__main__":
    main()