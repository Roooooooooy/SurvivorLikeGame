#!/usr/bin/env python3
"""最小 PNG 读写 + v1 图像几何分析（仅用标准库）。

用法：
    python3 PixelMarkings.py analyze <in.png>
"""
import struct
import sys
import zlib

PNG_SIG = b"\x89PNG\r\n\x1a\n"


def read_png(path):
    with open(path, "rb") as f:
        data = f.read()
    if data[:8] != PNG_SIG:
        raise ValueError("not a PNG")
    pos = 8
    width = height = bit_depth = color_type = interlace = None
    idat = bytearray()
    while pos < len(data):
        (length,) = struct.unpack(">I", data[pos:pos + 4])
        ctype = data[pos + 4:pos + 8]
        chunk = data[pos + 8:pos + 8 + length]
        pos += 12 + length
        if ctype == b"IHDR":
            width, height, bit_depth, color_type, comp, filt, interlace = struct.unpack(
                ">IIBBBBB", chunk)
        elif ctype == b"IDAT":
            idat += chunk
        elif ctype == b"IEND":
            break
    if bit_depth != 8 or interlace != 0:
        raise ValueError(f"unsupported PNG: depth={bit_depth} interlace={interlace}")
    channels = {0: 1, 2: 3, 4: 2, 6: 4}.get(color_type)
    if channels is None:
        raise ValueError(f"unsupported color type {color_type}")
    raw = zlib.decompress(bytes(idat))
    stride = width * channels
    rows = []
    prev = bytearray(stride)
    p = 0
    for _ in range(height):
        ft = raw[p]
        p += 1
        line = bytearray(raw[p:p + stride])
        p += stride
        if ft == 0:
            pass
        elif ft == 1:
            for i in range(channels, stride):
                line[i] = (line[i] + line[i - channels]) & 0xFF
        elif ft == 2:
            for i in range(stride):
                line[i] = (line[i] + prev[i]) & 0xFF
        elif ft == 3:
            for i in range(stride):
                left = line[i - channels] if i >= channels else 0
                line[i] = (line[i] + ((left + prev[i]) >> 1)) & 0xFF
        elif ft == 4:
            for i in range(stride):
                a = line[i - channels] if i >= channels else 0
                b = prev[i]
                c = prev[i - channels] if i >= channels else 0
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[i] = (line[i] + pr) & 0xFF
        else:
            raise ValueError(f"bad filter {ft}")
        rows.append(line)
        prev = line
    return width, height, channels, rows


def encode_png(path, width, height, channels, rows):
    color_type = 2 if channels == 3 else 6
    raw = bytearray()
    for row in rows:
        raw.append(0)
        raw += row

    def chunk(ctype, data):
        return (struct.pack(">I", len(data)) + ctype + data +
                struct.pack(">I", zlib.crc32(ctype + data) & 0xFFFFFFFF))

    out = bytearray(PNG_SIG)
    out += chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, color_type, 0, 0, 0))
    out += chunk(b"IDAT", zlib.compress(bytes(raw), 6))
    out += chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(out)


def px(rows, channels, x, y):
    o = x * channels
    r = rows[y]
    if channels >= 3:
        return r[o], r[o + 1], r[o + 2]
    v = r[o]
    return v, v, v


def is_bg(r, g, b, bg, tol=24):
    return (abs(r - bg[0]) <= tol and abs(g - bg[1]) <= tol and
            abs(b - bg[2]) <= tol)


def analyze(path):
    w, h, ch, rows = read_png(path)
    bg = px(rows, ch, 0, 0)
    print(f"background sample: #{bg[0]:02x}{bg[1]:02x}{bg[2]:02x}")
    minx, miny, maxx, maxy = w, h, -1, -1
    for y in range(h):
        for x in range(w):
            r, g, b = px(rows, ch, x, y)
            if not is_bg(r, g, b, bg):
                minx = min(minx, x)
                maxx = max(maxx, x)
                miny = min(miny, y)
                maxy = max(maxy, y)
    bw, bh = maxx - minx + 1, maxy - miny + 1
    print(f"image: {w}x{h} channels={ch}")
    print(f"character bbox: x={minx}..{maxx} y={miny}..{maxy}  ({bw}x{bh})")
    print(f"  relative: cx={((minx+maxx)/2-minx)/bw:.3f}  head_top=0.000  feet=1.000")

    # 颜色簇统计（量化到 16 级）
    hist = {}
    for y in range(miny, maxy + 1):
        for x in range(minx, maxx + 1):
            r, g, b = px(rows, ch, x, y)
            if is_bg(r, g, b, bg):
                continue
            key = (r // 16 * 16, g // 16 * 16, b // 16 * 16)
            hist[key] = hist.get(key, 0) + 1
    print("top colors:")
    for (r, g, b), n in sorted(hist.items(), key=lambda kv: -kv[1])[:14]:
        print(f"  #{r:02x}{g:02x}{b:02x}  {n}")

    # 棕色短裤 bbox
    brown = []
    amber = []
    white = 0
    for y in range(miny, maxy + 1):
        for x in range(minx, maxx + 1):
            r, g, b = px(rows, ch, x, y)
            if is_bg(r, g, b, bg):
                continue
            if r > g + 12 and g > b + 8 and 40 < r < 180:
                brown.append((x, y))
            if r > 150 and 90 < g < 200 and b < 90:
                amber.append((x, y))
            if r > 225 and g > 225 and b > 225:
                white += 1
    for name, pts in (("brown/shorts", brown), ("amber/eyes", amber)):
        if pts:
            xs = [p[0] for p in pts]
            ys = [p[1] for p in pts]
            print(f"{name}: x={min(xs)}..{max(xs)} y={min(ys)}..{max(ys)} n={len(pts)}")
    print(f"white pixels: {white}")


def luminance(r, g, b):
    return 0.299 * r + 0.587 * g + 0.114 * b


def is_fur_like(r, g, b):
    return max(r, g, b) - min(r, g, b) <= 38


def is_colored(r, g, b):
    if r > 180 and b > 180 and g < 90:          # magenta bg
        return True
    if r > g + 10 and g > b + 6 and r < 205:    # brown leather
        return True
    if r > g + 35 and g > b + 10:               # amber eyes
        return True
    if r > g + 15 and b > g + 5:                # pink nose
        return True
    return False


def set_px(rows, channels, x, y, rgb):
    o = x * channels
    rows[y][o] = rgb[0]
    rows[y][o + 1] = rgb[1]
    rows[y][o + 2] = rgb[2]


def point_in_poly(x, y, poly):
    inside = False
    j = len(poly) - 1
    for i in range(len(poly)):
        xi, yi = poly[i]
        xj, yj = poly[j]
        if (yi > y) != (yj > y) and x < (xj - xi) * (y - yi) / (yj - yi) + xi:
            inside = not inside
        j = i
    return inside


def in_ruff(x, y):
    dx = (x - 700) / 132.0
    dy = (y - 330) / 64.0
    if dx * dx + dy * dy > 1.0:
        return False
    if y > 350 and not (600 <= x <= 800):
        return False
    if y > 372 and not (620 <= x <= 780):
        return False
    return True


LEFT_LEG = [(590, 618), (712, 618), (700, 680), (670, 733), (586, 733), (590, 690)]
RIGHT_LEG = [(712, 618), (832, 618), (845, 700), (845, 733), (742, 733), (712, 690)]


def in_legs(x, y):
    if y < 618:
        return False
    return point_in_poly(x, y, LEFT_LEG) or point_in_poly(x, y, RIGHT_LEG)


def recolor(in_path, out_path, neck=True, legs=True):
    w, h, ch, rows = read_png(in_path)
    base = [bytearray(r) for r in rows]

    # 1) 抹掉原来的额前白斑，换成周围灰毛的最近邻颜色
    sources = []
    for y in range(136, 218):
        for x in range(698, 778):
            if 718 <= x <= 756 and 146 <= y <= 206:
                continue
            r, g, b = px(rows, ch, x, y)
            if is_fur_like(r, g, b) and 72 < luminance(r, g, b) < 196:
                sources.append((x, y, (r, g, b)))
    for y in range(146, 207):
        for x in range(718, 757):
            r, g, b = px(rows, ch, x, y)
            if r > 195 and g > 195 and b > 195 and max(r, g, b) - min(r, g, b) <= 30:
                best = None
                bestd = 1e18
                for sx, sy, col in sources:
                    d = (sx - x) ** 2 + (sy - y) ** 2
                    if d < bestd:
                        bestd = d
                        best = col
                if best is not None:
                    set_px(base, ch, x, y, best)

    # 2) 颈部一圈灰毛 -> 白毛（可选）
    if neck:
        for y in range(260, 430):
            for x in range(550, 860):
                if not in_ruff(x, y):
                    continue
                r, g, b = px(rows, ch, x, y)
                if is_colored(r, g, b):
                    continue
                lum = luminance(r, g, b)
                if lum < 62 or not is_fur_like(r, g, b):
                    continue
                t = max(0.0, min(1.0, (lum - 70.0) / 160.0))
                v = int(214 + 38 * t)
                set_px(base, ch, x, y, (v, v, min(255, v + 12)))

    # 3) 两条腿的灰毛 -> 白毛（可选）
    if legs:
        for y in range(618, 740):
            for x in range(560, 860):
                if not in_legs(x, y):
                    continue
                r, g, b = px(rows, ch, x, y)
                if is_colored(r, g, b):
                    continue
                lum = luminance(r, g, b)
                if lum < 62 or not is_fur_like(r, g, b):
                    continue
                t = max(0.0, min(1.0, (lum - 70.0) / 160.0))
                v = int(214 + 38 * t)
                set_px(base, ch, x, y, (v, v, min(255, v + 12)))

    # 4) 新菱形：狭长、纯白
    cx, top, bottom, half_w = 736, 152, 238, 11
    cy = (top + bottom) / 2.0
    half_h = (bottom - top) / 2.0
    for y in range(top, bottom + 1):
        t = 1.0 - abs(y - cy) / half_h
        half = int(round(half_w * t))
        for x in range(cx - half, cx + half + 1):
            set_px(base, ch, x, y, (252, 252, 255))

    encode_png(out_path, w, h, ch, base)
    print(f"saved {out_path}")


if __name__ == "__main__":
    if len(sys.argv) < 3:
        sys.exit("usage: PixelMarkings.py analyze|recolor <in.png> [out.png]")
    if sys.argv[1] == "analyze":
        analyze(sys.argv[2])
    elif sys.argv[1] == "recolor":
        if len(sys.argv) < 4:
            sys.exit("usage: PixelMarkings.py recolor <in.png> <out.png> [--no-neck] [--no-legs]")
        recolor(sys.argv[2], sys.argv[3],
                neck="--no-neck" not in sys.argv,
                legs="--no-legs" not in sys.argv)
    else:
        sys.exit("unknown command")
