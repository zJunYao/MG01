"""将主角受击 GIF 原样提取为透明图集，保留帧时序与原件。"""

import argparse
import shutil
from pathlib import Path

from PIL import Image


def prepare(source, project):
    with Image.open(source) as gif:
        if gif.size != (256, 256) or gif.n_frames != 9:
            raise ValueError("受击素材应为 9 帧，每帧 256×256。")
        atlas = Image.new("RGBA", (2048, 512))
        for i in range(gif.n_frames):
            gif.seek(i)
            frame = gif.convert("RGBA")
            if gif.info.get("duration") != 200 or frame.getpixel((0, 0))[3] != 0:
                raise ValueError(f"受击帧时长或透明度异常，帧 {i}。")
            if not frame.getchannel("A").getbbox():
                raise ValueError(f"受击素材存在空白帧，帧 {i}。")
            atlas.paste(frame, ((i % 8) * 256, (i // 8) * 256))
    root = project / "Assets/GameResources/Characters/Hero"
    destination = root / "Sprites/Hit_south-east.png"
    backup = root / "Source/Hit_south-east.gif"
    if destination.exists():
        with Image.open(destination) as current:
            if current.size != atlas.size or current.convert("RGBA").tobytes() != atlas.tobytes():
                raise FileExistsError(f"已有不同图集，拒绝覆盖：{destination}")
    if backup.exists() and backup.read_bytes() != source.read_bytes():
        raise FileExistsError(f"已有不同原件，拒绝覆盖：{backup}")
    destination.parent.mkdir(parents=True, exist_ok=True)
    backup.parent.mkdir(parents=True, exist_ok=True)
    if not destination.exists():
        atlas.save(destination)
    if not backup.exists():
        shutil.copyfile(source, backup)
    print("Hit_south-east: 9 帧，5 FPS，1.8 秒，透明图集和原件已准备。")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="准备主角右下受击图集")
    parser.add_argument("source", type=Path, help="原始受击 GIF 路径")
    parser.add_argument("--project", type=Path, default=Path(__file__).resolve().parents[3])
    args = parser.parse_args()
    prepare(args.source, args.project)
