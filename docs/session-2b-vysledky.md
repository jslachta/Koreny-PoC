# Session 2b: odstranění ztrátovosti editačního formuláře — výsledky

Datum: 2026-07-19. Cíl: uložení osoby/rodiny **beze změny** nesmí změnit exportovaný GEDCOM
(idempotence uložení). Navazuje na [session-2-vysledky.md](session-2-vysledky.md), odchylku 3
(formulář ukládal datum jako rok a u více jmen držel jen poslední).

## Vstupní stav

Pracovní strom byl na začátku session **čistý** (větev `gedcom-lossless-roundtrip`, veškerá
práce Session 1/2 už zacommitovaná), takže úvodní „WIP: stav před session" commit nebyl
potřeba. Práce pokračuje na téže větvi — 2b dokončuje tentýž příběh bezeztrátovosti a spadá
do stejného PR.

## Stav testů

**25 zelených / 26 celkem** (1 skip — ANSEL placeholder). Žádný červený.

| Skupina | Před 2b | Po 2b |
|---|---|---|
| Round-trip (corpus 01–06, vč. BOM) | 7 | 7 |
| Diff self-testy | 4 | 4 |
| GedcomParserTests | 3 | 3 |
| Edit-preservation a–d | 4 | 4 |
| **Idempotence (e) — 6 korpusů** | — | **6** |
| **Rename drží plné datum (f)** | — | **1** |
| ANSEL placeholder | skip | skip |

Ověřeno: `dotnet test` → `Passed: 25, Skipped: 1, Failed: 0`; `dotnet build Koreny` → 0 warnings.

Test (e) je hlavní důkaz: pro každý korpus otevře a uloží **beze změny** každou osobu i rodinu
toutéž cestou jako UI (`Fill` → `ApplyTo` → `GedcomSync`) a ověří, že export je sémanticky
totožný s importem. Prochází i corpus-01 (plné datum `3 MAR 1901`), corpus-04 (`BIRT Y`, dvě
NAME) a corpus-03 (tři NOTE u jedné osoby) — přesně případy, na kterých starý formulář ztrácel.

## Změněné soubory

| Soubor | Změna | Zdůvodnění |
|---|---|---|
| `Koreny/Services/PersonFormState.cs` | **nový** | testovatelný stav formuláře osoby; surové řetězce, zachování nezeditovaných dat (formát jména, nestandardní pohlaví, `BIRT Y`, druhé+další NOTE) |
| `Koreny/Services/FamilyFormState.cs` | **nový** | testovatelný stav formuláře rodiny; surové datum sňatku |
| `Koreny/Services/GedcomFormValues.cs` | **nový** | sdílené převody (surové datum → událost, prázdno → null) |
| `Koreny/Pages/Index.razor` | binding na `_personForm`/`_familyForm`, `ApplyPersonForm`/`ClearPersonForm` odstraněny, `SavePerson`/`SaveFamily`/`Open*ForEdit` volají `Fill`/`ApplyTo` | přesun ztrátové logiky do testovatelných tříd; vstupy data `type=number`→text s relabelem „Rok…"→„Datum…" |
| `Koreny/Models/SexType.cs` | **smazán** (samostatný commit) | osiřelý po Session 2 |
| `Koreny.Tests/GedcomEditPreservationTests.cs` | +testy (e), (f) | idempotence + rename-drží-datum |

Jádro round-tripu (`GedcomNode`, parser, writer, `GedcomSync`) se **nezměnilo** — ztráta
byla čistě v UI vrstvě, jak audit předpokládal.

## Jak je idempotence zajištěna

`PersonFormState`/`FamilyFormState` drží data jako surové řetězce a nesou i to, co formulář
needituje, takže `Fill` následované `ApplyTo` je sémantická identita:
- **Datum** = surový řetězec (`"3 MAR 1901"`, `"ABT 1900"`), žádné parsování na rok. Bez validace.
- **Jméno**: při nezměněném křestním jménu i příjmení se zachová původní surový `Raw`
  (formát `"Jan /Novák/"` i `"/Novák/ Jan"`); rekonstrukce `"/{příjmení}/ {jméno}"` proběhne
  jen při skutečné změně (test f).
- **Pohlaví**: nezměněný dropdown zachová původní řetězec (i nestandardní `U`).
- **Poznámky**: edituje se první, druhá a další se nesou beze změny.
- **Prázdná událost** (`BIRT Y`): zachová se; datum ji nmaže jen tehdy, když dostane DATE/PLAC.
- **Děti rodiny**: `GedcomSync.SyncChildren` drží pořadí existujících CHIL uzlů i s podtagy
  (`_FREL`), takže pořadí přežije nezávisle na pořadí výběru v UI.

## Odchylky od zadání

Od implementačního zadání **žádné**. Dvě vědomé změny chování, které stojí za zmínku k review:

1. **Práce pokračuje na větvi `gedcom-lossless-roundtrip`**, ne na nové větvi — 2b je dokončení
   téhož příběhu a patří do stejné PR. Cleanup (`SexType`) a implementace+testy jsou dva
   samostatné commity dle zadání.
2. **Události jsou „present-sticky":** když u existující události (např. narození s datem)
   uživatel vymaže datum i místo, zůstane prázdná událost místo jejího odstranění (dřív se
   událost odstranila). Je to důsledek zachování `BIRT Y` a preferuje nezničit data; UI dnes
   nemá explicitní tlačítko „odebrat událost". Idempotence tím není dotčena (beze změny se
   nic nemaže). Případné budoucí „odebrat událost" je samostatná úloha.

Drobnost: datum sňatku je převedeno na surový řetězec, ačkoli všechna sňatková data v korpusu
jsou jen roky (idempotence by u nich prošla i beze změny) — jde o obecnou správnost, aby
plné datum sňatku (`"15 JUN 1925"`) nedegradovalo.
