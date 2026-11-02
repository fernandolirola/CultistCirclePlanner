"""Draws the hideout toolbar Cultist Circle icon: a ritual ring around a five-point star (white on transparent)."""
import math
import sys
from PIL import Image, ImageDraw

S = 4
size = 128
img = Image.new("RGBA", (size * S, size * S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
w = (255, 255, 255, 255)
c = 64 * S


def ring(r, width):
    d.ellipse([c - r * S, c - r * S, c + r * S, c + r * S], outline=w, width=width * S)


ring(54, 8)
ring(44, 4)
pts = []
for i in range(5):
    a = math.radians(-90 + i * 144)
    pts.append((c + 36 * S * math.cos(a), c + 36 * S * math.sin(a)))
pts.append(pts[0])
d.line(pts, fill=w, width=6 * S, joint="curve")
d.ellipse([c - 6 * S, c - 6 * S, c + 6 * S, c + 6 * S], fill=w)

img = img.resize((size, size), Image.LANCZOS)
img.save(sys.argv[1])
print("saved", sys.argv[1])
