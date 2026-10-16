"""保留 GIF 透明度与帧时序，准备三个怪物的左下走路图集及原件备份。"""

import argparse
import shutil
from pathlib import Path

from PIL import Image


def prepare(source, project):
    jobs = [("GoblinWarrior", "Idle_v3_walking_south-west.gif"),
            ("GoblinThief", "Idle_v3_walking_south-west (1).gif"),
            ("GoblinChief", "Idle_v3_walking_south-west (2).gif")]
    prepared = []
    for character, name in jobs:
        original = source / name
        with Image.open(original) as gif:
            if gif.size != (256, 256) or gif.n_frames != 8:
                raise ValueError(f"走路素材尺寸或帧数异常：{original}")
            atlas = Image.new("RGBA", (2048, 256))
            for i in range(gif.n_frames):
                gif.seek(i)
                frame = gif.convert("RGBA")
                if gif.info.get("duration") != 200:
                    raise ValueError(f"素材帧时长不是 0.2 秒：{original}，帧 {i}")
                if frame.getpixel((0, 0))[3] != 0 or not frame.getchannel("A").getbbox():
                    raise ValueError(f"透明度或有效画面异常：{original}，帧 {i}")
                # 直接复制 RGBA，不能使用透明蒙版再次混合，以免破坏边缘透明度。
                atlas.paste(frame, (i * 256, 0))
        root = project / "Assets/GameResources/Characters" / character
        destination = root / "Sprites/Walk_south-west.png"
        backup = root / "Source/Walk_south-west.gif"
        if destination.exists():
            with Image.open(destination) as current:
                if current.convert("RGBA").tobytes() != atlas.tobytes() or current.size != atlas.size:
                    raise FileExistsError(f"已有不同图集，拒绝覆盖：{destination}")
        if backup.exists() and backup.read_bytes() != original.read_bytes():
            raise FileExistsError(f"已有不同原件，拒绝覆盖：{backup}")
        prepared.append((original, destination, backup, atlas))
    for original, destination, backup, atlas in prepared:
        destination.parent.mkdir(parents=True, exist_ok=True)
        backup.parent.mkdir(parents=True, exist_ok=True)
        if not destination.exists():
            atlas.save(destination)
        if not backup.exists():
            shutil.copyfile(original, backup)
        print(f"{destination.relative_to(project)}：8 帧，5 FPS，1.6 秒，透明通道与原件已保留。")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="准备三个怪物的透明走路图集")
    parser.add_argument("source", type=Path, help="三个原始 GIF 所在目录")
    parser.add_argument("--project", type=Path, default=Path(__file__).resolve().parents[3])
    args = parser.parse_args()
    prepare(args.source, args.project)
