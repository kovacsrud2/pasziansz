# -*- coding: utf-8 -*-
"""A BringaPont relációs adatbázis-diagramja (varjúláb / crow's foot jelölés).

Kimenet: img/db-megoldas.{png,svg}  (A3 fekvő, 1191 x 900 pt)
"""
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import dbdraw as D

W, H = 1191.0, 900.0
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "img")
DPI = int(os.environ.get("DPI", 200))
LW = 1.0


def build():
    fig, ax = D.new_canvas(W, H)

    # ------------------------------------------------------ fejléc, lábléc
    ax.plot([30, W - 30], [845, 845], color="#D5E0DC", lw=0.8, zorder=1)
    D.label(ax, 30, 874, "BringaPont kerékpár-kölcsönző — relációs adatbázis-diagram", size=14.5,
            weight=700, color=D.C_HEAD, ha="left")
    D.label(ax, 30, 856, "3NF / BCNF • varjúláb (crow's foot) jelölés • félkövér aláhúzott = PK • dőlt = FK",
            size=7.6, color=D.MUTED, ha="left")
    D.label(ax, W - 30, 874, "Adatbázis-tervezés • mintamegoldás • 3. feladat", size=8.6,
            color=D.FAINT, ha="right")
    D.label(ax, W - 30, 856, "11 tábla • 10 idegen kulcs", size=7.6, color=D.FAINT, ha="right")

    # --------------------------------------------------------------- táblák
    A, B, C, E = 35, 283, 545, 800
    ugyfel = D.db_table(ax, A, 810, "UGYFEL", [
        ("ugyfel_id", "INTEGER", "PK"),
        ("nev", "VARCHAR(120)", ""),
        ("email", "VARCHAR(160)", ""),
        ("szuletesi_datum", "DATE", ""),
    ], width=195)
    telefon = D.db_table(ax, A, 690, "UGYFEL_TELEFON", [
        ("ugyfel_id", "INTEGER", "PFK"),
        ("telefonszam", "VARCHAR(24)", "PK"),
    ], width=195)
    pont = D.db_table(ax, A, 512, "PONT", [
        ("pont_id", "INTEGER", "PK"),
        ("nev", "VARCHAR(80)", ""),
        ("iranyitoszam", "CHAR(4)", ""),
        ("varos", "VARCHAR(60)", ""),
        ("utca", "VARCHAR(80)", ""),
        ("hazszam", "VARCHAR(10)", ""),
        ("kapacitas", "SMALLINT", ""),
    ], width=195)

    berles = D.db_table(ax, B, 810, "BERLES", [
        ("berles_id", "INTEGER", "PK"),
        ("ugyfel_id", "INTEGER", "FK"),
        ("atvetel_pont_id", "INTEGER", "FK"),
        ("leadasi_pont_id", "INTEGER", "FK"),
        ("kezdet", "TIMESTAMP", ""),
        ("vege", "TIMESTAMP", ""),
        ("statusz", "VARCHAR(10)", ""),
    ], width=205)
    szamla = D.db_table(ax, B, 585, "SZAMLA", [
        ("szamla_id", "INTEGER", "PK"),
        ("berles_id", "INTEGER", "FK"),
        ("kiallitas_datuma", "DATE", ""),
        ("osszeg", "NUMERIC(10,2)", ""),
        ("fizetve", "BOOLEAN", ""),
    ], width=205)

    berles_bringa = D.db_table(ax, C, 810, "BERLES_BRINGA", [
        ("berles_id", "INTEGER", "PFK"),
        ("bringa_id", "INTEGER", "PFK"),
    ], width=195)
    szerviz = D.db_table(ax, C, 700, "SZERVIZ", [
        ("bringa_id", "INTEGER", "PFK"),
        ("sorszam", "SMALLINT", "PK"),
        ("datum", "DATE", ""),
        ("leiras", "VARCHAR(255)", ""),
        ("koltseg", "NUMERIC(9,2)", ""),
    ], width=195)
    tipus = D.db_table(ax, C, 560, "BRINGATIPUS", [
        ("tipus_id", "INTEGER", "PK"),
        ("megnevezes", "VARCHAR(40)", ""),
        ("napi_dij", "NUMERIC(8,2)", ""),
    ], width=195)

    bringa = D.db_table(ax, E, 800, "BRINGA", [
        ("bringa_id", "INTEGER", "PK"),
        ("gyari_szam", "VARCHAR(30)", ""),
        ("tipus_id", "INTEGER", "FK"),
        ("meret", "VARCHAR(4)", ""),
        ("allapot", "VARCHAR(12)", ""),
        ("beszerzes_datum", "DATE", ""),
        ("pont_id", "INTEGER", "FK"),
    ], width=195)
    ebringa = D.db_table(ax, E, 645, "EBRINGA", [
        ("bringa_id", "INTEGER", "PFK"),
        ("akkumulator_wh", "SMALLINT", ""),
        ("hatotav_km", "SMALLINT", ""),
    ], width=195)
    tandem = D.db_table(ax, E, 500, "TANDEM", [
        ("bringa_id", "INTEGER", "PFK"),
        ("masodik_ulesmagassag", "SMALLINT", ""),
    ], width=195)

    # --------------------------------------------------------- jelölő elemek
    def _unit(a, b):
        dx, dy = b[0] - a[0], b[1] - a[1]
        n = math.hypot(dx, dy) or 1.0
        return dx / n, dy / n

    def tick(p, toward, dist=8.0, lw=LW):
        ux, uy = _unit(p, toward)
        bx, by = p[0] + ux * dist, p[1] + uy * dist
        D.line(ax, (bx - uy * 3.6, by + ux * 3.6), (bx + uy * 3.6, by - ux * 3.6),
               color=D.LINE, lw=lw * 0.95, zorder=1.9)

    def crow(p, toward, lw=LW):
        ux, uy = _unit(toward, p)
        bx, by = p[0] - ux * 7.4, p[1] - uy * 7.4
        for ang in (-0.62, 0.0, 0.62):
            ca, sa = math.cos(ang), math.sin(ang)
            rx = -(ux * ca - uy * sa)
            ry = -(ux * sa + uy * ca)
            D.line(ax, (bx, by), (bx - rx * 8.0, by - ry * 8.0),
                   color=D.LINE, lw=lw * 0.95, zorder=1.9)

    def route(pts, many="end", dashed=False, lw=LW, tick_a=True, tick_b=True):
        ls = (0, (3.4, 2.2)) if dashed else "-"
        D.elbow(ax, pts, color=D.LINE, lw=lw, zorder=1.5, ls=ls)
        if many == "end":
            crow(pts[-1], pts[-2], lw=lw)
            if tick_b:
                tick(pts[-1], pts[-2], lw=lw)
            if tick_a:
                tick(pts[0], pts[1], lw=lw)
        elif many == "start":
            crow(pts[0], pts[1], lw=lw)
            if tick_a:
                tick(pts[0], pts[1], lw=lw)
            if tick_b:
                tick(pts[-1], pts[-2], lw=lw)
        else:
            tick(pts[0], pts[1], lw=lw)
            tick(pts[-1], pts[-2], lw=lw)

    def note(x, y, text, size=5.8, ha="center"):
        D.label(ax, x, y, text, size=size, color=D.MUTED, ha=ha, zorder=7,
                bbox=dict(boxstyle="round,pad=0.16", fc="white", ec="none", alpha=0.96))

    def edge(t, side, y=None, frac=None):
        if side in ("l", "r"):
            x = t["l"] if side == "l" else t["r"]
            return (x, y if y is not None else t["top"] - t["h"] * frac)
        yy = t["top"] if side == "t" else t["bottom"]
        return (t["l"] + t["w"] * (frac if frac is not None else 0.5), yy)

    # --------------------------------------------------------- kapcsolatok
    # UGYFEL 1 : N UGYFEL_TELEFON
    route([edge(ugyfel, "b", frac=0.40), edge(telefon, "t", frac=0.40)])
    note(150, 739, "1 : N")

    # UGYFEL 1 : N BERLES
    route([edge(ugyfel, "r", y=785), edge(berles, "l", y=785)])
    note(257, 793, "1 : N", size=5.6)

    # BERLES 1 : 1 SZAMLA
    route([edge(berles, "b", frac=0.50), edge(szamla, "t", frac=0.50)])
    note(392, 648, "1 : 1")

    # BERLES 1 : N BERLES_BRINGA
    route([edge(berles, "r", y=785), edge(berles_bringa, "l", y=785)])
    note(518, 793, "1 : N", size=5.6)

    # BERLES_BRINGA 1 : N BRINGA
    route([edge(berles_bringa, "r", y=785), edge(bringa, "l", y=785)])
    note(774, 793, "1 : N", size=5.6)

    # PONT 1 : N BERLES  (átvételi pont) — csatorna x = 264
    route([edge(pont, "r", y=482), (264, 482), (264, 757), edge(berles, "l", y=757)])
    note(276, 505, "1 : N", size=5.6, ha="left")

    # PONT 1 : N BERLES  (leadási pont) — csatorna x = 246
    route([edge(pont, "r", y=456), (246, 456), (246, 726), edge(berles, "l", y=726)])
    note(258, 479, "1 : N", size=5.6, ha="left")

    # PONT 1 : N BRINGA  (aktuális hely) — hosszú él a diagram alján
    route([edge(pont, "b", frac=0.52), (136, 386), (1064, 386), (1064, 795),
           edge(bringa, "r", y=785)])
    note(600, 396, "1 : N   a kerékpár aktuális helye (bérlés közben NULL)", size=6.2)

    # BRINGA 1 : N SZERVIZ — csatorna x = 760
    route([edge(bringa, "l", y=755), (760, 755), (760, 700), edge(szerviz, "r", y=700)])
    note(770, 745, "1 : N", size=5.6, ha="left")

    # BRINGA 1 : N BRINGATIPUS — csatorna x = 780
    route([edge(bringa, "l", y=724), (780, 724), (780, 530), edge(tipus, "r", y=530)])
    note(790, 714, "1 : N", size=5.6, ha="left")

    # BRINGA 1 : 1 EBRINGA  (alosztály)
    route([edge(bringa, "b", frac=0.28), edge(ebringa, "t", frac=0.28)],
          many=None, dashed=True, lw=LW * 0.9)
    note(852, 674, "1 : 1", size=5.6)

    # BRINGA 1 : 1 TANDEM  (alosztály)
    route([edge(bringa, "r", y=735), (1042, 735), (1042, 478), edge(tandem, "r", y=478)],
          many=None, dashed=True, lw=LW * 0.9)
    note(1052, 508, "1 : 1", size=5.6, ha="left")

    # ------------------------------------------------------- olvasási útmutató
    bx, by, bw, bh = 35, 42, 1095, 330
    ax.add_patch(D.plt.Rectangle((bx, by), bw, bh, facecolor="#F7FAF9",
                                 edgecolor="#DCE7E3", lw=0.8, zorder=0))
    D.label(ax, bx + 18, by + bh - 24, "Hogyan olvasd a diagramot", size=9.8,
            weight=700, color=D.C_HEAD, ha="left")
    D.label(ax, bx + 18, by + bh - 41,
            "A diagram a 2. feladat 3NF/BCNF felbontását ábrázolja. A vonalak iránya nem hordoz jelentést; "
            "a számosságot a vonalvégi jelek adják.",
            size=6.9, color=D.FAINT, ha="left")

    tips_a = [("varjúláb (háromágú villa)", "a „sok” oldalt jelöli"),
              ("vonás a vonal végén", "az „egy” oldalt jelöli"),
              ("dőlt mezőnév", "idegen kulcs (FK)"),
              ("PFK", "elsődleges és idegen kulcs egyszerre")]
    tips_b = [("BERLES_BRINGA", "az M:N kapcsolat feloldása"),
              ("SZERVIZ", "gyenge entitás"),
              ("SZAMLA.berles_id UNIQUE", "ettől 1:1 a bérlés–számla kapcsolat"),
              ("BRINGA.pont_id NULL", "bérlés közben nincs ponton")]
    for x0, tips in [(bx + 18, tips_a), (bx + 350, tips_b)]:
        y = by + bh - 72
        for a, b in tips:
            D.label(ax, x0, y, "•", size=7.6, color=D.C_ENTITY_EDGE, ha="left")
            D.label(ax, x0 + 11, y, a, size=7.1, weight=600, color=D.INK, ha="left")
            D.label(ax, x0 + 11, y - 12, b, size=6.9, color=D.MUTED, ha="left")
            y -= 40

    # kulcsösszesítő (harmadik hasáb)
    kx = bx + 700
    D.line(ax, (kx - 18, by + 22), (kx - 18, by + bh - 58), color="#DCE7E3", lw=0.7, zorder=1)
    D.label(ax, kx, by + bh - 72, "KULCSOK", size=7.4, weight=700, color=D.C_HEAD, ha="left")
    keys = [("UGYFEL", "ugyfel_id"), ("UGYFEL_TELEFON", "ugyfel_id + telefonszam"),
            ("PONT", "pont_id"), ("BERLES", "berles_id"),
            ("SZAMLA", "szamla_id"), ("BERLES_BRINGA", "berles_id + bringa_id"),
            ("BRINGA", "bringa_id"), ("BRINGATIPUS", "tipus_id"),
            ("EBRINGA", "bringa_id"), ("TANDEM", "bringa_id"),
            ("SZERVIZ", "bringa_id + sorszam")]
    per_col = 6
    for i, (t, k) in enumerate(keys):
        cx = kx + (i // per_col) * 175
        cy = by + bh - 92 - (i % per_col) * 24
        D.label(ax, cx, cy, t, size=6.5, weight=600, color=D.INK, ha="left")
        D.label(ax, cx, cy - 8.4, "PK: " + k, size=6.1, color=D.MUTED, ha="left")

    # --------------------------------------------------------------- legenda
    ax.add_patch(D.plt.Rectangle((30, 12), W - 60, 26, facecolor="#F4F8F7",
                                 edgecolor="#DCE7E3", lw=0.7, zorder=0))
    D.label(ax, 42, 28,
            "PK = elsődleges kulcs   •   PFK = elsődleges és idegen kulcs   •   FK = idegen kulcs   •   "
            "félkövér aláhúzott = PK   •   dőlt = FK",
            size=6.6, color=D.MUTED, ha="left")
    D.label(ax, 42, 17,
            "varjúláb = a „sok” oldal   •   vonás = az „egy” oldal   •   "
            "szaggatott vonal = alosztály-kapcsolat (EBRINGA / TANDEM)",
            size=6.6, color=D.MUTED, ha="left")

    D.save(fig, os.path.join(OUT, "db-megoldas"), dpi=DPI)


if __name__ == "__main__":
    build()
    print("ok")
