import argparse
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("source_directory", type=Path)
    parser.add_argument("output_path", type=Path)
    parser.add_argument("--pattern", default="*")
    args = parser.parse_args()

    supported = {".jpg", ".jpeg", ".png", ".webp", ".bmp", ".tif", ".tiff"}
    files = [
        path
        for path in sorted(args.source_directory.glob(args.pattern))
        if path.suffix.lower() in supported
    ]
    if not files:
        raise FileNotFoundError("没有找到图片。")

    columns = 4
    image_size = (300, 210)
    item_size = (320, 250)
    rows = math.ceil(len(files) / columns)
    sheet = Image.new(
        "RGB",
        (columns * item_size[0], rows * item_size[1]),
        (11, 16, 22),
    )
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()

    for index, path in enumerate(files):
        with Image.open(path) as image:
            fitted = ImageOps.contain(image.convert("RGB"), image_size)
        column = index % columns
        row = index // columns
        x = column * item_size[0] + 10
        y = row * item_size[1] + 8
        canvas = Image.new("RGB", image_size, (28, 35, 44))
        canvas.paste(
            fitted,
            (
                (image_size[0] - fitted.width) // 2,
                (image_size[1] - fitted.height) // 2,
            ),
        )
        sheet.paste(canvas, (x, y))
        draw.text(
            (x, y + image_size[1] + 8),
            path.name,
            fill=(230, 240, 247),
            font=font,
        )

    args.output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(args.output_path, quality=90)
    print(args.output_path)


if __name__ == "__main__":
    main()
