# etoxd – ETO Játékosportál (demó)

Statikus demó az ETO játékosportálhoz: bejelentkezés, edzésekre be- és kilépés, visszajelzés az edzések végén, edzői és admin nézet.
Nincs backend: minden adat a böngészőben (localStorage) tárolódik.

## Futtatás

```bash
python3 -m http.server 8000
# majd: http://localhost:8000
```

(Az `index.html` közvetlen megnyitása is működik, de az Excel-feltöltéshez és a betűtípusokhoz jobb a helyi szerver.)

## Demó felhasználók

Jelszó mindenkinek: `eto1904`. A belépés a TalentX azonosítóval történik.

| Szerepkör | TalentX | Név |
|-----------|---------|-----|
| Játékos   | TX-1001 | Kiss Bence (U17) |
| Edző      | TX-2001 | Nagy Péter (U17) |
| Edző      | TX-2002 | Szabó Anna (U19) |
| Admin     | TX-9001 | Kovács Eszter |
| Rendszergazda | TX-0001 | ETO Rendszergazda |

A bejelentkező oldalon a „Játékos / Edző / Admin / Rendszergazda” gombokkal egy kattintással be lehet lépni.

## Mit tud a demó

- **Játékos**: látja a csapata edzéseit napokra bontva. Az edzés kezdete előtt 15 perccel nyílik a belépés,
  az edzés alatt ki is léphet. Ha jelen volt, az edzés után este 22:00-ig visszajelzést küldhet csúszkás
  kérdésekkel (hangulat-arc, terhelés, saját teljesítmény), opcionális megjegyzéssel, névvel vagy névtelenül.
- **Edző**: a saját csapata edzéseinél látja, ki lépett be és mikor, és az érkezett visszajelzéseket (átlagok és megjegyzések).
- **Admin**: ugyanez minden csapatra, plusz játékosok és edzők kezelése (ki léphet be) és a heti beosztás feltöltése
  (.xlsx vagy .csv; minta letölthető az oldalon). A feltöltés felülírja az azonos napra és csapatra vonatkozó edzéseket.

- **Rendszergazda**: teljes rendszeráttekintés, adminisztrátori és rendszergazdai fiókok létrehozása, szerepkörök kiosztása és egyéni szerepkörök létrehozása. Minden csapat edzéseit, jelenlétét és visszajelzéseit látja; az anonim visszajelzések megjelenítése továbbra is névtelen.

A demó edzései az aktuális időponthoz igazodnak (van épp zajló, nemrég véget ért és későbbi edzés is),
és naponta, illetve 6 óránként újragenerálódnak. Az oldal alján a „Demó visszaállítása” gomb is újrakezdi.

## Fájlok

- `index.html`, `styles.css`, `script.js`: bejelentkezés
- `app.html`, `app.css`, `app.js`: portál (játékos / edző / admin nézet)
- `data.js`: demó adatok és tárolás
- `theme.js`: világos / sötét mód
- `public/assets/`: címer és betűtípusok

## Arculat

- Az arculat három rögzített alapszíne: ETO-zöld `#176131`, arany `#b8994d` és krém `#f9f5ef`.
  A klub a kiegészítő színek használatában rugalmas; az alapszínek árnyalatai, semleges szürkék és az arculathoz illő további színek is használhatók.
  A klub kifejezett kérése: lila és neon színek sehol ne szerepeljenek a felületen, egyik megjelenítési módban sem.
  A színek tokenként a `styles.css` elején vannak.
- Világos és sötét mód: alapból a rendszer beállítását követi, a nap/hold gombbal (bejelentkezés jobb felső sarok, portál fejléc) át lehet váltani; a választást a böngésző megjegyzi.
- Betűtípusok: `public/assets/fonts/` (FuturaDisplay 800/700, FuturaPT 400).

## Megjelenés és ötletdoboz

A „Megjelenés & ötletek” gomb a bejelentkező oldalon és a portál fejlécében is elérhető.
Négy választható színterv mini portálelőnézettel:

- **ETO Klasszikus**: az eredeti zöld–arany–krém paletta.
- **Homokkő**: meleg homokszínű felületek, terrakotta kiegészítő szín.
- **Palakék**: hűvös, világos felületek és visszafogott palakék kiegészítés.
- **Erdő**: zsályás felületek, erdőzöld kiegészítés.

Mindegyik paletta használható világos és sötét módban; a „Rendszer” beállítás az operációs rendszer megjelenését követi.
A paletta és a mód egymástól függetlenül választható. A választás böngészőnként a `localStorage`-ban marad meg,
nem felhasználói fiókhoz kötött és nem szinkronizálódik más eszközökre. Lila és neon színek egyik témában sincsenek.

Az **ötletdoboz** legfeljebb 50, egyenként 500 karakteres saját ötlet mentésére és törlésére szolgál.
Az ötletek csak az adott böngészőben tárolódnak; nem kerülnek elküldésre a klubnak, és ugyanazon böngésző más demó felhasználói is láthatják őket.

A játékosoldalon a „Most neked” szakasz emeli ki a belépést, kilépést és a még elküldhető visszajelzéseket.
Az edzői és adminisztrátori asztali nézet szélesebb munkafelületet, időszakszűrőt, csapatáttekintést és friss visszajelzéseket kapott.
Az adminisztráció asztali oldalsó navigációja mobilon vízszintes navigációra vált.


## Rendszerfelügyelet és szerepkörök

A `TX-0001` rendszergazda a demó jelszóval (`eto1904`) léphet be. Régi böngészőadatok esetén a rendszer automatikusan hozzáad egy rendszergazdát a meglévő fiókok megtartásával.
Ha a `TX-0001` azonosítót egy korábbi saját fiók már használja, az új rendszergazda egyedi `SYS-…` azonosítót kap; ez az admin felhasználólistájában látható.

- A rendszergazda adminokat és további rendszergazdákat is létrehozhat, illetve más felhasználók szerepkörét és hozzáférését módosíthatja. Saját fiókját nem tilthatja le és nem fokozhatja le.
- Az admin csak játékosokat, edzőket és ezekre épülő egyéni szerepkörű fiókokat hozhat létre és kezelhet; adminisztrátori vagy rendszergazdai jogot nem oszthat ki.
- Az egyéni szerepkör neve szabadon megadható; a jogosultsági szint a Játékos, Edző vagy Admin egyikét örökli. Az edző a saját csapatát, az admin az összes csapatot látja.
- A fiókok, egyéni szerepkörök és az utolsó 200 fiók-/szerepkörmódosítás a beosztás automatikus újragenerálásakor megmaradnak. A „Demó visszaállítása” ezeket is törli.

**Demóhatár:** nincs backend; a jelszó és a jogosultságvizsgálat a böngészőben működik, a helyi adatok módosíthatók. Ez nem éles hitelesítés vagy biztonságos hozzáférés-védelem. Éles használathoz szerveroldali hitelesítés, jogosultságvizsgálat és közös adattárolás szükséges.

## Folyamatos visszajelző csúszkák és mobilnézet

A csúszkák 0–4 között, 0,01-es lépéssel mozognak. A mentett értékek és az átlagok megőrzik a tört értékeket; a szöveges leírás a legközelebbi öt kategória egyikét mutatja. Az arc szeme és szája a csúszka értékével folyamatosan változik, a háttér és az arc színe is fokozatosan vált. Az operációs rendszer csökkentett mozgás beállítását tiszteletben tartjuk.

Telefonon a fejléc rendezett kétsoros elrendezést kapott, a csapatkártyák és névsorok tördelhetők, a visszajelző ablak kisebb kijelzőn is görgethető. A fő műveletek nagyobb érintési felületet kapnak.

## Ellenőrzések

```bash
node --test tests/accounts.test.cjs
# Futó helyi szerver, Playwright és Chromium mellett:
node tests/ui-smoke.cjs
```

A böngészős teszt alapértelmezésben a `/usr/bin/chromium` programot használja; más telepítésnél a `CHROMIUM_PATH` környezeti változóval megadható az útvonala. A tesztek külön böngészőkörnyezetben futnak.
