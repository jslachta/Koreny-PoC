# Session 4: náhrada DescendantTree knihovnou family-chart (f3) — výsledky

Datum: 2026-07-20. Cíl: overlay „Celý strom" renderuje živá komponenta nad family-chart (f3)
místo statického DescendantTree. HourglassTree a FullGraph beze změny. Větev
`renderer-cheap-fixes` (pokračování Session 3), pracovní strom na vstupu čistý.

## Verze a licence

| Knihovna | Verze (pinovaná) | Licence | Umístění |
|---|---|---|---|
| family-chart (f3) | **0.9.0** | **MIT** (LICENSE.txt v balíčku, Copyright 2021 Donat Soric) | `Koreny/wwwroot/lib/family-chart/` (min.js + css + LICENSE.txt) |
| d3 | **7.9.0** | ISC (Mike Bostock) | `Koreny/wwwroot/lib/d3/` (min.js + LICENSE) |

**Poznámka k licenci f3:** npm metadata (`package.json` pole `license`) chybně uvádějí „ISC",
ale licenční soubor přiložený v artefaktu je plný text MIT. Rozhodný je přiložený text; obě
licence jsou navíc permisivní a prakticky ekvivalentní, takže jsem se dle zadání NEzastavil —
rozpor je tímto zapsán pro review. d3 není v f3 bundlu (UMD čte `global.d3`), proto vendorováno
zvlášť. Žádný CDN odkaz v aplikaci není.

## Stav testů

**30 zelených / 31 celkem** (1 skip — ANSEL placeholder). `dotnet build` 0 warnings.

Změna oproti Session 3 (27+1): **+5** nových testů `F3DataMapperTests`, **−2** smazané
`DescendantLayoutTests` (odešly s nahrazeným rendererem — kryly slot-geometrii, kterou f3
řeší vlastním layoutem).

## Commity (logické celky)

| Commit | Obsah |
|---|---|
| `7b460a1` BUILD: vendor family-chart 0.9.0 + d3 7.9.0 | vendoring + licence |
| `876d0bd` FEATURE: F3DataMapper | mapování + testy |
| `d40a542` FEATURE: live f3 tree in the „Celý strom" overlay | interop modul, F3Tree, přepojení overlaye |
| `b79d25f` CLEANUP: remove DescendantTree replaced by f3 | smazání nahrazeného kódu + retarget toolbar exportu |
| (tento) DOCS: session 4 results | výsledky |

## Mapování dat (F3DataMapper)

Pravidla (zdokumentována i v komentáři třídy [F3DataMapper.cs](../Koreny/Services/F3DataMapper.cs)):
father/mother dítěte z PRVNÍ rodiny, kde je CHIL (`FindFamilyAsChild`, konzistentně
s HourglassTree); spouses ze VŠECH rodin osoby v pořadí souboru; children ze všech rodin,
pořadí dle uzlů, deduplikace; osoby bez vazeb v datasetu zůstávají; odkazy na neexistující ID
se vynechávají; `gender` jen pro SEX M/F. Emituje se f3 „legacy" tvar
`rels{father, mother, spouses, children}` — ověřeno ve vendorovaném bundlu, že f3 0.9.0 ho
normalizuje v `formatData` při vytvoření store.

Testy: (a) corpus-05 — oba spouses v pořadí souboru, děti sloučené bez duplicit, dítě
z prvního manželství nese první manželku; (b) corpus-05 I3 (CHIL ve dvou rodinách) — rodiče
z první rodiny; (c) corpus-03 — osoby bez rodin s prázdnými rels, nikdo nevypadl; tvar JSON klíčů.

## Checklist manuální verifikace (bod 5 zadání)

Prostředí: `dotnet run`, Chromium (Browser pane), corpus-05 nahraný programově přes file input.

- [x] **Overlay se otevře s f3 stromem, offline** — f3 SVG s kartami všech 5 osob;
  `performance.getEntriesByType('resource')` po celé seanci: **0 požadavků mimo
  `http://localhost:5227`** (d3, f3 i CSS z lokálního `lib/`).
- [x] **Pan/zoom funguje** — wheel: scale 1.945 → 2.713; drag: translate +150/+80 px
  (odečteno z d3 `__zoom` transformace před/po událostech).
- [x] **Osoba se dvěma manželstvími zobrazuje děti u správných partnerů** — corpus-05:
  Tomáš (kořen) s oběma manželkami vedle sebe, Eva+Pavel pod párem s Annou. Corpus-05 nemá
  dítě výlučně z druhého manželství (I3 je CHIL v obou rodinách), proto doplňkově nahrán
  rozšířený vstup (+ Květa jako CHIL jen v F2): **Květa se vykreslila pod Janou Svobodovou
  (druhá manželka), Eva+Pavel pod Annou** — ověřeno screenshotem i souřadnicemi karet.
  Přesně tato atribuce byla v DescendantTree chybná.
- [x] **Klik na osobu zavře overlay a přepne root** — klik na kartu Pavla: overlay zmizel,
  HourglassTree se přerootoval (zobrazuje Pavla s rodiči Tomáš+Anna). Escape overlay
  také zavírá (ověřeno v průběhu).
- [x] **5× otevřít/zavřít po sobě** — všech 5 cyklů otevřelo i zavřelo korektně,
  `console` po celé seanci **bez jediné chyby** (žádný leak DotNetObjectReference).

## Odchylky od zadání

1. **`init` má navíc parametr `rootId`**: signatura je `init(elementId, dataJson, rootId,
   dotNetRef)` místo zadané `init(elementId, data, dotNetRef)`. Overlay se má otevírat
   vycentrovaný na aktuální kořenovou osobu (chování zděděné po DescendantTree) a f3 jinak
   bere první osobu v datech; `rootId` se předává do `chart.store.updateMainId`.
2. **Licence f3: metadata ISC vs. přiložený MIT text** — viz sekce Verze a licence. Nezastavil
   jsem se, protože podmínka „pokud ne MIT" dle mého čtení nenastala (licenční text JE MIT);
   rozpor ale výslovně předkládám k review.
3. **Test dítěte výlučně z druhého manželství není nad corpus-05** — corpus-05 takové dítě
   neobsahuje (I3 je CHIL v F1 i F2, spadá pod pravidlo „první rodina" z bodu b). Atribuci
   druhé partnerce kryje in-memory scénář v témže testovacím souboru
   (`ChildOfSecondMarriage_BearsSecondWifeAsMother`) a manuální verifikace s rozšířeným
   vstupem. Korpus jsem neměnil — je sdílenou specifikací GEDCOM testů ze Session 1/2 a
   změna by rozbila ručně psané očekávané soubory `Corpus/Expected/`.

## Known limitations (rozhodne review)

- **Overlay „Celý strom" nemá Stáhnout SVG / Tisk.** f3 drží pan/zoom jako transformaci
  `g.view` uvnitř SVG a rozměry SVG odpovídají viewportu kontejneru — serializace z DOM přes
  stávající `export.js` by exportovala jen aktuální výřez v aktuálním zvětšení, ne celý strom.
  Tlačítka jsem proto z overlaye odstranil (cesta „pokud transformace rozbíjejí export" ze
  zadání). Toolbar „Stáhnout SVG"/„Tisk" zůstává funkční a nově vždy exportuje celý graf
  (FullGraph); dřív používal DescendantTree, když existoval kořen. Případný plnohodnotný
  export f3 stromu (reset transformace + přepočet bounding boxu před serializací) je
  ohraničený follow-up.
- Karty f3 zobrazují jméno + roky (`card_display`), bez fotek; vzhled je výchozí f3
  (tmavé karty M/F barvy). Ladění vzhledu nebylo součástí zadání.
