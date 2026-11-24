"""Generate the application's original vector-style aircraft icon, using stdlib only."""
import math
import struct
from pathlib import Path

points = [(32, 7), (37, 26), (54, 40), (54, 45), (36, 37), (36, 51), (43, 56), (43, 60), (32, 55), (21, 60), (21, 56), (28, 51), (28, 37), (10, 45), (10, 40), (27, 26)]

def inside(x, y):
    result = False
    j = len(points) - 1
    for i, (px, py) in enumerate(points):
        qx, qy = points[j]
        if (py > y) != (qy > y) and x < (qx - px) * (y - py) / (qy - py) + px:
            result = not result
        j = i
    return result

def frame(size):
    pixels = bytearray()
    for y in range(size - 1, -1, -1):
        for x in range(size):
            channels = [0, 0, 0, 0]
            for dy in (.25, .75):
                for dx in (.25, .75):
                    a, b = (x + dx) * 64 / size, (y + dy) * 64 / size
                    if (a - 32) ** 2 + (b - 32) ** 2 > 31 ** 2:
                        color = (0, 0, 0, 0)
                    elif inside(a, b):
                        color = (209, 222, 101, 255)
                    else:
                        color = (34, 20, 11, 255)
                    channels = [c + v for c, v in zip(channels, color)]
            pixels.extend(v // 4 for v in channels)
    mask = bytes(((size + 31) // 32) * 4 * size)
    header = struct.pack('<IiiHHIIiiII', 40, size, size * 2, 1, 32, 0, len(pixels), 0, 0, 0, 0)
    return header + pixels + mask

sizes = [32, 48, 64, 128]
frames = [frame(n) for n in sizes]
offset = 6 + 16 * len(frames)
directory = bytearray(struct.pack('<HHH', 0, 1, len(frames)))
for size, data in zip(sizes, frames):
    directory.extend(struct.pack('<BBBBHHII', size, size, 0, 0, 1, 32, len(data), offset))
    offset += len(data)
target = Path(__file__).resolve().parents[1] / 'src/AceCombat8.Desktop/Assets/app.ico'
target.parent.mkdir(parents=True, exist_ok=True)
target.write_bytes(directory + b''.join(frames))
