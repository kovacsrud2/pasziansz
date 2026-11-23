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
