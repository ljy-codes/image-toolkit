import argparse
import re
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps


def find_source(source_directory: Path, stem: str) -> Path:
    candidate_stems = [stem]
    numbered_suffix = re.search(r"(\d{3}-.+)$", stem)
    if numbered_suffix:
        candidate_stems.append(numbered_suffix.group(1))

    for candidate_stem in candidate_stems:
        matches = sorted(source_directory.glob(f"{candidate_stem}.*"))
        if matches:
            return matches[0]

    raise FileNotFoundError(f"找不到原图：{stem}")


def fit_on(image: Image.Image, background: tuple[int, int, int], size: tuple[int, int]):
    canvas = Image.new("RGBA", size, (*background, 255))
    fitted = ImageOps.contain(image.convert("RGBA"), size)
    x = (size[0] - fitted.width) // 2
    y = (size[1] - fitted.height) // 2
    canvas.alpha_composite(fitted, (x, y))
    return canvas.convert("RGB")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("source_directory", type=Path)
    parser.add_argument("cutout_directory", type=Path)
    parser.add_argument("output_path", type=Path)
    args = parser.parse_args()

    cutouts = sorted(args.cutout_directory.glob("*.png"))
    if not cutouts:
        raise FileNotFoundError("没有找到 AI 抠图输出。")

    panel_size = (320, 240)
    item_width = panel_size[0] * 3 + 32
    item_height = panel_size[1] + 62
    columns = 2
    rows = (len(cutouts) + columns - 1) // columns
    sheet = Image.new(
        "RGB",
        (item_width * columns + 32, item_height * rows + 24),
        (11, 16, 22),
    )
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()

    for index, cutout_path in enumerate(cutouts):
        source_path = find_source(args.source_directory, cutout_path.stem)
        with Image.open(source_path) as source, Image.open(cutout_path) as cutout:
            panels = [
                fit_on(source, (28, 35, 44), panel_size),
                fit_on(cutout, (245, 245, 245), panel_size),
                fit_on(cutout, (18, 72, 112), panel_size),
            ]

        column = index % columns
        row = index // columns
        x = 16 + column * item_width
        y = 16 + row * item_height
        scene = cutout_path.stem
        draw.text((x, y), scene, fill=(230, 240, 247), font=font)
        draw.text(
            (x, y + 18),
            "original / white / blue",
            fill=(132, 151, 166),
            font=font,
        )
        for panel_index, panel in enumerate(panels):
            sheet.paste(panel, (x + panel_index * panel_size[0], y + 42))

    args.output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(args.output_path, quality=92)
    print(args.output_path)


if __name__ == "__main__":
    main()
