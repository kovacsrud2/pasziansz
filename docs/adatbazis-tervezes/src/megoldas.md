# Megoldókulcs — BringaPont kerékpár-kölcsönző adatbázis-tervezése

> [A megoldókulcs használatáról] Ez a dokumentum a **tanáré**. A feladatlap önálló megoldásra készült; a pontozási javaslatok tájékoztató jellegűek, és a helyes, indokolt alternatív megoldásokat is el kell fogadni. Ahol a feladatban tudatosan több jó válasz van (például a számlaösszeg tárolása), ott a **döntés indoklását** pontozzuk, nem magát a döntést.

::: pagebreak

## 1. feladat — Kiinduló attribútumok és E-R diagram

### 1.1. Az attribútumok osztályozása (6 pont)

| # | Attribútum | Fajta | Hova tartozik | Tároljuk? |
|: 5,17,15,20,43|
| 1 | `ugyfel_id` | egyszerű, kulcs | ÜGYFÉL | igen |
| 2 | `ugyfel_nev` | egyszerű | ÜGYFÉL | igen |
| 3 | `ugyfel_cim` | **összetett** | ÜGYFÉL | nem így, részekre bontva |
| 4 | `ugyfel_telefon` | **többértékű** | ÜGYFÉL | külön táblába (UGYFEL_TELEFON) |
| 5 | `ugyfel_szuletesi_datum` | egyszerű | ÜGYFÉL | igen |
| 6 | `ugyfel_eletkor` | **számított** | ÜGYFÉL | nem — a születési dátumból számoljuk |
| 7 | `ugyfel_email` | egyszerű | ÜGYFÉL | igen |
| 8 | `berles_id` | egyszerű, kulcs | BÉRLÉS | igen |
| 9 | `berles_kezdet` | egyszerű | BÉRLÉS | igen |
| 10 | `berles_vege` | egyszerű, **nullázható** | BÉRLÉS | igen (nyitott bérlésnél NULL) |
| 11 | `berles_osszeg` | **számított** | BÉRLÉS | lásd 2.4.b — döntés kérdése |
| 12 | `berles_statusz` | egyszerű | BÉRLÉS | igen (kódolt értékkészlettel) |
| 13 | `atvetel_pont_nev` | egyszerű | PONT (szerep: átvételi pont) | igen, de a PONT táblában **egyszer** |
| 14 | `atvetel_pont_cim` | **összetett** | PONT | nem így, részekre bontva |
| 15 | `leadás_pont_nev` | egyszerű | PONT (szerep: leadási pont) | igen, a PONT táblában **egyszer** |
| 16 | `leadás_pont_cim` | **összetett** | PONT | nem így, részekre bontva |
| 17 | `pont_kapacitas` | egyszerű | PONT | igen |
| 18 | `bringa_gyariszam` | egyszerű, **alternatív kulcs** | BRINGA | igen (UNIQUE) |
| 19 | `bringa_tipus` | egyszerű | **BRINGATÍPUS** | igen, csak a típus táblában |
| 20 | `bringa_napidij` | egyszerű | **BRINGATÍPUS** | igen, csak a típus táblában |
| 21 | `bringa_meret` | egyszerű | BRINGA | igen |
| 22 | `bringa_allapot` | egyszerű | BRINGA | igen (kódolt értékkészlettel) |
| 23 | `bringa_beszerzes` | egyszerű | BRINGA | igen |
| 24 | `bringa_hely` | egyszerű, **hivatkozás (idegen kulcs)** | BRINGA–PONT kapcsolat (ELHELYEZ) | igen, de nem szövegként: `pont_id` idegen kulcsként; a pont neve a PONT táblában van |
| 25 | `szerviz_datum` | egyszerű | SZERVIZ | igen |
| 26 | `szerviz_leiras` | egyszerű | SZERVIZ | igen |
| 27 | `szerviz_koltseg` | egyszerű | SZERVIZ | igen |
| 28 | `szamla_sorszam` | egyszerű, kulcs | SZÁMLA | igen |
| 29 | `szamla_datum` | egyszerű | SZÁMLA | igen |
| 30 | `szamla_osszeg` | **számított** (vagy átvett érték) | SZÁMLA | lásd 2.4.b |
| 31 | `szamla_fizetve` | egyszerű, logikai | SZÁMLA | igen |

**Pontozás.** Soronként 0,2 pont a helyes „fajta”, illetve entitás-hozzárendelés; a három „csapda” (`ugyfel_cim`, `ugyfel_telefon`, `ugyfel_eletkor`) valamint a `bringa_hely`, `bringa_tipus`/`bringa_napidij` helyes felismerése külön-külön 0,5–0,5 pont. Teljes pont akkor jár, ha a „Tároljuk?” oszlop indoklása is helyes.

> [A három csapda] **`ugyfel_cim`:** egy mezőben négy különböző adat — ha így marad, nem tudunk városra szűrni, és a városnevek írásmódja szétesik. **`ugyfel_telefon`:** többértékű attribútum, relációs táblában nem tárolható egy mezőben; külön tábla kell. **`ugyfel_eletkor`:** számított érték, amely **minden évfordulón elavul**, ezért nem tároljuk.

### 1.2. Gyenge entitás és specializáció (4 pont)

**a) A gyenge entitás a SZERVIZ (1,5 pont).**

A szerviz önmagában nem azonosítható: két különböző kerékpáron is lehet „1. sorszámú” szerviz, ezért a `sorszam` csak **részleges kulcs**. A szervizt a kerékpárral együtt azonosítjuk, tehát:

- **Részleges kulcs:** `sorszam` (szaggatott aláhúzás);
- **Azonosító kapcsolat:** `SZERVIZEL`, dupla rombusz, a BRINGA oldalon `(1,1)`, a SZERVIZ oldalon `(1,N)`;
- **Teljes részvétel:** a SZERVIZ oldalon dupla vonal (minden szerviz tartozik valamelyik kerékpárhoz);
- A kulcs a logikai sémában: `(bringa_id, sorszam)`.

Indoklás: létezése függ a kerékpártól (nem létezhet „gazdátlan” szerviz), és a sorszám csak a kerékpáron belül egyedi.

**b) Specializáció (2,5 pont).**

A BRINGA entitást specializáljuk két alkategóriára: **EBRINGA** (akkumulátor-kapacitás, hatótávolság) és **TANDEM** (második ülésmagasság). A többi kerékpár az általános BRINGA entitásban marad.

Indoklás:

- az attribútumok **csak egy részhalmazra** jellemzők, ezért nem kerülhetnek a BRINGA táblába (a nem e-bike sorokban NULL lenne, ami sok üres cellát és értelmezhetetlen függéseket okoz);
- így a közös attribútumok (gyári szám, méret, állapot) **egyszer** szerepelnek, a speciálisak csak ott, ahol értelmesek;
- a specializáció **részleges és diszjunkt** (egy kerékpár vagy e-bike, vagy tandem, vagy egyik sem; a feladat szerint egyik sem lehet egyszerre mindkettő).

A gyakorlatban ez **alosztály-táblákkal** valósul meg: `EBRINGA(bringa_id PK/FK, akkumulator_wh, hatotav_km)` és `TANDEM(bringa_id PK/FK, masodik_ulesmagassag)`, ahol a `bringa_id` egyszerre kulcs és idegen kulcs a BRINGA-ra.

### 1.3. Az E-R diagram (20 pont)

::: pagebreak
::: a3 img/er-megoldas.svg | **1. ábra.** A BringaPont kerékpár-kölcsönző E-R diagramja Chen-jelöléssel. A BRINGA–SZERVIZ kapcsolat azonosító kapcsolat (dupla rombusz), a SZERVIZ gyenge entitás; a BRINGA kétszeresen kapcsolódik a ponthoz (ÁTVESZ / LEAD szerepnevek).

**Javítási szempontok (20 pont):**

| szempont | pont |
|: 30,70|
| Mind a hét entitás szerepel, helyes nevű téglalappal (ÜGYFÉL, BÉRLÉS, PONT, BRINGA, BRINGATÍPUS, SZÁMLA, SZERVIZ) | 3 |
| A gyenge entitás helyes jelölése (dupla téglalap) és a részleges kulcs szaggatott aláhúzása | 2 |
| A SZERVIZEL azonosító kapcsolat dupla rombusszal, `(1,N)` / `(1,1)` korlátokkal | 2 |
| Minden kapcsolat nevesítve, a számosság `(min,max)` formában helyes | 5 |
| A teljes részvétel jelölése dupla vonallal (BÉRLÉS a TARTALMAZ kapcsolatban, BRINGA a TARTOZIK kapcsolatban, SZERVIZ) | 2 |
| Attribútumok: kulcsok aláhúzva, többértékű dupla ellipszis, számított szaggatott ellipszis, összetett cím részekre bontva | 3 |
| Szerepnevek az ÁTVESZ / LEAD kapcsolatoknál | 1,5 |
| Olvasható, áttekinthető rajz (nem átfedő elemek, egyértelmű vonalvezetés) | 1,5 |

**Gyakori hibák:** az `ugyfel_telefon` egyszerű attribútumként; a `berles_osszeg` és a `szamla_osszeg` tárolt adatként; az ÁTVESZ és a LEAD összevonása egyetlen kapcsolattá (ez elveszíti a két szerepet); a PONT hiánya a kerékpár helyének modellezéséből; a SZÁMLA „kívülálló” entitásként, kapcsolat nélkül.

::: pagebreak

## 2. feladat — Normalizálás

Jelölés: \(X \to Y\) azt jelenti, hogy az \(X\) attribútumhalmaz funkcionálisan meghatározza \(Y\)-t.

### 2.1. Első normálforma (5 pont)

**a) Három funkcionális függés (2,5 pont):**

1. `ugyfel_id → ugyfel_nev, ugyfel_cim, ugyfel_telefon, ugyfel_email, ugyfel_szuletesi_datum` — egy ügyfélhez egy név, egy cím stb. tartozik.
2. `bringa_gyariszam → bringa_tipus, bringa_napidij, bringa_meret` — a gyári szám azonosítja a konkrét kerékpárt, az pedig meghatározza a típusát és ezen keresztül a napidíját.
3. `szamla_sorszam → szamla_datum, szamla_osszeg, szamla_fizetve` — a számla sorszáma egyértelműen azonosít egy számlát.
4. (További jó válasz:) `berles_id → berles_kezdet, berles_vege, berles_statusz` és `(berles_id, bringa_gyariszam) → berles_osszeg`.

**b) Miért nincs 1NF-ben (2,5 pont):**

A tábla nem 1NF-es, mert:

- a `ugyfel_telefon` mezőben **több érték** szerepel egy cellában („+36 30 111 2233, +36 20 445 6677”), azaz a cella nem atomi;
- a `ugyfel_cim` és a pont-kódok (`atvetel_pont_cim`, `leadás_pont_cim`) **összetett** adatot tartalmaznak (irányítószám + város + utca + házszám egy mezőben).

Javított (1NF-es) séma: a telefonszámot ki kell emelni külön táblába (`UGYFEL_TELEFON(ugyfel_id, telefonszam)`), a címet pedig bontani kell (`iranyitoszam`, `varos`, `utca`, `hazszam`).

### 2.2. Második normálforma (6 pont)

**a) Kulcsok (2 pont).**

- Elsődleges kulcs: `(berles_id, bringa_gyariszam)`.
- Lehetséges kulcs (alternatív kulcs): `(szamla_sorszam, bringa_gyariszam)`, hiszen egy számla egy bérléshez tartozik, és a pár a kerékpárral együtt egyedileg azonosítja a sort.

**b) Két részleges függés (2 pont).**

1. `berles_id → ugyfel_id, berles_kezdet, berles_vege, berles_statusz, szamla_sorszam, szamla_datum, szamla_osszeg, szamla_fizetve, atvetel_pont_nev, …` — a bérlés adatai **csak a kulcs egyik felétől** (`berles_id`) függenek, a kerékpártól nem.
2. `bringa_gyariszam → bringa_tipus, bringa_napidij, bringa_meret` — a kerékpár adatai **csak a másik kulcsrésztől** függenek.

**c) 2NF-es felbontás (2 pont).**

| tábla | attribútumok |
|: 30,70|
| `UGYFEL` | `ugyfel_id`, `ugyfel_nev`, `ugyfel_cim`, `ugyfel_email`, `ugyfel_szuletesi_datum` |
| `UGYFEL_TELEFON` | `ugyfel_id`, `ugyfel_telefon` |
| `PONT` | `atvetel_pont_nev`, `atvetel_pont_cim`, `pont_kapacitas` (a pont neve a kulcs) |
| `BERLES` | `berles_id`, `ugyfel_id`, `atvetel_pont_nev`, `atvetel_pont_cim`, `leadás_pont_nev`, `leadás_pont_cim`, `berles_kezdet`, `berles_vege`, `berles_statusz` |
| `BRINGA` | `bringa_gyariszam`, `bringa_tipus`, `bringa_napidij`, `bringa_meret` |
| `SZAMLA` | `szamla_sorszam`, `berles_id`, `szamla_datum`, `szamla_osszeg`, `szamla_fizetve` |
| `BERLES_BRINGA` | `berles_id`, `bringa_gyariszam` |

A részleges függések megszűntek: minden táblában a teljes kulcs határozza meg az attribútumokat.

### 2.3. Harmadik normálforma (8 pont)

**Két tranzitív függés (2,5 pont):**

1. `bringa_gyariszam → bringa_tipus → bringa_napidij`. A napidíj nem közvetlenül a kerékpártól, hanem a **típusától** függ. (A 2NF-es `BRINGA` táblában tehát `bringa_tipus` és `bringa_napidij` együtt maradt, de ez tranzitív.)
2. `berles_id → ugyfel_id → ugyfel_nev, …` a `BERLES` táblában már megszűnt, de a `SZAMLA` táblában `szamla_sorszam → berles_id → berles_osszeg` típusú lánc maradt, ha az összeget a számlán is tároljuk. Hasonló: `berles_id → atvetel_pont_nev → atvetel_pont_cim` (a pont címe a pont nevétől függ, nem a bérléstől) — ez a **leggyakoribb diákhiba**.

**A tranzitív függések feloldása (5,5 pont):**

| tábla | attribútumok | mi szűnt meg |
|: 25,48,27|
| `UGYFEL` | `ugyfel_id`, `nev`, `email`, `szuletesi_datum` | — |
| `UGYFEL_TELEFON` | `ugyfel_id`, `telefonszam` | — |
| `PONT` | `pont_id`, `nev`, `iranyitoszam`, `varos`, `utca`, `hazszam`, `kapacitas` | a cím a ponttól függ, egyszer szerepel |
| `BRINGATIPUS` | `tipus_id`, `megnevezes`, `napi_dij` | `bringa_tipus → bringa_napidij` |
| `BRINGA` | `bringa_id`, `gyari_szam`, `tipus_id`, `meret`, `allapot`, `beszerzes_datum` | a napidíj kikerült |
| `BERLES` | `berles_id`, `ugyfel_id`, `atvetel_pont_id`, `leadasi_pont_id`, `kezdet`, `vege`, `statusz` | a pont címe/neve kikerült |
| `BERLES_BRINGA` | `berles_id`, `bringa_id` | — |
| `SZAMLA` | `szamla_id`, `berles_id`, `kiallitas_datuma`, `osszeg`, `fizetve` | a bérlés adatai nem ismétlődnek |
| `SZERVIZ` | `bringa_id`, `sorszam`, `datum`, `leiras`, `koltseg` | gyenge entitás |

Ezzel minden nem kulcs attribútum **csak a kulcstól** függ, azaz a séma 3NF-es. A 3NF-be történő felbontás a gyakorlatban a `PONT` tábla bevezetését is jelenti: a pontok adatai (név, cím, kapacitás) **egy helyen** szerepelnek, és a bérlés csak a pont azonosítójára hivatkozik.

### 2.4. BCNF és gyakorlati döntés (6 pont)

**a) BCNF (1,5 pont).** A 3NF-es séma **BCNF-ben is van**. Minden táblában a determinánsok (`ugyfel_id`, `bringa_id`, `berles_id`, `berles_id + bringa_id`, `bringa_id + sorszam`) szuperkulcsok, és nincs olyan nem triviális függés, ahol a determináns nem szuperkulcs. A `SZAMLA.berles_id` egyedi (`UNIQUE`) és `NOT NULL`, ezért az is szuperkulcsként viselkedik.

> [Miért nem triviális ez?] Ha a `SZÁMLA` táblában a `berles_id` **nem** lenne egyedi, akkor a `berles_id → szamla_id` típusú függés sértené a BCNF-et. A feladat szabálya („egy bérléshez legfeljebb egy számla”) éppen ezt zárja ki, ezért kell a szabályt az `UNIQUE` kényszerrel is kimondani.

**b) A `berles_osszeg` és `szamla_osszeg` tárolása (3,5 pont).** Bármelyik döntés elfogadható, ha indokolt:

- **Számítás mellett:** az ár a típusból és az időtartamból származik; ha a napidíj változik, a tárolt összeg elavul, és **frissítési anomália** lép fel (ugyanarra a bérlésre két különböző összeg). A tárolt mező karbantartását triggerre kellene bízni, ami bonyolítja a rendszert.
- **Tárolás mellett:** a számla egy **jogi bizonylat**, amelynek a kiállítás pillanatában érvényes összeget **le kell fagyasztania** — a későbbi árváltozás nem írhatja át a már kiállított számlát. Ezért a `SZAMLA.osszeg` tárolandó (értéke a kiállításkor számított ár), miközben a bérlés „pillanatnyi” végösszege számítható.

**Jó megoldás:** a bérlés összegét **számoljuk** (nézetből), a számláét **tároljuk** (bizonylat). Ez a két szempontot ötvözi.

**c) A `statusz` külön táblába (1 pont).** Nem érdemes: az értékkészlet kicsi és stabil, ezért `CHECK` kényszer a célszerű. Külön tábla csak akkor lenne indokolt, ha a státuszokhoz további adat (szín, engedélyezett átmenetek) kapcsolódna.

::: pagebreak

## 3. feladat — Adatbázis diagram

::: pagebreak
::: a3 img/db-megoldas.svg | **2. ábra.** A BringaPont relációs adatbázis-diagramja varjúláb jelöléssel. Az idegen kulcsok dőltek, az elsődleges kulcsok félkövérek és aláhúzottak.

### 3.1. Logikai séma (12 pont)

**Javítási szempontok:**

| szempont | pont |
|: 30,70|
| Mind a 11 tábla szerepel, a 3NF-es felbontásnak megfelelően | 3 |
| Az elsődleges kulcsok jelölése (félkövér, aláhúzott) és helyessége | 2 |
| Az idegen kulcsok jelölése és minden kapcsolat berajzolása | 3 |
| A varjúláb jelölés helyes iránya (a „sok” oldal a hivatkozó táblán) | 1,5 |
| A számosság és a kötelezőség jelölése | 1,5 |
| Adattípusok megadása minden attribútumnál | 1 |

**Kulcsfontosságú döntések:**

- `BERLES_BRINGA` **összekötő tábla** az M:N kapcsolathoz, összetett kulccsal `(berles_id, bringa_id)`.
- `SZERVIZ` **összetett kulcsa** `(bringa_id, sorszam)` — a gyenge entitás leképezése.
- `SZAMLA.berles_id` **UNIQUE**, hogy a bérlés–számla kapcsolat 1:1 legyen.
- `BRINGA.gyari_szam` **UNIQUE** — alternatív kulcs, a természetes azonosító.
- `BRINGA.pont_id` **NULL engedélyezett**: ha a kerékpár éppen bérlésben van, nincs ponton.
- `EBRINGA` és `TANDEM` **alosztály-táblák**: kulcsuk egyszerre idegen kulcs a BRINGA-ra.

### 3.2. Integritási kényszerek (8 pont)

**a) Egy bérléshez legfeljebb egy számla (1,5 pont).** A `SZAMLA.berles_id` mezőn `UNIQUE` kényszer, emellett `NOT NULL` (minden számla bérléshez tartozik):

```sql
ALTER TABLE szamla
  ADD CONSTRAINT uq_szamla_berles UNIQUE (berles_id);
```

A fordított irány (minden bérléshez legfeljebb egy számla) ezzel automatikusan teljesül.

**b) `berles.vege` csak lezárt státusznál (1,5 pont).**

```sql
ALTER TABLE berles
  ADD CONSTRAINT ck_berles_vege
  CHECK (vege IS NULL OR statusz = 'lezart');
```

(Ez a legegyszerűbb megfogalmazás; a „lezárt ⇒ van vége” irányt külön `CHECK`-kel vagy triggerrel lehet kikényszeríteni.)

**c) Ugyanaz a kerékpár kétszer egy bérlésben (1,5 pont).** Az összekötő tábla **összetett elsődleges kulcsa** ezt automatikusan kizárja:

```sql
PRIMARY KEY (berles_id, bringa_id)
```

**d) Kezdet ≤ vége (1,5 pont).**

```sql
ALTER TABLE berles
  ADD CONSTRAINT ck_berles_idotartam
  CHECK (vege IS NULL OR kezdet < vege);
```

**e) Egy kerékpár ne legyen két nyitott bérlésben (2 pont).** Ez **időbeli átfedés**, amelyet tiszta kulcs-/`CHECK`-kényszerrel nem lehet leírni, mert a `CHECK` csak **egy soron belül** lát. Megoldási lehetőségek:

- **Kizáró kényszer (PostgreSQL, `EXCLUDE USING gist`)** időintervallum-típussal — ez a legtisztább;
- **trigger** `INSERT`/`UPDATE` előtt, amely lekérdezi az átfedő nyitott bérléseket;
- **tranzakció + zárolás** (`SELECT … FOR UPDATE`) az alkalmazásban, megfelelő izolációs szinten.

```sql
-- PostgreSQL: kizáró kényszer (a btree_gist kiterjesztéssel)
ALTER TABLE berles_bringa
  ADD CONSTRAINT ex_berles_bringa_atfedes
  EXCLUDE USING gist (
    bringa_id WITH =,
    tstzrange(
      (SELECT kezdet FROM berles b WHERE b.berles_id = berles_id),
      (SELECT vege   FROM berles b WHERE b.berles_id = berles_id)
    ) WITH &&
  );
```

> [Megjegyzés] A fenti kizáró kényszer a valóságban a `BERLES_BRINGA` táblára csak úgy tehető rá, ha az időpontokat is odamásoljuk, vagy nézetet/triggert használunk — ezért a **triggeres megoldás** a gyakorlatban gyakoribb. A diákoktól itt a probléma **felismerése** és egy megoldási irány megnevezése a cél, nem a tökéletes SQL.

::: pagebreak

## Összefoglaló pontozási javaslat

| feladat | téma | pont |
|: 34,44,12|
| 1.1 | Attribútumok osztályozása | 6 |
| 1.2 | Gyenge entitás és specializáció | 4 |
| 1.3 | E-R diagram | 20 |
| 2.1 | 1NF | 5 |
| 2.2 | 2NF | 6 |
| 2.3 | 3NF | 8 |
| 2.4 | BCNF és gyakorlati döntés | 6 |
| 3.1 | Logikai séma, adatbázis diagram | 12 |
| 3.2 | Integritási kényszerek | 8 |
| **összesen** | | **75** |

> [Formai pontok] A feladatlap 75 pontot ér. Javasolt formai pontozás: az E-R diagram és az adatbázis diagram **olvashatósága** (cím, jelmagyarázat, egyértelmű vonalvezetés) a feladaton belül, illetve maximum 5 pont levonás, ha a kulcsok és a kapcsolatok jelölése következetlen.

::: pagebreak

## 4. feladat — Az elkészült séma (tanári melléklet)

Ez a fejezet a feladatlaphoz nem tartozik hozzá; a tanár a javításhoz és a megbeszéléshez használhatja. Az alábbi táblák a 2. feladat normalizálásának **végeredményét** és a kötelező adattípusokat mutatják.

### 4.1. A normalizált táblák (3NF)

| tábla | attribútumok | kulcs |
|: 22,58,20|
| `UGYFEL` | `ugyfel_id`, `nev`, `email`, `szuletesi_datum` | `ugyfel_id` |
| `UGYFEL_TELEFON` | `ugyfel_id`, `telefonszam` | `ugyfel_id`, `telefonszam` |
| `PONT` | `pont_id`, `nev`, `iranyitoszam`, `varos`, `utca`, `hazszam`, `kapacitas` | `pont_id` |
| `BRINGATIPUS` | `tipus_id`, `megnevezes`, `napi_dij` | `tipus_id` |
| `BRINGA` | `bringa_id`, `gyari_szam`, `tipus_id`, `meret`, `allapot`, `beszerzes_datum`, `pont_id` | `bringa_id` |
| `EBRINGA` | `bringa_id`, `akkumulator_wh`, `hatotav_km` | `bringa_id` |
| `TANDEM` | `bringa_id`, `masodik_ulesmagassag` | `bringa_id` |
| `BERLES` | `berles_id`, `ugyfel_id`, `atvetel_pont_id`, `leadasi_pont_id`, `kezdet`, `vege`, `statusz` | `berles_id` |
| `BERLES_BRINGA` | `berles_id`, `bringa_id` | `berles_id`, `bringa_id` |
| `SZAMLA` | `szamla_id`, `berles_id`, `kiallitas_datuma`, `osszeg`, `fizetve` | `szamla_id` |
| `SZERVIZ` | `bringa_id`, `sorszam`, `datum`, `leiras`, `koltseg` | `bringa_id`, `sorszam` |

### 4.2. Adattípusok és kényszerek

| attribútum | adattípus | megjegyzés |
|: 30,24,46|
| azonosítók | `INTEGER` | `GENERATED ALWAYS AS IDENTITY` |
| nevek, szövegek | `VARCHAR(80)`–`VARCHAR(255)` | |
| telefonszám | `VARCHAR(24)` | szám is lehet vezető nullával vagy nemzetközi előtaggal |
| dátumok | `DATE` | |
| időpontok | `TIMESTAMP` | a bérlés kezdete és vége |
| pénzösszegek | `NUMERIC(10,2)` | **soha ne `FLOAT`!** |
| igen/nem | `BOOLEAN` | |
| `berles.statusz` | `VARCHAR(10)` + `CHECK` | `('nyitott','lezart','lemondott')` |
| `bringa.allapot` | `VARCHAR(12)` + `CHECK` | `('uzemkepes','szervizben','selejtezve')` |
| `ebringa.akkumulator_wh` | `SMALLINT` | wattóra |
| `bringa.meret` | `VARCHAR(4)` | S, M, L, XL |
| `szamla.berles_id` | `INTEGER` | `UNIQUE`, `NOT NULL` |
| `bringa.gyari_szam` | `VARCHAR(30)` | `UNIQUE` |
| `bringa.pont_id` | `INTEGER` | `NULL` engedélyezett |

> [Miért `NUMERIC` és miért nem `FLOAT`?] A lebegőpontos (`FLOAT`/`REAL`/`DOUBLE`) tárolás bináris, ezért a 0,1-hez hasonló értékeket nem tudja pontosan ábrázolni. Pénzösszegeknél ez kerekítési hibákhoz vezet, ami számlázásnál elfogadhatatlan. A `NUMERIC(10,2)` tizedes törtként, pontosan tárol.

### 4.3. Teljes SQL-séma (PostgreSQL)

Az alábbi séma a fenti táblákat hozza létre a kényszerekkel együtt. A kipróbáláshoz használt mintaadatokat a mellékelt `schema.sql` tartalmazza.

```sql
-- ============================================================================
--  BringaPont Kft. — kerékpár-kölcsönző adatbázis
--  Teljes (3NF / BCNF) PostgreSQL-séma
--  Adatbázis-tervezés feladat — mintamegoldás, 2–3. feladat
-- ============================================================================

DROP TABLE IF EXISTS szerviz CASCADE;
DROP TABLE IF EXISTS szamla CASCADE;
DROP TABLE IF EXISTS berles_bringa CASCADE;
DROP TABLE IF EXISTS berles CASCADE;
DROP TABLE IF EXISTS tandem CASCADE;
DROP TABLE IF EXISTS ebringa CASCADE;
DROP TABLE IF EXISTS bringa CASCADE;
DROP TABLE IF EXISTS bringatipus CASCADE;
DROP TABLE IF EXISTS pont CASCADE;
DROP TABLE IF EXISTS ugyfel_telefon CASCADE;
DROP TABLE IF EXISTS ugyfel CASCADE;


-- ---------------------------------------------------------------- ÜGYFÉL
CREATE TABLE ugyfel (
    ugyfel_id        INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nev              VARCHAR(120) NOT NULL,
    email            VARCHAR(160) NOT NULL,
    szuletesi_datum  DATE         NOT NULL,
    CONSTRAINT uq_ugyfel_email UNIQUE (email),
    CONSTRAINT ck_ugyfel_szul   CHECK (szuletesi_datum > DATE '1900-01-01')
);

-- A többértékű "telefonszám" attribútum feloldása külön táblával (1NF)
CREATE TABLE ugyfel_telefon (
    ugyfel_id    INTEGER     NOT NULL
                 REFERENCES ugyfel (ugyfel_id) ON DELETE CASCADE,
    telefonszam  VARCHAR(24) NOT NULL,
    PRIMARY KEY (ugyfel_id, telefonszam)
);


-- ------------------------------------------------------------ KÖLCSÖNZŐPONT
CREATE TABLE pont (
    pont_id       INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    nev           VARCHAR(80) NOT NULL,
    iranyitoszam  CHAR(4)     NOT NULL,
    varos         VARCHAR(60) NOT NULL,
    utca          VARCHAR(80) NOT NULL,
    hazszam       VARCHAR(10) NOT NULL,
    kapacitas     SMALLINT    NOT NULL,
    CONSTRAINT uq_pont_nev      UNIQUE (nev),
    CONSTRAINT ck_pont_kapac    CHECK (kapacitas > 0)
);


-- -------------------------------------------------------------- BRINGATÍPUS
CREATE TABLE bringatipus (
    tipus_id    INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    megnevezes  VARCHAR(40)   NOT NULL,
    napi_dij    NUMERIC(8, 2) NOT NULL,
    CONSTRAINT uq_tipus_nev  UNIQUE (megnevezes),
    CONSTRAINT ck_tipus_dij  CHECK (napi_dij >= 0)
);


-- ------------------------------------------------------------------ BRINGA
CREATE TABLE bringa (
    bringa_id         INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    gyari_szam        VARCHAR(30) NOT NULL,
    tipus_id          INTEGER     NOT NULL REFERENCES bringatipus (tipus_id),
    meret             VARCHAR(4)  NOT NULL,
    allapot           VARCHAR(12) NOT NULL DEFAULT 'uzemkepes',
    beszerzes_datum   DATE        NOT NULL,
    -- NULL, ha a kerékpár éppen bérlésben van (nincs kölcsönzőponton)
    pont_id           INTEGER     NULL REFERENCES pont (pont_id),
    CONSTRAINT uq_bringa_gyariszam UNIQUE (gyari_szam),
    CONSTRAINT ck_bringa_allapot
        CHECK (allapot IN ('uzemkepes', 'szervizben', 'selejtezve')),
    CONSTRAINT ck_bringa_meret CHECK (meret IN ('XS', 'S', 'M', 'L', 'XL'))
);

-- Specializáció: e-bike (részleges alosztály)
CREATE TABLE ebringa (
    bringa_id        INTEGER PRIMARY KEY
                     REFERENCES bringa (bringa_id) ON DELETE CASCADE,
    akkumulator_wh   SMALLINT NOT NULL,
    hatotav_km       SMALLINT NOT NULL,
    CONSTRAINT ck_ebringa_wh CHECK (akkumulator_wh BETWEEN 100 AND 2000),
    CONSTRAINT ck_ebringa_km CHECK (hatotav_km > 0)
);

-- Specializáció: tandem (részleges alosztály)
CREATE TABLE tandem (
    bringa_id              INTEGER PRIMARY KEY
                           REFERENCES bringa (bringa_id) ON DELETE CASCADE,
    masodik_ulesmagassag   SMALLINT NOT NULL,
    CONSTRAINT ck_tandem_ules CHECK (masodik_ulesmagassag BETWEEN 30 AND 120)
);


-- ------------------------------------------------------------------ BÉRLÉS
CREATE TABLE berles (
    berles_id         INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    ugyfel_id         INTEGER   NOT NULL REFERENCES ugyfel (ugyfel_id),
    atvetel_pont_id   INTEGER   NOT NULL REFERENCES pont (pont_id),
    leadasi_pont_id   INTEGER   NOT NULL REFERENCES pont (pont_id),
    kezdet            TIMESTAMP NOT NULL,
    vege              TIMESTAMP NULL,
    statusz           VARCHAR(10) NOT NULL DEFAULT 'nyitott',
    CONSTRAINT ck_berles_statusz
        CHECK (statusz IN ('nyitott', 'lezart', 'lemondott')),
    -- d) a kezdet nem lehet későbbi a végénél
    CONSTRAINT ck_berles_idotartam CHECK (vege IS NULL OR kezdet < vege),
    -- b) a vége csak lezárt státusz mellett lehet kitöltve
    CONSTRAINT ck_berles_vege CHECK (vege IS NULL OR statusz = 'lezart')
);

-- Az M:N kapcsolat (BÉRLÉS ↔ BRINGA) feloldása.
-- Az összetett PK kizárja, hogy ugyanaz a kerékpár kétszer szerepeljen
-- ugyanabban a bérlésben (3.2. c pont).
CREATE TABLE berles_bringa (
    berles_id  INTEGER NOT NULL REFERENCES berles (berles_id) ON DELETE CASCADE,
    bringa_id  INTEGER NOT NULL REFERENCES bringa (bringa_id),
    PRIMARY KEY (berles_id, bringa_id)
);


-- ------------------------------------------------------------------ SZÁMLA
CREATE TABLE szamla (
    szamla_id         INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    -- UNIQUE: egy bérléshez legfeljebb egy számla (3.2. a pont)
    berles_id         INTEGER       NOT NULL UNIQUE REFERENCES berles (berles_id),
    kiallitas_datuma  DATE          NOT NULL,
    osszeg            NUMERIC(10,2) NOT NULL,
    fizetve           BOOLEAN       NOT NULL DEFAULT FALSE,
    CONSTRAINT ck_szamla_osszeg CHECK (osszeg >= 0)
);


-- ----------------------------------------------------------------- SZERVIZ
-- Gyenge entitás: a részleges kulcs (sorszam) csak a bringa_id-val együtt
-- azonosít; a PK ezért (bringa_id, sorszam).
CREATE TABLE szerviz (
    bringa_id  INTEGER      NOT NULL REFERENCES bringa (bringa_id) ON DELETE CASCADE,
    sorszam    SMALLINT     NOT NULL,
    datum      DATE         NOT NULL,
    leiras     VARCHAR(255) NOT NULL,
    koltseg    NUMERIC(9,2) NOT NULL DEFAULT 0,
    PRIMARY KEY (bringa_id, sorszam),
    CONSTRAINT ck_szerviz_sorszam CHECK (sorszam > 0),
    CONSTRAINT ck_szerviz_koltseg CHECK (koltseg >= 0)
);


-- ============================================================================
--  NÉZETEK ÉS SZÁMÍTOTT ÉRTÉKEK (2.4. b pont)
-- ============================================================================

-- Az ügyfél életkora nem tárolt adat: mindig a születési dátumból számoljuk.
CREATE VIEW ugyfel_eletkor AS
SELECT ugyfel_id,
       nev,
       szuletesi_datum,
       DATE_PART('year', AGE(CURRENT_DATE, szuletesi_datum))::INTEGER AS eletkor
FROM ugyfel;

-- A bérlés pillanatnyi végösszege: a napidíjak összege × a bérlés napjainak
-- száma. (A számla összegét ezzel szemben le kell fagyasztani a kiállításkor,
-- mert az bizonylat.)
CREATE VIEW berles_osszeg AS
SELECT b.berles_id,
       b.kezdet,
       b.vege,
       SUM(t.napi_dij) AS napi_dij_osszeg,
       GREATEST(1, CEIL(EXTRACT(EPOCH FROM (COALESCE(b.vege, NOW()) - b.kezdet))
                        / 86400.0))::INTEGER AS napok_szama,
       ROUND(SUM(t.napi_dij) *
             GREATEST(1, CEIL(EXTRACT(EPOCH FROM (COALESCE(b.vege, NOW()) - b.kezdet))
                              / 86400.0)), 2) AS osszeg
FROM berles b
JOIN berles_bringa bb ON bb.berles_id = b.berles_id
JOIN bringa br        ON br.bringa_id = bb.bringa_id
JOIN bringatipus t    ON t.tipus_id = br.tipus_id
GROUP BY b.berles_id, b.kezdet, b.vege;


-- ============================================================================
--  SEGÉDFÜGGVÉNY ÉS TRIGGER AZ IDŐBELI ÁTFEDÉS KIZÁRÁSÁRA (3.2. e pont)
-- ============================================================================

-- Egy kerékpár ugyanabban az időpontban nem lehet két nyitott bérlésben.
CREATE OR REPLACE FUNCTION ellenoriz_atfedest() RETURNS TRIGGER AS $$
DECLARE
    uj_kezdet TIMESTAMP;
    uj_vege   TIMESTAMP;
    utkozes   INTEGER;
BEGIN
    SELECT kezdet, vege INTO uj_kezdet, uj_vege
    FROM berles WHERE berles_id = NEW.berles_id;

    SELECT COUNT(*) INTO utkozes
    FROM berles_bringa bb
    JOIN berles b ON b.berles_id = bb.berles_id
    WHERE bb.bringa_id = NEW.bringa_id
      AND bb.berles_id <> NEW.berles_id
      AND b.statusz <> 'lemondott'
      AND TSRANGE(uj_kezdet, COALESCE(uj_vege, 'infinity'::TIMESTAMP), '[)')
          && TSRANGE(b.kezdet, COALESCE(b.vege, 'infinity'::TIMESTAMP), '[)');

    IF utkozes > 0 THEN
        RAISE EXCEPTION
            'A(z) % kerékpár ebben az időszakban már egy másik bérlésben szerepel.',
            NEW.bringa_id;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_berles_bringa_atfedes
    BEFORE INSERT OR UPDATE ON berles_bringa
    FOR EACH ROW EXECUTE FUNCTION ellenoriz_atfedest();


-- ============================================================================
--  MINTAADATOK (a kipróbáláshoz)
-- ============================================================================

INSERT INTO pont (nev, iranyitoszam, varos, utca, hazszam, kapacitas) VALUES
    ('Pályaudvar',            '6720', 'Szeged', 'Állomás tér',     '1.',  40),
    ('Bevásárlóközpont',      '6725', 'Szeged', 'Kárász utca',     '12.', 30),
    ('Campus',                '6724', 'Szeged', 'Szegedi út',      '7.',  25),
    ('Városliget',            '6721', 'Szeged', 'Hősök tere',      '3.',  20);

INSERT INTO bringatipus (megnevezes, napi_dij) VALUES
    ('városi',   4.50),
    ('trekking', 6.00),
    ('e-bike',  12.00),
    ('gyerek',   3.00),
    ('tandem',   8.50);

INSERT INTO bringa (gyari_szam, tipus_id, meret, allapot, beszerzes_datum, pont_id) VALUES
    ('BP-2023-0001', 1, 'M',  'uzemkepes', DATE '2023-03-14', 1),
    ('BP-2023-0002', 1, 'L',  'uzemkepes', DATE '2023-03-14', 2),
    ('BP-2023-0003', 2, 'M',  'uzemkepes', DATE '2023-04-02', 1),
    ('BP-2024-0017', 3, 'L',  'uzemkepes', DATE '2024-02-20', 3),
    ('BP-2024-0018', 3, 'M',  'szervizben',DATE '2024-02-20', NULL),
    ('BP-2024-0031', 5, 'L',  'uzemkepes', DATE '2024-05-11', 4),
    ('BP-2024-0032', 4, 'S',  'uzemkepes', DATE '2024-05-11', 2);

INSERT INTO ebringa (bringa_id, akkumulator_wh, hatotav_km) VALUES
    (4, 500, 90),
    (5, 500, 90);

INSERT INTO tandem (bringa_id, masodik_ulesmagassag) VALUES
    (6, 62);

INSERT INTO ugyfel (nev, email, szuletesi_datum) VALUES
    ('Kovács Anna',   'anna.kovacs@example.com',  DATE '1990-06-12'),
    ('Nagy Béláné',   'nagy.belane@example.com',  DATE '1975-11-30'),
    ('Tóth Gergő',    'gergo.toth@example.com',   DATE '2001-02-08');

INSERT INTO ugyfel_telefon (ugyfel_id, telefonszam) VALUES
    (1, '+36 30 111 2233'),
    (1, '+36 20 445 6677'),
    (2, '+36 70 900 1122'),
    (3, '+36 1 234 5678');

INSERT INTO berles (ugyfel_id, atvetel_pont_id, leadasi_pont_id, kezdet, vege, statusz) VALUES
    (1, 1, 2, TIMESTAMP '2025-04-12 09:15', TIMESTAMP '2025-04-13 18:40', 'lezart'),
    (2, 2, 2, TIMESTAMP '2025-04-13 10:00', TIMESTAMP '2025-04-20 16:20', 'lezart'),
    (3, 3, 1, TIMESTAMP '2025-04-15 14:30', NULL,                          'nyitott');

INSERT INTO berles_bringa (berles_id, bringa_id) VALUES
    (1, 1), (1, 2),
    (2, 3),
    (3, 4);

INSERT INTO szamla (berles_id, kiallitas_datuma, osszeg, fizetve) VALUES
    (1, DATE '2025-04-13', 18.00, TRUE),
    (2, DATE '2025-04-20', 42.00, TRUE);

INSERT INTO szerviz (bringa_id, sorszam, datum, leiras, koltseg) VALUES
    (5, 1, DATE '2024-09-03', 'akkumulátor-csatlakozó csere', 12500),
    (5, 2, DATE '2025-03-22', 'féktárcsa és fékbetét csere',   18900),
    (1, 1, DATE '2025-01-15', 'lánc kenése, váltó beállítása',  4500);
```
