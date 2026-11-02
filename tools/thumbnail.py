"""Draws release/CultistCirclePlanner-thumbnail.png (1024x1024), same look as the other zezinho thumbnails.

Usage: python -I tools/thumbnail.py
"""
import math
import pathlib

from PIL import Image, ImageChops, ImageDraw, ImageFont

ROOT = pathlib.Path(__file__).resolve().parent.parent
S = 4  # supersampling
W = 1024 * S
BG = (17, 17, 17)
ORANGE = (242, 170, 60)
CREAM = (239, 235, 224)
FONT_BOLD = "C:/Windows/Fonts/segoeuib.ttf"
FONT_SEMI = "C:/Windows/Fonts/seguisb.ttf"


def p(v):
    return v * S


# Shared layout for every zezinho thumbnail: same icon height, title size and gaps, so the set looks alike
ICON_H, TITLE_CAP, SUB_CAP, GAP, MAX_TEXT_W = 470, 112, 40, 46, 860


def _cap_size(path, cap):
    """Font size whose capital letters are `cap` px tall at 1x."""
    box = ImageFont.truetype(path, 200).getbbox("H")
    return 200 * cap / (box[3] - box[1])


def _text_ink(parts, spacing):
    """The text on its own transparent layer, cropped to its visible ink so it centres exactly."""
    probe = ImageDraw.Draw(Image.new("RGBA", (1, 1)))
    width = sum(probe.textlength(ch, font=f) for ch, f, _ in parts) + spacing * (len(parts) - 1)
    layer = Image.new("RGBA", (int(width) + p(40), p(300)), (0, 0, 0, 0))
    ld = ImageDraw.Draw(layer)
    x = p(20)
    for ch, font, colour in parts:
        ld.text((x, p(150)), ch, font=font, fill=colour, anchor="lm")
        x += ld.textlength(ch, font=font) + spacing
    return layer.crop(layer.getbbox())


def finish(img, out, title, sub, sub_initials=()):
    """`img` holds only the icon (supersampled). Scales it, sets the title and subtitle under it and saves."""
    box = ImageChops.difference(img, Image.new("RGB", img.size, BG)).getbbox()  # the background is not black
    icon = img.crop(box)
    k = p(ICON_H) / icon.height
    icon = icon.resize((round(icon.width * k), round(icon.height * k)), Image.LANCZOS)
    # keep the icon where it was drawn horizontally, so shapes with a side part (a crown, a button) stay balanced
    icon_x = round(W / 2 + (box[0] - W / 2) * k)

    size = p(_cap_size(FONT_BOLD, TITLE_CAP))
    while True:
        font = ImageFont.truetype(FONT_BOLD, round(size))
        title_img = _text_ink([(ch, font, CREAM) for ch in title], 0)
        if title_img.width <= p(MAX_TEXT_W):
            break
        size *= 0.97

    # the subtitle keeps its wide spacing when it fits; long ones tighten the spacing first, then shrink
    size = p(_cap_size(FONT_SEMI, SUB_CAP))
    while True:
        regular, bold = ImageFont.truetype(FONT_SEMI, round(size)), ImageFont.truetype(FONT_BOLD, round(size))
        base = CREAM if sub_initials else ORANGE
        parts = [(ch, bold if i in sub_initials else regular, ORANGE if i in sub_initials else base) for i, ch in enumerate(sub)]
        sub_img = None
        for factor in (0.9, 0.8, 0.7, 0.6, 0.5, 0.4, 0.3, 0.2):
            sub_img = _text_ink(parts, size * factor)
            if sub_img.width <= p(MAX_TEXT_W):
                break
        if sub_img.width <= p(MAX_TEXT_W):
            break
        size *= 0.97

    total = icon.height + title_img.height + sub_img.height + 2 * p(GAP)
    top = (W - total) / 2
    canvas = Image.new("RGB", (W, W), BG)
    canvas.paste(icon, (icon_x, int(top)))
    y = top + icon.height + p(GAP)
    canvas.paste(title_img, (int(W / 2 - title_img.width / 2), int(y)), title_img)
    y += title_img.height + p(GAP)
    canvas.paste(sub_img, (int(W / 2 - sub_img.width / 2), int(y)), sub_img)
    out.parent.mkdir(parents=True, exist_ok=True)
    result = canvas.resize((1024, 1024), Image.LANCZOS)
    result.save(out, optimize=True)
    return result


def main():
    img = Image.new("RGB", (W, W), BG)
    d = ImageDraw.Draw(img)

    # ritual circle: a thick ring, a thin inner ring, a five-point star and a core, with five item slots around it
    cx, cy = p(512), p(372)
    r = p(236)

    def ring(radius, width, colour):
        d.ellipse([cx - radius, cy - radius, cx + radius, cy + radius], outline=colour, width=width)

    ring(r, p(24), ORANGE)
    ring(p(196), p(8), CREAM)
    star = []
    for i in range(5):
        a = math.radians(-90 + i * 144)
        star.append((cx + p(150) * math.cos(a), cy + p(150) * math.sin(a)))
    d.line(star + [star[0]], fill=CREAM, width=p(14), joint="curve")
    d.ellipse([cx - p(20), cy - p(20), cx + p(20), cy + p(20)], fill=ORANGE)
    # the five points of the star hold the slots
    for x, y in star[:5]:
        d.rounded_rectangle([x - p(17), y - p(17), x + p(17), y + p(17)], radius=p(7), fill=BG, outline=ORANGE, width=p(8))

    out = ROOT / "release" / "CultistCirclePlanner-thumbnail.png"
    finish(img, out, "CULTIST", "CIRCLE PLANNER")
    print(out)


if __name__ == "__main__":
    main()
