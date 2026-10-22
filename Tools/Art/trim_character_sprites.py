"""无损裁剪角色图集透明边缘，保留精灵标识及动画坐标。默认预览，--apply 写入。"""
import argparse
import json
import math
import re
import shutil
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
BLOCK = re.compile(r"    - serializedVersion: 2\n      name: .*?(?=    - serializedVersion: 2\n|    outline:)", re.S)
RECT = re.compile(r"(      rect:\n        serializedVersion: 2\n)        x: ([\d.]+)\n        y: ([\d.]+)\n        width: ([\d.]+)\n        height: ([\d.]+)")
PIVOT = re.compile(r"      pivot: \{x: ([-\d.eE]+), y: ([-\d.eE]+)\}")


def prepare(path):
    meta = Path(str(path) + '.meta')
    text = meta.read_text(encoding='utf-8')
    image = Image.open(path).convert('RGBA')
    blocks = list(BLOCK.finditer(text))
    if not blocks:
        raise ValueError(f'未找到切片：{path}')
    frames, pivots = [], []
    bounds = []
    for block in blocks:
        rect = RECT.search(block[0])
        x, y, w, h = map(lambda n: int(float(n)), rect.groups()[1:])
        pivot = tuple(map(float, PIVOT.search(block[0]).groups()))
        if '      alignment: 0\n' not in block[0] and '      alignment: 9\n' not in block[0]:
            raise ValueError(f'不支持的轴点模式：{path}')
        frame = image.crop((x, image.height-y-h, x+w, image.height-y))
        frames.append(frame)
        pivots.append((pivot[0]*w, (1-pivot[1])*h))
        box = frame.getchannel('A').getbbox()
        if box:
            bounds.append(box)
    if len({frame.size for frame in frames}) != 1 or not bounds:
        raise ValueError(f'切片尺寸不一致或全透明：{path}')
    w, h = frames[0].size
    left = max(0, math.floor(min([b[0] for b in bounds]+[p[0] for p in pivots]))-2)
    top = max(0, math.floor(min([b[1] for b in bounds]+[p[1] for p in pivots]))-2)
    right = min(w, math.ceil(max([b[2] for b in bounds]+[p[0] for p in pivots]))+2)
    bottom = min(h, math.ceil(max([b[3] for b in bounds]+[p[1] for p in pivots]))+2)
    width, height = right-left, bottom-top
    count = len(frames)
    columns = min(range(1, count+1), key=lambda c: (max(c*width, math.ceil(count/c)*height), c*width*math.ceil(count/c)*height))
    output = Image.new('RGBA', (columns*width, math.ceil(count/columns)*height))
    replacements = []
    for i, (block, frame, pivot) in enumerate(zip(blocks, frames, pivots)):
        cropped = frame.crop((left, top, right, bottom))
        # 重建逐帧验证：全部可见像素及坐标必须保持不变。
        restored = Image.new('RGBA', frame.size)
        restored.paste(cropped, (left, top))
        assert restored.getchannel('A').tobytes() == frame.getchannel('A').tobytes()
        original_pixels, restored_pixels = frame.tobytes(), restored.tobytes()
        assert all(original_pixels[j:j+4] == restored_pixels[j:j+4]
                   for j in range(0, len(original_pixels), 4) if original_pixels[j+3])
        x, y = i % columns*width, i // columns*height
        output.paste(cropped, (x, y))
        new_pivot = ((pivot[0]-left)/width, 1-(pivot[1]-top)/height)
        assert abs(new_pivot[0]*width+left-pivot[0]) < 1e-6
        assert abs((1-new_pivot[1])*height+top-pivot[1]) < 1e-6
        value = RECT.sub(lambda m: m[1]+f'        x: {x}\n        y: {output.height-y-height}\n        width: {width}\n        height: {height}', block[0])
        value = re.sub(r'      alignment: \d+', '      alignment: 9', value)
        value = PIVOT.sub(f'      pivot: {{x: {new_pivot[0]:.10f}, y: {new_pivot[1]:.10f}}}', value)
        replacements.append((block.start(), block.end(), value))
    for start, end, value in reversed(replacements):
        text = text[:start]+value+text[end:]
    return meta, text, output, {'文件': str(path.relative_to(ROOT)), '帧数': count,
        '原图': image.size, '裁剪后': output.size, '原帧': (w,h), '裁剪帧': (width,height)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apply', action='store_true')
    parser.add_argument('--backup', type=Path)
    args = parser.parse_args()
    paths = sorted((ROOT/'Assets/GameResources/Characters').glob('**/Sprites/*.png'))
    prepared = [(path, prepare(path)) for path in paths]
    if args.apply:
        if args.backup is None or args.backup.exists():
            raise ValueError('写入时必须指定不存在的备份目录')
        for path, (meta, text, output, report) in prepared:
            for original in (path, meta):
                backup = args.backup/original.relative_to(ROOT)
                backup.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(original, backup)
            output.save(path)
            meta.write_text(text, encoding='utf-8', newline='\n')
        (args.backup/'裁剪报告.json').write_text(json.dumps([p[1][3] for p in prepared], ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps([p[1][3] for p in prepared], ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
