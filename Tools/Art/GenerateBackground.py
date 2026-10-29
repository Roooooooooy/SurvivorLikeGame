#!/usr/bin/env python3
"""Generate pixel-art dungeon floor, grunge overlay and decals (stdlib only)."""
import math, os, random, struct, zlib

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "out")
os.makedirs(OUT, exist_ok=True)


# ---------- PNG ----------
def write_png(path, w, h, data):
    raw = bytearray()
    stride = w * 4
    for y in range(h):
        raw.append(0)
        raw += data[y * stride:(y + 1) * stride]

    def chunk(tag, body):
        return (struct.pack(">I", len(body)) + tag + body +
                struct.pack(">I", zlib.crc32(tag + body) & 0xFFFFFFFF))

    png = (b"\x89PNG\r\n\x1a\n" +
           chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0)) +
           chunk(b"IDAT", zlib.compress(bytes(raw), 9)) +
           chunk(b"IEND", b""))
    with open(path, "wb") as f:
        f.write(png)


class Img:
    def __init__(self, w, h, bg=(0, 0, 0, 0)):
        self.w, self.h = w, h
        self.d = bytearray(w * h * 4)
        if bg[3]:
            for i in range(w * h):
                self.d[i * 4:i * 4 + 4] = bytes(bg)

    def set(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            i = (y * self.w + x) * 4
            self.d[i:i + 4] = bytes(c)

    def get(self, x, y):
        i = (y * self.w + x) * 4
        return tuple(self.d[i:i + 4])

    def blend(self, x, y, c, wrap=False):
        if wrap:
            x %= self.w
            y %= self.h
        elif not (0 <= x < self.w and 0 <= y < self.h):
            return
        i = (y * self.w + x) * 4
        sa = c[3]
        if sa >= 255:
            self.d[i:i + 4] = bytes((c[0], c[1], c[2], 255))
            return
        if sa <= 0:
            return
        da = self.d[i + 3]
        outa = sa + da * (255 - sa) // 255
        if outa == 0:
            return
        for k in range(3):
            sc = c[k] * sa
            dc = self.d[i + k] * (da * (255 - sa) // 255)
            self.d[i + k] = min(255, (sc + dc) // outa)
        self.d[i + 3] = outa

    def rect(self, x0, y0, w, h, c, wrap=False):
        for yy in range(y0, y0 + h):
            for xx in range(x0, x0 + w):
                self.blend(xx, yy, c, wrap)


# ---------- helpers ----------
def h32(x, y, s):
    n = (x * 374761393 + y * 668265263 + s * 2246822519 + 0x9E3779B9) & 0xFFFFFFFF
    n = ((n ^ (n >> 13)) * 1274126177) & 0xFFFFFFFF
    n ^= n >> 16
    return n


def rnd(x, y, s):
    return h32(int(x), int(y), s) / 4294967295.0


def smooth(t):
    return t * t * (3 - 2 * t)


def vnoise(x, y, pw, ph, cells, seed):
    """Tileable value noise over a pw x ph domain, with `cells` lattice cells."""
    fx = x / pw * cells
    fy = y / ph * cells
    ix, iy = int(math.floor(fx)), int(math.floor(fy))
    tx, ty = smooth(fx - ix), smooth(fy - iy)
    x0, x1 = ix % cells, (ix + 1) % cells
    y0, y1 = iy % cells, (iy + 1) % cells
    a = rnd(x0, y0, seed)
    b = rnd(x1, y0, seed)
    c = rnd(x0, y1, seed)
    d = rnd(x1, y1, seed)
    return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty


def clamp(v, lo=0, hi=255):
    return lo if v < lo else hi if v > hi else int(v)


def col(r, g, b, a=255):
    return (clamp(r), clamp(g), clamp(b), clamp(a))


# ---------- floor ----------
MORTAR = (14, 17, 22, 255)
STONE_BASE = [40, 43, 46, 49, 52, 45]  # cold, low-contrast values


def build_floor(seed=7):
    W = H = 512
    img = Img(W, H, MORTAR)
    rng = random.Random(seed)

    # 行高不等，避免整齐的“砖墙”节奏
    row_heights = [72, 64, 56, 80, 56, 72, 64, 48]
    widths_pool = [64, 72, 80, 88, 96, 112, 128]
    offsets = [0, 96, 32, 144, 64, 176, 16, 128]

    y0 = 0
    for ry, row_h in enumerate(row_heights):
        widths = []
        total = 0
        while total < W - 72:
            w = rng.choice(widths_pool)
            if total + w > W - 72:
                break
            widths.append(w)
            total += w
        widths.append(W - total)
        x = offsets[ry] % W
        for bw in widths:
            shade = rng.choice(STONE_BASE) + rng.randint(-4, 4)
            warm = rng.randint(-2, 2)
            sx, sy = x + 3, y0 + 3
            sw, sh = bw - 6, row_h - 6
            for yy in range(sy, sy + sh):
                for xx in range(sx, sx + sw):
                    n = (rnd(xx, yy, seed) - 0.5) * 7.0
                    b = (vnoise(xx, yy, W, H, 8, seed + 11) - 0.5) * 8.0
                    v = shade + n + b
                    img.blend(xx, yy, col(v + warm, v + 6, v + 12 - warm), wrap=True)
            for k in range(2):
                for xx in range(sx, sx + sw):
                    img.blend(xx, sy + k, col(shade + 14, shade + 20, shade + 27), wrap=True)
                    img.blend(xx, sy + sh - 1 - k, col(shade - 12, shade - 7, shade - 2), wrap=True)
                for yy in range(sy, sy + sh):
                    img.blend(sx + k, yy, col(shade + 12, shade + 18, shade + 25), wrap=True)
                    img.blend(sx + sw - 1 - k, yy, col(shade - 11, shade - 6, shade - 1), wrap=True)
            for _ in range(rng.randint(0, 2)):
                cx = sx + rng.choice([0, sw - 1])
                cy = sy + rng.choice([0, sh - 1])
                sz = rng.randint(2, 5)
                ox = 0 if cx == sx else -sz + 1
                oy = 0 if cy == sy else -sz + 1
                for yy in range(cy + oy, cy + oy + sz):
                    for xx in range(cx + ox, cx + ox + sz):
                        if rnd(xx, yy, seed + 5) > 0.35:
                            img.blend(xx, yy, MORTAR, wrap=True)
            if rng.random() < 0.18:
                px = sx + rng.randint(4, max(5, sw - 5))
                py = sy + rng.randint(3, max(4, sh - 4))
                for _ in range(rng.randint(8, 20)):
                    img.blend(px, py, (8, 10, 14, 130), wrap=True)
                    px += rng.choice([-1, 0, 1])
                    py += rng.choice([0, 1])
                    if not (sx <= px < sx + sw and sy <= py < sy + sh):
                        break
            for _ in range(rng.randint(0, 3)):
                xx = sx + rng.randint(0, sw - 1)
                yy = sy + rng.randint(0, sh - 1)
                light = rng.random() < 0.5
                img.blend(xx, yy, (shade + 18, shade + 24, shade + 31, 160) if light
                          else (shade - 12, shade - 7, shade - 2, 140), wrap=True)
            x = (x + bw) % W
        y0 += row_h
    return img


# ---------- grunge overlay ----------
def build_grunge(seed=31):
    W = H = 512
    img = Img(W, H)
    rng = random.Random(seed)
    blobs = []
    for _ in range(15):
        blobs.append((rng.uniform(0, W), rng.uniform(0, H), rng.uniform(60, 160),
                      rng.choice(["dark", "dark", "cold", "warm"]),
                      rng.uniform(0.10, 0.20)))
    for cx, cy, rad, kind, alpha in blobs:
        if kind == "dark":
            base = (5, 7, 11)
        elif kind == "cold":
            base = (60, 82, 104)
            alpha *= 0.65
        else:
            base = (150, 82, 36)
            alpha *= 0.9
        for oy in (-H, 0, H):
            for ox in (-W, 0, W):
                x0 = int(cx + ox - rad)
                x1 = int(cx + ox + rad)
                y0 = int(cy + oy - rad)
                y1 = int(cy + oy + rad)
                for yy in range(y0, y1):
                    dy = yy - (cy + oy)
                    for xx in range(x0, x1):
                        dx = xx - (cx + ox)
                        d = math.sqrt(dx * dx + dy * dy) / rad
                        if d >= 1:
                            continue
                        f = (1 - d) ** 2
                        a = int(alpha * 255 * f)
                        if a > 0:
                            img.blend(xx, yy, (base[0], base[1], base[2], a), wrap=True)
    # sparse scratches
    for _ in range(9):
        x = rng.randint(0, W)
        y = rng.randint(0, H)
        ln = rng.randint(20, 70)
        ang = rng.uniform(0, math.pi)
        for i in range(ln):
            xx = int(x + math.cos(ang) * i)
            yy = int(y + math.sin(ang) * i)
            img.blend(xx, yy, (70, 84, 100, rng.randint(6, 14)), wrap=True)
    return img


# ---------- decals ----------
def new_decal(w, h):
    return Img(w, h)


def pebble(img, x, y, s, base, rng):
    hi = (min(255, base[0] + 26), min(255, base[1] + 26), min(255, base[2] + 26), 255)
    sh = (max(0, base[0] - 16), max(0, base[1] - 16), max(0, base[2] - 14), 255)
    for yy in range(y, y + s):
        for xx in range(x, x + s):
            if rnd(xx, yy, 99) < 0.12:
                continue
            img.set(xx, yy, base)
    for xx in range(x, x + s):
        img.set(xx, y, hi)
    for yy in range(y, y + s):
        img.set(x, yy, hi)
    for xx in range(x, x + s):
        img.set(xx, y + s - 1, sh)
    for yy in range(y, y + s):
        img.set(x + s - 1, yy, sh)
    if rng.random() < 0.5:
        img.set(x + s // 2, y + s // 2, hi)


def build_rubble(seed, count, size=32):
    rng = random.Random(seed)
    img = new_decal(size, size)
    for _ in range(count):
        s = rng.randint(3, 7)
        x = rng.randint(2, size - s - 2)
        y = rng.randint(2, size - s - 2)
        v = rng.choice([52, 60, 68])
        pebble(img, x, y, s, (v, v + 6, v + 14, 255), rng)
    return img


def build_crack(seed, size=64, wide=False):
    rng = random.Random(seed)
    img = new_decal(size, size)
    x, y = size // 2 + rng.randint(-6, 6), rng.randint(4, 10)
    ang = math.pi / 2 + rng.uniform(-0.5, 0.5)
    steps = rng.randint(26, 38)
    for i in range(steps):
        ang += rng.uniform(-0.35, 0.35)
        x += math.cos(ang) * 3
        y += math.sin(ang) * 3
        if not (2 <= x < size - 2 and 2 <= y < size - 2):
            break
        ix, iy = int(x), int(y)
        img.blend(ix, iy, (10, 13, 18, 200))
        if wide:
            img.blend(ix + 1, iy, (8, 10, 15, 170))
            img.blend(ix, iy - 1, (18, 22, 29, 140))
        if rng.random() < 0.18:
            bx, by = ix, iy
            b_ang = ang + rng.choice([-1, 1]) * rng.uniform(0.6, 1.2)
            for _ in range(rng.randint(4, 9)):
                b_ang += rng.uniform(-0.4, 0.4)
                bx += math.cos(b_ang) * 3
                by += math.sin(b_ang) * 3
                if 0 <= bx < size and 0 <= by < size:
                    img.blend(int(bx), int(by), (12, 15, 20, 160))
    return img


def build_ember(seed, size=64, dots=False):
    rng = random.Random(seed)
    img = new_decal(size, size)
    if dots:
        for _ in range(rng.randint(6, 10)):
            cx = rng.randint(8, size - 8)
            cy = rng.randint(8, size - 8)
            # scorch
            for yy in range(cy - 4, cy + 5):
                for xx in range(cx - 4, cx + 5):
                    d = math.hypot(xx - cx, yy - cy)
                    if d <= 4:
                        img.blend(xx, yy, (12, 10, 9, int(30 * (1 - d / 4))))
            # glow
            for yy in range(cy - 7, cy + 8):
                for xx in range(cx - 7, cx + 8):
                    d = math.hypot(xx - cx, yy - cy)
                    if d <= 7:
                        img.blend(xx, yy, (220, 110, 40, int(48 * (1 - d / 7) ** 2)))
            img.set(cx, cy, (255, 168, 80, 255))
            img.set(cx + 1, cy, (240, 130, 52, 255))
            img.set(cx, cy + 1, (210, 96, 38, 255))
        return img
    # ember fissure
    x, y = size // 2 + rng.randint(-6, 6), rng.randint(6, 12)
    ang = math.pi / 2 + rng.uniform(-0.4, 0.4)
    for i in range(rng.randint(24, 34)):
        ang += rng.uniform(-0.3, 0.3)
        x += math.cos(ang) * 3
        y += math.sin(ang) * 3
        if not (4 <= x < size - 4 and 4 <= y < size - 4):
            break
        ix, iy = int(x), int(y)
        # glow halo
        for yy in range(iy - 4, iy + 5):
            for xx in range(ix - 4, ix + 5):
                d = math.hypot(xx - ix, yy - iy)
                if d <= 4:
                    img.blend(xx, yy, (200, 100, 40, int(44 * (1 - d / 4) ** 2)))
        # char edges + hot core
        img.blend(ix - 1, iy, (24, 18, 16, 190))
        img.blend(ix + 1, iy, (24, 18, 16, 190))
        img.blend(ix, iy, (255, 150, 64, 255))
        if rng.random() < 0.3:
            img.set(ix, iy, (255, 210, 140, 255))
    return img


def build_stain(seed, size=96):
    rng = random.Random(seed)
    img = new_decal(size, size)
    for _ in range(7):
        cx = rng.randint(16, size - 16)
        cy = rng.randint(16, size - 16)
        rad = rng.uniform(10, 26)
        for yy in range(int(cy - rad), int(cy + rad + 1)):
            for xx in range(int(cx - rad), int(cx + rad + 1)):
                d = math.hypot(xx - cx, yy - cy) / rad
                if d < 1:
                    a = int(52 * (1 - d) ** 1.6)
                    img.blend(xx, yy, (7, 9, 13, a))
    return img


def build_bones(seed, size=48):
    rng = random.Random(seed)
    img = new_decal(size, size)
    bone = (176, 168, 148, 235)
    bone_sh = (118, 110, 94, 235)
    # skull
    sx, sy = rng.randint(10, 18), rng.randint(8, 16)
    for yy in range(sy, sy + 9):
        for xx in range(sx, sx + 9):
            if (xx - sx - 4) ** 2 / 16 + (yy - sy - 4) ** 2 / 20 <= 1:
                img.set(xx, yy, bone)
    for xx in range(sx + 2, sx + 7):
        img.set(xx, sy + 8, bone_sh)
    img.set(sx + 2, sy + 3, (18, 16, 14, 255))
    img.set(sx + 6, sy + 3, (18, 16, 14, 255))
    img.set(sx + 4, sy + 5, (18, 16, 14, 255))
    # two bones
    for _ in range(2):
        x = rng.randint(6, size - 16)
        y = rng.randint(20, size - 8)
        ln = rng.randint(8, 13)
        ang = rng.uniform(-0.4, 0.4)
        for i in range(ln):
            xx = int(x + math.cos(ang) * i)
            yy = int(y + math.sin(ang) * i)
            img.set(xx, yy, bone)
            img.set(xx, yy + 1, bone_sh)
        for ex in (x, x + int(math.cos(ang) * (ln - 1))):
            ey = y if ex == x else y + int(math.sin(ang) * (ln - 1))
            img.set(ex, ey - 1, bone)
            img.set(ex + 1, ey, bone)
    return img


def build_dust(seed, size=64):
    rng = random.Random(seed)
    img = new_decal(size, size)
    for _ in range(5):
        cx = rng.randint(12, size - 12)
        cy = rng.randint(12, size - 12)
        rad = rng.uniform(8, 20)
        for yy in range(int(cy - rad), int(cy + rad + 1)):
            for xx in range(int(cx - rad), int(cx + rad + 1)):
                d = math.hypot(xx - cx, yy - cy) / rad
                if d < 1 and rnd(xx, yy, seed) > 0.35:
                    img.blend(xx, yy, (118, 132, 148, int(18 * (1 - d))) )
    return img


def build_debris(seed, size=96):
    rng = random.Random(seed)
    img = new_decal(size, size)
    for _ in range(rng.randint(4, 7)):
        s = rng.randint(6, 14)
        x = rng.randint(3, size - s - 3)
        y = rng.randint(3, size - s - 3)
        v = rng.choice([44, 52, 60])
        for yy in range(y, y + s):
            for xx in range(x, x + s):
                dx = min(xx - x, x + s - 1 - xx)
                dy = min(yy - y, y + s - 1 - yy)
                if dx + dy < 2:
                    continue
                img.set(xx, yy, (v + 4, v + 10, v + 18, 255))
        for xx in range(x + 1, x + s - 1):
            img.set(xx, y + 1, (v + 20, v + 26, v + 34, 255))
            img.set(xx, y + s - 2, (v - 16, v - 11, v - 6, 255))
        for yy in range(y + 1, y + s - 1):
            img.set(x + 1, yy, (v + 18, v + 24, v + 32, 255))
            img.set(x + s - 2, yy, (v - 14, v - 9, v - 4, 255))
        # 表面裂纹
        px, py = x + rng.randint(2, s - 3), y + rng.randint(2, s - 3)
        for _ in range(rng.randint(4, 9)):
            img.blend(px, py, (12, 15, 20, 190))
            px += rng.choice([-1, 0, 1])
            py += rng.choice([0, 1])
            if not (x <= px < x + s and y <= py < y + s):
                break
    return img


# ---------- preview ----------
def composite_preview(floor, grunge, decals):
    S = 1536
    prev = Img(S, S, (10, 13, 18, 255))
    # 3x3 floor
    for ty in range(3):
        for tx in range(3):
            for y in range(512):
                for x in range(512):
                    i = (y * 512 + x) * 4
                    prev.d[((ty * 512 + y) * S + (tx * 512 + x)) * 4:((ty * 512 + y) * S + (tx * 512 + x)) * 4 + 4] = floor.d[i:i + 4]
    # grunge drawn at 4x (period 2048 px = 4 tiles)
    for y in range(S):
        gy = y // 4
        for x in range(S):
            gx = x // 4
            i = (gy * 512 + gx) * 4
            if grunge.d[i + 3]:
                prev.blend(x, y, tuple(grunge.d[i:i + 4]))
    # decals
    rng = random.Random(5)
    names = list(decals)
    weights = [26, 22, 18, 12, 8, 7, 8, 3, 8, 4]
    for _ in range(56):
        name = rng.choices(names, weights=weights)[0]
        im = decals[name]
        x = rng.randint(0, S - im.w)
        y = rng.randint(0, S - im.h)
        for yy in range(im.h):
            for xx in range(im.w):
                c = im.get(xx, yy)
                if c[3]:
                    prev.blend(x + xx, y + yy, c)
    return prev


def main():
    floor = build_floor()
    grunge = build_grunge()
    decals = {
        "decal_rubble_a": build_rubble(101, 7),
        "decal_rubble_b": build_rubble(102, 4),
        "decal_crack_a": build_crack(201, 64, False),
        "decal_crack_b": build_crack(202, 64, True),
        "decal_ember_a": build_ember(301, 64, False),
        "decal_ember_b": build_ember(302, 48, True),
        "decal_stain_a": build_stain(401),
        "decal_bones_a": build_bones(501),
        "decal_dust_a": build_dust(601),
        "decal_debris_a": build_debris(701),
    }
    write_png(os.path.join(OUT, "bg_stone_floor.png"), 512, 512, floor.d)
    write_png(os.path.join(OUT, "bg_grunge_large.png"), 512, 512, grunge.d)
    for name, im in decals.items():
        write_png(os.path.join(OUT, name + ".png"), im.w, im.h, im.d)
    prev = composite_preview(floor, grunge, decals)
    write_png(os.path.join(OUT, "preview.png"), prev.w, prev.h, prev.d)
    print("wrote", len(decals) + 3, "files to", OUT)


if __name__ == "__main__":
    main()