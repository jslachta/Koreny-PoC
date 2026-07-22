# 🌱 Kořeny

Jednoduchá aplikace pro tvorbu a prohlížení rodokmenů přímo v prohlížeči.
Žádná registrace, žádný server, žádná data nikam neodesíláme.

**[Otevřít aplikaci →](https://jslachta.github.io/Koreny-PoC/)**

## Vaše data jsou vaše

Kořeny stojí na jednoduchém principu: **váš rodokmen patří vám, ne platformě.**

Pokud přecházíte z MyHeritage, Ancestry nebo jiné služby — načtěte svůj
exportovaný `.ged` soubor a nepřijdete o jediný záznam. Kořeny zachovávají
veškerá data včetně proprietárních tagů, zdrojů, médií a poznámek, které
jiné aplikace při importu tiše zahazují — i to, co samy neumí zobrazit
ani editovat, nesou dál.

Při exportu zpět do GEDCOM se neztratí žádný záznam, tag ani hodnota:
dostanete vše, co jste načetli, plus vše, co jste v Kořenech přidali nebo
upravili. Jedinou výjimkou je hlavička souboru (HEAD), kterou export po
právu razítkuje jako vytvořený Kořeny. Bezeztrátovost je definovaná jako
sémantická ekvivalence — normalizovat se smí pouze lámání dlouhých řádků
(CONC/CONT) a konce řádků — a vymáhá ji testovací sada: round-trip testy
nad korpusem reálných exportních stylů a test idempotence uložení pro
každou osobu i rodinu.

## Co Kořeny umí

- Vytvořit rodokmen od nuly — přidat osoby, vztahy, základní data
- Načíst existující soubor ve formátu GEDCOM (.ged) z MyHeritage, Ancestry a dalších
- Načíst vestavěný vzorový rodokmen na vyzkoušení („Načíst vzorový GEDCOM")
- Zobrazit **rodokmen jedince** — interaktivní strom příbuzných vybrané osoby
  (předci, potomci, vedlejší větve), s posunem a přiblížením
- Zobrazit **celý rodokmen** — graf všech osob v souboru včetně přivdaných
  linií a jejich rodičů
- Exportovat rodokmen zpět do formátu GEDCOM bez ztráty dat
- Stáhnout kterýkoli pohled jako SVG obrázek nebo ho vytisknout

## Jak začít

**Nový rodokmen**
Klikněte na „Nová osoba" a začněte přidávat členy rodiny.
Vztahy mezi osobami definujete přes „Nová rodina".

**Existující rodokmen**
Klikněte na „Načíst GEDCOM" a vyberte svůj `.ged` soubor.
Soubor se zpracuje lokálně — nikam se neodesílá.

**Jen si to vyzkoušet**
Klikněte na „Načíst vzorový GEDCOM" — aplikace se naplní ukázkovou rodinou.

Osobu vyberete kliknutím v seznamu (vybraný řádek je zvýrazněný); tlačítko
„Upravit" na vybraném řádku otevře editaci. Výběr zároveň určuje, čí
rodokmen se zobrazí a stáhne.

## Důležité upozornění

> **Kořeny neukládají žádná data.**

Vše existuje pouze v paměti prohlížeče po dobu, kdy máte aplikaci otevřenou.

- **Před zavřením nebo obnovením stránky vždy exportujte** rodokmen přes „Uložit GEDCOM"
- **Reload stránky smaže veškerou rozdělanou práci** bez možnosti obnovy
- Při příštím použití jednoduše načtěte exportovaný `.ged` soubor

Toto chování je záměrné — vaše data nikdy neopustí váš počítač.

## Formát GEDCOM

Kořeny pracují se standardem GEDCOM 5.5.1 v kódování UTF-8 (s BOM i bez).
Exportovaný soubor lze otevřít v libovolném genealogickém programu
(MyHeritage, Ancestry, Gramps, MacFamilyTree a další).

## Jak je bezeztrátovost testovaná

Sémantický diff (`GedcomSemanticDiff`) porovnává GEDCOM soubory jako
stromy s vlastním minimálním readerem — nezávislým na aplikačním parseru,
aby se parser netestoval sám sebou. Nad ním stojí tři vrstvy testů:

- **round-trip** — import→export nad korpusem exportních stylů
  (MyHeritage, Ancestry, proprietární tagy, víceřádkové poznámky,
  prázdné události, pořadí záznamů),
- **zachování cizích dat při editaci** — změna jména nesmí pohnout
  `_MHID` o řádek,
- **idempotence uložení** — otevřít a uložit každou osobu i rodinu
  beze změny musí dát export sémanticky identický s importem.

## Známé limity

- **Kódování ANSEL** není podporováno — soubory ze starého softwaru
  v ANSEL se načtou s poškozenou diakritikou. Podporováno je UTF-8
  (s BOM i bez). V testech na to čeká připravený skip-test.
- **Adopce a vícenásobné rodičovské vazby**: dítě vedené ve více rodinách
  (FAMC×2, tag PEDI) se v rodokmenu jedince zobrazí jen s první
  rodičovskou vazbou. Data se při importu/exportu plně zachovávají —
  limit je pouze zobrazovací (formát knihovny Topola).
- **Druhé sňatky ne-kořenových osob** se v pohledu „rodokmen jedince"
  nezobrazují — vícenásobná manželství jsou vidět, když je daná osoba
  kořenem stromu. Pohled „celý rodokmen" zobrazuje všechny rodiny vždy.
- **Událost nelze odstranit z UI** — vymazání data i místa ponechá
  prázdnou událost (záměrně: raději nezničit data). Explicitní akce
  „odebrat událost" zatím chybí.
- **„Načíst GEDCOM" přepíše neuložené změny bez dotazu** (vzorový
  GEDCOM se před přepsáním ptá).
- **`lib/topola/topola.bundle.js` je generovaný artefakt** — knihovna
  Topola se z npm distribuuje jen jako CommonJS, proto je vendorovaná
  jako self-contained bundle. Regenerace: `tools/bundle-topola/README.md`
  (dva příkazy, vyžaduje Node).

## Technologie

- [Blazor WebAssembly](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor) — běží přímo v prohlížeči, bez serveru
- .NET 10
- [Topola](https://github.com/PeWu/topola) (Apache-2.0) — vykreslení rodokmenu jedince
- [Dagre](https://github.com/dagrejs/dagre) (MIT) + [d3](https://d3js.org/) (ISC) — layout celého rodokmenu, posun a přiblížení
- Vše vendorováno lokálně — aplikace funguje offline, žádné CDN

## Licence

MIT — používejte, upravujte, sdílejte.

## Přispívání

Projekt je v raném stádiu. Chyby a návrhy hlaste přes
[GitHub Issues](https://github.com/jslachta/Koreny-PoC/issues).
Pull requesty vítány.