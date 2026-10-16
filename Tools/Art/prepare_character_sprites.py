"""从外部导出图集中无损提取本次所需动作；依赖 Pillow，不复制未使用的素材。"""

import argparse
import json
from pathlib import Path

from PIL import Image


def prepare(source_root, project_root):
    root = project_root / "Assets/GameResources/Characters"
    jobs = [
        ("d51f6a2b-86f5-4b8f-b410-f194fb585719-Idle-spritesheet (1)",
         "Hero", "Run_south-east", 1, 8, "south-east", "Running"),
        ("Q-Idle-spritesheet (2)", "GoblinWarrior", "Attack_south-west", 1, 41, "south-west", None),
        ("Q-Idle-spritesheet (2)", "GoblinWarrior", "Idle_south-west", 3, 8, "south-west", "Idle"),
        ("Q-Idle-spritesheet", "GoblinThief", "Attack_south-west", 1, 41, "south-west", None),
        ("Q-Idle-spritesheet", "GoblinThief", "Idle_south-west", 3, 8, "south-west", "Idle"),
        ("Q-Idle-spritesheet (1)", "GoblinChief", "Attack_south-west", 1, 41, "south-west", None),
        ("Q-Idle-spritesheet (1)", "GoblinChief", "Idle_south-west", 5, 8, "south-west", "Idle"),
    ]
    prepared = []
    for folder, character, action, row_index, count, direction, animation in jobs:
        metadata_path, = (source_root / folder).glob("*.json")
        metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
        sheet = metadata["spritesheet"]
        row, = [r for r in sheet["rows"] if r["row"] == row_index]
        if (sheet["cell_size"] != {"width": 256, "height": 256}
                or row["type"] != "animation" or row["frame_count"] != count
                or row["direction"] != direction
                or (animation is not None and row["animation"] != animation)):
            raise ValueError(f"素材元数据不符合预期：{metadata_path}，行 {row_index}")
        with Image.open(metadata_path.parent / sheet["path"]) as source:
            if source.size != (sheet["sheet_size"]["width"], sheet["sheet_size"]["height"]):
                raise ValueError(f"原始图集尺寸与元数据不一致：{metadata_path}")
            source = source.convert("RGBA")
            columns = min(8, count)
            rows = (count + columns - 1) // columns
            packed = Image.new("RGBA", (columns * 256, rows * 256))
            for frame in range(count):
                cell = source.crop((frame * 256, row_index * 256,
                                    (frame + 1) * 256, (row_index + 1) * 256))
                if cell.getchannel("A").getbbox() is None:
                    raise ValueError(f"素材含空白帧：{metadata_path}，行 {row_index}，帧 {frame}")
                packed.paste(cell, ((frame % columns) * 256, (frame // columns) * 256))
            destination = root / character / "Sprites" / (action + ".png")
            if destination.exists():
                with Image.open(destination) as existing:
                    if existing.size != packed.size or existing.convert("RGBA").tobytes() != packed.tobytes():
                        raise FileExistsError(f"已有不同内容，拒绝覆盖：{destination}")
            prepared.append((destination, packed, count))
    # 所有输入检查通过后才写入，已有且一致的图片保持原样。
    for destination, packed, count in prepared:
        destination.parent.mkdir(parents=True, exist_ok=True)
        if not destination.exists():
            packed.save(destination)
        print(f"已准备：{destination.relative_to(project_root)}，{count} 帧，{packed.width}×{packed.height}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="准备主角奔跑与三个哥布林的指定方向图集")
    parser.add_argument("source", type=Path, help="包含四个解压素材目录的文件夹")
    parser.add_argument("--project", type=Path, default=Path(__file__).resolve().parents[2], help="项目根目录")
    args = parser.parse_args()
    prepare(args.source, args.project)
