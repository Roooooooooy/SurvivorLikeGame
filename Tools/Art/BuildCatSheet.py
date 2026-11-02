#!/usr/bin/env python3
"""把生图模型产出的 2x2 行走 sheet 切成游戏帧。

用法：
    python BuildCatSheet.py <raw_sheet.png> <out_dir>

raw_sheet.png 必须是 2x2 网格、品红底、每格一只同一只猫。
脚本会：裁掉格子边框 -> 抠品红 -> 按脚底对齐 -> 缩到 44px -> 限色/描边/描轮廓光，
最后输出 <out_dir>/player_cat_sheet.png（4 帧 x 64x48）和预览图。

依赖 Pillow：python3 -m pip install "Pillow>=10.1"
"""
import os
import sys

from PIL import Image

SRC = sys.argv[1] if len(sys.argv) > 1 else "raw_sheet.png"
OUT_DIR = sys.argv[2] if len(sys.argv) > 2 else "frames"
CELL_H = 44          # cat height in the output cell
CANVAS = (64, 48)    # output cell size
GROUND_Y = 46        # paw line inside the canvas

# detected borders (inclusive)
ROW_BANDS = [(0, 510), (513, 1023)]
COL_BANDS = [(0, 510), (513, 1023)]


def key_magenta(im, tol=70):
    """Return RGBA with the magenta background removed."""
    im = im.convert("RGB")
    w, h = im.size
    out = Image.new("RGBA", (w, h))
    src = im.load()
    dst = out.load()
    for y in range(h):
        for x in range(w):
            r, g, b = src[x, y]
            # distance to pure magenta
            dm = ((r - 255) ** 2 + g ** 2 + (b - 255) ** 2) ** 0.5
            if dm < tol:
                dst[x, y] = (0, 0, 0, 0)
            else:
                # decontaminate magenta spill on edge pixels
                a = 255 if dm > tol * 1.6 else int(255 * (dm - tol) / (tol * 0.6))
                a = max(0, min(255, a))
                dst[x, y] = (r, g, b, a)
    return out



OUTLINE = (10, 14, 20)
RIM = (176, 208, 226)


def crispify(img, colors=24, outline=True, rim=True):
    """Binary alpha, limited palette, 1px dark outline and a top-left rim light."""
    img = img.convert("RGBA")
    w, h = img.size
    px = img.load()

    # 1) binary alpha
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            px[x, y] = (r, g, b, 255) if a >= 128 else (0, 0, 0, 0)

    # 2) palette reduction on opaque pixels (median cut over a flattened copy)
    rgb = Image.new("RGB", (w, h), (0, 0, 0))
    m = rgb.load()
    for y in range(h):
        for x in range(w):
            if px[x, y][3]:
                m[x, y] = px[x, y][:3]
    pal = rgb.quantize(colors=colors, method=Image.MEDIANCUT)
    palpx = pal.convert("RGB").load()
    for y in range(h):
        for x in range(w):
            if px[x, y][3]:
                r, g, b = palpx[x, y]
                px[x, y] = (r, g, b, 255)

    if rim:
        marks = []
        for y in range(h):
            for x in range(w):
                if not px[x, y][3]:
                    continue
                up = px[x, y - 1][3] if y > 0 else 0
                left = px[x - 1, y][3] if x > 0 else 0
                if up == 0 or left == 0:
                    marks.append((x, y))
        for (x, y) in marks:
            r, g, b, a = px[x, y]
            k = 0.4 if y > h * 0.45 else 0.62
            px[x, y] = (int(r + (RIM[0] - r) * k), int(g + (RIM[1] - g) * k),
                        int(b + (RIM[2] - b) * k), 255)

    if outline:
        edge = []
        for y in range(h):
            for x in range(w):
                if px[x, y][3]:
                    continue
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < w and 0 <= ny < h and px[nx, ny][3]:
                        edge.append((x, y))
                        break
        for (x, y) in edge:
            px[x, y] = (OUTLINE[0], OUTLINE[1], OUTLINE[2], 255)
    return img


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    sheet = Image.open(SRC).convert("RGB")
    frames = []
    for ri, (y0, y1) in enumerate(ROW_BANDS):
        for ci, (x0, x1) in enumerate(COL_BANDS):
            pad = 6
            cell = sheet.crop((x0 + pad, y0 + pad, x1 + 1 - pad, y1 + 1 - pad))
            rgba = key_magenta(cell)
            bbox = rgba.getbbox()
            if bbox is None:
                print("empty cell", ri, ci)
                continue
            cut = rgba.crop(bbox)
            scale = CELL_H / cut.height
            new_w = max(1, round(cut.width * scale))
            cut = cut.resize((new_w, CELL_H), Image.LANCZOS)

            canvas = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
            # 用 alpha 质心做水平对齐，避免尾巴位置变化把身体推来推去
            px = cut.load()
            sx = sy = n = 0
            for yy in range(cut.height):
                for xx in range(cut.width):
                    if px[xx, yy][3] > 128:
                        sx += xx; sy += yy; n += 1
            cx = sx / n if n else new_w / 2
            x = int(round(CANVAS[0] / 2 - cx))
            x = max(-8, min(CANVAS[0] - new_w + 8, x))
            y = GROUND_Y - CELL_H
            canvas.alpha_composite(cut, (x, y))
            canvas = crispify(canvas)
            canvas.save(os.path.join(OUT_DIR, f"f{ri * 2 + ci + 1}.png"))
            frames.append(canvas)
            print(f"cell r{ri}c{ci}: bbox={bbox} -> {new_w}x{CELL_H}")

    # horizontal sheet + 6x preview
    if frames:
        sheet_out = Image.new("RGBA", (CANVAS[0] * len(frames), CANVAS[1]), (0, 0, 0, 0))
        for i, f in enumerate(frames):
            sheet_out.alpha_composite(f, (i * CANVAS[0], 0))
        sheet_out.save(os.path.join(OUT_DIR, "player_cat_sheet.png"))

        prev = Image.new("RGBA", (sheet_out.width * 4, sheet_out.height * 4), (26, 32, 42, 255))
        prev.alpha_composite(sheet_out.resize((sheet_out.width * 4, sheet_out.height * 4), Image.NEAREST))
        prev.save(os.path.join(OUT_DIR, "player_cat_preview.png"))
        print("frames:", len(frames))


if __name__ == "__main__":
    main()
