"""Rajzoló segédkönyvtár E-R és relációs adatbázis diagramokhoz.

A koordinátarendszer pontban (1/72 inch) értendő, az y tengely felfelé mutat.
A betűtípus az IBM Plex Sans (magyar ékezetekkel), a diagramsablonok A3/A4
méretre készülnek, hogy a PDF-be méretezés nélkül illeszkedjenek.
"""

from __future__ import annotations

import math
import os

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.font_manager import FontProperties, fontManager
from matplotlib.patches import Ellipse, FancyBboxPatch, Polygon, Rectangle
from matplotlib.textpath import TextToPath

HERE = os.path.dirname(os.path.abspath(__file__))
FONT_DIR = os.path.normpath(os.path.join(HERE, "..", "fonts"))

_FILES = {
    ("normal", 400): "IBMPlexSans-Regular.ttf",
    ("normal", 500): "IBMPlexSans-Medium.ttf",
    ("normal", 600): "IBMPlexSans-SemiBold.ttf",
    ("normal", 700): "IBMPlexSans-Bold.ttf",
    ("italic", 400): "IBMPlexSans-Italic.ttf",
    ("italic", 500): "IBMPlexSans-MediumItalic.ttf",
    ("normal", 300): "IBMPlexSans-Regular.ttf",
}
_MONO = {
    ("normal", 400): "IBMPlexMono-Regular.ttf",
    ("normal", 700): "IBMPlexMono-Medium.ttf",
    ("italic", 400): "IBMPlexMono-Italic.ttf",
}


def _register():
    for f in set(_FILES.values()) | set(_MONO.values()):
        p = os.path.join(FONT_DIR, f)
        if os.path.exists(p):
            fontManager.addfont(p)


_register()
_FP_CACHE: dict[tuple, FontProperties] = {}
_TTP = TextToPath()


def fp(size=8.0, weight=400, style="normal", mono=False):
    table = _MONO if mono else _FILES
    weight = min(table, key=lambda k: (k[0] != style, abs(k[1] - weight)))[1]
    fname = os.path.join(FONT_DIR, table[(style, weight)])
    key = (fname, size)
    if key not in _FP_CACHE:
        prop = FontProperties(fname=fname, size=size)
        _FP_CACHE[key] = prop
    return _FP_CACHE[key]


def text_width(s: str, size: float, weight: int = 400, style: str = "normal",
               mono: bool = False) -> float:
    w, _, _ = _TTP.get_text_width_height_descent(s, fp(size, weight, style, mono), False)
    return w


# ---------------------------------------------------------------- színek
INK = "#16211F"
MUTED = "#5D6C69"
FAINT = "#8C9B97"
LINE = "#41514D"

C_ENTITY_FILL = "#E3F1EC"
C_ENTITY_EDGE = "#1B6A56"
C_WEAK_FILL = "#FCF0DC"
C_WEAK_EDGE = "#96661A"
C_REL_FILL = "#FBF2D9"
C_REL_EDGE = "#A5741A"
C_ATTR_FILL = "#FFFFFF"
C_ATTR_EDGE = "#829691"

C_HEAD = "#16483B"
C_HEAD_TXT = "#FFFFFF"
C_ROW_ALT = "#F1F7F5"
C_BOX_EDGE = "#9DB2AC"

plt.rcParams["svg.fonttype"] = "path"
plt.rcParams["pdf.fonttype"] = 42


def new_canvas(w: float, h: float, bg: str = "white"):
    fig = plt.figure(figsize=(w / 72.0, h / 72.0), dpi=72)
    fig.patch.set_facecolor(bg)
    ax = fig.add_axes([0, 0, 1, 1])
    ax.set_xlim(0, w)
    ax.set_ylim(0, h)
    ax.set_aspect("equal")
    ax.axis("off")
    return fig, ax


# ------------------------------------------------------- geometriai segédek
def rect_edge(cx, cy, w, h, tx, ty, pad=0.0):
    dx, dy = tx - cx, ty - cy
    if dx == 0 and dy == 0:
        return cx, cy
    sx = (w / 2 + pad) / abs(dx) if dx else math.inf
    sy = (h / 2 + pad) / abs(dy) if dy else math.inf
    s = min(sx, sy)
    return cx + dx * s, cy + dy * s


def diamond_edge(cx, cy, w, h, tx, ty, pad=0.0):
    dx, dy = tx - cx, ty - cy
    if dx == 0 and dy == 0:
        return cx, cy
    s = 1.0 / (abs(dx) / (w / 2 + pad) + abs(dy) / (h / 2 + pad))
    return cx + dx * s, cy + dy * s


def ellipse_edge(cx, cy, rx, ry, tx, ty):
    dx, dy = tx - cx, ty - cy
    if dx == 0 and dy == 0:
        return cx, cy
    s = 1.0 / math.sqrt((dx / rx) ** 2 + (dy / ry) ** 2)
    return cx + dx * s, cy + dy * s


def shape_edge(shape, tx, ty, pad=0.0):
    if shape["kind"] == "rect":
        return rect_edge(shape["cx"], shape["cy"], shape["w"], shape["h"], tx, ty, pad)
    if shape["kind"] == "diamond":
        return diamond_edge(shape["cx"], shape["cy"], shape["w"], shape["h"], tx, ty, pad)
    return ellipse_edge(shape["cx"], shape["cy"], shape["rx"], shape["ry"], tx, ty)


def perp(dx, dy, dist):
    n = math.hypot(dx, dy) or 1.0
    return -dy / n * dist, dx / n * dist


def line(ax, p, q, color=LINE, lw=0.9, ls="-", zorder=2, alpha=1.0):
    ax.plot([p[0], q[0]], [p[1], q[1]], color=color, lw=lw, ls=ls, zorder=zorder,
            solid_capstyle="round", alpha=alpha)


def bridge_line(ax, p, q, color=LINE, lw=0.9, zorder=2, extend=1.0):
    """Vonal rajzolása úgy, hogy a végénél kicsit rövidebb (kereszteződés-híd)."""
    dx, dy = q[0] - p[0], q[1] - p[1]
    n = math.hypot(dx, dy) or 1.0
    q2 = (q[0] - dx / n * extend, q[1] - dy / n * extend)
    line(ax, p, q2, color=color, lw=lw, zorder=zorder)


def gap_breaks(ax, p, q, gap_pts, color=LINE, lw=0.9, zorder=2, gap_r=3.4,
               dash=None):
    """Egy szakasz kirajzolása úgy, hogy a gap_pts pontok körül hézag legyen."""
    length = math.dist(p, q)
    if length < 1:
        return
    steps = max(int(length * 2), 8)
    path = [(p[0] + (q[0] - p[0]) * i / steps, p[1] + (q[1] - p[1]) * i / steps)
            for i in range(steps + 1)]
    chunks, chunk = [], []
    for pt in path:
        if any(math.dist(pt, g) < gap_r for g in gap_pts):
            if len(chunk) > 1:
                chunks.append(chunk)
            chunk = []
        else:
            chunk.append(pt)
    if len(chunk) > 1:
        chunks.append(chunk)
    for c in chunks:
        ax.plot([a[0] for a in c], [a[1] for a in c], color=color, lw=lw, zorder=zorder,
                solid_capstyle="round", ls=dash if dash else "-")


def elbow(ax, pts, color=LINE, lw=0.9, zorder=2, ls="-", rounding=4.0):
    """Töröttvonal lekerekített sarkokkal."""
    if len(pts) == 2:
        line(ax, pts[0], pts[1], color, lw, ls, zorder)
        return
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    ax.plot(xs, ys, color=color, lw=lw, ls=ls, zorder=zorder,
            solid_capstyle="round", solid_joinstyle="round")


def label(ax, x, y, s, size=7.0, color=INK, weight=400, ha="center", va="center",
          style="normal", zorder=6, rotation=0, bbox=None, mono=False):
    ax.text(x, y, s, fontsize=size, color=color, ha=ha, va=va,
            fontproperties=fp(size, weight, style, mono),
            zorder=zorder, rotation=rotation, bbox=bbox)


# --------------------------------------------------------------- E-R elemek
def er_entity(ax, x, y, name, size=9.6, weak=False, padx=17, pady=10):
    w = text_width(name, size, 700) + padx * 2
    h = pady * 2 + size
    ax.add_patch(FancyBboxPatch((x - w / 2, y - h / 2), w, h,
                                boxstyle="round,pad=0,rounding_size=2.0",
                                linewidth=1.35,
                                edgecolor=C_WEAK_EDGE if weak else C_ENTITY_EDGE,
                                facecolor=C_WEAK_FILL if weak else C_ENTITY_FILL,
                                zorder=4))
    if weak:
        ax.add_patch(Rectangle((x - w / 2 + 3.0, y - h / 2 + 3.0), w - 6, h - 6,
                               fill=False, linewidth=0.9, edgecolor=C_WEAK_EDGE,
                               zorder=5))
    label(ax, x, y, name, size=size, weight=700, zorder=6)
    return dict(cx=x, cy=y, w=w, h=h, kind="rect")


def er_relation(ax, x, y, name, size=7.9, identifying=False, wpad=19, h=27):
    w = text_width(name, size, 700) + wpad
    pts = [(x, y + h / 2), (x + w / 2, y), (x, y - h / 2), (x - w / 2, y)]
    ax.add_patch(Polygon(pts, closed=True, linewidth=1.15,
                         edgecolor=C_REL_EDGE, facecolor=C_REL_FILL, zorder=4))
    if identifying:
        inset = 3.4
        k = h / w
        pts2 = [(x, y + h / 2 - inset), (x + w / 2 - inset / k, y),
                (x, y - h / 2 + inset), (x - w / 2 + inset / k, y)]
        ax.add_patch(Polygon(pts2, closed=True, fill=False, linewidth=0.85,
                             edgecolor=C_REL_EDGE, zorder=5))
    label(ax, x, y, name, size=size, weight=700, zorder=6)
    return dict(cx=x, cy=y, w=w, h=h, kind="diamond")


def er_attr(ax, x, y, name, size=7.3, key=False, multi=False, derived=False,
            color=None, padx=8, pady=5.4, partial=False):
    w = text_width(name, size, 700 if key else 400) + padx * 2
    h = size + pady * 2
    rx, ry = w / 2, h / 2
    edge = color or (C_ENTITY_EDGE if key else C_ATTR_EDGE)
    ax.add_patch(Ellipse((x, y), w, h, linewidth=0.95, edgecolor=edge,
                         facecolor=C_ATTR_FILL, zorder=4,
                         linestyle=(0, (3, 2)) if derived else "-"))
    if multi:
        ax.add_patch(Ellipse((x, y), w - 3.6, h - 3.6, fill=False, linewidth=0.8,
                             edgecolor=edge, zorder=5,
                             linestyle=(0, (3, 2)) if derived else "-"))
    label(ax, x, y, name, size=size, weight=700 if key else 400, zorder=6)
    if key:
        tw = text_width(name, size, 700)
        ax.plot([x - tw / 2, x + tw / 2], [y - size * 0.44, y - size * 0.44],
                color=INK, lw=0.75, zorder=7,
                linestyle=(0, (2.2, 1.6)) if partial else "-")
    return dict(cx=x, cy=y, rx=rx, ry=ry, kind="ellipse")


def connect(ax, a, b, color=LINE, lw=0.9, zorder=2, total_a=False, total_b=False,
            gaps=(), gap_r=3.4, pad=0.0):
    """Két alakzat közötti egyenes (opcionális teljes részvétel = dupla vonal)."""
    p = shape_edge(a, b["cx"], b["cy"], pad)
    q = shape_edge(b, a["cx"], a["cy"], pad)
    dx, dy = q[0] - p[0], q[1] - p[1]
    n = math.hypot(dx, dy) or 1.0
    off = 1.5
    ox, oy = perp(dx, dy, off)
    lines = []
    if total_a:
        lines.append((p, q))
        lines.append(((p[0] + ox, p[1] + oy), (q[0] + ox * 0.4, q[1] + oy * 0.4)))
    elif total_b:
        lines.append((p, q))
        lines.append(((p[0] + ox * 0.4, p[1] + oy * 0.4), (q[0] + ox, q[1] + oy)))
    else:
        lines.append((p, q))
    if gaps:
        for a_, b_ in lines:
            gap_breaks(ax, a_, b_, gaps, color=color, lw=lw, zorder=zorder, gap_r=gap_r)
    else:
        for a_, b_ in lines:
            line(ax, a_, b_, color=color, lw=lw, zorder=zorder)
    return p, q


def card_text(ax, a, b, text, along=0.30, side=1, dist=8.5, size=7.6, color=INK):
    p = shape_edge(a, b["cx"], b["cy"])
    dx, dy = b["cx"] - p[0], b["cy"] - p[1]
    n = math.hypot(dx, dy) or 1.0
    x = p[0] + dx * along
    y = p[1] + dy * along
    ox, oy = perp(dx, dy, dist * side)
    label(ax, x + ox, y + oy, text, size=size, color=color, weight=600, zorder=7,
          bbox=dict(boxstyle="round,pad=0.10", fc="white", ec="none", alpha=0.85))


def along_text(ax, p, q, frac, text, side=1, dist=10.0, size=6.8, rotation=None,
               color=MUTED, style="italic"):
    """Szöveg egy szakasz mentén, a szakasztól oldalra eltolva."""
    import math as _m
    dx, dy = q[0] - p[0], q[1] - p[1]
    n = _m.hypot(dx, dy) or 1.0
    x = p[0] + dx * frac
    y = p[1] + dy * frac
    ox, oy = perp(dx, dy, dist * side)
    ang = _m.degrees(_m.atan2(dy, dx))
    if rotation is None:
        rotation = ang
    if -90 > rotation or rotation > 90:
        rotation = rotation + 180 if rotation < -90 else rotation - 180
    label(ax, x + ox, y + oy, text, size=size, color=color, rotation=rotation,
          style=style, zorder=8,
          bbox=dict(boxstyle="round,pad=0.12", fc="white", ec="none", alpha=0.92))


# ------------------------------------------------------- relációs tábla elemek
def db_table(ax, x, y, name, cols, width=200, row_h=11.6, head_h=17, size=6.4,
             head_size=7.4, tag=""):
    """Relációs tábla: (x, y) a doboz bal felső sarka, y lefelé csökken.

    cols: [(név, típus, jel)], ahol jel: 'PK', 'PFK', 'FK' vagy ''.
    """
    h = head_h + row_h * len(cols) + 2
    ax.add_patch(Rectangle((x, y - h), width, h, facecolor="white",
                           edgecolor=C_BOX_EDGE, linewidth=1.0, zorder=3))
    ax.add_patch(Rectangle((x, y - head_h), width, head_h, facecolor=C_HEAD,
                           edgecolor=C_HEAD, linewidth=0, zorder=4))
    if tag:
        label(ax, x + 6.5, y - head_h / 2, tag, size=5.4, color="#A9D6C8",
              ha="left", zorder=6)
    label(ax, x + width / 2, y - head_h / 2, name, size=head_size, color=C_HEAD_TXT,
          weight=700, zorder=6)
    for i, (cname, ctype, ctag) in enumerate(cols):
        cy = y - head_h - row_h * i - row_h / 2 - 1.0
        if i % 2 == 1:
            ax.add_patch(Rectangle((x, cy - row_h / 2), width, row_h, facecolor=C_ROW_ALT,
                                   edgecolor="none", zorder=3.4))
        is_pk = "PK" in ctag
        col = INK if is_pk else ("#42544F" if ctag else MUTED)
        label(ax, x + 25, cy, cname, size=size, color=col, ha="left", zorder=6,
              weight=600 if is_pk else 400, style="italic" if "FK" in ctag and not is_pk else "normal")
        if is_pk:
            tw = text_width(cname, size, 600)
            ax.plot([x + 25, x + 25 + tw], [cy - size * 0.40, cy - size * 0.40],
                    color=col, lw=0.6, zorder=6)
        label(ax, x + width - 6, cy, ctype, size=size - 0.6, color=FAINT, ha="right",
              zorder=6)
        if ctag:
            label(ax, x + 5, cy, ctag.replace("*", ""), size=4.7,
                  color=C_ENTITY_EDGE if is_pk else FAINT,
                  weight=600 if is_pk else 400, ha="left", zorder=6)
    return dict(x=x, y=y, w=width, h=h, cx=x + width / 2, cy=y - h / 2,
                l=x, r=x + width, top=y, bottom=y - h, cols=cols, row_h=row_h,
                head_h=head_h)


def table_col_pt(t, index, frac=0.5):
    """A tábla adott sorának y koordinátája (0 = első adatsor)."""
    return t["top"] - t["head_h"] - t["row_h"] * index - t["row_h"] * frac - 1.0


def fk_line(ax, src, dst, label_text="", color=LINE, lw=0.9, zorder=1.6,
            one_side="src", elbow_pts=None, label_at=None):
    """Idegen kulcs kapcsolat varjúláb jelöléssel.

    one_side = 'src'  -> a src oldalon van az 'egy', a dst oldalon a 'sok'.
    """
    if elbow_pts:
        pts = elbow_pts
        p, q = pts[0], pts[-1]
        elbow(ax, pts, color=color, lw=lw, zorder=zorder)
    else:
        p = (src["cx"], src["cy"]) if one_side == "src" else (src["cx"], src["cy"])
        # a dobozok legközelebbi oldalpontjai
        p = _nearest_border(src, dst["cx"], dst["cy"])
        q = _nearest_border(dst, src["cx"], src["cy"])
        line(ax, p, q, color=color, lw=lw, zorder=zorder)
    # varjúláb a 'sok' oldalon
    many_pt = q if one_side == "src" else p
    one_pt = p if one_side == "src" else q
    ref = p if one_side == "src" else q
    dx, dy = many_pt[0] - ref[0], many_pt[1] - ref[1]
    n = math.hypot(dx, dy) or 1.0
    ux, uy = dx / n, dy / n
    back = (many_pt[0] - ux * 6.5, many_pt[1] - uy * 6.5)
    for ang in (-0.55, 0.0, 0.55):
        ca, sa = math.cos(ang), math.sin(ang)
        rx = -(ux * ca - uy * sa)
        ry = -(ux * sa + uy * ca)
        line(ax, back, (back[0] + rx * 6.0, back[1] + ry * 6.0), color=color, lw=lw,
             zorder=zorder)
    if label_text:
        mx, my = (p[0] + q[0]) / 2, (p[1] + q[1]) / 2
        if label_at:
            mx, my = label_at
        label(ax, mx, my, label_text, size=5.9, color=MUTED, zorder=7,
              bbox=dict(boxstyle="round,pad=0.14", fc="white", ec="none", alpha=0.92))


def _nearest_border(t, tx, ty):
    return rect_edge(t["cx"], t["cy"], t["w"], t["h"], tx, ty)


def save(fig, base, dpi=200):
    fig.savefig(base + ".png", dpi=dpi, facecolor="white")
    fig.savefig(base + ".svg", facecolor="white")
    plt.close(fig)
