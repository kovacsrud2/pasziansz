# Adatbázis-tervezési feladat — BringaPont kerékpár-kölcsönző

Komplett, kinyomtatható feladatcsomag egy kerékpár-kölcsönző szolgáltatás adatbázisának
megtervezéséhez. A feladat az **E-R diagram rajzolását**, a **normalizálást (1NF → 2NF → 3NF → BCNF)**
és a **relációs adatbázis-diagram** elkészítését kéri számon, megoldókulccsal és javítási
útmutatóval együtt.

## Fájlok

| fájl | tartalom |
| --- | --- |
| **`feladatlap.pdf`** | A kiadható feladatsor (9 oldal): háttér, szabályok, kiinduló attribútumlista, 3 feladat rajzfelületekkel. |
| **`megoldokulcs.pdf`** | Tanári megoldókulcs (20 oldal): attribútum-osztályozás, E-R diagram, normalizálási lépések, adatbázis-diagram, kényszerek, SQL, pontozás. |
| `schema.sql` | A teljes PostgreSQL-séma mintaadatokkal, nézetekkel és átfedésvizsgáló triggerrel. |
| `img/er-megoldas.svg` / `.png` | Az E-R diagram (A3, Chen-jelölés), önálló képként is. |
| `img/db-megoldas.svg` / `.png` | A relációs adatbázis-diagram (A3, varjúláb jelölés). |
| `src/*.md` | A dokumentumok Markdown-forrása. |
| `tools/*.py` | Az ábrák és a PDF-ek előállítószkriptjei. |
| `fonts/*.ttf` | IBM Plex Sans / Plex Mono (SIL OFL), a beágyazott betűtípusokhoz. |

## A feladat felépítése

| feladat | téma | pont |
| --- | --- | --- |
| 1. | Kiinduló attribútumlista osztályozása, gyenge entitás, specializáció, **E-R diagram** | 30 |
| 2. | **Normalizálás**: 1NF, 2NF, 3NF, BCNF és a tárolás/számítás gyakorlati döntése | 25 |
| 3. | **Adatbázis diagram** (logikai séma) és integritási kényszerek | 20 |
| | **összesen** | **75** |

A tanári anyagban külön melléklet mutatja a normalizált táblákat, az adattípusokat és a
teljes SQL-sémát.

## A mintaadatbázis

A `schema.sql` a következő 11 táblát hozza létre:

```
UGYFEL, UGYFEL_TELEFON, PONT, BRINGATIPUS, BRINGA, EBRINGA, TANDEM,
BERLES, BERLES_BRINGA, SZAMLA, SZERVIZ
```

Főbb tervezési döntések, amelyeket a feladat megbeszélésén érdemes kiemelni:

- **Gyenge entitás:** a `SZERVIZ` kulcsa `(bringa_id, sorszam)` — a sorszám csak a kerékpáron
  belül egyedi.
- **M:N kapcsolat:** a `BERLES_BRINGA` összekötő tábla oldja fel, összetett kulccsal.
- **1:1 kapcsolat:** a `SZAMLA.berles_id` `UNIQUE` kényszere biztosítja.
- **Specializáció:** az `EBRINGA` és a `TANDEM` alosztály-tábla, kulcsa egyszerre idegen kulcs.
- **Számított adatok:** az életkor és a bérlés végösszege nézetből számolódik; a számla
  összege viszont **tárolt** érték, mert bizonylat.
- **Időbeli átfedés:** a `schema.sql` triggerrel zárja ki, hogy egy kerékpár két nyitott
  bérlésben szerepeljen.

## Újragenerálás

```bash
cd docs/adatbazis-tervezes

# ábrák (A3, PNG + SVG)
python3 tools/make_er.py
python3 tools/make_db.py

# PDF-ek a Markdown-forrásból
pip install reportlab svglib lxml
python3 tools/build_pdf.py
```

A PDF-ek a diagramokat **vektorosan** tartalmazzák (SVG → ReportLab), így nyomtatásban is
élesek, és az A3-as ábraoldalak külön oldalméretet kapnak.

## Az SQL kipróbálása

A séma PostgreSQL-hez készült (identitás-oszlopok, `EXCLUDE`, `plpgsql`). A kulcs- és
`CHECK`-kényszerek SQLite-ban is működnek a `schema.sql` megfelelő átiratával.

```bash
psql -d bringapont -f schema.sql
```
