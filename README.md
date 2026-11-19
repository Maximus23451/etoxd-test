# ETO játékosportál

React 19 + Vite frontend, ASP.NET Core 10 API és Microsoft SQL Server 2022 adatbázis. A hitelesítés, jogosultságvizsgálat, korosztálytagságok, jelenlét, visszajelzések és naplók a szerveren működnek. Nincs Node/SQLite backend.

## Helyi fejlesztés

Szükséges: Node.js 24+, a `global.json` szerinti .NET 10 SDK (10.0.404), Docker SQL Serverhez vagy elérhető saját Microsoft SQL Server.

```bash
npm ci
npm run db:start
npm run build:api
npm run dev
```

A Vite a 5173-as, az API a 3001-es porton fut; a frontend `/api` kéréseit a Vite továbbítja. A SQL Server a 127.0.0.1:1433 címen érhető el. Egészségellenőrzés: `GET /api/health`, amely az adatbázis elérhetőségét is ellenőrzi.

A kezelt cloud környezetben a .NET a `/workspace/.dotnet` könyvtárban van; a segédszkriptek automatikusan megtalálják. Más gépen a PATH-on levő `dotnet` vagy a `DOTNET_ROOT` használható. A cloud sandboxban a .NET/NuGet névvel ellátott rendszer-mutexe miatt a build néha a sandboxon kívüli végrehajtást igényli. A futó API-nak erre nincs szüksége.

A `db:start` helyi, véletlen SA jelszót generál a figyelmen kívül hagyott `.runtime/sql.env` fájlba, az API kapcsolatát pedig a `.runtime/config.json` fájlba. Nem írja ki a jelszót. A SQL Server image digestre rögzített, a tároló neve `etoxd-sql`, az adatvolume `etoxd-sql-data`. A parancs ismételhető, meglévő adatokat nem töröl. A kizárólag loopbacken futó fejlesztői kapcsolat titkosított, a tároló önaláírt szervertanúsítványát megbízhatónak tekinti. Éles SQL Servernél ellenőrzött tanúsítványt és `TrustServerCertificate=False` beállítást használj.

Saját SQL Serverhez a `ConnectionStrings__Portal` környezeti változóval add meg a kapcsolatot; az `.env.example` csak minta, automatikusan nem töltődik be. A `.runtime` fájlokat, cookie-védelmi kulcsokat és SQL volume-ot őrizd meg; a futó folyamatok újraindítása külön lépés. Docker volume-visszaállítást új cloud feladatban nem tekintünk ellenőrzöttnek.

## Build és futtatás

```bash
npm run build:all
npm start
```

Az API a `dist` könyvtárból a kész React felületet is kiszolgálja. Az `api-start.mjs` alapértelmezése fejlesztői környezet. Éles telepítéskor állítsd be az `ASPNETCORE_ENVIRONMENT=Production`, HTTPS és a saját SQL kapcsolat / `AllowedHosts` értékeket. A `Production` környezet biztonságos cookie-t követel, és önmagában nem hoz létre demófiókokat.

A séma az első indításkor EF Core `EnsureCreated` segítségével jön létre. Ez a tesztváltozat első sémája: meglévő éles adatbázis verzióváltásához még verziózott migrációk és telepítési folyamat szükségesek. Ez működő helyi tesztalkalmazás, nem kész éles infrastruktúra: a TLS végpont, adatbázis-mentések, kulcsvédelem, jelszó-visszaállítás és naplómegőrzési szabályzat telepítési feladat.

## Demófiókok

A fejlesztői adatbázis első indításkor az alábbiakat kapja. Közös **demó** jelszó: `eto1904`. Az új fiókokhoz az admin saját kezdő jelszót ad meg, a szerver hash-elve tárolja. A régi localStorage demóadatai nem kerülnek automatikusan az új közös adatbázisba.

| Szerepkör | Azonosító | Név | Korosztály |
|---|---|---|---|
| Rendszergazda | TX-0001 | ETO Rendszergazda | teljes klub |
| Admin | TX-9001 | Kovács Eszter | teljes klub |
| Edző | TX-2001 | Nagy Péter | U17 |
| Edző | TX-2002 | Szabó Anna | U19 |
| Edző | TX-2003 | Varga Tamás | U15 |
| Játékos | TX-1001 | Kiss Bence | U17 |
| Játékos | TX-1501 | Kovács Józsi | U15 |

## Heti naptár és Excel

A heti naptár játékosoknak és edzőknek a hozzájuk tartozó korosztályok edzéseit mutatja; az admin és a rendszergazda az összeset láthatja. Minden edzésen megjelenik a kezdés/vég, a helyszín, az edző és az U-korosztály. Az edzés részleteiben látszanak a csapattagok és a jelenlétek, edzőnek a visszajelzések is. A játékos csak saját értékelését látja; a névtelen értékelés szerzője a személyzeti felületen sem jelenik meg.

Asztalon hétoszlopos naptár, közepes kijelzőn heti lista, telefonon hétválasztó és napi kártyák működnek. A hét- és napszűrők nem írják át az adatbázist. Minden dátumot és időt `Europe/Budapest` szerint értelmezünk, a szerver UTC offsettel tárol.

Az admin a naptár alján tölthet fel `.xlsx` vagy `.csv` fájlt (legfeljebb 2 MB és 1000 edzés). A feldolgozás ASP.NET/ClosedXML oldalon történik; nincs külső böngészős Excel-CDN.

| Oszlop | Példa | Kötelező |
|---|---|---|
| Dátum | 2026-11-16 | igen |
| Kezdés | 16:00 | igen |
| Vége | 17:30 | igen |
| Csapat | U17 | igen |
| Edzés | Csapatedzés | igen |
| Helyszín | Füves pálya 1 | igen |
| Edző | Nagy Péter | nem; hiányakor a korosztály aktív edzői |

A táblázat első munkalapját olvassuk. A magyar oszlopnevek és a `Date, Start, End, Team, Training, Location, Coach` megfelelőik támogatottak. CSV-nél a vessző / pontosvessző és az idézőjelezett mezők is működnek. Az Excel dátum- és időcellái, illetve a szabványos ISO dátum / 24 órás idő szövegek használhatók. A „Minta CSV letöltése” link a felületen elérhető.

Minden sor ellenőrzése a mentés előtt történik; hibás sor esetén semmi nem kerül importálásra. Az azonos nap/korosztály érintetlen edzéseit a feltöltés lecseréli. A jelenléttel vagy értékeléssel rendelkező edzések megmaradnak; azonos kezdésű ilyen edzés ütközése esetén a feltöltést elutasítjuk. Ismeretlen korosztályt nem hoz létre automatikusan a rendszer.

## Több korosztály és játékos-áthelyezés

Egy játékoshoz több tagság tartozhat. Józsi például U15 és U17 tagja is lehet, és mindkét naptárat látja.

- **Hozzáadás:** U15 → U15 + U17.
- **Áthelyezés:** U15 + U19 → U17 + U19, ha az U15 tagságot U17-re cseréljük.
- **Összes tagság beállítása:** a kiválasztott korosztályok lesznek az aktuális tagságok; legalább egy szükséges.

A tagság változtatása nem töröl edzést, jelenlétet vagy visszajelzést. A korábban látogatott edzések a játékos történetében továbbra is látszanak. Az admin és a rendszergazda alapból módosíthat tagságokat. A rendszergazda a „Szerepkörök” oldalon egyéni, például **Korosztály-koordinátor** szerepet hozhat létre Edző alapszinttel és külön **korosztálytagság-kezelési** joggal. Ez klubszintű tagságkezelést ad, de nem jogosít adminfiókok vagy szerepkörök létrehozására. A jogosultságellenőrzés minden API-kéréskor megtörténik.

## Rendszerfelügyelet és tevékenységnapló

A rendszergazda adminokat és további rendszergazdákat is létrehozhat, egyéni szerepköröket adhat meg, és megnézheti a közös eseménynaplót. Saját fiókját nem tilthatja le vagy fokozhatja le. Az admin játékosokat és edzőket kezelhet, magasabb szintet nem oszthat ki.

A SQL Serverben tárolt, a felületen csak olvasható napló tartalmazza:

- be- és kijelentkezést, sikertelen bejelentkezéseket;
- fiók-, szerepkör- és tagságmódosításokat (szereplő, cél, tagságok előtte/utána);
- importálást, edzésre belépést/kilépést, értékelés beküldésének tényét;
- témamentést, ötlet létrehozását/törlését;
- API útvonalsablont, státuszt, futási időt és kérésazonosítót;
- kiválasztott böngészős műveleteket: navigáció, hét/nap választása, szűrés, edzés vagy tagságablak megnyitása és hibajelzés.

Nem naplózunk jelszót, cookie-t, nyers HTTP törzset, begépelt szöveget, értékelési pontokat vagy megjegyzéstartalmat. Az anonim értékelés auditbejegyzése és HTTP bejegyzése nem kap játékosazonosítót; a névtelenség a felhasználói megjelenítésre vonatkozik, nem az adatbázis üzemeltetőjével szembeni garantált anonimitásra.

A böngészős események „tájékoztató” forrásjelölést kapnak, nem használhatók bizonyító erejű biztonsági naplóként. Nem rögzítünk minden billentyűt vagy egérmozgást. A kritikus szervermódosítás és a hozzá tartozó napló ugyanabban az EF tranzakcióban kerül mentésre. A kérésnapló külön, best-effort művelet, hibáját az API konzolja jelzi. A napló kereshető eseménytípusra, 100 soros lapokkal; automatikus törlés jelenleg nincs. A szabályzat szerinti megőrzés és külső, módosíthatatlan naplótár éles üzemeltetési feladat.

## Hibaképernyők, betöltés és műveleti visszajelzések

- Egyedi 401/403/404/500, kapcsolatvesztési és React hibahatár képernyők, visszatérés/újrapróbálás lehetőséggel.
- Kezdő betöltő, vázkártya animáció, folyamatjelző, küldés alatti gombállapotok, siker- és hibajelzések; ismételt beküldést a gombok tiltása is megelőzi.
- Rendszergazdának az **Állapotteszt** oldalon valódi API-válaszokkal tesztelhető a lassú betöltés és a 401/403/404/500. A teszt nem módosít játékosadatot. Fejlesztői módban érhető el; máshol külön `DIAGNOSTICS=1` kell.
- A sima hiányzó útvonal a React 404 oldalát mutatja. Offline viselkedés a böngésző fejlesztői eszközeiben és hálózatmegszakítással tesztelhető.

## Arculat és személyre szabás

A három rögzített alapszín ETO-zöld `#176131`, arany `#b8994d`, krém `#f9f5ef`. A klub a visszafogott kiegészítő színekben rugalmas. **Lila és neon színek sehol, sötét módban sem szerepelhetnek.**

A **Megjelenés & ötletek** négy színtervet mutat: ETO Klasszikus, Homokkő/terrakotta, Palakék és Erdő/zsálya. Világos, sötét és rendszer mód választható. A beállítás most már fiókhoz kötötten, az SQL adatbázisban tárolódik. Az ötletdoboz is a fiókhoz kötött, más felhasználó nem fér hozzá.

A 0–4-es értékelő csúszkák 0,01-es lépéssel működnek; az arc szeme/szája interpolálva változik. A tárolt értékek és a személyzeti kijelzés megőrzik a törtrészt. A csökkentett mozgás beállítást tiszteletben tartjuk. A helyi Futura betűtípusok és a címer a `public/assets` könyvtárban maradtak.

## Tesztek

```bash
npm run build:api
npm run build
npm test
npm run test:browser
```

Az API tesztjei és a Playwright tesztek valódi ASP.NET folyamatot és külön, `EtoPortal_Test_…` nevű SQL Server adatbázist használnak; a normál portál adatait nem módosítják. A tesztadatbázis a futás végén törlődik, a nem tesztnevű adatbázis törlését a karbantartó parancs elutasítja. A böngészős teszt alapértelmezése `/usr/bin/chromium`; más telepítésnél `CHROMIUM_PATH` állítható. A `tests/fixture.js` cloud alapértelmezése a `/workspace/.dotnet`, más gépen `DOTNET_ROOT` szükséges.

A tesztek ellenőrzik a szerveroldali jogosultságokat, Józsi több korosztályát, áthelyezést és történetmegőrzést, delegált jogot, valós XLSX/CSV importot és hibás import atomikusságát, pontszámokat, naplók titokmentességét, témákat/ötleteket, hibaképernyőket és a 320/390/430/768/1440 pixeles nézeteket.
