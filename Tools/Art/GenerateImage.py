#!/usr/bin/env python3
"""调用本机可用的图片生成代理，保存 PNG。

用法：
    python3 GenerateImage.py --prompt "..." --out out/character/v1.png
    python3 GenerateImage.py --prompt "..." --out v2.png --image ref.png

说明：
- 通道来自 HANDOFF.md：llm-proxy.learnings.ai，模型 gemini-3-pro-image。
- Gemini 系必须带 response_format=b64_json，否则 data 为空。
- --image 支持多张参考图（重复传参），按原始 base64 字符串发送；
  如代理要求 data URL，加 --image-dataurl。
"""
import argparse
import base64
import json
import os
import sys
import urllib.error
import urllib.request

API = "https://llm-proxy.learnings.ai/v1/images/generations"


def read_b64(path, as_data_url):
    with open(path, "rb") as f:
        raw = f.read()
    b64 = base64.b64encode(raw).decode("ascii")
    if as_data_url:
        ext = os.path.splitext(path)[1].lower().lstrip(".") or "png"
        if ext == "jpg":
            ext = "jpeg"
        return f"data:image/{ext};base64,{b64}"
    return b64


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--prompt", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--image", action="append", default=[],
                    help="参考图路径，可重复")
    ap.add_argument("--image-dataurl", action="store_true",
                    help="把参考图编码成 data:image/...;base64,...")
    ap.add_argument("--model", default="gemini-3-pro-image")
    ap.add_argument("--n", type=int, default=1)
    ap.add_argument("--size", default="",
                    help="可选，如 1024x1024；代理不支持时会忽略")
    ap.add_argument("--raw-response", default="",
                    help="把完整响应 JSON 写到这个路径（调试用）")
    args = ap.parse_args()

    key = os.environ.get("LEARNINGS_API_KEY")
    if not key:
        sys.exit("LEARNINGS_API_KEY 未设置")

    body = {
        "model": args.model,
        "prompt": args.prompt,
        "response_format": "b64_json",
        "n": args.n,
    }
    if args.size:
        body["size"] = args.size
    if args.image:
        imgs = [read_b64(p, args.image_dataurl) for p in args.image]
        body["image"] = imgs[0] if len(imgs) == 1 else imgs

    req = urllib.request.Request(
        API,
        data=json.dumps(body).encode("utf-8"),
        headers={
            "Authorization": f"Bearer {key}",
            "Content-Type": "application/json",
        },
        method="POST",
    )

    try:
        with urllib.request.urlopen(req, timeout=300) as resp:
            raw = resp.read()
    except urllib.error.HTTPError as e:
        detail = e.read().decode("utf-8", "replace")
        sys.exit(f"HTTP {e.code}: {detail[:2000]}")
    except urllib.error.URLError as e:
        sys.exit(f"请求失败: {e}")

    text = raw.decode("utf-8", "replace")
    if args.raw_response:
        os.makedirs(os.path.dirname(os.path.abspath(args.raw_response)), exist_ok=True)
        with open(args.raw_response, "w", encoding="utf-8") as f:
            f.write(text)

    try:
        obj = json.loads(text)
    except json.JSONDecodeError:
        sys.exit(f"响应不是 JSON: {text[:500]}")

    data = obj.get("data") or obj.get("images") or []
    if not data:
        keys = list(obj.keys())
        sys.exit(f"响应里没有图片，顶层字段: {keys}\n{text[:1000]}")

    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    saved = 0
    for i, item in enumerate(data):
        b64 = item.get("b64_json")
        if b64:
            with open(args.out if args.n == 1 else
                      f"{os.path.splitext(args.out)[0]}_{i}.png", "wb") as f:
                f.write(base64.b64decode(b64))
            saved += 1
            continue
        url = item.get("url")
        if url:
            out = args.out if args.n == 1 else f"{os.path.splitext(args.out)[0]}_{i}.png"
            urllib.request.urlretrieve(url, out)
            saved += 1
    if not saved:
        sys.exit(f"图片字段无法识别: {list(data[0].keys())}")

    print(f"saved {saved} image(s) -> {args.out}")


if __name__ == "__main__":
    main()
