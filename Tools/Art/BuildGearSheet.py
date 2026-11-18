#!/usr/bin/env python3
"""把生图模型产出的装备视角图切成游戏用的 8 方向图集。

输入：两张 2x2 网格图（共 8 个视角），品红底，每格只画同一件装备。
      A 图顺序：west, north-west, north, north-east
      B 图顺序：east, south-east, south, south-west
输出：512x64 RGBA 图集（8 帧 x 64x64），以及叠在身体帧上的预览图。

对齐规则：读取对应方向的 idle 第 0 帧，按身体 alpha 包围盒的头顶和中心
自动放置装备，保证画布/枢轴与身体完全一致。装备尺寸和位置可再用参数微调。

用法：
    /tmp/gear_env/bin/python Tools/Art/BuildGearSheet.py \
        --views-a /tmp/gear_gen/hat_views_a.png \
        --views-b /tmp/gear_gen/hat_views_b.png \
        --out Assets/Resources/Player/Cat8Dir/Equipment/hat_wizard.png \
        --preview /tmp/gear_gen/hat_wizard_preview.png

依赖 Pillow。
"""
import argparse
import math
import os

from PIL import Image, ImageDraw

DIRS = ["west", "north-west", "north", "north-east",
        "east", "south-east", "south", "south-west"]

# 背面视角的耳朵尖最高，bbox 顶部离头顶较远，帽檐需要多下压一点才不会悬空。
BACK_DIRS = {"north", "north-west", "north-east"}
BACK_EXTRA_OVERLAP = 6

FRAME = 64
MAGENTA_HARD = 110   # 距离品红小于这个值直接判为背景
MAGENTA_SOFT = 180   # 介于两者之间的边缘像素也清掉，避免紫边


def key_magenta(im):
    """把品红背景抠成透明；生成图边缘的紫边一起清掉。"""
    im = im.convert("RGBA")
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            d = math.sqrt((r - 255) ** 2 + g ** 2 + (b - 255) ** 2)
            if d < MAGENTA_SOFT:
                # 越接近品红越透明；软边也直接去掉，保持像素硬边
                px[x, y] = (0, 0, 0, 0)
            else:
                px[x, y] = (r, g, b, 255)
    return im


def split_2x2(path, pad=12):
    """切 2x2，向内缩 pad 像素避开生成图的黑色分隔线。"""
    sheet = Image.open(path).convert("RGBA")
    w, h = sheet.size
    half_w, half_h = w // 2, h // 2
    cells = []
    for row in range(2):
        for col in range(2):
            box = (col * half_w + pad, row * half_h + pad,
                   (col + 1) * half_w - pad, (row + 1) * half_h - pad)
            cell = key_magenta(sheet.crop(box))
            bbox = cell.getbbox()
            if bbox is None:
                raise SystemExit("空的装备格子：" + path + f" r{row}c{col}")
            cells.append(cell.crop(bbox))
    return cells


def body_frame(body_root, direction):
    path = os.path.join(body_root, "idle_" + direction + ".png")
    sheet = Image.open(path).convert("RGBA")
    return sheet.crop((0, 0, FRAME, FRAME))


def body_bbox(frame):
    bbox = frame.getbbox()
    if bbox is None:
        raise SystemExit("身体帧为空")
    return bbox


def place_on_canvas(hat, frame, width_ratio, max_height_ratio, overlap,
                    offset_x, offset_y):
    bbox = body_bbox(frame)
    body_w = bbox[2] - bbox[0]
    body_h = bbox[3] - bbox[1]
    body_cx = (bbox[0] + bbox[2]) * 0.5
    body_top = bbox[1]

    target_w = max(1, int(round(body_w * width_ratio)))
    scale = target_w / max(1, hat.width)
    target_h = max(1, int(round(hat.height * scale)))

    max_h = max(8, int(round(body_h * max_height_ratio)))
    # 帽子必须在画布内：顶部最多到 y=0，底部压在头顶 overlap 像素。
    max_h = min(max_h, max(1, body_top + overlap))
    if target_h > max_h:
        target_h = max_h
        target_w = max(1, int(round(hat.width * target_h / max(1, hat.height))))

    hat = hat.resize((target_w, target_h), Image.NEAREST)

    x = int(round(body_cx - target_w * 0.5 + offset_x))
    y = int(round(body_top - target_h + overlap + offset_y))
    x = max(0, min(FRAME - target_w, x))
    y = max(0, min(FRAME - target_h, y))

    canvas = Image.new("RGBA", (FRAME, FRAME), (0, 0, 0, 0))
    canvas.alpha_composite(hat, (x, y))
    return canvas


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--views-a", required=True)
    ap.add_argument("--views-b", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--preview", default="")
    ap.add_argument("--body-root",
                    default="SurvivorLikeGame/Assets/Resources/Player/Cat8Dir/Idle")
    ap.add_argument("--width-ratio", type=float, default=0.85)
    ap.add_argument("--max-height-ratio", type=float, default=0.80)
    ap.add_argument("--overlap", type=int, default=4)
    ap.add_argument("--offset-x", type=int, default=0)
    ap.add_argument("--offset-y", type=int, default=0)
    args = ap.parse_args()

    hats = split_2x2(args.views_a) + split_2x2(args.views_b)
    if len(hats) != 8:
        raise SystemExit("需要 8 个视角，实际 " + str(len(hats)))

    frames = []
    for i, direction in enumerate(DIRS):
        frame = body_frame(args.body_root, direction)
        overlap = args.overlap + (BACK_EXTRA_OVERLAP if direction in BACK_DIRS else 0)
        placed = place_on_canvas(
            hats[i], frame,
            args.width_ratio, args.max_height_ratio,
            overlap, args.offset_x, args.offset_y)
        frames.append(placed)
        print(direction, "hat", hats[i].size, "->", placed.getbbox())

    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    sheet = Image.new("RGBA", (FRAME * len(frames), FRAME), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        sheet.alpha_composite(f, (i * FRAME, 0))
    sheet.save(args.out)
    print("saved", args.out, sheet.size)

    if args.preview:
        # 把装备叠到身体上，4x 放大，方便检查对齐
        cell = FRAME * 4
        preview = Image.new("RGBA", (cell * 4, cell * 2), (26, 32, 42, 255))
        draw = ImageDraw.Draw(preview)
        for i, direction in enumerate(DIRS):
            body = body_frame(args.body_root, direction)
            composed = Image.new("RGBA", (FRAME, FRAME), (0, 0, 0, 0))
            composed.alpha_composite(body, (0, 0))
            composed.alpha_composite(frames[i], (0, 0))
            big = composed.resize((cell, cell), Image.NEAREST)
            x = (i % 4) * cell
            y = (i // 4) * cell
            preview.alpha_composite(big, (x, y))
            draw.text((x + 8, y + 8), direction, fill=(220, 220, 220, 255))
        os.makedirs(os.path.dirname(os.path.abspath(args.preview)), exist_ok=True)
        preview.save(args.preview)
        print("saved", args.preview, preview.size)


if __name__ == "__main__":
    main()
