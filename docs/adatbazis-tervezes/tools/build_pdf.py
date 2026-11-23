# -*- coding: utf-8 -*-
"""A feladatlap és a megoldókulcs PDF-jeinek legyártása.

Használat:  python3 tools/build_pdf.py
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, ".."))
sys.path.insert(0, HERE)

import docbuild as DB
from reportlab.lib.units import mm
from reportlab.platypus import (KeepTogether, PageBreak, Paragraph, Spacer, Table,
                                TableStyle)

SRC = os.path.join(ROOT, "src")
OUT = ROOT

DOC_META = {
    "feladat": dict(
        title="Adatbázis-tervezési feladat\nBringaPont kerékpár-kölcsönző",
        out="feladatlap.pdf",
        footer="Adatbázis-tervezés — BringaPont kerékpár-kölcsönző",
    ),
    "megoldas": dict(
        title="Megoldókulcs\nBringaPont kerékpár-kölcsönző adatbázis-tervezése",
        out="megoldokulcs.pdf",
        footer="Megoldókulcs — BringaPont kerékpár-kölcsönző",
    ),
}


def cover(meta, styles, is_solution=False):
    story = []
    title = meta["title"]
    story.append(Spacer(1, 92))
    story.append(DB.Rule(thickness=2.2, color=DB.GREEN, space=0))
    story.append(Spacer(1, 16))
    for i, line in enumerate(title.split("\n")):
        story.append(Paragraph(line, styles["cover_t"] if i == 0 else styles["cover_s"]))
    story.append(Spacer(1, 26))
    story.append(DB.Rule(thickness=0.8, color=DB.RULE, space=0))
    story.append(Spacer(1, 22))
    if is_solution:
        body = [
            ("Tantárgy", "Adatbázis-kezelés / adatbázis-tervezés"),
            ("Téma", "relációs adatbázis tervezése — E-R modell, normalizálás, logikai séma"),
            ("Célcsoport", "közép- és emelt szintű informatikaoktatás, 11–13. évfolyam"),
            ("Tartalom", "javítási útmutató, pontozási javaslat, teljes SQL-séma"),
            ("Pontszám", "75 pont"),
        ]
    else:
        body = [
            ("Tantárgy", "Adatbázis-kezelés / adatbázis-tervezés"),
            ("Téma", "relációs adatbázis tervezése — E-R modell, normalizálás, logikai séma"),
            ("Célcsoport", "közép- és emelt szintű informatikaoktatás, 11–13. évfolyam"),
            ("Időigény", "90–120 perc"),
            ("Pontszám", "75 pont"),
        ]
    meta_tbl = Table(
        [[Paragraph(f'<font color="#566661">{k}</font>', styles["cell"]),
          Paragraph(v, styles["cell"])] for k, v in body],
        colWidths=[78, 346], hAlign="LEFT")
    meta_tbl.setStyle(TableStyle([
        ("VALIGN", (0, 0), (-1, -1), "TOP"),
        ("TOPPADDING", (0, 0), (-1, -1), 2.6),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 2.6),
        ("LEFTPADDING", (0, 0), (-1, -1), 0),
    ]))
    story.append(meta_tbl)
    story.append(Spacer(1, 24))
    intro = ("Ez a dokumentum a feladatlap **megoldásait** tartalmazza: az E-R diagramot, a "
             "normalizálás lépéseit, a relációs adatbázis-diagramot, a kényszerek magyarázatát és "
             "a teljes SQL-sémát.") if is_solution else (
             "A BringaPont Kft. kerékpár-kölcsönző adatbázisát kell megtervezni: a kiinduló "
             "attribútumokból E-R diagramot rajzolni, a lapos nyilvántartást normalizálni, majd "
             "a 3NF-es sémából relációs adatbázis-diagramot készíteni.")
    story.append(DB.callout(
        "A megoldókulcsról" if is_solution else "A feladatról",
        [Paragraph(intro, styles["quote"])],
        440, styles))
    story.append(Spacer(1, 18))
    rows = [
        ["1. feladat", "Kiinduló attribútumok, gyenge entitás, E-R diagram", "30 pont"],
        ["2. feladat", "Normalizálás 1NF → 2NF → 3NF → BCNF", "25 pont"],
        ["3. feladat", "Relációs adatbázis-diagram és integritási kényszerek", "20 pont"],
    ]
    t = Table([[Paragraph(f"<b>{a}</b>", styles["cell"]),
                Paragraph(b, styles["cell"]),
                Paragraph(f"<b>{c}</b>", styles["cell"])] for a, b, c in rows],
              colWidths=[62, 300, 62], hAlign="LEFT")
    t.setStyle(TableStyle([
        ("LINEBELOW", (0, 0), (-1, -2), 0.4, DB.RULE),
        ("LINEABOVE", (0, 0), (-1, 0), 0.8, DB.GREEN),
        ("LINEBELOW", (0, -1), (-1, -1), 0.8, DB.GREEN),
        ("TOPPADDING", (0, 0), (-1, -1), 4),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
        ("LEFTPADDING", (0, 0), (-1, -1), 0),
    ]))
    story.append(t)
    if not is_solution:
        story.append(Spacer(1, 22))
        story.append(Paragraph(
            "<b>Munkaeszközök:</b> ceruza és radír, vonalzó, A3-as lap (E-R diagram és adatbázis-"
            "diagram), illetve igény szerint diagramkészítő szoftver.", styles["small"]))
    story.append(PageBreak())
    return story


def build(key):
    DB.register_fonts()
    meta = DOC_META[key]
    styles = DB.make_styles()
    src = open(os.path.join(SRC, key + ".md"), encoding="utf-8").read()

    story = cover(meta, styles, is_solution=(key == "megoldas"))
    if key == "megoldas":
        import docbuild as _d
        # a megoldókulcs borítójára a tartalomjegyzék-szerű áttekintés kerül
        pass

    # Az első oldal a borító; az 1. fejezet új oldalon kezdődik.
    avail = 595.28 - 2 * 58
    story += DB.parse_markdown(src, avail, styles, ROOT)

    out = os.path.join(OUT, meta["out"])
    d = DB.Doc(out, meta["title"].replace("\n", " "), footer_left=meta["footer"])
    d.build(story)
    print("kész:", out)


if __name__ == "__main__":
    for k in ("feladat", "megoldas"):
        build(k)
