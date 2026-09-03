import argparse
from collections import deque
from pathlib import Path

from PIL import Image


def component_sizes(path: Path, threshold: int) -> list[int]:
    with Image.open(path) as image:
        alpha = image.convert("RGBA").getchannel("A")
        width, height = alpha.size
        foreground = bytearray(
            1 if value >= threshold else 0
            for value in alpha.get_flattened_data()
        )

    visited = bytearray(len(foreground))
    sizes = []
    for start, value in enumerate(foreground):
        if not value or visited[start]:
            continue

        queue = deque([start])
        visited[start] = 1
        size = 0
        while queue:
            index = queue.popleft()
            size += 1
            x = index % width
            y = index // width
            neighbors = []
            if x > 0:
                neighbors.append(index - 1)
            if x < width - 1:
                neighbors.append(index + 1)
            if y > 0:
                neighbors.append(index - width)
            if y < height - 1:
                neighbors.append(index + width)
            for neighbor in neighbors:
                if foreground[neighbor] and not visited[neighbor]:
                    visited[neighbor] = 1
                    queue.append(neighbor)
        sizes.append(size)

    return sorted(sizes, reverse=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("paths", nargs="+", type=Path)
    parser.add_argument("--threshold", type=int, default=128)
    args = parser.parse_args()

    for path in args.paths:
        sizes = component_sizes(path, args.threshold)
        print(
            f"{path.name}: components={len(sizes)}; "
            f"largest={sizes[:20]}"
        )


if __name__ == "__main__":
    main()
