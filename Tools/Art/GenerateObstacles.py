#!/usr/bin/env python3
"""Generate top-down stone-ruin obstacle sprites (stdlib only).

Reuses the pixel helpers from GenerateBackground.py.
Output: Tools/Art/out/obstacles/*.png + contact sheet.
"""
import math
import os
import random
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from GenerateBackground import Img, write_png, rnd, vnoise, col, pebble  # noqa: E402

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out", "obstacles")
os.makedirs(OUT, exist_ok=True)


def shadow(img, cx, cy, r, alpha=70, ox=3, oy=4):
    for y in range(int(cy - r + oy), int(cy + r + oy) + 1):
        for x in range(int(cx - r + ox), int(cx + r + ox) + 1):
            dx = (x - cx - ox) / (r * 1.05)
            dy = (y - cy - oy) / (r * 0.85)
            d = dx * dx + dy * dy
            if d < 1:
                img.blend(x, y, (0, 0, 0, int(alpha * (1 - d) ** 0.8)))


def stone_value(x, y, seed, base=50, light=0.0):
    v = base + light * 16 + (vnoise(x, y, 128, 128, 6, seed) - 0.5) * 6
    return col(v, v + 7, v + 14)


def build_pillar(seed, size=64, broken=True):
    rng = random.Random(seed)
    img = Img(size, size)
    cx = cy = size // 2
    R = size * 0.43
    r = R * 0.52
    shadow(img, cx, cy, R + 2)
    notch = (-1.15, -0.55) if broken else None

    for y in range(size):
        for x in range(size):
            dx, dy = x - cx, y - cy
            d = math.hypot(dx, dy)
            if d > R:
                continue
            ang = math.atan2(dy, dx)
            if notch and notch[0] < ang < notch[1] and d > r * 0.9:
                continue
            light = (-dx - dy) / (R * 1.6)
            if d >= r:
                img.set(x, y, stone_value(x, y, seed, 52, light))
            else:
                # 断裂柱芯：更暗并带粗糙纹理
                v = 30 + (vnoise(x, y, size, size, 7, seed + 3) - 0.5) * 8
                img.set(x, y, col(v, v + 5, v + 10))

    # rim highlight / shadow + inner lip
    for y in range(size):
        for x in range(size):
            dx, dy = x - cx, y - cy
            d = math.hypot(dx, dy)
            light = (-dx - dy) / (R * 1.6)
            if R - 1.6 <= d <= R + 0.4:
                if light > 0.2:
                    img.set(x, y, (82, 92, 108, 255))
                elif light < -0.2:
                    img.set(x, y, (24, 29, 37, 255))
            if r - 1.3 <= d <= r + 0.3:
                img.blend(x, y, (14, 18, 24, 220))

    # 断口内侧的碎石与塌陷
    for _ in range(rng.randint(4, 7)):
        a = rng.uniform(0, math.pi * 2)
        rad = rng.uniform(0, r * 0.9)
        sx = int(cx + math.cos(a) * rad)
        sy = int(cy + math.sin(a) * rad)
        s2 = rng.randint(2, 4)
        v = rng.choice([40, 48, 56])
        pebble(img, sx, sy, s2, (v, v + 7, v + 14, 255), rng)

    # cracks on the shaft
    for _ in range(3):
        px = cx + rng.randint(-int(r * 0.7), int(r * 0.7))
        py = cy + rng.randint(-int(r * 0.7), int(r * 0.7))
        ang = rng.uniform(0, math.pi * 2)
        for _ in range(rng.randint(5, 12)):
            img.blend(int(px), int(py), (10, 13, 18, 180))
            ang += rng.uniform(-0.5, 0.5)
            px += math.cos(ang) * 1.5
            py += math.sin(ang) * 1.5
            if (px - cx) ** 2 + (py - cy) ** 2 > (r * 0.95) ** 2:
                break

    # rubble around the broken notch
    if broken:
        for _ in range(rng.randint(3, 6)):
            a = rng.uniform(notch[0] - 0.1, notch[1] + 0.1)
            rad = R + rng.uniform(2, 6)
            sx = int(cx + math.cos(a) * rad)
            sy = int(cy + math.sin(a) * rad)
            s = rng.randint(2, 4)
            v = rng.choice([46, 54, 62])
            pebble(img, sx, sy, s, (v, v + 7, v + 14, 255), rng)
    return img


def build_rubble(seed, size=64):
    rng = random.Random(seed)
    img = Img(size, size)
    cx = cy = size // 2
    R = size * 0.42
    shadow(img, cx, cy, R + 2, alpha=80)
    # 底部土石堆，让碎石连成一片
    for y in range(size):
        for x in range(size):
            dx = (x - cx) / (R * 1.02)
            dy = (y - cy) / (R * 0.78)
            if dx * dx + dy * dy < 1:
                img.blend(x, y, (12, 15, 20, 120))
    stones = []
    for i in range(rng.randint(26, 34)):
        a = rng.uniform(0, math.pi * 2)
        rad = abs(rng.gauss(0, 0.52)) * R
        s = rng.randint(7, 12) if rad < R * 0.55 else rng.randint(4, 8)
        x = int(cx + math.cos(a) * rad - s / 2)
        y = int(cy + math.sin(a) * rad - s / 2)
        stones.append((x, y, s))
    stones.sort(key=lambda t: -t[2])
    for x, y, s in stones:
        v = rng.choice([38, 44, 50, 56, 62])
        pebble(img, x, y, s, (v, v + 7, v + 15, 255), rng)
    return img


def build_brazier(seed, size=48):
    rng = random.Random(seed)
    img = Img(size, size)
    cx = cy = size // 2
    R = size * 0.42
    r = R * 0.56
    # warm halo
    for y in range(size):
        for x in range(size):
            d = math.hypot(x - cx, y - cy) / (R + 6)
            if d < 1:
                img.blend(x, y, (220, 110, 40, int(30 * (1 - d) ** 2)))
    shadow(img, cx, cy, R + 1, alpha=60, oy=3)
    for y in range(size):
        for x in range(size):
            dx, dy = x - cx, y - cy
            d = math.hypot(dx, dy)
            if d > R:
                continue
            light = (-dx - dy) / (R * 1.6)
            if d >= r:
                img.set(x, y, stone_value(x, y, seed, 54, light))
            else:
                img.set(x, y, (24, 18, 15, 255))
    # embers
    for _ in range(rng.randint(9, 14)):
        a = rng.uniform(0, math.pi * 2)
        rad = rng.uniform(0, r * 0.82)
        x = int(cx + math.cos(a) * rad)
        y = int(cy + math.sin(a) * rad)
        c = rng.choice([(255, 168, 80), (255, 210, 130), (220, 100, 45)])
        for yy in range(y, y + 2):
            for xx in range(x, x + 2):
                img.set(xx, yy, (c[0], c[1], c[2], 255))
        img.blend(x + 1, y + 1, (255, 235, 180, 200))
    # rim bevel
    for x in range(size):
        for y in range(size):
            dx, dy = x - cx, y - cy
            d = math.hypot(dx, dy)
            light = (-dx - dy) / (R * 1.6)
            if R - 2.0 <= d <= R + 0.4 and light > 0.1:
                img.set(x, y, (96, 106, 122, 255))
    return img


def build_statue_base(seed, size=64):
    rng = random.Random(seed)
    img = Img(size, size)
    cx = cy = size // 2
    half = size * 0.38
    shadow(img, cx, cy, half + 2, alpha=65)
    x0, y0 = int(cx - half), int(cy - half)
    s = int(half * 2)
    for y in range(y0, y0 + s):
        for x in range(x0, x0 + s):
            light = -((x - cx) + (y - cy)) / (s * 0.9)
            img.set(x, y, stone_value(x, y, seed, 50, light))
    # bevel
    for k in range(3):
        for x in range(x0 + k, x0 + s - k):
            img.set(x, y0 + k, (80, 90, 106, 255))
            img.set(x, y0 + s - 1 - k, (26, 31, 39, 255))
        for y in range(y0 + k, y0 + s - k):
            img.set(x0 + k, y, (76, 86, 102, 255))
            img.set(x0 + s - 1 - k, y, (28, 33, 42, 255))
    # carved circle
    for y in range(y0 + 8, y0 + s - 8):
        for x in range(x0 + 8, x0 + s - 8):
            d = math.hypot(x - cx, y - cy)
            if abs(d - s * 0.27) < 1.2:
                img.blend(x, y, (18, 22, 30, 200))
    # broken statue remnant
    rw = max(3, int(s * 0.10))
    rh = int(s * 0.26)
    for x in range(cx - rw, cx + rw):
        for y in range(cy - rh // 2, cy + rh // 2):
            light = -((x - cx) + (y - cy)) / 20
            img.set(x, y, stone_value(x, y, seed + 9, 56, light))
    for _ in range(rng.randint(2, 5)):
        sx = rng.randint(x0 + 4, x0 + s - 6)
        sy = rng.randint(y0 + 4, y0 + s - 6)
        img.blend(sx, sy, (16, 20, 26, 200))
    return img


def build_sarcophagus(seed, size=64):
    rng = random.Random(seed)
    img = Img(size, size)
    cx = cy = size // 2
    w, h = int(size * 0.86), int(size * 0.62)
    x0, y0 = cx - w // 2, cy - h // 2
    shadow(img, cx, cy, max(w, h) * 0.5, alpha=60)
    for y in range(y0, y0 + h):
        for x in range(x0, x0 + w):
            # rounded corners
            dx = min(x - x0, x0 + w - 1 - x)
            dy = min(y - y0, y0 + h - 1 - y)
            if dx + dy < 3:
                continue
            light = -((x - cx) + (y - cy)) / (w * 0.7)
            img.set(x, y, stone_value(x, y, seed, 48, light))
    # bevel
    for k in range(2):
        for x in range(x0 + 3, x0 + w - 3):
            img.set(x, y0 + k + 2, (78, 88, 104, 255))
            img.set(x, y0 + h - 3 - k, (24, 29, 37, 255))
        for y in range(y0 + 3, y0 + h - 3):
            img.set(x0 + k + 2, y, (74, 84, 100, 255))
            img.set(x0 + w - 3 - k, y, (26, 31, 40, 255))
    # carved lid lines
    for y in range(y0 + 8, y0 + h - 8):
        img.blend(cx, y, (18, 22, 30, 190))
    for x in range(cx - 10, cx + 11):
        img.blend(x, cy, (18, 22, 30, 190))
    # chips
    for _ in range(rng.randint(3, 6)):
        sx = rng.randint(x0 + 4, x0 + w - 6)
        sy = rng.randint(y0 + 4, y0 + h - 6)
        img.blend(sx, sy, (16, 20, 26, 200))
    return img


def build_crate(seed, size=48):
    rng = random.Random(seed)
    img = Img(size, size)
    cx = cy = size // 2
    half = size * 0.38
    x0, y0 = int(cx - half), int(cy - half)
    s = int(half * 2)
    shadow(img, cx, cy, half + 1, alpha=55)
    wood = [(96, 70, 46), (86, 62, 40), (106, 78, 52)]
    for y in range(y0, y0 + s):
        for x in range(x0, x0 + s):
            img.set(x, y, (*rng.choice(wood), 255))
    # planks
    for i in range(1, 4):
        yy = y0 + s * i // 4
        for x in range(x0, x0 + s):
            img.blend(x, yy, (54, 38, 26, 200))
    # metal bands
    for yy in (y0 + 2, y0 + s - 4):
        for x in range(x0, x0 + s):
            img.set(x, yy, (72, 78, 88, 255))
            img.set(x, yy + 1, (54, 60, 70, 255))
    # bevel
    for x in range(x0, x0 + s):
        img.set(x, y0, (128, 96, 64, 255))
        img.set(x, y0 + s - 1, (52, 36, 24, 255))
    for y in range(y0, y0 + s):
        img.set(x0, y, (120, 90, 60, 255))
        img.set(x0 + s - 1, y, (56, 40, 28, 255))
    # nails
    for nx in (x0 + 5, x0 + s - 6):
        for ny in (y0 + 5, y0 + s - 6):
            img.set(nx, ny, (150, 150, 156, 255))
    return img


def paste_scaled(dst, src, ox, oy, scale, bg=(32, 38, 48, 255)):
    for y in range(src.h * scale):
        for x in range(src.w * scale):
            c = src.get(x // scale, y // scale)
            if c[3] == 0:
                continue
            dst.blend(ox + x, oy + y, c)


def main():
    sprites = {
        "obstacle_pillar_a": build_pillar(11, 64, True),
        "obstacle_pillar_b": build_pillar(12, 64, False),
        "obstacle_rubble_a": build_rubble(21, 64),
        "obstacle_brazier_a": build_brazier(31, 48),
        "obstacle_statue_a": build_statue_base(41, 64),
        "obstacle_sarcophagus_a": build_sarcophagus(51, 64),
        "obstacle_crate_a": build_crate(61, 48),
    }
    for name, im in sprites.items():
        write_png(os.path.join(OUT, name + ".png"), im.w, im.h, im.d)

    # contact sheet: 4 columns, 2 rows, 2x scale
    cell = 144
    cols = 4
    rows = 2
    sheet = Img(cell * cols, cell * rows, (24, 28, 35, 255))
    for i, (name, im) in enumerate(sprites.items()):
        ox = (i % cols) * cell + (cell - im.w * 2) // 2
        oy = (i // cols) * cell + (cell - im.h * 2) // 2
        paste_scaled(sheet, im, ox, oy, 2)
    write_png(os.path.join(OUT, "obstacles_preview.png"), sheet.w, sheet.h, sheet.d)
    print("wrote", len(sprites) + 1, "files to", OUT)


if __name__ == "__main__":
    main()