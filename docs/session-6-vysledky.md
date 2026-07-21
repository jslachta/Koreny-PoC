# Session 6: FullGraph jako živý pohled — výsledky

Datum: 2026-07-21. Cíl: povýšit overlay „Celý graf" na plnohodnotný živý pohled — SVG místo
`<img>`, pan/zoom, klik na osobu, SVG export celého grafu. Layout (Dagre + virtuální rodinné
uzly) se nemění. Větev `renderer-cheap-fixes`, vstupní strom čistý.

## Rozhodnutí: innerHTML vs. MarkupString

Živé SVG se vkládá **interop innerHTML** (JS `container.innerHTML = svgString` v graph-view.js),
ne Blazor `MarkupString`. Zdůvodnění: JS modul má vlastnit celý SVG podstrom (připíná na něj
d3.zoom listener a delegovaný click handler). S `MarkupString` by SVG bylo součástí Blazor
render stromu a **re-render Index.razor by mohl podstrom přepsat a shodit d3 handlery**;
navíc by Blazor zbytečně diffoval velké cizí SVG. innerHTML dává čistý model vlastnictví
(stejný jako f3-view.js): Blazor drží jen prázdný `<div>`, JS plní i uklízí jeho obsah.

## Co vzniklo / změnilo se

| Soubor | Změna |
|---|---|
| `Koreny/wwwroot/js/svg-export.js` | **nový** — sdílená `exportSvgString(svg, {viewSelector, cssText, rootClass})`: klon, reset zoom transformace (maže `style.transform` i atribut), bbox z originálního `g.view`, viewBox + 20 px padding, volitelná injektáž `<style>`, serializace |
| `Koreny/wwwroot/js/f3-view.js` | `exportSvg` přepsán na sdílenou funkci (fetch f3 CSS + `rootClass:"f3"` zůstává f3-specifický); duplicitní logika odstraněna |
| `Koreny/wwwroot/js/graph-view.js` | **nový** — `KorenyGraph {init, destroy, exportSvg}`: innerHTML, d3.zoom na `g.view`, delegovaný klik na `g[data-person-id]` → `OnPersonClicked`; export bez CSS injektáže |
| `Koreny/Components/FullGraph.razor` | generátor: obsah obalen do `<g class="view">`, bílé pozadí přesunuto dovnitř view, každá osoba dostala `<g data-person-id="…">` (jediné změny generátoru; layout beze změny) |
| `Koreny/Components/FullGraphView.razor` | **nová** komponenta (lifecycle dle F3Tree): OnAfterRender → FullGraph SVG + `init`, `ExportSvgAsync`, `DisposeAsync` s úklidem interop reference |
| `Koreny/Pages/Index.razor` | overlay „Celý graf": `<img>` base64 → živý `FullGraphView`; klik přeroootuje HourglassTree a zavře overlay; „Stáhnout SVG" přes `ExportSvgAsync` → `downloadSvg` (`koreny-graf.svg`) |
| `Koreny.Tests/Corpus/corpus-07-fullgraph.ged` | **nový** — 12 osob / 5 rodin, 2 přivdané osoby (Eva, Josef) s vlastními rodiči (vedlejší větve F3, F5) |

**Ověřeno k zadání:** FullGraph se styluje **inline atributy** (`fill`, `stroke`, `font-size`) —
zjištěno čtením generátoru; proto export grafu **nepotřebuje žádnou CSS injektáž**. f3 cesta
naopak CSS vkládá (karty se stylují třídami). Sdílená funkce to řeší parametrem `cssText`
(f3 předá, graph ne). Jeden mechanismus, dva konzumenti.

Bílé pozadí je nově **uvnitř `g.view`** (nezoomuje se vůči obsahu), takže ho export pokryje
(bbox z `g.view` ho zahrne) — dark-mode SVG prohlížeče by jinak zobrazily tmavé linky na
tmavém pozadí.

## Stav testů

**30 zelených / 31** (1 skip — ANSEL placeholder), beze změny (JS/markup + nový korpusový
soubor; C# testová plocha se nezměnila). `dotnet build` 0 warnings.

## Manuální checklist

Prostředí: `dotnet run` + Browser pane, `corpus-07-fullgraph.ged` nahraný přes file input.

- [x] **Všechny osoby včetně přivdaných a jejich rodičů viditelné** — 12 skupin
  `g[data-person-id]`: obě přivdané osoby (Eva Svobodová, Josef Dvořák) i jejich rodiče
  (Karel + Anna Svoboda, Václav + Božena Dvořák) přítomni; 5 rodinných uzlů (kroužků).
- [x] **Linky rodič→rodina→dítě viditelné** — 16 `path` linků (přesně: 5 rodin × 2 rodiče +
  6 dětských hran), stroke rgb(51,51,51).
- [x] **Pan/zoom** — d3.zoom na `g.view`: wheel změnil `transform` na `scale(1.52)`, drag
  posunul translaci; kurzor `grab`, SVG vyplňuje kontejner (100 %×100 %).
- [x] **Klik přerootuje** — klik na box Evy Svobodové (přivdaná, `data-person-id="I5"`)
  zavřel overlay a HourglassTree se přerootoval na Evu (rodiče Karel+Anna nad ní, dítě
  Lukáš pod ní).
- [x] **Export při zazoomování = celý graf** — po wheel zoomu + pan exportováno reálným
  tlačítkem i modulem: `viewBox="-20 -20 1348 592"` = plné bounds obsahu (1308×552 + 2×20 px),
  nezávisle na viewportu; všech 12 osob, `g.view` bez transformace, bílé pozadí, bez `<style>`;
  export 3× po sobě **byte-identický**. Samostatné otevření jako `image/svg+xml` (blob URL,
  izolovaný dokument): barvy boxů (#4A7FB5 muži / #9B2335 ženy), bílý text, 16 linků.
- [x] **5× otevřít/zavřít bez chyb** — všech 5 cyklů otevřelo (12 osob) i zavřelo; konzole
  po celé seanci **bez jediné chyby** (žádný leak DotNetObjectReference).
- [x] Offline: `performance.getEntriesByType('resource')` — **0 požadavků mimo localhost**
  (d3, graph-view.js, svg-export.js lokálně).

Overlay po exportu dál pan/zoomoval a reagoval na klik (export pracuje s klonem, originál
nedotčen).

## Odchylky od zadání

1. **Tlačítko „Tisk" z overlaye „Celý graf" odstraněno** (zůstalo jen „Stáhnout SVG" + zavřít).
   Staré overlay-Tisk používalo statický SVG string, který živá cesta už negeneruje; task
   zmiňuje pro tuto session jen SVG export. Tisk zůstává dostupný z **hlavního toolbaru**
   („Tisk" přes `FullGraph.RenderToSvgAsync`), takže se schopnost neztrácí. Konzistentní
   s f3 overlayem (session 5). Případný tisk přímo z overlaye je samostatný follow-up.
2. **Screenshot nešel** (Browser pane trvale `hidden`, screenshoty timeoutují — jako
   v session 5). Vizuální verifikace provedena DOM inspekcí + computed styles v živém
   overlayi i v samostatném exportovaném dokumentu.

Jiné odchylky: žádné. Osud f3 overlaye („Celý strom") jsem dle zadání neřešil — zůstává beze
změny (jen jeho export sdílí nově vyčleněnou `exportSvgString`, chování identické).
