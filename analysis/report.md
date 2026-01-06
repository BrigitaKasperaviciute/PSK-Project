# Integracinių testų generavimo analizė

## Kiekybiniai rodikliai (šio seanso duomenys)
- Iteracijų iki sėkmingo kompiliavimo: ~3 (pirmas bandymas – trūko Program partial, antras – tipų neatitikimai, trečias – OK).
- Iteracijų iki visų testų praeities: ~4 (kompiliavimo pataisymai + papildyti testai, paskutinis `dotnet test` sėkmingas).
- Vidutiniškai sugeneruoti testai praeina iškart: ~60–70% (flakiness nepastebėta, nes klaidos buvo deterministinės – tipų neatitikimai / trūkstami using).
- Pateikus kontekstą, praeinamumas: ~80% (po pridėtų using ir program partial liko tik smulkūs pataisymai).
- Programuotojo intervencijų: ~4–5 minimalios korekcijos (Program partial, using, property pavadinimas, string palyginimas, Http.Json using).
- Testų vykdymo greitis: ~6 s visam suite (18 testų, in-memory DB, WebApplicationFactory).

## Kokybiniai kriterijai
- Funkcionalumų padengimas: 9 pagrindiniai endpointai, po 2 scenarijus (pozityvus/negatyvus). Apima auth, board CRUD dalį, tasks, comments, notifications. Kodų aprėpties metrika neskaičiuota.
- Testų kūrimo paprastumas: su minimaliu kontekstu sugeneruota bazinė infrastruktūra (WebApplicationFactory, in-memory DB, fake auth/email/file). Reikėjo kelių pataisų dėl API tipų ir using.
- Flakiness: nepastebėta – klaidos buvo kompiliavimo/konfigūracijos pobūdžio.
- Mutacijų testavimas: neatliktas (nėra įrankio integracijos šiame seanse).
- Sudėtingesni scenarijai (failų įkėlimas, eksportai): nepilnai dengta; turime tik bazinį upload stub’ą (TestFileService), bet nėra e2e įkėlimo testo.

## Rekomendacijos tobulinimui
- Pridėti kodų aprėpties matavimą (`dotnet test /p:CollectCoverage=true` ar coverlet) ir nustatyti tikslinę % ribą.
- Įtraukti sudėtingesnius scenarijus (failo įkėlimas į `/api/upload/image`, notifikacijų priėmimas/šalinimas, ownership transfer) ir konkurencijos atvejus (optimistic lock).
- Atnaujinti TestAuthHandler į TimeProvider API, kad pašalinti įspėjimus.
- Jei reikia mutacijų testavimo, integruoti Stryker.NET ir įvertinti gyvybingumą.


## Kodo aprėptis ir šakų padengimas gaunamas šia komanda:
dotnet test --collect:"XPlat Code Coverage"