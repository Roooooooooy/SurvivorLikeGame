#!/usr/bin/env python3
"""Import a PixelLab goblin export into normalized 96x96 Unity sprite sheets.

PixelLab uses a different canvas size for each animation (56/64/68/80), but the
character is centered in every canvas. Centering each source canvas into a fixed
96x96 frame keeps the character root aligned across idle/run/attack/hit/death.

Usage:
    uv run --with pillow python Tools/Art/ImportGoblinExport.py <export_folder>

Output:
    SurvivorLikeGame/Assets/Resources/Enemies/GoblinWarrior/{State}/*_{dir}.png
    .../manifest.json

The script prints the C# `directionGroundY` array in WORLD direction order
(0=east ... 7=south-east). PixelLab art is horizontally mirrored relative to the
game world, so the array is reordered through WORLD_TO_ART below.
"""
import argparse
import json
from pathlib import Path

from PIL import Image

CANVAS = 96
PPU = 24
ALPHA_THRESHOLD = 8

# PixelLab export direction -> game world direction.
# The HD-2D billboard mirrors sprites horizontally, so E<->W, NE<->NW, SE<->SW.
WORLD_DIRS = ["east", "north-east", "north", "north-west",
              "west", "south-west", "south", "south-east"]
WORLD_TO_ART = ["west", "north-west", "north", "north-east",
                "east", "south-east", "south", "south-west"]

# (output folder, filename prefix, source animation folder)
STATES = [
    ("Idle", "idle", "7bfe4866/animations/idle_breathing"),
    ("Run", "run", "7bfe4866/animations/Running"),
    ("Attack", "attack", "7bfe4866/animations/The_right_hand_draws_the_sword_back_to_the_shoulde"),
    ("Hit", "hit", "7bfe4866/animations/The_body_recoils_sharply_as_the_shoulders_hunch_an"),
    ("Death", "death_forward", "7bfe4866/animations/Collapse_the_knees_forward_to_sink_the_torso_then"),
    ("Death", "death_back", "7bfe4866/animations/Falling_Back_Death"),
    ("Death", "death_collapse", "7bfe4866/animations/The_character_collapses_under_its_own_weight_knees"),
]


def frames_in(folder):
    if not folder.is_dir():
        return []
    return sorted(folder.glob("frame_*.png"))


def ground_line(img):
    """Lowest y that at least 25% of opaque columns reach (ignores sword tips)."""
    alpha = img.getchannel("A").load()
    w, h = img.size
    bottoms = []
    for x in range(w):
        bottom = -1
        for y in range(h):
            if alpha[x, y] > ALPHA_THRESHOLD:
                bottom = y
        if bottom >= 0:
            bottoms.append(bottom)
    if not bottoms:
        return None

    need = max(1, int(len(bottoms) * 0.25))
    for y in sorted(set(bottoms), reverse=True):
        if sum(1 for b in bottoms if b >= y) >= need:
            return y
    return max(bottoms)


def normalize(img):
    """Center the source canvas into CANVAS x CANVAS; return (image, ground)."""
    canvas = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
    dx = (CANVAS - img.width) // 2
    dy = (CANVAS - img.height) // 2
    canvas.paste(img, (dx, dy), img)

    ground = ground_line(img)
    return canvas, (None if ground is None else ground + dy)


def group_ground(paths):
    grounds = []
    for path in paths:
        with Image.open(path) as img:
            g = ground_line(img.convert("RGBA"))
            if g is not None:
                grounds.append(g + (CANVAS - img.height) // 2)
    return max(grounds) if grounds else None


def reference_ground(source, direction):
    for rel in (STATES[0][2], STATES[1][2]):
        paths = frames_in(source / rel / direction)
        if paths:
            return group_ground(paths)
    rotation = source / "Idle" / "rotations" / f"{direction}.png"
    if rotation.is_file():
        return group_ground([rotation])
    return None


def write_sheet(path, frames):
    sheet = Image.new("RGBA", (CANVAS * len(frames), CANVAS), (0, 0, 0, 0))
    for i, frame in enumerate(frames):
        sheet.paste(frame, (i * CANVAS, 0))
    path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(path)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("--output", type=Path, default=Path(__file__).resolve().parents[2]
                        / "SurvivorLikeGame/Assets/Resources/Enemies/GoblinWarrior")
    args = parser.parse_args()

    if not (args.source / "metadata.json").is_file():
        raise SystemExit(f"metadata.json not found in {args.source}")

    art_ground = {}
    for direction in WORLD_DIRS:
        ground = reference_ground(args.source, direction)
        if ground is None:
            raise SystemExit(f"No idle/run reference for direction {direction}")
        art_ground[direction] = ground
        print(f"reference {direction:<11} groundY={ground}")

    manifest = {
        "canvas": [CANVAS, CANVAS],
        "pixelsPerUnit": PPU,
        "directions": WORLD_DIRS,
        "artGround": art_ground,
        "worldGround": [art_ground[WORLD_TO_ART[i]] for i in range(8)],
        "sheets": {},
    }

    for folder, prefix, rel in STATES:
        for direction in WORLD_DIRS:
            paths = frames_in(args.source / rel / direction)
            if not paths:
                continue

            frames = []
            for path in paths:
                with Image.open(path) as img:
                    frame, _ = normalize(img.convert("RGBA"))
                frames.append(frame)

            out = args.output / folder / f"{prefix}_{direction}.png"
            write_sheet(out, frames)
            manifest["sheets"][f"{folder}/{prefix}/{direction}"] = {
                "path": f"{folder}/{prefix}_{direction}",
                "frames": len(frames),
            }

    # Missing idle directions: one static rotation frame, planted on the same line.
    for direction in WORLD_DIRS:
        if frames_in(args.source / STATES[0][2] / direction):
            continue
        rotation = args.source / "Idle" / "rotations" / f"{direction}.png"
        if not rotation.is_file():
            print(f"!! missing idle rotation {direction}")
            continue

        with Image.open(rotation) as img:
            img = img.convert("RGBA")
            frame, ground = normalize(img)
            if ground is not None:
                shift = art_ground[direction] - ground
                if shift:
                    shifted = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
                    shifted.paste(frame, (0, shift))
                    frame = shifted
        write_sheet(args.output / "Idle" / f"idle_{direction}.png", [frame])
        manifest["sheets"][f"Idle/idle/{direction}"] = {
            "path": f"Idle/idle_{direction}", "frames": 1, "static": True,
        }
        print(f"idle fallback {direction}")

    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")

    world_ground = manifest["worldGround"]
    print("\nC# DirectionalEnemyView.directionGroundY (world order):")
    print("    public float[] directionGroundY = { " +
          ", ".join(f"{g}f" for g in world_ground) + " };")
    print(f"sheets written to {args.output}")


if __name__ == "__main__":
    main()
