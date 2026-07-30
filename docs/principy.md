# Kořeny — principy

Kořeny jsou pokus o aplikaci **bezpečnou z principu**: rizika se neřeší opatřeními navrch,
ale konstrukcí — celé třídy problémů nemohou nastat, protože nemají kde vzniknout.
Genealogická data jsou přitom mimořádně citlivá: vztahy, žijící osoby, data narození,
poznámky o zdraví či původu.

Tento dokument je závazný pro další vývoj. Každá změna se dá měřit otázkou
**„neporušuje to některý princip?"** — stejně jako se dnes měří „je round-trip pořád čistý?".

## 1. Data neopouštějí prohlížeč

Není server, databáze, účet ani telemetrie. Data nelze ukrást z databáze, protože žádná není;
nelze sledovat, kdo aplikaci používá, protože není komu hlásit.

*V kódu:* Blazor WebAssembly, statický hosting. Žádné HTTP volání na cizí původ.
*Ověřuje se:* každý manuální checklist měří `performance.getEntriesByType('resource')` —
požadavků mimo `localhost` musí být **0**.

## 2. Žádné běhové závislosti mimo repozitář

Co je v repu, to běží. Nic se nedotahuje z CDN za běhu — ani knihovna, ani font, ani ikona.
Výpadek či kompromitace cizí sítě nemůže změnit chování aplikace.

*V kódu:* d3 a Topola jsou vendorované včetně licencí a pinovaných verzí; Topola bundle má
v `lib/topola/README.md` zapsaný sha256 a dva příkazy na přegenerování. Bundling je **autorský**
krok, ne podmínka běhu — `dotnet run` stačí. Fonty jsou systémové.

## 3. Uživatel může kdykoli odejít se vším

Formát je otevřený (GEDCOM 5.5.1) a export je **bezeztrátový na úrovni sémantické ekvivalence**.
Aplikace není past: co do ní vložíš, to z ní dostaneš zpět — včetně věcí, kterým sama nerozumí.

*V kódu:* surový strom `GedcomNode` je nosič pravdy, doménový model je jen jeho projekce.
Neznámé a proprietární tagy (`_MHID`, `_APID`, citace, víceřádkové poznámky) přežijí editaci
i uložení, protože se nikdy nezahazují.
*Ověřuje se:* korpus + sémantický diff (`GedcomSemanticDiff`), round-trip testy a testy
idempotence uložení.

## 4. Transakční model: soubor je databáze

Aplikace je **bezstavová editační seance nad souborem**:

| Krok | Operace |
|---|---|
| `begin` | Načíst GEDCOM (nebo Vzorová rodina / Nový rodokmen) |
| rozpracovaná transakce | editace v prohlížeči, nikam se neukládají |
| `commit` | Uložit GEDCOM — zápis do souboru |
| verzování | na uživateli: vlastní soubor, klidně vlastní `git` |

Uživatel drží transakci vědomě v ruce. Aplikace mu ji nesmí vzít, ale ani za něj potvrdit.

*Důsledek:* **autosave by byl porušením principu, ne jeho naplněním.** Vytvořil by druhý,
skrytý zdroj pravdy v prohlížeči — na sdíleném počítači navíc citlivá data, o kterých uživatel
neví — a rozmazal by transakční hranici.

*Co z modelu naopak plyne:*
- **Idempotence commitu** — „načíst → uložit beze změny" je sémantická identita, takže cyklus
  lze opakovat bez degradace dat (testy `Edit_SaveUnchanged_IsIdempotent`).
- **Minimální diffy** — writer zachovává pořadí záznamů a sahá jen na editované uzly, takže
  `git diff` nad vlastním rodokmenem ukáže tři řádky, ne přegenerovaný soubor.
- **Rozpracovaná transakce musí být chráněná a viditelná** (ne perzistovaná): tečka v titulku,
  štítek *Neuloženo*, zvýrazněné „Uložit GEDCOM" a `beforeunload` varování při zavření záložky.

## 5. Nikdy nevydat poškozená data

Commit musí být vždy konzistentní — uživatel si ho může verzovat a předat jinému softwaru.
Tiché ořezání, rozbitý odkaz nebo neplatný atribut jsou bezpečnostní selhání v ose *safety*,
i když nikdo nic „nehackl".

*V kódu:* rozměry SVG se formátují invariantní kulturou (desetinná čárka by rozbila atribut);
writer láme dlouhé hodnoty dle 5.5.1; parser je tolerantní a nepadá. Smazání osoby či rodiny
odstraní i **všechny ukazatele na ně** (`HUSB`/`WIFE`/`CHIL`, `FAMS`/`FAMC` i další), takže
commit nikdy neobsahuje odkaz na neexistující záznam.

*Hranice:* uklízí se výhradně po vlastním mazání. Odkaz rozbitý už v importu se zachová —
uložení beze změny nesmí měnit cizí soubor (princip 3 a idempotence commitu).

## 6. Invarianty žijí v doméně, ne v UI

Pravidlo, které platí jen dokud se uživatel drží cesty vytyčené UI, není pravidlo.

*V kódu:* `RelationshipRules` brání vzniku cyklů (dítě nesmí být rodič ani jeho předek) a
kontrola je i v `SaveFamily`, nejen ve výběru v seznamu. Načtení souboru, vzorová rodina
i nový prázdný dokument sdílejí jediný `ApplyDocument`, aby žádný vstupní bod nemohl
zapomenout vynulovat stav.

## 7. Destruktivní operace se ptají a nepřekvapí

Cokoli, co zahazuje práci, se ptá předem a v otázce říká, co přesně se ztratí.

*V kódu:* „Nový rodokmen" i „Vzorová rodina" rozlišují, zda jsou změny neuložené (nenávratně)
nebo jen načtené ze souboru (lze načíst znovu). Zrušení dialogu nemění nic.

## 8. Jediný nedůvěryhodný vstup je soubor

Bez serveru se hranice důvěry přesouvá na parser: nahraný GEDCOM je jediné místo, kudy do
aplikace vstupují cizí data. Podle toho se k němu chováme.

*V kódu:* parser nepadá na neznámých či poškozených konstrukcích, čtení má limit velikosti,
výpočty nad grafem (předci) si drží množinu navštívených uzlů, takže cyklus v datech je
nezacyklí. Text z dokumentu se do HTML vkládá escapovaný.

---

## Známé dluhy vůči principům

Poctivý seznam míst, kde aplikace vlastní principy zatím neplní:

1. **Parser nebyl systematicky trápen zlomyslným vstupem** (princip 8). Chybí testy pro
   extrémní hloubku vnoření, obří počet záznamů a patologické `CONC`/`CONT` řetězení.
2. **`MarkupString` ve stavovém řádku** (princip 8). Dnes se jméno escapuje ručně a je to
   pokryté, ale je to typ místa, kde díra vznikne příští nevinnou editací. Kandidát na
   nahrazení běžným řetězcem s `<strong>` mimo interpolaci.
3. **Editor umí zlomek toho, co parser uchová** (princip 3 v duchu, ne liteře). Data se
   neztrácejí, ale spoustu z nich nelze v aplikaci upravit — uživatel to nevnímá jako
   záměr, ale jako „nejde to".
