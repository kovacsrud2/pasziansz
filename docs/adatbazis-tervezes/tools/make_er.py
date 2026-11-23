# -*- coding: utf-8 -*-
"""A BringaPont kerékpár-kölcsönző E-R diagramja (Chen-jelölés).

Kimenet: img/er-megoldas.{png,svg,pdf}  (A3 fekvő, 1191 x 900 pt)
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import dbdraw as D

W, H = 1191.0, 900.0
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "img")
DPI = int(os.environ.get("DPI", 200))


def build():
    fig, ax = D.new_canvas(W, H)

    # ------------------------------------------------------- fejléc, lábléc
    ax.plot([30, W - 30], [845, 845], color="#D5E0DC", lw=0.8, zorder=1)
    D.label(ax, 30, 874, "BringaPont kerékpár-kölcsönző — E-R diagram", size=15,
            weight=700, color=D.C_HEAD, ha="left")
    D.label(ax, 30, 856, "Chen-jelölés • (min,max) részvételi korlát • dupla vonal = teljes részvétel",
            size=7.6, color=D.MUTED, ha="left")
    D.label(ax, W - 30, 874, "Adatbázis-tervezés • mintamegoldás • 1. feladat", size=8.6,
            color=D.FAINT, ha="right")
    D.label(ax, W - 30, 856, "BringaPont Kft. — 6 kölcsönzőpont, 180 kerékpár", size=7.6,
            color=D.FAINT, ha="right")

    # ------------------------------------------------------------ entitások
    ugyfel = D.er_entity(ax, 150, 640, "ÜGYFÉL")
    berles = D.er_entity(ax, 565, 640, "BÉRLÉS")
    szamla = D.er_entity(ax, 905, 640, "SZÁMLA")
    pont = D.er_entity(ax, 150, 400, "PONT")
    bringa = D.er_entity(ax, 700, 330, "BRINGA")
    tipus = D.er_entity(ax, 420, 175, "BRINGATÍPUS")
    szerviz = D.er_entity(ax, 1010, 168, "SZERVIZ", weak=True)

    # -------------------------------------------------------- kapcsolatok
    r_berel = D.er_relation(ax, 355, 640, "BÉREL")
    r_atvesz = D.er_relation(ax, 312, 540, "ÁTVESZ")
    r_lead = D.er_relation(ax, 402, 462, "LEAD")
    r_tartalmaz = D.er_relation(ax, 650, 480, "TARTALMAZ")
    r_helyez = D.er_relation(ax, 470, 322, "ELHELYEZ")
    r_tartozik = D.er_relation(ax, 560, 240, "TARTOZIK")
    r_kiallit = D.er_relation(ax, 735, 640, "KIÁLLÍT")
    r_szervizel = D.er_relation(ax, 862, 255, "SZERVIZEL", identifying=True)

    # -------------------------------------------------------------- élek
    # ÜGYFÉL (0,N) — BÉREL — (1,1) BÉRLÉS
    D.connect(ax, ugyfel, r_berel)
    D.connect(ax, r_berel, berles)
    D.card_text(ax, ugyfel, r_berel, "(0,N)", along=0.50, dist=-9.5)
    D.card_text(ax, berles, r_berel, "(1,1)", along=0.50, dist=-9.5)

    # PONT (0,N) — ÁTVESZ — (1,1) BÉRLÉS   [átvételi pont]
    D.connect(ax, pont, r_atvesz)
    D.connect(ax, r_atvesz, berles, total_b=True)
    D.card_text(ax, pont, r_atvesz, "(0,N)", along=0.72, side=-1, dist=8.6)
    D.card_text(ax, berles, r_atvesz, "(1,1)", along=0.62, side=-1, dist=9.0)

    # PONT (0,N) — LEAD — (1,1) BÉRLÉS       [leadási pont]
    D.connect(ax, pont, r_lead)
    D.connect(ax, r_lead, berles, total_b=True)
    D.card_text(ax, pont, r_lead, "(0,N)", along=0.74, side=-1, dist=8.6)
    D.card_text(ax, berles, r_lead, "(1,1)", along=0.66, side=1, dist=9.0)

    # PONT (0,N) — ELHELYEZ — (0,1) BRINGA
    D.connect(ax, pont, r_helyez)
    D.connect(ax, r_helyez, bringa)
    D.card_text(ax, pont, r_helyez, "(0,N)", along=0.80, side=1, dist=8.6)
    D.card_text(ax, bringa, r_helyez, "(0,1)", along=0.45, side=1, dist=8.6)

    # BÉRLÉS (1,N) — TARTALMAZ — (0,N) BRINGA  (M:N)
    D.connect(ax, berles, r_tartalmaz, total_a=True)
    D.connect(ax, r_tartalmaz, bringa)
    D.card_text(ax, berles, r_tartalmaz, "(1,N)", along=0.55, side=-1, dist=9.0)
    D.card_text(ax, bringa, r_tartalmaz, "(0,N)", along=0.62, side=-1, dist=9.0)

    # BRINGA (1,1) — TARTOZIK — (0,N) BRINGATÍPUS
    D.connect(ax, bringa, r_tartozik, total_a=True)
    D.connect(ax, r_tartozik, tipus)
    D.card_text(ax, bringa, r_tartozik, "(1,1)", along=0.56, side=-1, dist=9.0)
    D.card_text(ax, tipus, r_tartozik, "(0,N)", along=0.50, side=-1, dist=9.0)

    # BÉRLÉS (0,1) — KIÁLLÍT — (1,1) SZÁMLA
    D.connect(ax, berles, r_kiallit, total_a=True)
    D.connect(ax, r_kiallit, szamla)
    D.card_text(ax, berles, r_kiallit, "(0,1)", along=0.50, dist=-9.5)
    D.card_text(ax, szamla, r_kiallit, "(1,1)", along=0.50, dist=-9.5)

    # BRINGA (0,N) — SZERVIZEL — (1,1) SZERVIZ (gyenge entitás)
    D.connect(ax, bringa, r_szervizel)
    D.connect(ax, r_szervizel, szerviz, total_b=True)
    D.card_text(ax, bringa, r_szervizel, "(0,N)", along=0.40, side=1, dist=9.0)
    D.card_text(ax, szerviz, r_szervizel, "(1,1)", along=0.62, side=1, dist=9.0)

    # szerepnevek a vonalakon
    pa = D.shape_edge(pont, r_atvesz["cx"], r_atvesz["cy"])
    qa = D.shape_edge(r_atvesz, pont["cx"], pont["cy"])
    D.along_text(ax, pa, qa, 0.42, "átvételi pont", side=1, dist=8.4, size=6.8)
    pl = D.shape_edge(pont, r_lead["cx"], r_lead["cy"])
    ql = D.shape_edge(r_lead, pont["cx"], pont["cy"])
    D.along_text(ax, pl, ql, 0.44, "leadási pont", side=-1, dist=8.4, size=6.8)

    # ---------------------------------------------------------- attribútumok
    def att(x, y, name, ent, **kw):
        a = D.er_attr(ax, x, y, name, **kw)
        D.connect(ax, ent, a, lw=0.8)
        return a

    # ÜGYFÉL
    att(46, 700, "ügyfél_id", ugyfel, key=True)
    att(126, 762, "név", ugyfel)
    att(228, 800, "e-mail", ugyfel)
    att(300, 742, "telefonszám", ugyfel, multi=True)
    att(152, 826, "születési_dátum", ugyfel)
    att(44, 786, "életkor", ugyfel, derived=True)

    # BÉRLÉS
    att(470, 700, "bérlés_id", berles, key=True)
    att(556, 742, "kezdet", berles)
    att(650, 736, "vége", berles)
    att(504, 800, "státusz", berles)
    att(618, 806, "összeg", berles, derived=True)

    # SZÁMLA
    att(846, 706, "számla_id", szamla, key=True)
    att(966, 738, "kiállítás_dátuma", szamla)
    att(872, 800, "összeg", szamla)
    att(986, 790, "fizetve", szamla)

    # PONT
    att(56, 468, "pont_id", pont, key=True)
    att(40, 400, "név", pont)
    att(66, 334, "kapacitás", pont)
    cim = att(74, 268, "cím", pont)
    for cx, cy, cn in [(50, 196, "irányítószám"), (126, 178, "város"),
                       (198, 192, "utca"), (266, 220, "házszám")]:
        comp = D.er_attr(ax, cx, cy, cn, size=6.7)
        D.connect(ax, cim, comp, lw=0.8)

    # BRINGA
    att(806, 404, "bringa_id", bringa, key=True)
    att(918, 438, "gyári_szám", bringa)
    att(1002, 412, "méret", bringa)
    att(1074, 452, "állapot", bringa)
    att(1102, 356, "beszerzés_dátuma", bringa)

    # BRINGATÍPUS
    att(330, 110, "típus_id", tipus, key=True)
    att(456, 106, "megnevezés", tipus)
    att(396, 52, "napi_díj", tipus)

    # SZERVIZ (gyenge entitás)
    att(1108, 88, "sorszám", szerviz, key=True, partial=True)
    att(1122, 176, "dátum", szerviz)
    att(1042, 246, "leírás", szerviz)
    att(952, 78, "költség", szerviz)

    # --------------------------------------------------------------- legenda
    ax.add_patch(D.plt.Rectangle((30, 12), W - 60, 25, facecolor="#F4F8F7",
                                 edgecolor="#DCE7E3", lw=0.7, zorder=0))
    D.label(ax, 42, 24,
            "téglalap = entitás   •   dupla téglalap = gyenge entitás   •   rombusz = kapcsolat   "
            "•   dupla rombusz = azonosító kapcsolat   •   ellipszis = attribútum",
            size=6.6, color=D.MUTED, ha="left")
    D.label(ax, W - 42, 24,
            "aláhúzott = kulcs   •   szaggatott aláhúzás = részleges kulcs   •   dupla ellipszis = többértékű   "
            "•   szaggatott ellipszis = számított",
            size=6.6, color=D.MUTED, ha="right")

    D.save(fig, os.path.join(OUT, "er-megoldas"), dpi=DPI)


if __name__ == "__main__":
    build()
    print("ok")
