## 0. A feladat háttere

**BringaPont Kft.** városi kerékpár-kölcsönző hálózatot üzemeltet. A cégnek jelenleg hat kölcsönzőpontja van (két pályaudvar, két bevásárlóközpont, egy campus és egy városligeti stand), összesen mintegy 180 kerékpárral. A kölcsönzés ma papíron és egy táblázatkezelőben történik; a napi forgalom 60–120 kölcsönzés, csúcsidőben ennek a duplája.

A cég egy relációs adatbázist szeretne, amelyből kiolvasható:

- melyik ügyfél mikor, melyik ponton vett át és hol adott le kerékpárt;
- melyik kerékpár éppen hol van, ki használja, és milyen állapotban van;
- mennyi a kölcsönzés díja, milyen számla készült róla és kifizették-e;
- melyik kerékpáron mikor, milyen javítást végeztek, és az mennyibe került;
- melyik pont mennyi kerékpárt tud befogadni (kapacitás), és mikor van tele.

A telephelyeken a **kerékpárok típusonként** azonosak: városi, trekking, e-bike, gyerek- és tandemkerékpárok vannak. Egy típusnak saját neve és napi díja van. Minden egyes kerékpárnak van egyedi gyári száma, saját mérete (vázzám) és állapota (üzemképes, szervizben, selejtezve).

A cég szabályai, amelyeket az adatbázisnak is követnie kell:

1. Egy ügyfélnek lehet **több bérlése** is, de egy bérlés mindig **pontosan egy ügyfélhez** tartozik. Egy bérlésben **egy vagy több kerékpár** szerepelhet (családi kölcsönzés).
2. Minden bérlésnek van **átvételi** és **leadási pontja** is (a kettő lehet ugyanaz). Egy bérlés pontosan egy átvételi és egy leadási helyen történik. Egy ponton sok bérlés indulhat és sok végződhet.
3. Egy bérléshez **legfeljebb egy számla** tartozik, és egy számla mindig pontosan egy bérléshez. (A bérlés lezárásakor készül.)
4. Egy kerékpár **élettartama alatt több szervizen** is áteshet, de egy szerviz mindig pontosan egy kerékpárhoz tartozik. A szervizeket a kerékpár azonosítja: ugyanazon a kerékpáron belül 1-től növekvő sorszámmal jelölik őket.
5. Egy kerékpárnak **pontosan egy típusa** van; egy típushoz sok kerékpár tartozhat.
6. Egy kerékpár **egyszerre legfeljebb egy helyen** van: vagy egy kölcsönzőponton, vagy egy bérlés keretében valakinél.
7. Az ügyfélnek több telefonszáma is lehet, és az ügyfél **életkorát** nem tároljuk, hanem a születési dátumából számítjuk. Az ügyfél címe utcára, házszámra, városra és irányítószámra bontható.
8. A bérlés **végösszege** nem tárolt adat: a bérlés időtartamából és a benne szereplő kerékpárok típusának napi díjából számoljuk.

::: pagebreak

## 1. feladat — Kiinduló attribútumlista és E-R diagram (30 pont)

Az alábbi lista a cég jelenlegi nyilvántartásának **kiinduló attribútumait** tartalmazza, ahogyan a táblázatkezelőben szerepelnek. A lista szándékosan „lapos”: nem jelöli a kulcsokat, az összetett és többértékű adatokat, és tartalmaz olyan mezőket, amelyeket az adatbázisban nem érdemes tárolni.

| # | Kiinduló attribútum | Megjegyzés a jelenlegi nyilvántartásból |
|: 5,42,53|
| 1 | `ugyfel_id` | ügyfél sorszáma, kézzel kiadott |
| 2 | `ugyfel_nev` | az ügyfél teljes neve |
| 3 | `ugyfel_cim` | egyetlen szövegmező, pl. „6720 Szeged, Kárász u. 9.” |
| 4 | `ugyfel_telefon` | egy cellában akár több szám is, vesszővel |
| 5 | `ugyfel_szuletesi_datum` | |
| 6 | `ugyfel_eletkor` | képlettel számolt oszlop |
| 7 | `ugyfel_email` | |
| 8 | `berles_id` | bérlés sorszáma |
| 9 | `berles_kezdet` | dátum és időpont |
| 10 | `berles_vege` | dátum és időpont, üres, amíg nyitott |
| 11 | `berles_osszeg` | képlettel számolt oszlop |
| 12 | `berles_statusz` | nyitott / lezárt / lemondott |
| 13 | `atvetel_pont_nev` | annak a pontnak a neve, ahol átvették |
| 14 | `atvetel_pont_cim` | ugyanennek a pontnak a címe |
| 15 | `leadás_pont_nev` | annak a pontnak a neve, ahol leadták |
| 16 | `leadás_pont_cim` | ugyanennek a pontnak a címe |
| 17 | `pont_kapacitas` | hány kerékpár fér el a ponton |
| 18 | `bringa_gyariszam` | a kerékpár egyedi gyári száma |
| 19 | `bringa_tipus` | pl. „trekking” |
| 20 | `bringa_napidij` | a típus napi díja |
| 21 | `bringa_meret` | vázzám (S, M, L, …) |
| 22 | `bringa_allapot` | üzemképes / szervizben / selejtezve |
| 23 | `bringa_beszerzes` | beszerzés dátuma |
| 24 | `bringa_hely` | melyik ponton van éppen |
| 25 | `szerviz_datum` | a javítás napja |
| 26 | `szerviz_leiras` | mit javítottak |
| 27 | `szerviz_koltseg` | a javítás költsége |
| 28 | `szamla_sorszam` | a számla azonosítója |
| 29 | `szamla_datum` | kiállítás dátuma |
| 30 | `szamla_osszeg` | a számlán szereplő összeg |
| 31 | `szamla_fizetve` | igen / nem |

### 1.1. Részfeladat — Attribútumok osztályozása (6 pont)

Másold át a táblázatot, és minden sorhoz írd be a hiányzó oszlopok tartalmát:

- **Fajta:** egyszerű / összetett / többértékű / számított / származtatott kulcs;
- **Entitás vagy kapcsolat:** melyik fogalomhoz tartozik (pl. ÜGYFÉL, BÉRLÉS, PONT, BRINGA, BRINGATÍPUS, SZERVIZ, SZÁMLA, vagy valamelyik kapcsolat);
- **Tároljuk?** igen / nem — és ha nem, miért nem.

### 1.2. Részfeladat — Gyenge entitás és specializáció (4 pont)

a) A lista alapján melyik fogalom **gyenge entitás**, és miért? Add meg a részleges kulcsát és az azonosító kapcsolatát.

b) A kerékpárok között vannak olyanok, amelyek különleges kezelést igényelnek: az **e-bike-oknak** van akkumulátorkapacitásuk (Wh) és hatótávolságuk (km), a **tandemeknek** pedig van egy második ülésmagasságuk. Rajzold be a specializációt az E-R diagramba, és indokold, miért érdemes specializálni.

### 1.3. Részfeladat — E-R diagram (20 pont)

Készítsd el a BringaPont **E-R diagramját** Chen-jelöléssel a kiinduló attribútumokból és a 0. pont szabályaiból. A diagramon szerepeljen:

- minden entitás és gyenge entitás (a gyenge entitást dupla téglalappal jelöld);
- minden kapcsolat nevével, a **részvételi korlátokkal** `(min,max)` formában;
- a **teljes részvételt** dupla vonallal;
- minden attribútum, a **kulcsattribútumok aláhúzva**, a többértékű attribútum dupla ellipszissel, a számított attribútum szaggatott ellipszissel, az összetett attribútum részekre bontva;
- a gyenge entitás **részleges kulcsa** szaggatott aláhúzással, az **azonosító kapcsolat** dupla rombusszal;
- a **szerepnevek** ott, ahol ugyanaz az entitás kétszer kapcsolódik ugyanahhoz a kapcsolathoz (ÁTVESZ / LEAD).

> [Hogyan fogj hozzá?] Először **gyűjtsd ki a főneveket** a 0. pont szövegéből — ezek adják az entitásokat (ügyfél, bérlés, pont, kerékpár, típus, számla, szerviz, telefonszám, cím). Utána keresd meg az **igéket és a birtokviszonyokat**: ezekből lesznek a kapcsolatok (bérel, átvesz, lead, tartalmaz, elhelyez, tartozik, kiállít, szervizel). Végül minden entitás mellé írd oda az attribútumait, és jelöld a kulcsot. A kapcsolatoknál a kérdés mindig ez: *egy X-hez hány Y tartozhat, és egy Y-hez hány X?* — a válasz adja a `(min,max)` korlátokat.

> [Jelölések a diagramon] Téglalap = entitás • dupla téglalap = gyenge entitás • rombusz = kapcsolat • dupla rombusz = azonosító kapcsolat • ellipszis = attribútum • aláhúzott név = kulcsattribútum • szaggatott aláhúzás = részleges kulcs • dupla ellipszis = többértékű attribútum • szaggatott ellipszis = számított attribútum • dupla vonal = teljes részvétel • `(min,max)` = részvételi korlát.

::: pagebreak
::: vazlat 630 | 1.3. feladat — E-R diagram (Chen-jelölés). A végleges rajzot A3-as lapra készítsd!

> [Figyelem] Az `ugyfel_eletkor`, a `berles_osszeg` és a `szamla_osszeg` közül legalább kettő **számított** adat. Ezek helyét a diagramon jelöld, de a döntésedet indokold: melyiket érdemes esetleg mégis tárolni, és miért.

::: pagebreak

## 2. feladat — Normalizálás (25 pont)

A jelenlegi nyilvántartás egyetlen lapos táblából áll, a következő mezőkkel:

| mezo | tartalom |
|: 26,74|
| `ugyfel_id`, `ugyfel_nev`, `ugyfel_cim`, `ugyfel_telefon`, `ugyfel_email` | ügyfél adatai |
| `berles_id`, `berles_kezdet`, `berles_vege`, `berles_statusz` | a bérlés adatai |
| `atvetel_pont_nev`, `atvetel_pont_cim`, `leadás_pont_nev`, `leadás_pont_cim` | a pontok adatai |
| `bringa_gyariszam`, `bringa_tipus`, `bringa_napidij`, `bringa_meret` | a kerékpár adatai |
| `szamla_sorszam`, `szamla_datum`, `szamla_osszeg`, `szamla_fizetve` | a számla adatai |

A táblázat egy sora **egy bérlés egy kerékpárját** írja le, azaz ha valaki három kerékpárt bérel, akkor három sor keletkezik, és a bérlés adatai mindhárom sorban ismétlődnek.

### 2.1. Részfeladat — Első normálforma (5 pont)

a) Adj meg **három funkcionális függést** (FD) a táblázatból, és indokold mindegyiket!

b) Mutasd meg, hogy a tábla **nincs 1NF-ben**, és adj meg egy javított (1NF-es) sémát!

### 2.2. Részfeladat — Második normálforma (6 pont)

a) Határozd meg a táblázat **összes kulcsát** (a lehetséges kulcsokat is)! A tábla elsődleges kulcsa: `berles_id`, `bringa_gyariszam`.

b) Mutass meg **két részleges függést** (azaz olyan FD-t, ahol a determináns a kulcs valódi részhalmaza), és mondd meg, mely attribútumok függenek részlegesen a kulcstól!

c) Bontsd fel a táblát 2NF-be, és add meg a kapott táblák kulcsait!

### 2.3. Részfeladat — Harmadik normálforma (8 pont)

A 2NF-es táblákban mutass meg **két tranzitív függést**, majd bontsd tovább a sémát 3NF-be!

> [Segítség] A `berles_osszeg` és a `szamla_osszeg` a bérléstől függ, de a `bringa_tipus` → `bringa_napidij` függés miatt a napidíj nem a bérlés attribútuma. Gondold végig, melyik mező melyik táblába kerül!

### 2.4. Részfeladat — BCNF és a gyakorlati döntés (6 pont)

a) A 3NF-es sémádban van-e olyan tábla, amely **nincs BCNF-ben**? Ha igen, mutasd meg, és bontsd fel; ha nincs, indokold, miért nem.

b) A tervezés végén döntened kell: a `berles_osszeg` és a `szamla_osszeg` mezőket **tárolod** vagy **számítod**? Írj 3–4 mondatos indoklást, amelyben kitérsz az anomáliákra és a teljesítményre is.

c) A `berles_statusz` mezőt érdemes-e külön táblába tenni? Mi szól mellette és mi ellene?

::: pagebreak

## 3. feladat — Adatbázis diagram (20 pont)

A 2. feladat 3NF-es (illetve BCNF-es) sémájából készítsd el a **relációs adatbázis diagramját**.

### 3.1. Részfeladat — Logikai séma (12 pont)

Rajzold meg a táblákat a következő jelölésekkel:

- a tábla neve a fejlécben, alatta az attribútumok **adattípussal** (`INTEGER`, `VARCHAR(… )`, `DATE`, `TIMESTAMP`, `NUMERIC(10,2)`, `BOOLEAN`, …);
- az **elsődleges kulcs** félkövér és aláhúzott, az **idegen kulcs** dőlt;
- a kapcsolatok **varjúláb (crow's foot)** jelöléssel: a „sok” oldalon háromágú villa, az „egy” oldalon egy vonás;
- a kapcsolat **számossága** (`1:1`, `1:N`) a vonal mellett;
- a **kötelezőség**: teli vonal = kötelező, szaggatott/semmi = opcionális.

### 3.2. Részfeladat — Integritási kényszerek (8 pont)

A diagram alapján írd le szövegesen (vagy SQL `CHECK`/`UNIQUE`/`NOT NULL` formában) az alábbi kényszereket:

a) Egy bérléshez legfeljebb egy számla tartozhat.

b) A `berles.vege` csak akkor lehet kitöltve, ha a bérlés státusza „lezárt”.

c) Egy bérléshez nem tartozhat ugyanaz a kerékpár kétszer.

d) A bérlés kezdete nem lehet későbbi a végénél.

e) Egy kerékpár ugyanabban az időpontban nem lehet két nyitott bérlésben.

> [Tipp] Az a)–d) pontok egyszerű kulcs- és `CHECK`-kényszerekkel megoldhatók. Az e) pont **időbeli átfedés**, amelyet tiszta kulcskényszerrel nem lehet leírni — gondold végig, miért, és milyen adatbázis-eszköz (trigger, kizáró kényszer, tranzakció + zárolás) kell hozzá.

::: pagebreak
::: vazlat 690 | A logikai séma vázlata — a végleges diagramot A3-as lapon készítsd el
