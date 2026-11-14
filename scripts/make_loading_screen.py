#!/usr/bin/env python3
"""
Gészi pasziánsz - betöltőképernyő előállítása.

A kép a játék saját kártyalapjaiból (Assets/Cards/*.png), a feltöltött
nemeskep_2.jpg fotóból és a téma arany/zöld színeiből épül fel.

Futtatás:
    pip install pillow numpy
    python scripts/make_loading_screen.py \
        --photo nemeskep_2.jpg \
        --out docs/loading_geszi_pasziansz.png

Betűtípusok (SIL Open Font License): Playfair Display (cím) és
Cinzel Decorative (kis felirat). A script a --font-dir mappában keresi
őket, vagy letölti a Google Fonts repóból.
"""

from __future__ import annotations

import argparse
import base64
import json
import math
import os
import urllib.request
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent.parent

# ---------------------------------------------------------------- méretek ---
W, H = 1920, 1280          # 3:2, mint a játékablak (1260x840)
S = 2                      # szupermintavételezés
CW, CH = W * S, H * S

# ---------------------------------------------------------------- színek ----
GOLD = (216, 188, 120)
CREAM = (243, 238, 220)
INK = (7, 24, 17)

TITLE = "Gészi pasziánsz"
LOADING_LABEL = "A kártyák keverése…"

SUITS_FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"


# ------------------------------------------------------------- segédfüggvények
def font(path, size: float, weight=None) -> ImageFont.FreeTypeFont:
    f = ImageFont.truetype(str(path), int(round(size)))
    if weight is not None:
        try:
            f.set_variation_by_axes([weight])
        except Exception:
            pass
    return f


def diag_gradient(size, stops, angle=45.0) -> Image.Image:
    """Átlós gradiens (angle fokban, 90 = felülről lefelé)."""
    w, h = size
    xs = np.linspace(0, 1, w, dtype=np.float32)[None, :]
    ys = np.linspace(0, 1, h, dtype=np.float32)[:, None]
    a = math.radians(angle)
    t = xs * math.cos(a) + ys * math.sin(a)
    t = (t - t.min()) / (t.max() - t.min())
    pos = np.array([s[0] for s in stops], dtype=np.float32)
    cols = np.array([s[1] for s in stops], dtype=np.float32)
    out = np.empty((h, w, 3), dtype=np.float32)
    for c in range(3):
        out[..., c] = np.interp(t, pos, cols[:, c])
    return Image.fromarray(out.astype(np.uint8), "RGB")


def drop_shadow(layer: Image.Image, blur: float, opacity: float, offset=(0, 0)) -> Image.Image:
    alpha = layer.split()[-1].filter(ImageFilter.GaussianBlur(blur))
    a = np.asarray(alpha, dtype=np.float32) * opacity
    alpha = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
    if offset != (0, 0):
        shifted = Image.new("L", layer.size, 0)
        shifted.paste(alpha, offset)
        alpha = shifted
    shadow = Image.new("RGBA", layer.size, (0, 0, 0, 255))
    shadow.putalpha(alpha)
    return shadow


def paste_at(base: Image.Image, layer: Image.Image, xy):
    base.alpha_composite(layer, (int(round(xy[0])), int(round(xy[1]))))


def paste_center(base: Image.Image, layer: Image.Image, center):
    paste_at(base, layer, (center[0] - layer.width / 2, center[1] - layer.height / 2))


def rounded_mask(size, radius):
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).rounded_rectangle([0, 0, size[0] - 1, size[1] - 1], radius=radius, fill=255)
    return m


def text_mask(text, fnt, tracking=0.0, pad=8) -> Image.Image:
    """Fehér szöveg átlátszó alapon."""
    probe = ImageDraw.Draw(Image.new("L", (8, 8)))
    if tracking == 0:
        box = probe.textbbox((0, 0), text, font=fnt)
        layer = Image.new("L", (int(box[2] - box[0]) + 2 * pad, int(box[3] - box[1]) + 2 * pad), 0)
        ImageDraw.Draw(layer).text((pad - box[0], pad - box[1]), text, font=fnt, fill=255)
        return layer

    widths = [probe.textlength(ch, font=fnt) for ch in text]
    asc, desc = fnt.getmetrics()
    total = sum(widths) + tracking * (len(text) - 1)
    layer = Image.new("L", (int(total) + 2 * pad, asc + desc + 2 * pad), 0)
    d = ImageDraw.Draw(layer)
    x = float(pad)
    for ch, w in zip(text, widths):
        d.text((x, pad), ch, font=fnt, fill=255)
        x += w + tracking
    return layer


def tint(mask: Image.Image, color) -> Image.Image:
    """Szín + maszk -> RGBA réteg. A color lehet RGB vagy RGBA."""
    rgba = tuple(color) if len(color) == 4 else tuple(color) + (255,)
    layer = Image.new("RGBA", mask.size, rgba)
    layer.putalpha(mask)
    return layer


GOLD_STOPS = [
    (0.00, (255, 248, 220)),
    (0.26, (234, 208, 145)),
    (0.46, (196, 156, 84)),
    (0.58, (243, 224, 172)),
    (0.80, (168, 124, 52)),
    (1.00, (216, 180, 112)),
]


def gold_text(text, fnt, tracking=0.0) -> Image.Image:
    mask = text_mask(text, fnt, tracking)
    grad = diag_gradient(mask.size, GOLD_STOPS, angle=96).convert("RGBA")
    grad.putalpha(mask)
    return grad


# ------------------------------------------------------------------ háttér ---
def make_background() -> Image.Image:
    rng = np.random.default_rng(20261114)
    yy = np.linspace(0, 1, CH, dtype=np.float32)[:, None]
    xx = np.linspace(0, 1, CW, dtype=np.float32)[None, :]

    stops = [(0.00, (4, 22, 15)), (0.35, (11, 50, 34)), (0.62, (16, 72, 49)), (1.00, (5, 26, 18))]
    pos = np.array([s[0] for s in stops], dtype=np.float32)
    cols = np.array([s[1] for s in stops], dtype=np.float32)
    img = np.empty((CH, CW, 3), dtype=np.float32)
    for c in range(3):
        img[..., c] = np.interp(yy[:, 0], pos, cols[:, c])[:, None]

    # lámpafény a kép közepe felé
    r2 = ((xx - 0.5) * 1.0) ** 2 + ((yy - 0.50) * 0.80) ** 2
    img += np.exp(-r2 / 0.12)[..., None] * np.array([24, 88, 60], dtype=np.float32) * 0.55

    # finom filc-szemcse
    img += rng.normal(0.0, 3.2, (CH, CW, 1)).astype(np.float32)
    img += rng.normal(0.0, 1.4, (CH, CW, 1)).astype(np.float32)

    base = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), "RGB").convert("RGBA")

    # nagy suit-vízjelek
    # halvány, kártyahátra emlékeztető rácsminta a filcen
    lattice = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
    ld = ImageDraw.Draw(lattice)
    step = 56 * S
    line_col = (150, 220, 180, 12)
    for k in range(-CH, CW + CH, step):
        ld.line([(k, 0), (k + CH, CH)], fill=line_col, width=max(1, S))
        ld.line([(k, CH), (k + CH, 0)], fill=line_col, width=max(1, S))
    base.alpha_composite(lattice)

    # vignetta
    r = np.sqrt(((xx - 0.5) * 1.06) ** 2 + ((yy - 0.5) * 0.90) ** 2) / 0.72
    vig = np.clip(1.0 - 0.85 * np.clip(r - 0.42, 0, None) ** 1.7, 0, 1)[..., None]
    arr = np.asarray(base.convert("RGB"), dtype=np.float32) * vig
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB").convert("RGBA")


# ------------------------------------------------------------------- keret ----
def make_ornament(size, alpha=205) -> Image.Image:
    """Sarokdísz: két negyedkör-ív és egy gyémánt (metszett arany)."""
    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    lw = max(2, int(1.5 * S))
    for frac, a in ((0.40, 0.70), (0.62, 0.42), (0.84, 0.22)):
        r = size * frac
        d.arc([-r, -r, r, r], start=0, end=90, fill=GOLD + (int(alpha * a),), width=lw)

    # gyémánt a középső íven
    r = size * 0.62
    off = r / math.sqrt(2)
    g = 7 * S
    d.polygon([(off, off - g), (off + g, off), (off, off + g), (off - g, off)], fill=GOLD + (alpha,))
    return layer


def add_frame(canvas: Image.Image):
    inset = 40 * S
    layer = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.rectangle([inset, inset, CW - inset, CH - inset], outline=GOLD + (150,), width=max(2, int(1.8 * S)))
    d.rectangle([inset + 12 * S, inset + 12 * S, CW - inset - 12 * S, CH - inset - 12 * S],
                outline=GOLD + (66,), width=max(1, int(1 * S)))

    orn = make_ornament(int(230 * S))
    for ang, corner in ((0, (inset, inset)), (90, (CW - inset, inset)),
                        (180, (CW - inset, CH - inset)), (270, (inset, CH - inset))):
        piece = orn.rotate(ang, resample=Image.BICUBIC, expand=True)
        x = corner[0] if ang in (0, 270) else corner[0] - piece.width
        y = corner[1] if ang in (0, 90) else corner[1] - piece.height
        layer.alpha_composite(piece, (x, y))
    canvas.alpha_composite(layer)


# ------------------------------------------------------------------ fotó -----
def make_photo(path: Path, w: int, h: int) -> Image.Image:
    im = Image.open(path).convert("RGB")
    target = w / h
    src = im.width / im.height
    if src > target:
        nw = int(im.height * target)
        im = im.crop(((im.width - nw) // 2, 0, (im.width + nw) // 2, im.height))
    else:
        nh = int(im.width / target)
        im = im.crop((0, (im.height - nh) // 2, im.width, (im.height + nh) // 2))
    im = im.resize((w, h), Image.LANCZOS)

    a = np.asarray(im, dtype=np.float32)
    a[..., 0] *= 1.05          # meleg tónus
    a[..., 1] *= 1.005
    a[..., 2] *= 0.95
    mean = a.mean()
    a = (a - mean) * 1.10 + mean + 3

    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    rr = np.sqrt(((xx / w - 0.5) * 2) ** 2 + ((yy / h - 0.5) * 2) ** 2) / 1.42
    a *= (1.0 - 0.26 * np.clip(rr, 0, 1) ** 2.4)[..., None]

    im = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
    im.putalpha(rounded_mask((w, h), 9 * S))
    return im


def frame_photo(photo: Image.Image) -> Image.Image:
    pad = 24 * S
    w, h = photo.size
    total = (w + 2 * pad, h + 2 * pad)
    layer = Image.new("RGBA", total, (0, 0, 0, 0))

    bw = 17 * S
    gold = diag_gradient(total, GOLD_STOPS, angle=118).convert("RGBA")
    mask = Image.new("L", total, 0)
    md = ImageDraw.Draw(mask)
    md.rounded_rectangle([0, 0, total[0] - 1, total[1] - 1], radius=16 * S, fill=255)
    md.rounded_rectangle([bw, bw, total[0] - bw - 1, total[1] - bw - 1], radius=10 * S, fill=0)
    gold.putalpha(mask)
    layer.alpha_composite(gold)

    d = ImageDraw.Draw(layer)
    d.rounded_rectangle([bw - 1 * S, bw - 1 * S, total[0] - bw + 1 * S, total[1] - bw + 1 * S],
                        radius=10 * S, outline=(52, 34, 10, 200), width=int(2 * S))
    d.rounded_rectangle([0, 0, total[0] - 1, total[1] - 1], radius=16 * S,
                        outline=(255, 248, 220, 110), width=max(1, int(1.2 * S)))

    # fénycsík a keret felső peremén
    gloss = Image.new("RGBA", total, (0, 0, 0, 0))
    ImageDraw.Draw(gloss).rounded_rectangle(
        [bw * 0.4, bw * 0.4, total[0] - bw * 0.4, bw * 0.92], radius=8 * S, fill=(255, 255, 255, 40))
    gloss.putalpha(Image.composite(gloss.split()[3], Image.new("L", total, 0), mask))
    layer.alpha_composite(gloss)

    layer.alpha_composite(photo, (pad, pad))
    return layer


# ------------------------------------------------------------------ kártyák ---
# (fájl, elforgatás fokban, vízszintes eltolás a középponttól, függőleges eltolás)
FAN = [
    ("ace_of_spades.png", -14.0, -272, 22),
    ("ace_of_hearts.png", -5.0, -92, 0),
    ("ace_of_clubs.png", 5.0, 92, 0),
    ("ace_of_diamonds.png", 14.0, 272, 22),
]


def load_card(cards_dir: Path, name: str, cw: int, ch: int) -> Image.Image:
    card = Image.open(cards_dir / name).convert("RGBA")
    card = card.resize((cw, ch), Image.LANCZOS)
    card = card.filter(ImageFilter.UnsharpMask(radius=1.2 * S, percent=32, threshold=2))
    edge = Image.new("RGBA", card.size, (0, 0, 0, 0))
    ImageDraw.Draw(edge).rounded_rectangle(
        [1, 1, cw - 2, ch - 2], radius=5 * S, outline=(0, 0, 0, 45), width=max(1, int(1 * S)))
    card.alpha_composite(edge)
    # halvány meleg tónus, hogy illeszkedjen az arany/zöld palettához
    warm = np.asarray(card, dtype=np.float32)
    warm[..., 0] *= 1.005
    warm[..., 1] *= 0.995
    warm[..., 2] *= 0.975
    return Image.fromarray(np.clip(warm, 0, 255).astype(np.uint8), "RGBA")


def make_fan(cards_dir: Path, cw: int, ch: int, bottom_y: float, center_x: float) -> Image.Image:
    """Legyező: a lapok az alsó élük körül fordulnak el."""
    layer = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
    pad = int(cw * 0.42) + 12 * S          # elég hely az elforgatott lapnak
    for name, ang, dx, dy in FAN:
        card = load_card(cards_dir, name, cw, ch)
        stage = Image.new("RGBA", (cw + 2 * pad, ch + 2 * pad), (0, 0, 0, 0))
        stage.alpha_composite(card, (pad, pad))
        pivot = (pad + cw / 2.0, pad + float(ch))       # alsó él közepe
        rot = stage.rotate(ang, resample=Image.BICUBIC, center=pivot)  # expand=False

        target = (center_x + dx * S, bottom_y + dy * S)
        pos = (target[0] - pivot[0], target[1] - pivot[1])
        shadow = drop_shadow(rot, blur=13 * S, opacity=0.42, offset=(3 * S, 7 * S))
        paste_at(layer, shadow, (pos[0] + 2 * S, pos[1] + 4 * S))
        paste_at(layer, rot, pos)
    return layer


# --------------------------------------------------------------- feliratok ----
def add_divider(canvas: Image.Image, y: float):
    layer = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    half = 300 * S
    gap = 30 * S
    for sgn in (-1, 1):
        x_start = CW / 2 + sgn * gap
        x_end = CW / 2 + sgn * (gap + half)
        d.line([(x_start, y), (x_end, y)], fill=GOLD + (140,), width=max(1, int(1.5 * S)))
        d.line([(x_start, y + 6 * S), (x_end, y + 6 * S)], fill=GOLD + (55,),
               width=max(1, int(0.8 * S)))
        r = 3.5 * S
        d.polygon([(x_end, y + 3 * S - r), (x_end + r, y + 3 * S),
                   (x_end, y + 3 * S + r), (x_end - r, y + 3 * S)], fill=GOLD + (160,))
    paste_center(layer, tint(text_mask("♠", font(SUITS_FONT, 26 * S)), GOLD + (215,)),
                 (CW / 2, y + 3 * S))
    canvas.alpha_composite(layer)


def add_progress(canvas: Image.Image, fnt_label, value=0.68, y=1150.0):
    bw, bh = 600 * S, 24 * S
    x0 = (CW - bw) // 2
    y0 = int(y * S)
    layer = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)

    d.rounded_rectangle([x0, y0, x0 + bw, y0 + bh], radius=bh // 2, fill=(4, 20, 14, 165))
    d.rounded_rectangle([x0, y0, x0 + bw, y0 + bh], radius=bh // 2,
                        outline=GOLD + (160,), width=max(2, int(1.6 * S)))

    inset = int(3 * S)
    fill_w = max(int(bw * value), bh)
    gold = diag_gradient((fill_w, bh), [(0.0, (247, 229, 176)), (0.55, (216, 188, 120)),
                                        (1.0, (172, 126, 54))], angle=8).convert("RGBA")
    fmask = Image.new("L", (fill_w, bh), 0)
    ImageDraw.Draw(fmask).rounded_rectangle([0, 0, fill_w - 1, bh - 1], radius=bh // 2, fill=255)
    gold.putalpha(fmask)
    layer.alpha_composite(gold, (x0 + inset, y0 + inset))

    gloss = Image.new("RGBA", (fill_w, bh), (0, 0, 0, 0))
    ImageDraw.Draw(gloss).rounded_rectangle(
        [int(6 * S), int(3.5 * S), fill_w - int(6 * S), int(bh * 0.45)],
        radius=int(bh * 0.2), fill=(255, 255, 255, 66))
    gloss.putalpha(Image.composite(gloss.split()[3], Image.new("L", (fill_w, bh), 0), fmask))
    layer.alpha_composite(gloss, (x0 + inset, y0 + inset))

    label = tint(text_mask(LOADING_LABEL, fnt_label, tracking=1.4 * S), CREAM + (205,))
    paste_center(layer, label, (CW / 2, y * S + 52 * S))
    canvas.alpha_composite(layer)


# --------------------------------------------------------------------- main ---
def ensure_font(font_dir: Path, name: str, remote: str) -> Path:
    p = font_dir / name
    if p.exists():
        return p
    font_dir.mkdir(parents=True, exist_ok=True)
    req = urllib.request.Request(remote, headers={"User-Agent": "pasziansz-loading-screen"})
    with urllib.request.urlopen(req, timeout=60) as r:
        data = json.load(r)
    p.write_bytes(base64.b64decode(data["content"]))
    print(f"letöltve: {p.name}")
    return p


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--photo", default=str(ROOT / "docs" / "nemeskep_2.jpg"))
    ap.add_argument("--out", default=str(ROOT / "docs" / "loading_geszi_pasziansz.png"))
    ap.add_argument("--font-dir", default=os.environ.get("FONT_DIR", "/tmp/fonts"))
    args = ap.parse_args()

    font_dir = Path(args.font_dir)
    title_font_path = ensure_font(
        font_dir, "PlayfairDisplay[wght].ttf",
        "https://api.github.com/repos/google/fonts/contents/ofl/playfairdisplay/PlayfairDisplay%5Bwght%5D.ttf")
    label_font_path = ensure_font(
        font_dir, "CinzelDecorative-Regular.ttf",
        "https://api.github.com/repos/google/fonts/contents/ofl/cinzeldecorative/CinzelDecorative-Regular.ttf")

    canvas = make_background()

    # ---- cím
    size = 132 * S
    while size > 40 * S:
        f = font(title_font_path, size, weight=700)
        if text_mask(TITLE, f).width <= int(1180 * S):
            break
        size -= 3 * S

    title_mask = text_mask(TITLE, f)
    title = gold_text(TITLE, f)

    # arany derengés a cím mögött
    glow = tint(title_mask.filter(ImageFilter.GaussianBlur(16 * S)), (232, 196, 120))
    glow.putalpha(glow.split()[3].point(lambda v: int(v * 0.30)))
    paste_center(canvas, glow, (CW / 2, 150 * S))

    # sötét kontúr + lágy árnyék
    stroke = tint(title_mask.filter(ImageFilter.MaxFilter(3)), INK + (185,))
    paste_center(canvas, stroke, (CW / 2, 150 * S))
    sh = drop_shadow(title, blur=12 * S, opacity=0.55, offset=(0, 6 * S))
    paste_center(canvas, sh, (CW / 2, 150 * S))
    paste_center(canvas, title, (CW / 2, 150 * S))

    add_divider(canvas, 236 * S)

    # ---- fotó arany keretben
    pw, ph = 640 * S, 480 * S
    photo = make_photo(Path(args.photo), pw, ph)
    framed = frame_photo(photo)
    f_sh = drop_shadow(framed, blur=26 * S, opacity=0.66, offset=(0, 16 * S))
    paste_center(canvas, f_sh, (CW / 2, 540 * S + 12 * S))
    paste_center(canvas, framed, (CW / 2, 540 * S))

    # ---- a négy ász legyezőben
    fan = make_fan(ROOT / "Pasziansz" / "Assets" / "Cards", 202 * S, 294 * S,
                   bottom_y=1100 * S, center_x=CW / 2)
    canvas.alpha_composite(fan)

    add_frame(canvas)
    add_progress(canvas, font(label_font_path, 24 * S))

    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    final = canvas.convert("RGB").resize((W, H), Image.LANCZOS)
    final.save(out, optimize=True)
    print(f"kész: {out}  ({final.width}x{final.height})")


if __name__ == "__main__":
    main()
