import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps


def checkerboard(size: tuple[int, int], cell: int = 20) -> Image.Image:
    image = Image.new("RGBA", size, (230, 230, 230, 255))
    draw = ImageDraw.Draw(image)
    for y in range(0, size[1], cell):
        for x in range(0, size[0], cell):
            if (x // cell + y // cell) % 2:
                draw.rectangle(
                    (x, y, x + cell - 1, y + cell - 1),
                    fill=(185, 185, 185, 255),
                )
    return image


def render_panel(image: Image.Image, size: tuple[int, int], background):
    fitted = ImageOps.contain(image.convert("RGBA"), size)
    canvas = background(size) if callable(background) else Image.new(
        "RGBA",
        size,
        (*background, 255),
    )
    position = (
        (size[0] - fitted.width) // 2,
        (size[1] - fitted.height) // 2,
    )
    canvas.alpha_composite(fitted, position)
    return canvas.convert("RGB")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("cutout_directory", type=Path)
    parser.add_argument("output_path", type=Path)
    args = parser.parse_args()

    files = sorted(args.cutout_directory.glob("*.png"))
    if not files:
        raise FileNotFoundError("没有找到透明 PNG。")

    panel_size = (420, 315)
    row_height = panel_size[1] + 54
    sheet = Image.new(
        "RGB",
        (panel_size[0] * 4 + 32, row_height * len(files) + 16),
        (11, 16, 22),
    )
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default()

    for index, path in enumerate(files):
        with Image.open(path) as image:
            rgba = image.convert("RGBA")
            alpha = rgba.getchannel("A")
            panels = [
                render_panel(rgba, panel_size, checkerboard),
                render_panel(rgba, panel_size, (245, 245, 245)),
                render_panel(rgba, panel_size, (18, 72, 112)),
                ImageOps.contain(alpha.convert("RGB"), panel_size),
            ]

        y = 12 + index * row_height
        draw.text((16, y), path.name, fill=(230, 240, 247), font=font)
        draw.text(
            (16, y + 18),
            "checker / white / blue / alpha",
            fill=(132, 151, 166),
            font=font,
        )
        for panel_index, panel in enumerate(panels):
            x = 16 + panel_index * panel_size[0]
            panel_y = y + 38
            sheet.paste(panel, (x, panel_y))

    args.output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(args.output_path, quality=92)
    print(args.output_path)


if __name__ == "__main__":
    main()
