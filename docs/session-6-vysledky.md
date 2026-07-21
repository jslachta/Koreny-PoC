# Session 6 (Topola): jediný rodokmen na knihovně Topola — výsledky

Datum: 2026-07-21. Cíl: jediný plný pohled na rodokmen postavený na Topole (PeWu/topola,
Relatives chart); odstranit f3 overlay i FullGraph. HourglassTree beze změny. Větev
`renderer-cheap-fixes`.

> Tento dokument nahrazuje předchozí (FullGraph) session-6 — ta funkce je touto session
> odstraněna; její záznam zůstává v git historii (commit „live FullGraph overlay").

## Bod 0 — proveditelnost (schváleno review: možnost 1, autorský esbuild bundle)

- **Distribuce:** topola 3.10.3 je z npm **jen CommonJS** — žádný UMD/IIFE pro `<script>`,
  žádné ESM; jsdelivr `/+esm` externalizuje deps na živé CDN URL. Proto je nutný jednorázový
  **autorský bundling** (esbuild), schválený review.
- **d3:** topola potřebuje d3-array/hierarchy/selection/transition@3 (jsou v d3 7 umbrella)
  + d3-flextree@2 a parse-gedcom (mimo umbrella). Vše je **inlinované v bundlu**; vendorované
  d3 7.9.0 zůstává zvlášť pro d3.zoom (topola zoom nemá).
- **API:** `createChart({ json, chartType: RelativesChart, renderer: DetailedRenderer, startIndi })`;
  klik → `indiCallback(info)`; zoom/pan si připínáme sami. Vstup `JsonGedcomData { indis, fams }`.

## Verze a licence

| Balíček | Verze (pinovaná) | Licence | Umístění |
|---|---|---|---|
| topola | **3.10.3** | **Apache-2.0** | `Koreny/wwwroot/lib/topola/` (bundle + LICENSE + README) |
| d3 | 7.9.0 (zůstává) | ISC | `Koreny/wwwroot/lib/d3/` |

Bundling: `tools/bundle-topola/` (package.json + package-lock pinují topola 3.10.3, `build.mjs`
= esbuild IIFE, global `topola`, deps inlinované). Autorský krok (`npm ci && npm run build`),
**ne** součást `dotnet run` — aplikace běží z committed `topola.bundle.js`. README nese verzi,
dva příkazy na přegenerování a sha256 bundlu; `.gitattributes` drží bundle v LF, aby hash seděl.

## Stav testů

**31 zelených / 32** (1 skip — ANSEL placeholder). `dotnet build` 0 warnings.
GEDCOM testová sada (round-trip, idempotence, parser) **nedotčená a zelená**. Přibylo
**6 TopolaDataMapperTests**, ubylo 5 F3DataMapperTests (f3 odstraněn).

## Mapování dat (TopolaDataMapper)

Family-based, near 1:1 z GEDCOM: `indis` (id, firstName, lastName, sex, famc, fams, birth/death
year), `fams` (id, husb, wife, children, marriage). `famc` = PRVNÍ rodina, kde je osoba dítětem
(Topola má famc jen jako jediný string); `fams` = všechny rodiny osoby; odkazy na neexistující
ID se vynechávají; osoby bez vazeb zůstávají. Testy: corpus-05 dvě manželství (oba fams, děti po
rodinách), FAMC×2 → první rodina, corpus-07 přivdaná osoba drží vlastní famc+fams, corpus-03 bez
vazeb, dangling reference, JSON klíče.

## Commity

| Commit | Obsah |
|---|---|
| `e8aa625` BUILD: vendor Topola 3.10.3 … | tools/bundle-topola + lib/topola bundle + LICENSE/README + .gitattributes |
| `92b805c` FEATURE: TopolaDataMapper … | mapper + 6 testů |
| `d2c1704` FEATURE: Topola family tree view … | topola-view.js, TopolaTree.razor, Index.razor toolbar/overlay, index.html |
| `a63658d` CLEANUP: remove family-chart (f3) | F3Tree, f3-view.js, F3DataMapper+testy, lib/family-chart (commit A) |
| `8ca5234` CLEANUP: remove FullGraph + Dagre | FullGraph(+View), graph-view.js, dagre-layout.js, lib/dagre (commit B) |
| (tento) DOCS: session 6 (Topola) | tento dokument + corpus-08-potter.ged |

Sdílený `svg-export.js` (klon/reset/bbox) zůstává — Topola cesta ho používá (viz zadání:
přesunout před smazáním f3-view.js; byl vyčleněn už v předchozí session).

## UI

Toolbar: odstraněno „Celý strom", „Celý graf", „Stáhnout SVG", „Tisk"; přidáno **„Zobrazit
rodokmen"** (overlay s Topolou, root = aktuální osoba) a **„Stáhnout rodokmen"** (bez overlaye:
skrytý off-screen render → `OnReady` → export → downloadSvg → odmountuje a uklidí; skrytý
rendering se ukázal spolehlivý, fallback nebyl potřeba). V overlayi **„Tisk"** (printSvg nad
zoom-resetnutým exportem) a **„Stáhnout rodokmen"**. Klik na osobu přeroootuje HourglassTree a
zavře overlay.

## Manuální checklist

Prostředí: `dotnet run` + Browser pane; `corpus-07-fullgraph.ged` a `corpus-08-potter.ged`.

- [x] **Vedlejší větve viditelné** — corpus-07 root Lukáš: teta Jana, sestřenice Tereza
  (potomci předků) i rodiče přivdané matky Evy (Karel+Anna) zobrazeni.
- [x] **Vícenásobná manželství: děti u správných partnerů** — Potter, root Vernon (I8, dvě
  rodiny): obě manželky (Petunia, Marjorie) i obě děti; **Dudley u Petunie, Victor u Marjorie**
  (ověřeno x-souřadnicemi). Pozn.: RelativesChart ukazuje více manželství, když je multi-ženatá
  osoba KOŘENEM (`additionalMarriage`); z pohledu potomka se druhý sňatek rodiče nezobrazí —
  sémantika RelativesChart, ne vada.
- [x] **Karty se nepřekrývají** — 0 překryvů z 36 párů uzlů (flextree layout).
- [x] **Linky viditelné** — Topola kreslí spojnice mezi kartami.
- [x] **Pan/zoom** — d3.zoom na `g.view`: wheel mění scale, drag translaci.
- [x] **Klik přerootuje HourglassTree a zavře overlay** — klik na Janu → overlay zmizel,
  HourglassTree ukazuje Janu s rodiči a dítětem.
- [x] **„Stáhnout rodokmen" z toolbaru i z overlaye při zazoomování → celý strom** — export
  je `<svg>` s celým stromem (viewBox z bbox obsahu, ne výřez), self-styled (embedded topola
  `<style>`), deterministický 3× po sobě; toolbar varianta bez otevření overlaye.
- [x] **Tisk: náhled obsahuje celý strom** — printSvg vloží do print-rootu tentýž zoom-resetnutý
  export (celý strom), ne aktuální výřez.
- [x] **5× otevřít/zavřít bez chyb** — 5 cyklů OK, konzole čistá.
- [x] **Offline** — 0 požadavků mimo localhost (topola bundle, d3, svg-export lokálně).

## Odchylky od zadání

1. **Node.js nebyl na stroji nainstalovaný.** Autorský esbuild krok jsem spustil přes
   **přenosný Node 20.18.1** stažený do scratchpadu (necommituje se). `tools/bundle-topola`
   i tak vyžaduje Node k přegenerování bundlu (README to uvádí) — to je vlastnost možnosti 1
   schválené review, ne odchylka od ní.
2. **Vícenásobné manželství se v Relatives chartu ukazuje jen z kořene multi-ženaté osoby**
   (viz checklist). Ověřeno rootem na Vernona; z pohledu potomka topola druhý sňatek rodiče
   nekreslí. Sémantika knihovny.
3. **FAMC×2 (adopce) → jen první rodičovská vazba** (Topola má famc jediný string) — known
   limitation, zafixováno testem 1b.
4. **Přepis předchozího session-6 dokumentu** (FullGraph) — ta funkce je odstraněna; historie
   zůstává v gitu.
5. **Screenshoty v skrytém Browser pane timeoutují** (jako v session 5/6) — vizuální verifikace
   provedena DOM inspekcí, computed styles a měřením souřadnic karet.
6. `corpus-07-fullgraph.ged` má legacy název (z FullGraph session), ale je to platný korpus
   používaný TopolaDataMapperTests; existující korpus jsem neměnil, `corpus-08-potter.ged` je nový.

## Known limitations

- Adopce (dítě ve dvou rodinách) se zobrazí jako jediná rodičovská vazba (viz odchylka 3).
- Topola bundle je generovaný artefakt; jeho přegenerování vyžaduje Node/esbuild (autorský krok).
