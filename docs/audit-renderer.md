# Audit: rendering genealogického stromu

Stav k commitu `d8c30bc` (2026-07-19). Zjištění vycházejí ze čtení kódu; kde je závěr
odvozen bez spuštění aplikace, je označen jako **domněnka**.

## 1. Kde se generuje SVG

V repozitáři existují **tři nezávislé renderery**, každý generuje SVG jiným způsobem:

| Komponenta | Soubor | Způsob generování | Použití |
|---|---|---|---|
| Hourglass (přesýpací hodiny) | `Koreny/Components/HourglassTree.razor` | inline SVG v Razor markupu (řádky 12–31), interaktivní (klik/dvojklik) | hlavní pohled na stránce (`Index.razor:65–68`) |
| Potomkovský strom | `Koreny/Components/DescendantTree.razor` | statický SVG string přes `StringBuilder` (`BuildSvgXml`, řádky 77–99) | overlay „Celý strom" (`Index.razor:320–332`) |
| Celý graf | `Koreny/Components/FullGraph.razor` | statický SVG string, souřadnice počítá **Dagre** přes JS interop (řádky 96–101) | overlay „Celý graf" (`Index.razor:371–383`) |

Podpůrné JS soubory:

- `Koreny/wwwroot/js/dagre-layout.js` — `KorenyDagre.computeLayout`: postaví `dagre.graphlib.Graph` (rankdir TB, ranker `tight-tree`, řádky 3–10), spustí `dagre.layout` a vrátí JSON s pozicemi uzlů.
- Dagre se načítá **z CDN** v `Koreny/wwwroot/index.html:41` (`cdn.jsdelivr.net/npm/@dagrejs/dagre@2.0.0`). Offline nebo při výpadku CDN selže layout a `FullGraph.razor:103–106` vrátí náhradní SVG s textem „Layout failed".
- `Koreny/wwwroot/js/export.js` — `downloadSvg` (Blob + `<a download>`, řádky 26–37), `printSvg` (injektáž do print-only rootu + `@media print`, řádky 43–73), `downloadText`.

### Tok dat od modelu k výstupu

```
GEDCOM soubor ──GedcomParser──▶ GedcomDocument (AppState.Document, singleton)
                                      │
              Index.razor (_doc, _rootPersonId)
                ├─▶ HourglassTree: OnParametersSet → _nodes/_segments → inline <svg> v DOM
                ├─▶ DescendantTree.RenderToSvg(doc, rootId) → SVG string
                │       → base64 data-URI → <img> v overlayi (Index.razor:327–329, 226)
                └─▶ FullGraph.RenderToSvgAsync(doc, JS) → JSON uzlů/hran → Dagre (JS)
                        → pozice → SVG string → base64 data-URI → <img> (Index.razor:378–380, 245)
```

Klíčový detail: **jen HourglassTree je živé SVG v DOM** (proto funguje klik na osobu,
`HourglassTree.razor:76–102`). Oba overlaye zobrazují SVG jako `<img src="data:image/svg+xml;base64,…">`
uvnitř `<div style="overflow:auto">` (`Index.razor:225–227` a `244–246`) — žádná interaktivita,
žádný zoom, jen scroll.

## 2. Layout algoritmy

### 2.1 HourglassTree — pevné 4 řady

`OnParametersSet` (`HourglassTree.razor:104–155`) posbírá **přesně tyto osoby**: 4 prarodiče,
2 rodiče, root, děti roota. Víc generací nahoru ani dolů neexistuje — hloubka je natvrdo
2 nahoru / 1 dolů.

- `BuildLayout` (řádky 157–217) skládá řady `Grandparents / Parents / Root / Children` shora dolů,
  krok `NodeHeight + VerticalGap` (48 + 40 px, řádky 35–38). Prázdné řady se vynechávají.
- Každá řada se **centruje nezávisle kolem x = 0** (`LayoutParentRow:468`, `LayoutChildrenRow:493`),
  pak se celek posune, aby minX = padding (řádky 201–209). Řada prarodičů se dělí na dva klastry
  (otcovská/mateřská linie) s mezerou `HorizontalGapBetweenClusters = 44` (řádky 402–446), aby otec
  seděl pod otcovskou linií — komentář na řádku 408.
- Hrany jsou **přímé diagonální úsečky** od spodní hrany rodiče ke horní hraně dítěte
  (`ConnectTreeEdges:308–375`, `AddEdge:555`), žádné ortogonální vedení.
- Rodiče roota se hledají jako **první rodina, kde je root uveden jako CHIL**
  (`FindFamilyAsChild:578–589` — vrací první match). Děti roota se naopak sbírají
  **sjednocením přes všechny rodiny**, kde root figuruje jako HUSB/WIFE (`FindChildrenIds:591–606`).

### 2.2 DescendantTree — rekurzivní leaf-slot layout

- `BuildDescNode` (`DescendantTree.razor:111–151`) rekurzivně staví strom potomků: pro osobu najde
  rodiny, kde je partnerem; **pár (HusbId/WifeId) bere z první rodiny** (`fams[0]`, řádek 128),
  ale **děti slévá ze všech rodin** (`SelectMany`, řádek 129).
- `AssignLeafSlots` (řádky 154–183) přiřadí listům sloty post-orderem; bezdětný pár rezervuje
  2 sloty (řádky 174–180). Vnitřní uzel dostane rozsah `[min, max]` slotů svých dětí.
- `LayoutGeometry` (řádky 186–265) umístí uzel na střed svého slot-rozsahu (`RangeCenterX:267–272`),
  pár vykreslí jako dva boxy vedle sebe (160 + 24 + 160 = 344 px, řádky 205–219) spojené vodorovnou
  čarou; k dětem vede ortogonální „hřeben" přes `bendY` v půlce mezigeneračního prostoru (řádky 234–259).
- Cykly hlídá množina `visiting` (řádky 45, 113–116, 149) — je to ale **path-set** (osoba se po
  návratu z rekurze odebere), takže chrání jen proti nekonečné rekurzi, ne proti duplicitám
  (viz limity níže).

### 2.3 FullGraph — Dagre s virtuálními rodinnými uzly

- Každá osoba = uzel 160×56, každá rodina = malý virtuální uzel 8×8 (`FullGraph.razor:39–58`),
  hrany HUSB→FAM, WIFE→FAM, FAM→CHIL (řádky 60–83). Komentář na řádku 8 vysvětluje proč:
  bez virtuálního uzlu by Dagre kladl manžele do vztahu rodič/dítě.
- Pozice spočítá Dagre; C# pak kreslí ortogonální cesty rodič→rodinný uzel→dítě
  (`OrthogonalParentToFamilyPath:292–301`, `OrthogonalFamilyToChildPath:303–312`) a boxy osob
  s barvou podle pohlaví (`FillFor:354–367`).
- Hrany, které Dagre vrátí (`dagre-layout.js:39–46`), se **nepoužívají** — C# si cesty kreslí
  samo z pozic uzlů; body hran z Dagre se zahazují.

## 3. Identifikované limity

### 3.1 Vícenásobná manželství

- **HourglassTree**: partneři roota se **vůbec nekreslí** — žádná řada pro manžele/manželky
  neexistuje (`BuildLayout:167–186` zná jen Grandparents/Parents/Root/Children). Děti ze všech
  manželství se slijí do jedné řady bez rozlišení, z které rodiny pocházejí (`FindChildrenIds:591`).
- **DescendantTree**: zobrazí se **jen partner z první rodiny** (`fams[0]`, řádek 128); děti
  z druhého a dalších manželství se vykreslí pod tímto prvním párem — tedy **vizuálně chybná
  atribuce dětí nesprávnému partnerovi**.
- **FullGraph**: jediný pohled, který vícenásobná manželství zobrazuje korektně (každá rodina má
  vlastní uzel), za cenu obecného grafového layoutu.

### 3.2 Nevlastní / adoptivní vztahy

Model tento pojem nezná: `GedcomFamily` má jen `HusbandId/WifeId/ChildrenIds`
(`Models/GedcomModels.cs:83–91`) a parser tag `PEDI` (i `ADOP`, `FAMC` s kvalifikátory) zahazuje.
Pokud je osoba dítětem ve více rodinách (biologická + adoptivní), `FindFamilyAsChild`
(`HourglassTree.razor:578`) vezme první rodinu v pořadí souboru — **která to bude, je věcí pořadí
záznamů, ne dat**. Renderer nevlastní vztah nemůže odlišit, protože informace zanikla už při importu.

### 3.3 Kolize větví

- **DescendantTree — konkrétní překryv**: pár s právě jedním dítětem-listem dostane slot-rozsah
  o šířce jednoho slotu (`SlotPitch` = 184 px), ale kreslí se jako dvojbox široký 344 px
  (řádky 205–219). Dva takové páry v sousedních podstromech mají středy 184 px od sebe →
  boxy se překryjí o ~160 px. Ověřeno výpočtem z konstant (řádky 7–9, 267–272), nespuštěno —
  **domněnka jen co do přesných pixelů, mechanismus je z kódu jednoznačný**.
- **HourglassTree**: řady se centrují nezávisle, takže diagonální hrany prarodič→rodič mohou při
  asymetrických klastrech vést vizuálně „napříč"; boxy samotné se nepřekrývají (jsou v oddělených
  řadách). Přímé diagonály navíc při mnoha dětech tvoří vějíř protínajících se čar.
- **FullGraph**: kolize řeší Dagre, ale ruční ortogonální cesty (kreslené z pozic, ne z bodů hran
  Dagre) mohou procházet přes cizí boxy — Dagre routuje hrany kolem uzlů, tenhle kód ne
  (`OrthogonalParentToFamilyPath` je vždy tříúsečková cesta bez detekce překážek). **Domněnka**
  co do četnosti, mechanismus jistý.

### 3.4 Velké stromy — výkon a pan/zoom

- **Pan/zoom neexistuje.** Overlaye jsou `<img>` v `overflow:auto` divu (`Index.razor:225, 244`) —
  jen scrollbary, žádné přiblížení. HourglassTree má `max-width:100%; height:auto`
  (`HourglassTree.razor:12`), tj. při širokém stromu se **celé SVG zmenší** pod čitelnost.
- **O(n²) vyhledávání**: `FindIndividual` je lineární průchod listem
  (`HourglassTree.razor:560–576`, `DescendantTree.razor:350–361`, obdobně `FindFamilyAsChild`).
  DescendantTree volá `FindIndividual` pro každý box; při tisících osob kvadratické chování.
  Chybí `Dictionary<string, GedcomIndividual>` index — jednořádková oprava.
- **Base64 data-URI**: SVG celého grafu se serializuje do stringu, base64 kóduje (+33 % velikosti)
  a vkládá jako `src` (`Index.razor:329, 380`). U velkých stromů drží paměť SVG string, base64
  kopii i dekódovaný obraz. Šířka DescendantTree roste lineárně s počtem listů (184 px/list);
  1 000 listů ≈ 184 000 px široký obrázek — **domněnka**: na této velikosti prohlížeče rastrují
  `<img>` nespolehlivě.
- **Dagre na velkém grafu**: layout běží synchronně v JS na hlavním vlákně; u tisíců uzlů
  znatelné zamrznutí UI (**domněnka**, Dagre je ale známě superlineární).

### 3.5 Duplicitní osoby (pedigree collapse)

- **DescendantTree**: `visiting` je path-set, ne globální množina navštívených. Když se osoba
  objeví ve dvou větvích (sňatek bratranců → společný potomek dosažitelný dvěma cestami),
  **celý její podstrom se vykreslí dvakrát**, bez vizuálního označení duplicity. Při skutečném
  cyklu v datech (chybný GEDCOM) se rekurze zastaví a osoba se vykreslí jako list (řádky 113–116).
- **HourglassTree**: hloubka 2/1 → collapse se projeví nanejvýš tím, že tatáž osoba sedí ve dvou
  boxech prarodičů; nedetekuje se.
- **FullGraph**: jediný korektní — osoba je vždy jeden uzel, collapse je vidět jako sbíhající se hrany.

## 4. Odhad: oprava ve stávajícím kódu vs. náhrada JS knihovnou

### Oprava ve stávajícím kódu

| Úprava | Rozsah | Poznámka |
|---|---|---|
| Slovníkový index osob/rodin (odstranění O(n²)) | malý (hodiny) | zavést do `GedcomDocument` nebo předávat lookup |
| Pan/zoom | malý–střední | vyměnit `<img>` za inline SVG + JS modul (vlastní ~100 řádek, nebo svg-pan-zoom vendorovaný lokálně); precedent JS interop už existuje (`export.js`, `dagre-layout.js`) |
| Kolize párů v DescendantTree | malý | bezdětné páry už rezervují 2 sloty (řádek 174); stačí, aby **každý** pár měl `SlotMax ≥ SlotMin + 1`, tj. rozšířit rozsah i pro páry s úzkým podstromem |
| Vícenásobná manželství v DescendantTree | střední | přestavět `DescNode` z „osoba" na „osoba + seznam rodin", každá rodina vlastní podvětev s vlastním partnerem; layout logika (sloty, hřebeny) zůstane |
| Partneři + manželství v HourglassTree | střední | přidat boxy partnerů vedle roota a seskupit děti po rodinách; rozbije to symetrii centrovaných řad, nutné přepočítat řádkování |
| Pedigree collapse (dedup s odkazem „viz výše") | střední | vyžaduje rozhodnutí o vizuální konvenci (duplikát jako odkaz/ghost box); technicky globální `visited` + speciální renderování |
| Hlubší generace v HourglassTree | velký | současný kód je napevno 4řadý; obecný ancestor-layout je fakticky přepis komponenty |

Celkově: výkon, pan/zoom a kolize jsou levné. Vícenásobná manželství a obecná hloubka znamenají
přepsat layoutové jádro obou stromových komponent — v tom bodě se náklad blíží náhradě.

### Náhrada JS knihovnou přes Blazor interop

- **Interop vzor je už zavedený a funkční**: `FullGraph` + `KorenyDagre.computeLayout` dělá přesně
  tohle (C# → JSON → JS layout → JSON → C# kreslí). Náhrada by tento vzor jen rozšířila.
- Kandidáti: **family-chart (f3)** či **Topola** (genealogicky specifické — manželství, partneři
  a generace řeší nativně), **ELK.js** (obecný, ale s lepším layered layoutem než Dagre),
  případně zůstat u Dagre a jen zlepšit post-processing.
- Náklady navíc oproti dnešku:
  1. mapování `GedcomDocument` → datový formát knihovny (srovnatelné s dnešním `NodeJson`/`EdgeJson`),
  2. zpětné události (klik na osobu → `[JSInvokable]` callback do Blazoru) — dnes je klikací jen
     HourglassTree, tohle by interaktivitu naopak rozšířilo,
  3. hosting knihovny lokálně (dnešní CDN závislost v `index.html:41` je sama o sobě k opravě),
  4. export SVG/tisk zůstane funkční — knihovny renderují do SVG v DOM, `export.js` umí serializovat.
- Co by se náhradou vyřešilo „zadarmo": kolize, pan/zoom (většina knihoven má vestavěný),
  vícenásobná manželství, hlubší generace, částečně i pedigree collapse (graf místo stromu).

**Doporučení**: drobné opravy (index, kolize slotů, lokální Dagre) se vyplatí hned; pro
vícenásobná manželství a hloubku generací je náhrada genealogickou JS knihovnou levnější než
přepis tří vlastních layoutů — a interop infrastruktura pro ni už v projektu existuje.
