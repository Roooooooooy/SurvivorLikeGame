#!/usr/bin/env python3
"""Import a PixelLab character export without resampling or changing existing Unity GUIDs.

Usage: uv run --with pillow python Tools/Art/ImportCatExport.py <export_folder>
"""
import argparse
import json
from pathlib import Path

from PIL import Image


def build_sheet(source, paths, output, normalize=True):
    frames = []
    for path in paths:
        frame = Image.open(source / path).convert("RGBA")
        if normalize and frame.size == (68, 68):
            # These attack canvases add transparent margins. Keep all opaque pixels
            # and align their standing feet (y=57) with the 64px frames (y=54).
            crop = (2, 3, 66, 67)
            bounds = frame.getbbox()
            if bounds and not (bounds[0] >= crop[0] and bounds[1] >= crop[1]
                               and bounds[2] <= crop[2] and bounds[3] <= crop[3]):
                raise ValueError(f"Cannot normalize without clipping: {path}: {bounds}")
            frame = frame.crop(crop)
        if normalize and frame.size != (64, 64):
            raise ValueError(f"Unexpected frame size: {path}: {frame.size}")
        frames.append(frame)
    width = max(f.width for f in frames)
    height = max(f.height for f in frames)
    sheet = Image.new("RGBA", (width * len(frames), height))
    for i, frame in enumerate(frames):
        sheet.paste(frame, (i * width, 0))
    output.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(output)
    return {"path": str(output), "frames": len(frames), "frame_size": [width, height]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("--animation", action="append",
                        help="Import only this animation; repeat to select multiple animations")
    parser.add_argument("--output", type=Path, default=Path(__file__).resolve().parents[2]
                        / "SurvivorLikeGame/Assets/Resources/Player/Cat8Dir")
    args = parser.parse_args()
    metadata = json.loads((args.source / "metadata.json").read_text())
    animations = {}
    for state in metadata["states"]:
        # The "睡觉" state is a separate sleep prop, not player gameplay.
        if state.get("character", {}).get("name") == "睡觉":
            continue
        for name, directions in state["frames"].get("animations", {}).items():
            animations[name] = directions
    destinations = {
        "Fight_Stance_Idle": ("Idle", "idle"),
        "Running": ("Running", "run"),
        "Full_Sprint": ("Sprint", "sprint"),
        "Taking_Punch": ("Hurt", "hurt"),
        "Left_Punch": ("Attack/LeftPunch", "attack"),
        "Right_Punch": ("Attack/RightPunch", "attack"),
        "Uppercut": ("Attack/Uppercut", "attack"),
        # Preserve the legacy atlas path used by the equipment pose editor.
        "Two_Punch_Combo": ("Attack", "attack"),
        # New protagonist states.
        "Falling_Back_Death": ("Death", "death"),
        "Clutch_the_chest_with_both_hands_as_the_knees_buck": ("Death", "death"),
        "Tuck_the_chin_and_shoulders_inward_to_initiate_a_t": ("Dodge", "dodge"),
    }
    if args.animation:
        missing = set(args.animation) - animations.keys()
        if missing:
            parser.error(f"Animations not found: {', '.join(sorted(missing))}")
    manifest_path = args.output / "cat_export.json"
    manifest = (json.loads(manifest_path.read_text())
                if args.animation and manifest_path.exists() else {"animations": {}})
    manifest["export_date"] = metadata["export_date"]
    for name, directions in animations.items():
        if args.animation and name not in args.animation:
            continue
        folder, prefix = destinations.get(name, (f"Extras/{name}", name.lower()))
        entries = {}
        for direction, paths in directions.items():
            output = args.output / folder / f"{prefix}_{direction}.png"
            # Death frames can extend to the canvas edge when the body falls.
            # Preserve their 64/68px canvases; the runtime reads the tile size.
            entry = build_sheet(args.source, paths, output,
                                name in destinations and folder != "Death")
            entry["path"] = str(output.relative_to(args.output))
            entries[direction] = entry
        # A replacement state can write the same atlas paths as the old state.
        replaced_paths = {entry["path"] for entry in entries.values()}
        for old_name, old_entries in list(manifest["animations"].items()):
            if replaced_paths.intersection(entry["path"] for entry in old_entries.values()):
                del manifest["animations"][old_name]
        manifest["animations"][name] = entries
        print(f"{name}: {len(entries)} directions")
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n")


if __name__ == "__main__":
    main()
