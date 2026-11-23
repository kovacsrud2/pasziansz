# -*- coding: utf-8 -*-
"""Kis dokumentum-motor: Markdown-szerű forrás -> nyomdai PDF (ReportLab).

Támogatott blokkok
------------------
  # H1              ## H2              ### H3
  sima bekezdés (sorkizárt)
  - felsorolás elem           1. számozott elem
  > idézet / kiemelt doboz     (a > után opcionális [cím]: ...)
  ``` nyelv ... ```             kódrészlet
  | a | b |  pipe-táblázat   (a második sor --- választó)
  ![képaláírás](útvonal)       ábra (SVG vektorosan)
  ::: pagebreak                oldaltörés
  ::: a3 ábra-útvonal | képaláírás   teljes A3-as ábraoldal
  ::: spacer 12                függőleges hely

Sor közbeni jelölések: **félkövér**, *dőlt*, `kód`.
"""

from __future__ import annotations

import os
import re

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_JUSTIFY, TA_LEFT
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas as pdfcanvas
from reportlab.platypus import (BaseDocTemplate, CondPageBreak, Flowable, Frame,
                                FrameBreak, KeepTogether, NextPageTemplate, PageBreak,
                                PageTemplate, Paragraph, Spacer, Table, TableStyle)

HERE = os.path.dirname(os.path.abspath(__file__))
FONT_DIR = os.path.normpath(os.path.join(HERE, "..", "fonts"))

# ------------------------------------------------------------------ színek
GREEN = colors.HexColor("#16483B")
GREEN_MID = colors.HexColor("#1E6B57")
GREEN_LT = colors.HexColor("#E3F1EC")
GREEN_PALE = colors.HexColor("#F2F8F6")
AMBER = colors.HexColor("#A5741A")
AMBER_LT = colors.HexColor("#FCF4E2")
INK = colors.HexColor("#16211F")
MUTED = colors.HexColor("#566661")
FAINT = colors.HexColor("#8C9B97")
RULE = colors.HexColor("#D5E0DC")
WHITE = colors.white

SANS = "Plex"
SANS_B = "Plex-Bold"
SANS_I = "Plex-Italic"
SANS_BI = "Plex-BoldItalic"
SANS_M = "Plex-Medium"
MONO = "PlexMono"

_A4 = (595.28, 841.89)
_A3L = (1191.0, 841.89)


def register_fonts():
    pairs = [
        (SANS, "IBMPlexSans-Regular.ttf"),
        (SANS_B, "IBMPlexSans-Bold.ttf"),
        (SANS_I, "IBMPlexSans-Italic.ttf"),
        (SANS_BI, "IBMPlexSans-Bold.ttf"),
        (SANS_M, "IBMPlexSans-Medium.ttf"),
        (MONO, "IBMPlexMono-Regular.ttf"),
    ]
    for name, f in pairs:
        pdfmetrics.registerFont(TTFont(name, os.path.join(FONT_DIR, f)))
    pdfmetrics.registerFontFamily(SANS, normal=SANS, bold=SANS_B, italic=SANS_I,
                                  boldItalic=SANS_BI)


# --------------------------------------------------------------- stílusok
def make_styles(hyphen=True):
    hyp = "hu_HU" if hyphen else None
    s = {}
    s["body"] = ParagraphStyle("body", fontName=SANS, fontSize=9.6, leading=13.9,
                               textColor=INK, alignment=TA_JUSTIFY,
                               spaceAfter=5.2, hyphenationLang=hyp,
                               splitLongWords=1)
    s["body_left"] = ParagraphStyle("body_left", parent=s["body"], alignment=TA_LEFT)
    s["small"] = ParagraphStyle("small", parent=s["body"], fontSize=8.2, leading=11.4,
                                textColor=MUTED, alignment=TA_LEFT)
    s["caption"] = ParagraphStyle("caption", parent=s["body"], fontSize=7.9,
                                  leading=10.4, textColor=MUTED, alignment=TA_LEFT,
                                  spaceBefore=4, spaceAfter=10)
    s["h1"] = ParagraphStyle("h1", fontName=SANS_B, fontSize=16.5, leading=19.5,
                             textColor=GREEN, spaceBefore=2, spaceAfter=7.5,
                             alignment=TA_LEFT)
    s["h2"] = ParagraphStyle("h2", fontName=SANS_B, fontSize=11.6, leading=14.6,
                             textColor=GREEN, spaceBefore=13, spaceAfter=5,
                             alignment=TA_LEFT, keepWithNext=1)
    s["h3"] = ParagraphStyle("h3", fontName=SANS_B, fontSize=9.9, leading=12.8,
                             textColor=INK, spaceBefore=9, spaceAfter=3.5,
                             alignment=TA_LEFT, keepWithNext=1)
    s["bullet"] = ParagraphStyle("bullet", parent=s["body"], leftIndent=13,
                                 bulletIndent=3, spaceAfter=2.6)
    s["num"] = ParagraphStyle("num", parent=s["body"], leftIndent=17,
                              bulletIndent=2, spaceAfter=2.6)
    s["cell"] = ParagraphStyle("cell", fontName=SANS, fontSize=8.0, leading=10.6,
                               textColor=INK, alignment=TA_LEFT,
                               hyphenationLang=hyp, splitLongWords=1)
    s["cell_c"] = ParagraphStyle("cell_c", parent=s["cell"], alignment=TA_CENTER)
    s["cell_h"] = ParagraphStyle("cell_h", fontName=SANS_B, fontSize=8.0, leading=10.6,
                                 textColor=WHITE, alignment=TA_LEFT)
    s["cell_hc"] = ParagraphStyle("cell_hc", parent=s["cell_h"], alignment=TA_CENTER)
    s["code"] = ParagraphStyle("code", fontName=MONO, fontSize=7.5, leading=9.6,
                               textColor=INK, alignment=TA_LEFT)
    s["quote"] = ParagraphStyle("quote", parent=s["body"], fontSize=9.2, leading=13.2,
                                textColor=INK)
    s["quote_t"] = ParagraphStyle("quote_t", parent=s["body"], fontName=SANS_B,
                                  fontSize=8.6, leading=11.6, textColor=GREEN,
                                  spaceAfter=2.5, alignment=TA_LEFT)
    s["cover_t"] = ParagraphStyle("cover_t", fontName=SANS_B, fontSize=33, leading=37,
                                  textColor=GREEN, alignment=TA_LEFT)
    s["cover_s"] = ParagraphStyle("cover_s", fontName=SANS, fontSize=13.5, leading=18,
                                  textColor=MUTED, alignment=TA_LEFT)
    return s


# ------------------------------------------------------- sor közbeni jelölés
_INLINE = re.compile(r"(\*\*.+?\*\*|`[^`]+?`|\*[^*\n]+?\*)")
_ESC = {"&": "&amp;", "<": "&lt;", ">": "&gt;"}


def esc(text: str) -> str:
    return "".join(_ESC.get(c, c) for c in text)


def inline(text: str, mono_size: float | None = None) -> str:
    out = []
    for tok in _INLINE.split(text):
        if not tok:
            continue
        if tok.startswith("**") and tok.endswith("**") and len(tok) > 4:
            out.append("<b>%s</b>" % inline(tok[2:-2], mono_size))
        elif tok.startswith("`") and tok.endswith("`") and len(tok) > 2:
            size = f' size="{mono_size}"' if mono_size else ""
            out.append('<font name="%s" color="#17493C"%s>%s</font>'
                       % (MONO, size, esc(tok[1:-1])))
        elif tok.startswith("*") and tok.endswith("*") and len(tok) > 2:
            out.append("<i>%s</i>" % inline(tok[1:-1], mono_size))
        else:
            out.append(esc(tok))
    return "".join(out)


# ------------------------------------------------------------- segéd-osztályok
class Rule(Flowable):
    """Vékony vízszintes vonal."""

    def __init__(self, width=None, thickness=0.7, color=RULE, space=3):
        super().__init__()
        self._width = width
        self.thickness = thickness
        self.color = color
        self.space = space
        self.width = width or 0

    def wrap(self, aw, ah):
        self.width = self._width or aw
        self.height = self.thickness + self.space * 2
        return self.width, self.height

    def draw(self):
        c = self.canv
        c.setStrokeColor(self.color)
        c.setLineWidth(self.thickness)
        c.line(0, self.space, self.width, self.space)


class Figure(Flowable):
    """Vektoros (SVG -> Drawing) vagy raszteres ábra adott szélességre méretezve."""

    def __init__(self, path, width=None, height=None, align="center"):
        super().__init__()
        self.path = path
        self.target_w = width
        self.target_h = height
        self.align = align
        self._drawing = None

    def _load(self):
        from svglib.svglib import svg2rlg
        from reportlab.graphics import renderPDF
        self._render = renderPDF
        self._drawing = svg2rlg(self.path)
        return self._drawing

    def wrap(self, aw, ah):
        d = self._load()
        sw, sh = d.width, d.height
        scale = 1.0
        if self.target_w:
            scale = min(scale if not self.target_h else scale, self.target_w / sw)
        if self.target_h:
            scale = min(scale, self.target_h / sh)
        if not self.target_w and not self.target_h:
            scale = min(aw / sw, ah / sh)
        self.scale = scale
        self.width = sw * scale
        self.height = sh * scale
        return self.width, self.height

    def draw(self):
        self.canv.saveState()
        self.canv.scale(self.scale, self.scale)
        self._render.draw(self._drawing, self.canv, 0, 0)
        self.canv.restoreState()


class Vazlat(Flowable):
    """Halvány pontrácsos rajzolási felület a diáknak."""

    def __init__(self, width, height, label="Vázlat", hint=""):
        super().__init__()
        self.width = width
        self.height = height
        self.label = label
        self.hint = hint

    def wrap(self, aw, ah):
        return self.width, self.height

    def draw(self):
        c = self.canv
        w, h = self.width, self.height
        c.saveState()
        c.setFillColor(colors.HexColor("#FBFDFC"))
        c.setStrokeColor(colors.HexColor("#CFE0DA"))
        c.setLineWidth(0.7)
        c.setDash(2.2, 2.2)
        c.rect(0, 0, w, h, stroke=1, fill=1)
        c.setDash()
        c.setFillColor(colors.HexColor("#C3D6D0"))
        step = 14.0
        y = step
        while y < h - 6:
            x = step
            while x < w - 6:
                c.circle(x, y, 0.45, stroke=0, fill=1)
                x += step
            y += step
        c.setFillColor(FAINT)
        c.setFont(SANS, 7.2)
        c.drawString(8, h - 12, self.label)
        if self.hint:
            c.drawRightString(w - 8, h - 12, self.hint)
        c.restoreState()


class CoverBanner(Flowable):
    def __init__(self, title, subtitle, width, height=150):
        super().__init__()
        self.title = title
        self.subtitle = subtitle
        self.width = width
        self.height = height

    def wrap(self, aw, ah):
        return self.width, self.height

    def draw(self):
        c = self.canv
        w, h = self.width, self.height
        c.setFillColor(GREEN)
        c.rect(0, 0, w, h, stroke=0, fill=1)
        c.setFillColor(colors.HexColor("#2E8A70"))
        c.rect(0, h - 5, w, 5, stroke=0, fill=1)
        c.setFillColor(WHITE)
        c.setFont(SANS_B, 30)
        y = h - 58
        for line in self.title.split("\n"):
            c.drawString(24, y, line)
            y -= 36
        c.setFillColor(colors.HexColor("#B9DCD1"))
        c.setFont(SANS, 12)
        for line in self.subtitle.split("\n"):
            c.drawString(24, y - 2, line)
            y -= 16


# --------------------------------------------------------------- táblaépítés
def _cell_w(text, style):
    from reportlab.pdfbase.pdfmetrics import stringWidth
    plain = re.sub(r"</?(b|i|font)[^>]*>", "", text)
    return stringWidth(plain, style.fontName, style.fontSize)


def _clamp_min(w, avail, minw=27.0):
    """Minden oszlop legalább minw széles legyen; a többletet a széles oszlopoktól vesszük el."""
    w = list(w)
    for _ in range(6):
        short = [i for i, x in enumerate(w) if x < minw]
        if not short:
            break
        need = sum(minw - w[i] for i in short)
        big = [i for i, x in enumerate(w) if x > minw + 1]
        if not big:
            break
        room = sum(w[i] - minw for i in big)
        for i in big:
            w[i] -= need * (w[i] - minw) / room
        for i in short:
            w[i] = minw
    # ha így sem fér el, arányosan összenyomjuk
    total = sum(w)
    if total > avail:
        w = [x * avail / total for x in w]
    return w


def build_table(rows, avail, styles, header=True, aligns=None, widths=None,
                font_size=8.0, zebra=True, first_col_bold=False, valign="TOP"):
    ncols = max(len(r) for r in rows)
    rows = [r + [""] * (ncols - len(r)) for r in rows]
    aligns = aligns or ["l"] * ncols
    cs, ch, cc = styles["cell"], styles["cell_h"], styles["cell_c"]
    cs = ParagraphStyle(f"c{font_size}", parent=cs, fontSize=font_size,
                        leading=font_size * 1.33)
    ch = ParagraphStyle(f"h{font_size}", parent=ch, fontSize=font_size,
                        leading=font_size * 1.33)
    ccell = ParagraphStyle(f"cc{font_size}", parent=cc, fontSize=font_size,
                           leading=font_size * 1.33)

    def mk(text, st, al):
        txt = inline(text)
        st2 = ccell if al == "c" else st
        if text.startswith("**") and text.endswith("**") and not header:
            pass
        return Paragraph(txt, st2)

    parsed = []
    for ri, row in enumerate(rows):
        prow = []
        for ci, cell in enumerate(row):
            st = ch if (header and ri == 0) else cs
            if header and ri == 0 and aligns[ci] == "c":
                st = ParagraphStyle("hh", parent=ch, alignment=TA_CENTER)
            elif aligns[ci] == "c":
                st = ccell
            prow.append(Paragraph(inline(cell), st))
        parsed.append(prow)

    if widths:
        w = [avail * x / sum(widths) for x in widths]
        w = _clamp_min(w, avail)
    else:
        # automatikus oszlopszélesség: a leghosszabb tartalom, max 42% összesen
        natural = []
        for ci in range(ncols):
            mx = 0
            for ri, row in enumerate(rows):
                st = ch if (header and ri == 0) else cs
                mx = max(mx, _cell_w(inline(row[ci]), st))
            natural.append(mx + 11)
        total = sum(natural)
        if total > avail:
            f = avail / total
            natural = [n * f for n in natural]
            # nagyon hosszú cellák tördelése: max 30% szélesség
            cap = avail * 0.34
            over = 0
            for i, n in enumerate(natural):
                if n > cap:
                    over += n - cap
                    natural[i] = cap
            if over:
                flex = [i for i, n in enumerate(natural) if n < cap]
                room = sum(natural[i] for i in flex)
                for i in flex:
                    natural[i] += over * natural[i] / room if room else 0
        else:
            scale = avail / total
            natural = [n * scale for n in natural]
        w = _clamp_min(natural, avail)

    t = Table(parsed, colWidths=w, repeatRows=1 if header else 0, hAlign="LEFT")
    cmds = [
        ("VALIGN", (0, 0), (-1, -1), valign),
        ("LEFTPADDING", (0, 0), (-1, -1), 4.5),
        ("RIGHTPADDING", (0, 0), (-1, -1), 4.5),
        ("TOPPADDING", (0, 0), (-1, -1), 3.4),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 3.4),
        ("LINEBELOW", (0, 0), (-1, -1), 0.4, RULE),
    ]
    if header:
        cmds += [
            ("BACKGROUND", (0, 0), (-1, 0), GREEN),
            ("TOPPADDING", (0, 0), (-1, 0), 4.5),
            ("BOTTOMPADDING", (0, 0), (-1, 0), 4.5),
            ("LINEBELOW", (0, 0), (-1, 0), 0, GREEN),
            ("BOX", (0, 0), (-1, 0), 0, GREEN),
        ]
        if zebra:
            for ri in range(1, len(parsed)):
                if ri % 2 == 0:
                    cmds.append(("BACKGROUND", (0, ri), (-1, ri), GREEN_PALE))
    else:
        if zebra:
            for ri in range(len(parsed)):
                if ri % 2 == 1:
                    cmds.append(("BACKGROUND", (0, ri), (-1, ri), GREEN_PALE))
    if first_col_bold:
        pass
    t.setStyle(TableStyle(cmds))
    return t


def callout(title, body_paragraphs, avail, styles, color=GREEN_LT, bar=GREEN_MID):
    inner = []
    if title:
        inner.append(Paragraph(inline(title), styles["quote_t"]))
    inner.extend(body_paragraphs)
    t = Table([[inner]], colWidths=[avail])
    t.setStyle(TableStyle([
        ("BACKGROUND", (0, 0), (-1, -1), color),
        ("LINEBEFORE", (0, 0), (0, -1), 2.6, bar),
        ("LEFTPADDING", (0, 0), (-1, -1), 9),
        ("RIGHTPADDING", (0, 0), (-1, -1), 9),
        ("TOPPADDING", (0, 0), (-1, -1), 7),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 7),
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
    ]))
    return t


class CodeSpan(Flowable):
    """Egy kódsor (háttérrel), hogy a hosszú kódblokk oldalak között törhető legyen."""

    def __init__(self, text, avail, font_size, first=False, last=False, width=None):
        super().__init__()
        self.text = text
        self.avail = avail
        self.fs = font_size
        self.first = first
        self.last = last
        self.width = width or avail

    def wrap(self, aw, ah):
        self.height = self.fs * 1.5 + (2 if self.first else 0) + (3 if self.last else 0)
        return self.width, self.height

    def draw(self):
        c = self.canv
        top = self.height
        pad_top = 3 if self.first else 0
        c.setFillColor(colors.HexColor("#F5F8F7"))
        c.rect(0, 0, self.width, self.height, stroke=0, fill=1)
        c.setStrokeColor(colors.HexColor("#CBDED8"))
        c.setLineWidth(2.2)
        if self.first or self.last:
            pass
        c.setFillColor(INK)
        c.setFont(MONO, self.fs)
        y = self.height - self.fs * 1.12 - pad_top
        c.drawString(8, y, self.text.replace(" ", "\u00a0"))
        # bal oldali sáv
        c.setFillColor(colors.HexColor("#CBDED8"))
        c.rect(0, 0, 1.6, self.height, stroke=0, fill=1)


def code_block(text, avail, styles):
    lines = text.rstrip("\n").split("\n")
    maxlen = max((len(l) for l in lines), default=0)
    fs = 7.5
    from reportlab.pdfbase.pdfmetrics import stringWidth
    while fs > 5.6 and stringWidth("M" * maxlen, MONO, fs) > avail - 18:
        fs -= 0.2
    out = []
    n = len(lines)
    for i, line in enumerate(lines):
        out.append(CodeSpan(line, avail, fs, first=(i == 0), last=(i == n - 1),
                            width=avail))
    out.append(Spacer(1, 4))
    return out


# ------------------------------------------------------------------ parse
def parse_markdown(text, avail, styles, base_dir):
    story = []
    lines = text.split("\n")
    i = 0
    n = len(lines)
    pending_para = []

    def flush():
        if pending_para:
            story.append(Paragraph(inline(" ".join(pending_para)), styles["body"]))
            pending_para.clear()

    while i < n:
        line = lines[i]
        stripped = line.strip()

        if not stripped:
            flush()
            i += 1
            continue

        # szakasz / oldaltörés
        if stripped.startswith(":::") or stripped == "::" or stripped.startswith(":: "):
            flush()
            directive = stripped.lstrip(":").strip()
            if directive == "pagebreak":
                story.append(PageBreak())
            elif directive == "columnbreak":
                story.append(FrameBreak())
            elif directive.startswith("vazlat"):
                parts = directive.split(None, 2)
                hh = float(parts[1]) if len(parts) > 1 else 200.0
                hint = parts[2] if len(parts) > 2 else ""
                story.append(Spacer(1, 4))
                story.append(Vazlat(avail, hh, "Vázlat — ide rajzolhatsz", hint))
                story.append(Spacer(1, 8))
            elif directive.startswith("spacer"):
                story.append(Spacer(1, float(directive.split()[1])))
            elif directive.startswith("a3"):
                rest = directive[2:].strip()
                path, _, caption = rest.partition("|")
                path = os.path.join(base_dir, path.strip())
                # a közvetlenül előtte álló oldaltörés felesleges (üres oldalt okoz)
                while story and isinstance(story[-1], PageBreak):
                    story.pop()
                story.append(NextPageTemplate("A3"))
                story.append(PageBreak())
                story.append(Figure(path, width=1136, height=780))
                if caption.strip():
                    st = ParagraphStyle("cap_a3", parent=styles["caption"], fontSize=9.0,
                                        leading=12.0, spaceBefore=3, spaceAfter=0)
                    story.append(Paragraph(inline(caption.strip()), st))
                story.append(NextPageTemplate("A4"))
                story.append(PageBreak())
            i += 1
            continue

        # címsorok
        m = re.match(r"^(#{1,3})\s+(.*)$", stripped)
        if m:
            flush()
            level = len(m.group(1))
            txt = m.group(2)
            if level == 1:
                story.append(Rule(thickness=0.9, color=RULE, space=0))
                story.append(Paragraph(inline(txt), styles["h1"]))
            elif level == 2:
                story.append(Paragraph(inline(txt), styles["h2"]))
            else:
                story.append(Paragraph(inline(txt), styles["h3"]))
            i += 1
            continue

        # kódblokk
        if stripped.startswith("```"):
            flush()
            i += 1
            buf = []
            while i < n and not lines[i].strip().startswith("```"):
                buf.append(lines[i])
                i += 1
            i += 1
            story.extend(code_block("\n".join(buf), avail, styles))
            continue

        # idézet / doboz
        if stripped.startswith(">"):
            flush()
            buf = []
            while i < n and lines[i].strip().startswith(">"):
                buf.append(lines[i].strip()[1:].strip())
                i += 1
            title = None
            if buf and buf[0].startswith("[") and "]" in buf[0]:
                idx = buf[0].index("]")
                title = buf[0][1:idx]
                rest = buf[0][idx + 1:].strip()
                buf = ([rest] if rest else []) + buf[1:]
            paras = []
            chunk = []
            for b in buf:
                if not b:
                    if chunk:
                        paras.append(Paragraph(inline(" ".join(chunk)), styles["quote"]))
                        chunk = []
                else:
                    chunk.append(b)
            if chunk:
                paras.append(Paragraph(inline(" ".join(chunk)), styles["quote"]))
            story.append(Spacer(1, 2))
            story.append(callout(title, paras, avail, styles))
            story.append(Spacer(1, 8))
            continue

        # kép
        m = re.match(r"^!\[(.*?)\]\((.*?)\)$", stripped)
        if m:
            flush()
            caption, path = m.group(1), m.group(2)
            full = os.path.join(base_dir, path)
            story.append(Spacer(1, 2))
            story.append(Figure(full, width=avail))
            if caption:
                story.append(Paragraph(inline(caption), styles["caption"]))
            else:
                story.append(Spacer(1, 8))
            i += 1
            continue

        # táblázat
        if stripped.startswith("|"):
            flush()
            rows, aligns, widths = [], [], None
            hdr_align = None
            while i < n and lines[i].strip().startswith("|"):
                raw = lines[i].strip()
                if raw.startswith("|:"):  # szélesség-sor  |: 3,1,2|
                    spec = raw.strip("|").strip()
                    widths = [float(x) for x in re.findall(r"[\d.]+", spec)]
                    i += 1
                    continue
                cells = [c.strip() for c in raw.strip("|").split("|")]
                if all(re.fullmatch(r":?-{2,}:?", c) for c in cells):
                    i += 1
                    continue
                rows.append(cells)
                i += 1
            ncols = max(len(r) for r in rows)
            if widths and len(widths) != ncols:
                widths = None
            header = None
            if rows and all(not c for c in rows[0]):
                header = False
                rows = rows[1:]
            # igazítás: a fejléc alatti elválasztó sor :---: jelei alapján
            aligns = ["l"] * ncols
            for ci, r in enumerate(rows):
                pass
            story.append(build_table(rows, avail, styles, header=True, aligns=aligns,
                                     widths=widths))
            story.append(Spacer(1, 9))
            continue

        # felsorolás
        m = re.match(r"^[-–]\s+(.*)$", stripped)
        if m:
            flush()
            txt = m.group(1)
            story.append(Paragraph(inline(txt), styles["bullet"], bulletText="•"))
            i += 1
            continue

        m = re.match(r"^(\d+)[.)]\s+(.*)$", stripped)
        if m:
            flush()
            num, txt = m.group(1), m.group(2)
            story.append(Paragraph(inline(txt), styles["num"], bulletText=f"{num}."))
            i += 1
            continue

        pending_para.append(stripped)
        i += 1

    flush()
    return story


# ------------------------------------------------------------------ dokumentum
class Doc:
    def __init__(self, path, title, subtitle="", styles=None, footer_left="",
                 cover=True):
        self.path = path
        self.title = title
        self.subtitle = subtitle
        self.styles = styles or make_styles()
        self.footer_left = footer_left or title
        self.page_sizes = {}
        self.cover = cover

    # --- oldaldekorációk -------------------------------------------------
    def _footer(self, canvas, doc, size="A4"):
        canvas.saveState()
        w, h = canvas._pagesize
        canvas.setStrokeColor(RULE)
        canvas.setLineWidth(0.6)
        canvas.line(52, 42, w - 52, 42)
        canvas.setFillColor(FAINT)
        canvas.setFont(SANS, 7.2)
        canvas.drawString(52, 31, self.footer_left)
        canvas.drawRightString(w - 52, 31, f"{doc.page}")
        canvas.restoreState()

    def _on_a4(self, canvas, doc):
        self._footer(canvas, doc)

    def _on_a4_first(self, canvas, doc):
        pass

    def _on_a3(self, canvas, doc):
        canvas.saveState()
        w, h = canvas._pagesize
        canvas.setFillColor(GREEN)
        canvas.rect(0, h - 3, w, 3, stroke=0, fill=1)
        canvas.setFillColor(FAINT)
        canvas.setFont(SANS, 7.4)
        canvas.drawString(28, 16, self.footer_left)
        canvas.drawRightString(w - 28, 16, f"{doc.page}")
        canvas.restoreState()

    def build(self, story):
        a4_w, a4_h = _A4
        ml, mr, mt, mb = 58, 58, 56, 58
        a4_frame = Frame(ml, mb + 6, a4_w - ml - mr, a4_h - mt - mb - 6, id="main",
                         leftPadding=0, rightPadding=0, topPadding=0, bottomPadding=0)
        a4_first = Frame(ml, mb + 6, a4_w - ml - mr, a4_h - mt - mb - 6, id="first")
        a3_w, a3_h = _A3L
        a3_frame = Frame(26, 24, a3_w - 52, a3_h - 38, id="a3",
                         leftPadding=0, rightPadding=0, topPadding=0, bottomPadding=0)

        doc = BaseDocTemplate(self.path, pagesize=_A4,
                              leftMargin=ml, rightMargin=mr, topMargin=mt,
                              bottomMargin=mb, title=self.title,
                              author="Adatbázis-tervezés tananyag",
                              subject=self.subtitle or self.title)
        doc.addPageTemplates([
            PageTemplate(id="A4", frames=[a4_frame], onPage=self._on_a4),
            PageTemplate(id="A3", frames=[a3_frame], onPage=self._on_a3,
                         pagesize=_A3L),
        ])
        doc.build(story)
        return doc


def make_pdf(path, story, title, footer_left=""):
    d = Doc(path, title, footer_left=footer_left)
    return d.build(story)
