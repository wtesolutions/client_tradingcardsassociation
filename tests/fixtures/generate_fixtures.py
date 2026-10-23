#!/usr/bin/env python3
"""Generate small synthetic PNG fixtures (not real photos)."""
from __future__ import annotations

import struct
import zlib
from pathlib import Path

OUT = Path(__file__).resolve().parent

COLORS = {
    "logo.png": ((16, 24, 43), (229, 185, 86), 200, 200),
    "photo-1.png": ((36, 99, 197), None, 640, 400),
    "photo-2.png": ((32, 99, 68), None, 640, 400),
    "photo-3.png": ((169, 44, 56), None, 640, 400),
    "photo-4.png": ((83, 96, 119), None, 640, 400),
    "photo-5.png": ((214, 167, 68), None, 640, 400),
    "photo-6.png": ((20, 40, 72), None, 640, 400),
}


def png(width: int, height: int, rgb: tuple[int, int, int], accent: tuple[int, int, int] | None) -> bytes:
    rows = []
    for y in range(height):
        row = bytearray([0])
        for x in range(width):
            pixel = rgb
            if accent is not None:
                inset = min(width, height) // 4
                if inset < x < width - inset and inset < y < height - inset:
                    pixel = accent
            row.extend(pixel)
        rows.append(bytes(row))
    raw = b"".join(rows)
    ihdr = struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0)

    def chunk(tag: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    return b"".join(
        [
            b"\x89PNG\r\n\x1a\n",
            chunk(b"IHDR", ihdr),
            chunk(b"IDAT", zlib.compress(raw, 9)),
            chunk(b"IEND", b""),
        ]
    )


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    for name, (rgb, accent, w, h) in COLORS.items():
        (OUT / name).write_bytes(png(w, h, rgb, accent))
        print("wrote", name)


if __name__ == "__main__":
    main()
