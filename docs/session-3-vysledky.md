# Session 3: levné opravy rendereru — výsledky

Datum: 2026-07-19. Cíl: levné opravy z [audit-renderer.md](audit-renderer.md), sekce 4
(řádky malý / malý–střední) — dictionary index, vendorovaný Dagre, kolize párů —
plus drobná oprava rekonstrukce jména z 2b. Žádná náhrada knihoven ani přestavba layoutu
(to je Session 4).

## Vstupní stav a větev

Práce na **nové větvi `renderer-cheap-fixes`** stacknuté na `gedcom-lossless-roundtrip`.
Nový branch přímo z `main` nešel: item 1 rozšiřuje `GedcomDocument` a item 4 upravuje
`PersonFormState`, které existují až od Session 2/2b (nejsou na `main`). Renderer PR proto
sedí nad GEDCOM PR; po jeho mergnutí lze rebasovat na `main`. Pracovní strom byl na vstupu
čistý (žádný „WIP" commit nebyl potřeba).

## Stav testů

**27 zelených / 28 celkem** (1 skip — ANSEL placeholder). Žádný červený.
`dotnet build` (celé řešení) 0 warnings.

Přírůstek oproti 2b: +2 testy (`DescendantLayoutTests`: kolize párů, bezdětný pár).
Idempotence i round-trip testy zůstávají zelené.

## Commity (logické celky)

| Commit | Obsah |
|---|---|
| `fdac7f9` FIX: reconstruct edited name… | item 4 — rekonstrukce jména `Jméno /Příjmení/` |
| `66d1481` PERF: O(1) dictionary lookups… | item 1 — index na `GedcomDocument` |
| `130d570` BUILD: vendor Dagre locally… | item 2 — vendorovaný Dagre |
| `e15b7e6` FIX: DescendantTree pairs no longer overlap… | item 3 — kolize párů + extrakce + test |

## Změněné soubory

| Soubor | Změna |
|---|---|
| `Koreny/Services/PersonFormState.cs` | rekonstrukce jména při změně: `"/Novák/ Jan"` → `"Jan /Novák/"` |
| `Koreny/Models/GedcomModels.cs` | lazy indexy `FindIndividual`/`FindFamily`/`FindFamilyAsChild` + `InvalidateLookups` |
| `Koreny/Components/HourglassTree.razor` | volá index dokumentu; lokální lineární `FindIndividual`/`FindFamilyAsChild` odstraněny |
| `Koreny/Components/DescendantTree.razor` | volá index; layout přesunut do `DescendantLayout`, zbývá jen prezentace (SVG) |
| `Koreny/Pages/Index.razor` | `InvalidateLookups()` po přidání/editaci/smazání osoby i rodiny |
| `Koreny/Services/DescendantLayout.cs` | **nový** — čistý layout potomků (strom, sloty s opravou kolize, geometrie) |
| `Koreny/wwwroot/lib/dagre/dagre.min.js` + `LICENSE` | **nové** — vendorovaný Dagre 2.0.0 (MIT) |
| `Koreny/wwwroot/index.html` | Dagre z `lib/dagre/` místo CDN |
| `Koreny.Tests/DescendantLayoutTests.cs` | **nový** — výpočtový test kolize párů |
| `Koreny.Tests/GedcomEditPreservationTests.cs` | 1 assertion aktualizována na nový formát jména |

## Detaily k jednotlivým položkám

### Item 1 — dictionary index (audit 3.4)

`GedcomDocument` dostal tři lazy slovníky (osoba podle ID, rodina podle ID, první rodina, kde
je osoba dítětem) s `InvalidateLookups()`. Sémantika „první match v pořadí souboru" zachována
přes `TryAdd`. Nahrazená volání:
- `HourglassTree.razor`: `_root`, otec/matka, 4 prarodiče, děti (řádky ~114–149) → `Document.FindIndividual`/`Document.FindFamilyAsChild`.
- `DescendantTree.razor` / `DescendantLayout.cs`: `MakeBox` a validace rootu → `document.FindIndividual`.
- **FullGraph.razor**: žádné takové průchody nemá (staví přímo z kolekcí a pozičního slovníku),
  takže beze změny. „Všechny tři komponenty" jsou tím pokryté.

Invalidace: `Index.razor` volá `InvalidateLookups()` v `SavePerson`, `DeletePerson`,
`SaveFamily`, `DeleteFamily`. Reparse vytváří nový dokument (index přirozeně čerstvý).

### Item 2 — vendorovaný Dagre

`@dagrejs/dagre@2.0.0` staženo do `Koreny/wwwroot/lib/dagre/dagre.min.js` (+ MIT `LICENSE`),
`index.html` odkazuje lokálně. **Ověřeno v prohlížeči** (spuštěná app): `dagre.min.js` se načítá
z `/lib/dagre/` (žádný požadavek na CDN), globální `dagre` i `dagre.graphlib.Graph` jsou k dispozici
a `KorenyDagre.computeLayout` vrátí korektní vrstvený layout. V `index.html` **nezbyl žádný jiný
externí/CDN odkaz** (Bootstrap se načítá lokálně z `lib/bootstrap`).

### Item 3 — kolize párů (audit 3.3)

Layout potomků extrahován z `DescendantTree.razor` do čisté třídy `DescendantLayout` (strom,
přiřazení slotů, geometrie). Oprava: každý pár má slot-rozsah šířky ≥ 2 sloty; když je podstrom
užší (jediné dítě-list), rezervuje se další slot. Rozšíření se propaguje nahoru přes stávající
roll-up min/max rozsahů dětí.

Test `DescendantLayoutTests.AdjacentPairs_DoNotOverlap`: dva sousední páry, každý s jedním
dítětem — středy jsou nově **368 px** od sebe (= 344 dvojbox + 24 mezera), tj. ≥ šířka dvojboxu;
před opravou to bylo 184 px (překryv). Kontrolní test: bezdětný pár rezervuje 2 sloty.

### Item 4 — rekonstrukce jména

`PersonFormState.ApplyTo` stavěla při změně jména `"/Novák/ Jan"` (příjmení-first). Opraveno na
konvenční `"Jan /Novák/"` (příjmení v lomítkách). Prázdné příjmení → jen křestní jméno.

## Odchylky od zadání

1. **Úprava existujícího testu byla nutná i mimo edit-test (a).** Zadání povolilo jako jedinou
   změnu existujícího testu očekávaný soubor edit-testu (a). Ukázalo se ale, že **(a) formát
   `ApplyTo` vůbec netestuje** — konstruuje `GedcomName` přímo (`Raw = "/Novák/ Honza"`), takže
   se opravou item 4 nerozbije a jeho očekávaný soubor jsem neměnil (zůstává zelený). Jediný test,
   který skutečně závisí na rekonstrukci z `ApplyTo`, je **`Edit_RenameKeepsFullBirthDate` (f)**;
   u něj jsem aktualizoval assertion na `"Honza /Novák/"`. To je záměrná změna specifikace formátu
   (item 4) a je to jediná dotčená assertion. `edit-c` také konstruuje `Raw` přímo, takže rovněž
   beze změny.
2. **Pan/zoom (audit tabulka, malý–střední) NENÍ součástí této session.** Není v číslovaném
   zadání (1–4) a je nejnáročnější z „levných" úprav (nahrazení `<img>` overlayů živým SVG +
   JS interop). Nechávám ho jako samostatný follow-up — flaguji explicitně, ať je jasné, že
   řádek „Pan/zoom" z tabulky zůstává neudělaný.

Jiné odchylky: žádné.
