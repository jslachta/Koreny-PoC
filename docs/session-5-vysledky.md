# Session 5: SVG export f3 overlaye — výsledky

Datum: 2026-07-21. Cíl: „Stáhnout SVG" zpět v overlayi „Celý strom" — export celého f3
stromu jako samostatný, korektně stylovaný SVG soubor, nezávislý na pan/zoom stavu.
Větev `renderer-cheap-fixes`, vstupní strom čistý.

## Bod 0 — zjištění: jak jsou karty renderované

**Karty jsou SVG elementy uvnitř `<svg class="main_svg">`** — žádný přepínač nebyl nutný,
žádné produktové rozhodnutí k review. Ověřeno DOM inspekcí běžící aplikace (rozšířený
corpus-05 vstup, overlay otevřený):

- struktura: `div.f3 > div(wrapper) > svg.main_svg > g.view > { g.links_view (path.link),
  g.cards_view (g.card_cont > g.card > rect + text/tspan) }`; 6 karet = 6 `g.card_cont`,
  jména v `<text>/<tspan>`, 0 `<foreignObject>`;
- vedle svg existuje HTML vrstva `div.cards_view` pro HTML-kartový režim — je **prázdná**
  (0 dětí), protože Session 4 nastavila `setCardSvg()`;
- **zoom/pan drží f3 jako inline CSS** `style="transform: translate(…) scale(…)"` na
  `g.view` (SVG atribut `transform` je null, `transform.baseVal` prázdný; ověřeno
  `getComputedStyle` + `getScreenCTM` proti `__zoom` stavu d3-zoomu); pozice karet jsou
  naopak klasické atributové transformace (d3 přechody).

Z toho plyne detail implementace: reset zoomu v klonu = smazat `style.transform`
(ne atribut), a `getBBox()` nad `g.view` vrací nezoomované souřadnice obsahu (getBBox
ignoruje vlastní CSS transform elementu) — přesně to, co export potřebuje.

## Implementace

- **`Koreny/wwwroot/js/f3-view.js`** — `exportSvg(elementId)` přesně dle bodů a–e zadání:
  klon `<svg>` (originál nedotčen), reset zoomu na klonu, bbox z **originálního** `g.view`
  (klon mimo DOM `getBBox` neumí — potvrzeno předpokladem zadání; na originálu funguje),
  `viewBox`/`width`/`height` = bbox + 20 px padding, f3 CSS načtené **fetch-em
  z vendorovaného souboru** (jediný zdroj pravdy) do `<style>` v klonu, `XMLSerializer`.
  Klonu se přidává třída `f3`, aby selektory `.f3 …` platily bez rodičovského divu.
- **`Koreny/Components/F3Tree.razor`** — `ExportSvgAsync()` (interop na `exportSvg`).
- **`Koreny/Pages/Index.razor`** — tlačítko „Stáhnout SVG" zpět v overlayi; volá
  `ExportSvgAsync` a předává stávajícímu `downloadSvg` v export.js (soubor
  `koreny-strom.svg`). Tisk dle zadání záměrně není.
- **Robustnost pro skrytý dokument** (`f3-view.js` init): ve skrytém dokumentu
  (`document.hidden` — background tab, headless verifikace) nikdy neběží
  `requestAnimationFrame`, takže d3 přechody zůstanou viset na startovních pozicích
  a strom by se nikdy nedokreslil. Init proto v tomto stavu nastaví `transition_time 0`
  a po `updateTree` zavolá `d3.timerFlush()` (duration-0 přechody doběhnou synchronně —
  ověřeno experimentem). Viditelného chování se to nedotýká.

## Stav testů

**30 zelených / 31** (1 skip — ANSEL placeholder), beze změny proti Session 4 (JS/markup
změny; C# testová plocha se nezměnila). `dotnet build` 0 warnings.

## Checklist manuální verifikace

Prostředí: `dotnet run` + Browser pane, rozšířený corpus-05 vstup ze Session 4 (6 osob,
Květa jako CHIL jen v F2).

- [x] **Export při zazoomovaném/odtaženém stavu obsahuje CELÝ strom** — exportováno při
  `__zoom = translate(800, 403) scale(1.74)` (wheel zoom + drag pan před exportem);
  výsledný `viewBox="-363.75 -55 727.5 250"` = plné bounds obsahu (688×210 + 2×20 px
  padding), nezávisle na viewportu; všech 6 jmen přítomno, `g.view` v exportu bez
  transformace, 5 spojnic (`path.link`) včetně větve ke Květě.
- [x] **Samostatné otevření: karty se jmény, barvami a fonty jako v overlayi** — exportované
  SVG otevřeno jako **samostatný `image/svg+xml` dokument** (blob: URL v iframe — viz
  Odchylky bod 2): všech 6 jmen, `fill` karet z inline `<style>` = rgb(120,159,172) muži /
  rgb(196,138,146) ženy (f3 barvy), font `Roboto, sans-serif` (shodné s overlayem — Roboto
  aplikace nenačítá, obojí padá na sans-serif). Jediný stylesheet = vložený `<style>`.
- [x] **Overlay po exportu dál funguje** — export přes skutečné tlačítko (C# → interop →
  `downloadSvg`), overlay zůstal otevřený; wheel zoom po exportu mění `__zoom`; klik na
  kartu Pavla overlay zavřel a HourglassTree se přerootoval (Tomáš+Anna nad Pavlem).
- [x] **Determinismus** — `exportSvg` 3× po sobě → tři identické stringy (porovnání `===`).
- [x] Konzole po celé seanci **bez jediné chyby**.

Automatický test nepřidán — JS test infra neexistuje a C# se na DOM nedostane; checklist
je primární verifikace (vědomá výjimka dle zadání).

## Commity

| Commit | Obsah |
|---|---|
| `bb0ab7a` FEATURE: full-tree SVG export for the "Cely strom" overlay | exportSvg + tlačítko + hidden-doc robustnost |
| (tento) DOCS: session 5 results | tento dokument |

## Odchylky od zadání

1. **Přídavek: robustnost initu pro skrytý dokument** (transition_time 0 + `timerFlush`,
   viz Implementace). Důvod: Browser pane byl při této session trvale `hidden` (rAF neběží),
   d3 přechody zamrzly na vstupních pozicích a bez úpravy nešel ověřit vůbec žádný bod
   checklistu; úprava je zároveň korektní produktové chování pro background taby. Bez ní by
   `getBBox` vracel rozměry zamrzlého mezistavu (660×70 místo 688×210).
2. **„file://" nahrazeno blob: `image/svg+xml` dokumentem.** Browser pane odmítl navigaci
   nového tabu (`navigation denied`) a reálný download na disk není z pane dosažitelný.
   Blob dokument má stejné parsování (`contentType: image/svg+xml`, kořen `<svg>`) i stejnou
   izolaci jako file:// — k dispozici je pouze vložený `<style>`, žádné styly aplikace.
   Sémanticky jde o týž test samostatnosti souboru.
3. **Vizuální kontrola screenshotem nebyla možná** (skrytý pane — screenshoty timeoutují);
   verifikace provedena DOM inspekcí a computed styles v samostatném dokumentu. Session 4
   ověřila tentýž strom vizuálně, layout se nezměnil.
4. **Drobnost — barva textu:** v exportu je text černý (výchozí), v aplikaci
   rgb(33,37,41) (dědičnost z Bootstrapu přes `currentColor` — pravidlo
   `.f3 svg.main_svg text { fill: currentColor }` v exportu nematchuje, protože `.f3` je
   na samotném kořeni). Rozdíl je vizuálně nerozlišitelný (tmavý text na barevných
   kartách v obou případech); nechávám bez korekce.

## Poznámky pro budoucí session

- Exportované SVG spoléhá na CSS selektory a vložený stylesheet — v prohlížečích korektní;
  striktní ne-CSS SVG renderery (např. starší ImageMagick) mohou barvy z tříd ignorovat.
  Případný „plochý" export (zapečení fill/stroke do atributů) je ohraničený follow-up.
- Tisk overlaye zůstává neimplementovaný (mimo rozsah této session, dle zadání).
