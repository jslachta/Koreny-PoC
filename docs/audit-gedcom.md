# Audit: GEDCOM import/export

Stav k commitu `d8c30bc` (2026-07-19). Zjištění vycházejí ze čtení kódu; kde je závěr odvozen
bez spuštění, je označen jako **domněnka**.

## 1. Architektura parseru

**Parser čte přímo do doménového modelu, žádná raw struktura neexistuje.**

- Vstupní bod: `GedcomParser.Parse` (`Koreny/Services/GedcomParser.cs:10–27`) — jednoprůchodový
  řádkový parser. Každý řádek rozloží `TryParseLine` (řádky 223–312) na `level / xref / tag / value`.
- Kontext drží zásobník `ContextFrame` (řádky 7, 335–351) s pěti druhy rámců:
  `Individual, Family, Birth, Death, Marriage` — a šestým druhem **`Skip`** (řádek 327), do kterého
  padá všechno ostatní. `PopUntil` (řádky 29–35) zásobník ořezává podle levelu řádku.
- Cílová struktura je rovnou doménový model `GedcomDocument` → `GedcomIndividual` / `GedcomFamily`
  (`Koreny/Models/GedcomModels.cs:6–91`). Model je záměrně úzký: jméno, pohlaví, narození, úmrtí,
  poznámky; rodina má HUSB/WIFE/CHIL/MARR/NOTE.
- Na levelu 0 se rozpoznávají jen `INDI` a `FAM` (řádky 46–60); **všechny ostatní záznamy —
  `HEAD`, `SUBM`, `SOUR`, `REPO`, `OBJE`, samostatné `NOTE` záznamy, `TRLR` — se pohltí Skip rámcem**
  (řádky 62–64) a zahodí.
- Kódování se neřeší v parseru, ale při načtení souboru: `Index.razor:943–945` čte soubor natvrdo
  jako UTF-8 (`new StreamReader(stream, Encoding.UTF8)`).

Vedle toho existuje `Koreny/Services/DraftGedcomBuilder.cs` a modely `Koreny/Models/Drafts/*` —
mapování „draft" osob/rodin na `GedcomDocument`. Podle grepu na ně **nikdo neodkazuje**
(reference jen uvnitř vlastních souborů); **domněnka**: pozůstatek starší verze editoru,
dnes mrtvý kód.

Testy: `Koreny.Tests/GedcomParserTests.cs` pokrývají happy-path parsování INDI/FAM
(minimální GEDCOM, řádky 10–60+); round-trip test import→export neexistuje.

## 2. Co se při importu ZAHAZUJE nebo ztrácí

### 2.1 Mechanismus ztráty

Cokoli, co nemá explicitní `if (tag == …)` větev, skončí ve Skip rámci:

- neznámý tag pod INDI → `HandleIndividualLine`, řádek 132,
- neznámý tag pod BIRT/DEAT → `HandleLifeEventLine`, řádek 150,
- neznámý tag pod FAM → `HandleFamilyLine`, řádek 192,
- neznámý tag pod MARR → `HandleMarriageLine`, řádek 210,
- neznámý záznam na levelu 0 → `ProcessLine`, řádek 63.

Skip rámec pohltí i celý podstrom (řádky 89–92). Data se nikam neukládají — ztráta je úplná a tichá.

### 2.2 Konkrétní výčet ztrát

**Neznámé a proprietární tagy.** `_MHID`, `_APID`, `_UID`, `RIN`, `CHAN`, `REFN`, `RFN` a
jakýkoli jiný `_*` tag → Skip. Totéž platí pro standardní, ale neimplementované struktury:
všechny události kromě BIRT/DEAT/MARR (`CHR`, `BURI`, `CREM`, `DIV`, `ENGA`, `EVEN`…), atributy
(`OCCU`, `RESI`, `RELI`, `EDUC`…), citace zdrojů (`SOUR` + `PAGE`/`QUAY`), média (`OBJE`),
`FAMC`/`FAMS` na INDI (vazby se při zobrazení rekonstruují z FAM záznamů — směr FAM→osoba přežije,
ale kvalifikátory `PEDI`, `_FREL`/`_MREL`, adopce zaniknou), podtagy jména (`GIVN`, `SURN`, `NPFX`,
`NICK`, `TYPE`) — jméno se parsuje jen z hodnoty řádku NAME (`GedcomNameParser.Parse`, řádky 354–380).

**Vícenásobné hodnoty přepisem.** Druhé `NAME` přepíše první (`indi.Name = …`, řádek 100);
druhé `BIRT`/`DEAT`/`MARR` přepíše první (řádky 113, 121, 181). Zůstává **poslední**, dřívější
se ztratí. U `BIRT`/`DEAT` se navíc ignoruje hodnota na samotném řádku — `1 BIRT Y`
(potvrzení „událost nastala" bez data) se ztratí úplně, protože event bez DATE/PLAC writer
nevypíše (`GedcomWriter.cs:108–111`).

**CONC/CONT lámání — vůbec neimplementováno.** V celém repozitáři se řetězce `CONC`/`CONT`
nevyskytují (ověřeno grepem). Poznámka `1 NOTE první část` + `2 CONC pokračování` dopadne takto:
řádek NOTE se uloží (řádek 128, bez push rámce), řádek CONC na levelu 2 dorazí do
`HandleIndividualLine` (na vrcholu zásobníku je stále rámec Individual) a spadne do Skip →
**poznámka je tiše oříznuta na první fyzický řádek**. Totéž pro CONT (víceřádkové poznámky).
Navíc pointer forma `1 NOTE @N1@` se uloží jako doslovný text „@N1@" — a při exportu se vypíše
`1 NOTE @N1@`, což jiný software přečte jako odkaz na neexistující záznam. To už není jen ztráta,
ale **aktivní poškození sémantiky**.

**Kódování.** `Index.razor:944` dekóduje vždy UTF-8; hlavička `CHAR` se nikdy nečte (parser HEAD
zahazuje). Soubor v **ANSEL** (běžný u GEDCOM 5.5 z legacy softwaru) se rozpadne na mojibake —
česká diakritika je v ANSEL kódovaná kombinujícími znaky, výsledek bude nečitelný. UTF-16 s BOM
projde jen díky tomu, že `StreamReader` má implicitně zapnutou detekci BOM (**domněnka** — jde
o výchozí chování daného konstruktoru .NET, v kódu to není explicitně).

**Verze GEDCOM.** `VERS` pod HEAD se zahazuje; parser se chová identicky k 5.5, 5.5.1 i 7.0.
Prakticky: základní INDI/FAM řádky projdou ze všech verzí, ale GEDCOM 7.0 specifika se ignorují
nebo poškodí — zdvojené `@@` v hodnotách se nedekódují, `SNOTE` odkazy a nové struktury spadnou
do Skip. Naopak parser je tolerantní k odsazeným řádkům (`line.Trim()`,
`GedcomParser.cs:230`), což 5.5.1 formálně zakazuje — pro import je to výhoda.

**Pořadí.** Pořadí záznamů v souboru se neuchovává — writer řadí osoby i rodiny podle ID
ordinálně (`GedcomWriter.cs:15, 20`), takže `I2` předběhne `I10`. Pořadí podtagů uvnitř záznamu
je dané napevno writerem (NAME, SEX, BIRT, DEAT, NOTE), původní pořadí zaniká. Pořadí `CHIL`
uvnitř rodiny se zachovává (list, `GedcomModels.cs:88`) — jediné pořadí, které přežije.

## 3. Export

**Export se generuje čistě z doménového modelu, nic „zachovaného" neexistuje** —
`GedcomWriter.Write` (`Koreny/Services/GedcomWriter.cs:10–27`) projde `doc.Individuals` a
`doc.Families` a vypíše je. Hlavička je syntetická (`WriteHead`, řádky 29–38): `SOUR Koreny`,
`GEDC/VERS 5.5.1`, `FORM Lineage-Linked`, `CHAR UTF-8`. Původní hlavička (autor, datum, jazyk,
`SUBM`) se nepřenáší, protože ji parser zahodil.

Detaily:

- `NAME` se vypisuje z `Name.Raw` (řádek 45) — importovaná jména round-tripují textově přesně.
  Ale osoby editované v UI dostanou `Raw = "/Příjmení/ Jméno"` (`Index.razor:688`,
  `DraftGedcomBuilder.cs:33`) — příjmení **před** křestním jménem. Je to parsovatelné (lomítka
  vymezují příjmení), ale nekonvenční; jiný software může zobrazit „Novák Jan" divně.
  **Domněnka**: jde o nezáměr, ne o volbu.
- `NOTE` se vypisuje jako jediný řádek bez CONC lámání (řádky 56–62). GEDCOM 5.5.1 limituje
  fyzický řádek na 255 znaků — dlouhá poznámka z UI vyprodukuje nevalidní řádek (**domněnka**
  co do toho, jak přísně to čtečky vymáhají; porušení specifikace je jisté).
- Konce řádků jsou `\n` (řádky 130, 144); spec připouští CR/LF/CRLF, většina čteček si poradí.

### Co by dnes ztratil round-trip import → export

Vezmeme-li reálný GEDCOM z MyHeritage/Ancestry a proženeme ho aplikací:

1. celá hlavička HEAD (zdroj, datum, SUBM, jazyk) — nahrazena syntetickou,
2. všechny záznamy SOUR/REPO/OBJE/NOTE(top-level)/SUBM,
3. u osob: všechny události kromě narození a úmrtí, povolání a atributy, citace zdrojů, média,
   druhá a další jména, podtagy jmen, `FAMC`/`FAMS` (writer je ani negeneruje — **výstup tak
   porušuje lineage-linked strukturu 5.5.1, kde INDI má mít FAMS/FAMC odkazy**; většina čteček
   si vazby odvodí z FAM, ale validátory to ohlásí — druhá půlka věty je **domněnka**),
4. u rodin: rozvody, zásnuby, druhé a další MARR, citace,
5. všechny proprietární tagy (`_MHID`, `_APID`, …),
6. víceřádkové poznámky ořezané na první řádek; NOTE-pointery proměněné v rozbité odkazy,
7. `1 BIRT Y` / `1 DEAT Y` (událost bez data),
8. původní pořadí záznamů i podtagů,
9. u ANSEL vstupu veškerou diakritiku (poškozeno už při čtení).

Přežije: xref ID osob a rodin (beze změny formátu), NAME (textově), SEX, BIRT/DEAT s DATE+PLAC
(datum jako surový string, tj. `ABT 1900` apod. projde beze změny — `GedcomEvent.Date` je string,
`GedcomModels.cs:64`), HUSB/WIFE/CHIL vazby včetně pořadí dětí, MARR s DATE+PLAC, jednořádkové NOTE.

## 4. Minimální architektonická změna pro bezeztrátový round-trip

Cíl: **sémantická ekvivalence** import→export (ne bajtová identita — pořadí konců řádků či
přesné lámání CONC se smí lišit, obsah ne).

### Princip: raw strom jako nosič pravdy, doménový model jako projekce

1. **Zavést `GedcomNode`** — obecný uzel řádku:

   ```csharp
   class GedcomNode {
       int Level; string Tag; string? Xref; string Value;
       List<GedcomNode> Children;
   }
   ```

   Parser se rozdělí na dvě fáze. Fáze 1 staví z řádků strom `List<GedcomNode>` (záznamy
   levelu 0 včetně HEAD, SUBM, SOUR, TRLR — nic se nezahazuje). Zásobníková logika už existuje
   (`GedcomParser.cs:29–35, PopUntil`), jen místo typovaných rámců drží `GedcomNode`; je to
   zjednodušení dnešního kódu, ne komplikace. Fáze 1 zároveň **jediná** řeší CONC/CONT: při čtení
   je slije do `Value` (logická hodnota), při zápisu se hodnota zpětně rozláme. Uzly si CONC/CONT
   jako děti nenesou.

2. **Doménový model se stane projekcí.** Fáze 2 = dnešní `ProcessLine`/`Handle*` logika
   přepsaná na průchod stromem: z uzlů `INDI`/`FAM` vytáhne to, co dnes (NAME, SEX, BIRT, DEAT,
   MARR, vazby, NOTE). Každý `GedcomIndividual`/`GedcomFamily` si podrží referenci na svůj
   `GedcomNode` (`public GedcomNode? SourceNode`), `GedcomDocument` podrží celý seznam top-level
   uzlů (HEAD, neznámé záznamy, TRLR) v původním pořadí.

3. **Editace = zápis zpět do uzlu.** `SavePerson`/`SaveFamily` (`Index.razor:651, 839`) po úpravě
   doménového objektu promítne známé tagy do `SourceNode`: přepíše/vytvoří/odstraní jen uzly
   NAME, SEX, BIRT/DATE/PLAC, DEAT/…, HUSB/WIFE/CHIL/MARR, NOTE — **sourozenecké neznámé uzly
   (`_MHID`, SOUR citace, OCCU…) zůstanou nedotčené na svém místě**. Nová osoba dostane čerstvý
   uzel. Prakticky jde o jednu metodu `SyncKnownTags(GedcomNode, GedcomIndividual)` — zrcadlo
   dnešního `Handle*`.

4. **Export = serializace stromu.** `GedcomWriter` se zredukuje na rekurzivní výpis uzlů
   (level, xref, tag, value + rozlámání dlouhých hodnot na CONC/CONT). Původní HEAD se zachová
   (volitelně s aktualizací `SOUR`/`CHAR`), pořadí záznamů i podtagů je pořadím uzlů — tedy
   původním. Dnešní syntetická hlavička zůstane jen pro dokumenty vytvořené od nuly v UI.

5. **Kódování (menší, oddělený krok):** načítat soubor jako `byte[]`, nasniffovat BOM a řádek
   `1 CHAR` z prvních ~kB, podle něj zvolit dekodér; pro ANSEL buď převodní tabulka
   (~1 stránka kódu, dobře dokumentovaná), nebo aspoň explicitní chybová hláška místo tichého
   mojibake. Zápis: `CHAR UTF-8` + skutečné UTF-8, jako dnes.

### Proč je to minimální

- Parser fáze 1 je **obecnější a kratší** než dnešní typovaný zásobník; fáze 2 recykluje
  existující rozpoznávání tagů. Writer se zjednoduší (výpis stromu místo ručních `WriteIndividual`).
- Doménový model, komponenty rendereru ani UI formuláře se **nemění vůbec** — dál pracují
  s `GedcomIndividual`/`GedcomFamily`; přibude jen neviditelná reference na `SourceNode`.
- Jediné netriviální místo je bod 3 (sync editů do uzlu) — a ten je ohraničený množinou tagů,
  které UI umí editovat (dnes: jméno, pohlaví, 2 události, poznámka, vazby rodiny).

Tím se round-trip ztráty zredukují z bodů 1–9 výše na nulu na úrovni sémantické ekvivalence;
jediné vědomé odchylky budou normalizace CONC lámání a konců řádků.
