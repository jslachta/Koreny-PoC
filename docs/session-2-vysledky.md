# Session 2: bezeztrátový round-trip — výsledky

Datum: 2026-07-19. Session implementovala architekturu z [audit-gedcom.md](audit-gedcom.md)
sekce 4, kroky 1–4 (GedcomNode strom jako nosič pravdy, doménový model jako projekce,
sync editů do stromu, writer jako serializace stromu). Krok 5 (kódování/ANSEL) sem nepatří.

## Stav testů

**18 zelených / 19 celkem** (1 skip — ANSEL placeholder). Žádný červený.

| Skupina | Před Session 2 | Po Session 2 |
|---|---|---|
| Round-trip corpus-01…05 | 5× červená (specifikace) | **5× zelená** |
| Round-trip corpus-06 (bez/ s BOM) | 2× zelená | 2× zelená |
| Diff self-testy | 4× zelená | 4× zelená |
| GedcomParserTests (původní) | 3× zelená | 3× zelená |
| **Edit-preservation (nové)** | — | **4× zelená** |
| ANSEL placeholder | 1 skip | 1 skip |

Ověřeno: `dotnet test Koreny.Tests/Koreny.Tests.csproj` → `Failed: 0, Passed: 18, Skipped: 1`.
Aplikace `dotnet build Koreny/Koreny.csproj` → 0 warnings, 0 errors.

Nesahal jsem na existující testy, korpus ani `GedcomSemanticDiff.cs` (jediná výjimka viz
Odchylky bod 1). 5 round-trip testů zezelenalo beze změny svého kódu — tím se potvrzuje,
že Session 1 byla korektní specifikací.

## Změněné soubory

| Soubor | Změna | Zdůvodnění |
|---|---|---|
| `Koreny/Models/GedcomNode.cs` | **nový** | surový uzel stromu (Tag/Xref/Value/Children); CONC/CONT už slité do Value |
| `Koreny/Models/GedcomModels.cs` | +`GedcomDocument.Nodes`, +`SourceNode` na INDI/FAM | strom jako nosič pravdy, doménové objekty drží referenci na svůj uzel |
| `Koreny/Services/GedcomParser.cs` | přepis na dvě fáze | fáze 1 řádky→strom (nic se nezahazuje, CONC/CONT slévá jen zde), fáze 2 projekce do modelu; rozpoznávání tagů zachováno včetně „poslední vyhrává" |
| `Koreny/Services/GedcomWriter.cs` | přepis na serializaci stromu | pořadí uzlů = pořadí výstupu (žádné řazení podle ID), lámání CONC/CONT dle 5.5.1, razítko HEAD u importu / syntetická HEAD od nuly |
| `Koreny/Services/GedcomSync.cs` | **nový** | `SyncIndividual`/`SyncFamily`/`Remove*Node` — promítají edity do stromu, cizí sourozenecké uzly nechávají na místě |
| `Koreny/Pages/Index.razor` | 4 volání syncu | SavePerson→SyncIndividual, SaveFamily→SyncFamily, DeletePerson/DeleteFamily→Remove*Node; žádný jiný refactoring UI |
| `Koreny/Services/DraftGedcomBuilder.cs` | **smazán** | mrtvý kód (jen self-reference; ověřeno grepem) |
| `Koreny/Models/Drafts/Individual.cs` | **smazán** | mrtvý kód |
| `Koreny/Models/Drafts/Family.cs` | **smazán** | mrtvý kód |
| `Koreny.Tests/GedcomEditPreservationTests.cs` | **nový** | editační sada a–d |
| `Koreny.Tests/Corpus/Expected/*.ged` | **nové** (4) | ručně psané očekávané výstupy (ne generované writerem) |
| `Koreny.Tests/Koreny.Tests.csproj` | glob `Corpus/**` už kopíruje i `Expected/` | podadresář se do výstupu kopíruje rekurzivně |
| `Koreny.Tests/GedcomRoundTripTests.cs` | oprava 1 řádku | viz Odchylky bod 2 |

Mrtvý kód smazán jako **samostatný commit** (`git rm` tří souborů) s odůvodněním v commit
message; zbytek Session 2 zůstává nezacommitovaný pro review, stejně jako Session 1.

## Jak architektura plní round-trip

Import: text → `BuildTree` → `doc.Nodes` (úplný strom, nic nezahozeno) → `Project` naplní
`Individuals`/`Families` jako projekci. Export: `GedcomWriter` serializuje `doc.Nodes`, ne
doménový model — u neupraveného importu je výstup re-serializací původního stromu, tedy
sémanticky identický. Edit: `GedcomSync` mutuje uzly ve stromu (jen známé tagy), sourozenecké
neznámé uzly zůstávají na místě → další export je nese dál.

Klíčová rozhodnutí syncu (v komentářích `GedcomSync.cs`):
- Známé vícenásobné tagy (NAME, BIRT, DEAT, MARR) se synchronizují na **posledním** výskytu —
  shodně s tím, jak je čte projekce („poslední vyhrává"). Neupravený vícenásobný záznam se tak
  přepíše sám na sebe a další výskyty zůstanou (viz edit-b: druhé NAME „Josef /Malovaný/" přežije).
- Prázdná událost bez data i místa (`BIRT Y`) se **zachová i s hodnotou** — maže se jen tehdy,
  když událost dostane DATE/PLAC (viz edit-b: `BIRT Y` zůstává, `DEAT Y` → `DEAT`/`DATE`).
- Smazání osoby odstraní **jen** uzel INDI; reference HUSB/WIFE/CHIL v rodinách se záměrně
  neuklízejí (viz edit-d: `@F2@` si drží `WIFE @I4@`).

## Odchylky od zadání

1. **Oprava jednoho řádku v `GedcomRoundTripTests.cs`.** Řádek 46 přišel do session
   poškozený — před signaturu metody `RoundTrip_Corpus01_MyHeritage` byl vložen token
   `8905221`, což je syntaktická chyba bránící kompilaci celého testovacího projektu (a tedy
   běhu všech testů). Odstranil jsem pouze tento token; logika ani assert testu se nezměnily
   a řádek je teď identický s tím, co vyprodukovala Session 1. Nejde o „opravu testu, protože
   je chybný" (to zadání zakazuje), ale o odstranění mechanické korupce, bez níž nelze splnit
   kritérium hotovosti. Zadání zakazuje měnit testy — proto to uvádím zde explicitně k review.

2. **Osiřelý `Koreny/Models/SexType.cs`.** Zadání povolilo smazat `DraftGedcomBuilder`
   a `Models/Drafts/*`. Po jejich smazání zůstává enum `SexType` bez jediného použití
   (dřív ho používal jen `DraftGedcomBuilder` a `Drafts/Individual`). `SexType.cs` NENÍ pod
   `Models/Drafts/`, takže spadá mimo výslovně povolený rozsah smazání — nechal jsem ho být.
   Doporučení pro review: smazat i `SexType.cs` (je nyní mrtvý). Nezpůsobuje warning ani chybu.

3. **Edit-testy jdou mimo UI formulář, ne přes `ApplyPersonForm`.** Zadání popisuje operace
   jako „změň jméno / přidej datum úmrtí". Formulář v `Index.razor` je ale sám ztrátový
   (data ukládá jako rok místo plného řetězce, u více jmen bere poslední). Kdybych testy hnal
   přes něj, „změna jména" by zároveň degradovala `BIRT 3 MAR 1901` na `1901` a test „vše
   ostatní beze změny" by nemohl projít. Testy proto volají `GedcomSync` přímo nad doménovou
   úpravou — testují kontrakt syncu, ne omezení formuláře. Sync sám plné datum zachovává;
   ztrátovost formuláře je pre-existující vlastnost UI, kterou tato session nemění a
   nezhoršuje (i původní writer psal z domény, kam formulář ukládal rok). Není to odchylka
   od implementačního zadání, jen upřesnění metodiky testů.

Jiné odchylky: žádné.
