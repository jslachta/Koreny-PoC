/**
 * KorenyTopola — interop modul pro Topola (PeWu/topola). Který graf, jaký směr a jaké
 * barvy, to říká viewJson (Services/TreeViewSettings.cs); bez něj platí Relatives chart.
 * Co stojí na kartách (místa, povolání) tudy nejde — to si uživatel zapíná v datech,
 * která mapper pošle v dataJson.
 * Očekává globály `topola` (vendorovaný self-contained bundle) a `d3` (vendorované 7.9.0).
 *
 * init(elementId, dataJson, rootId, viewJson, dotNetRef) — vyrenderuje rodokmen do kontejneru.
 * exportSvg(elementId) — vrátí samostatný SVG string celého stromu (nezávislý na pan/zoom).
 * destroy(elementId) — odpojí listenery a vyprázdní kontejner.
 *
 * Topola nemá vestavěný zoom/pan — připínáme d3.zoom z vendorovaného d3 sami. Topola kreslí
 * do svg jednu root <g> (s translate(origin)) a vkládá <style> (renderer.getCss()) přímo do
 * svg; my root <g> obalíme do <g class="view">, na kterou jde zoom transformace i export.
 * Embedded <style> se klonuje se svg, takže export je self-styled (žádný CSS fetch).
 *
 * animate:false → render je synchronní (bez d3-transition), takže funguje i ve skrytém
 * dokumentu (background tab / headless verifikace), kde requestAnimationFrame neběží.
 *
 * Klik na kartu volá topola indiCallback → dotNetRef.invokeMethodAsync("OnPersonClicked", id).
 */

import { exportSvgString } from "./svg-export.js";

const SVG_NS = "http://www.w3.org/2000/svg";
const EXPORT_PADDING = 20;

/**
 * Úpravy řádků pod jménem, které Topola sama neudělá. Obalíme `getIndiDetails`
 * a `getFamDetails`: zavoláme původní implementaci a na její výsledek saháme zvenčí.
 * Není to kopie knihovní logiky, takže upgrade Topoly nemá co rozbít — a kdyby některý
 * z údajů začala kreslit sama, stačí smazat jeho pár řádků.
 *
 * Na kartě osoby:
 * - **rodné jméno** Topola zná (`getMaidenName()` i vlastní čtečka `NAME`/`TYPE maiden`),
 *   ale žádný z jejích rendererů ho nekreslí — vložíme ho nad řádky událostí;
 * - **povolání** nezná vůbec; mapper ho posílá pod vlastním klíčem `occupation`, který pro
 *   Topolu není ničím — a je tam jen když si ho uživatel zapnul (Services/TreeViewSettings.cs).
 *   Přijde pod události, stejné pořadí jako v náhledu vybrané osoby;
 * - **místa** Topola kreslí sama, ale každé na vlastní řádek pod datem. Karta se čtyřmi
 *   řádky pod sebou se přestává číst, tak je sloučíme k datu do závorky
 *   („* 31. 7. 1980 (Godrikův Dol)"). Totéž na uzlu rodiny u sňatku.
 *
 * Velikost karty si Topola dopočítá z počtu a délky řádků (`getPreferredIndiSize`,
 * `getPreferredFamSize`), takže ani přidaný, ani prodloužený řádek nemá jak přetéct.
 *
 * Bundle zůstává nedotčený (princip 2: jeho sha256 v lib/topola/README.md platí dál).
 */
let detailsPatched = false;

function patchDetailsRendering() {
  if (detailsPatched || typeof topola === "undefined" || !topola.DetailedRenderer) {
    return;
  }

  const baseIndi = topola.DetailedRenderer.prototype.getIndiDetails;
  const baseFam = topola.DetailedRenderer.prototype.getFamDetails;
  if (typeof baseIndi !== "function" || typeof baseFam !== "function") {
    return; // jiná verze Topoly — radši nekreslit nic než spadnout
  }

  topola.DetailedRenderer.prototype.getIndiDetails = function (indi) {
    const details = baseIndi.call(this, indi);
    mergePlacesIntoDates(details, [placeOf(indi, "getBirthPlace"), placeOf(indi, "getDeathPlace")]);

    const maiden = typeof indi.getMaidenName === "function" ? indi.getMaidenName() : null;
    if (maiden) {
      // Bez symbolu: hvězdička a křížek mají v rodokmenu ustálený význam, „roz." se mezi ně neplete.
      details.unshift({ symbol: "", text: `roz. ${maiden}` });
    }

    const occupation = occupationOf(indi);
    if (occupation) {
      details.push({ symbol: "", text: occupation });
    }

    return details;
  };

  topola.DetailedRenderer.prototype.getFamDetails = function (fam) {
    const details = baseFam.call(this, fam);
    mergePlacesIntoDates(details, [placeOf(fam, "getMarriagePlace")]);
    return details;
  };

  detailsPatched = true;
}

/**
 * Místo z řádků do závorky k datu téže události.
 *
 * Řádek s místem poznáme podle textu, ne podle pozice — a sloučíme ho jen tehdy, když řádek
 * nad ním nese symbol události (* narození, + úmrtí, ⚭ sňatek). Topola totiž symbol dává
 * PRVNÍMU řádku události: je-li tam datum, je první ono a místo stojí hned pod ním; chybí-li
 * datum, je první samo místo — a to pak zůstává, jak je, i se svým symbolem. Podmínka se tak
 * stará sama o sebe a nespoléhá na to, kolik řádků která událost zrovna má.
 *
 * Jde se odzadu, aby mazání nerozhodilo dosud neprojité indexy. Trim je nutný: Topola skládá
 * datum jako [kvalifikátor, datum, text].join(" "), takže prázdné sloty nechávají mezery.
 */
function mergePlacesIntoDates(details, places) {
  const wanted = places.filter(Boolean);
  if (wanted.length === 0) {
    return;
  }

  for (let i = details.length - 1; i > 0; i--) {
    const line = details[i];
    if (!wanted.some((place) => line.text === place)) {
      continue; // není to řádek s místem
    }

    const above = details[i - 1];
    if (!above.symbol) {
      continue; // nad ním nezačíná událost — místo patří něčemu jinému
    }

    above.text = `${above.text.trim()} (${line.text.trim()})`;
    details.splice(i, 1);
  }
}

/**
 * Místo z osoby nebo rodiny. Getter Topola má, ale starší či novější verze ho mít nemusí —
 * bez něj se nesloučí nic a řádky zůstanou, jak byly.
 */
function placeOf(node, getter) {
  return typeof node[getter] === "function" ? node[getter]() : null;
}

/**
 * Povolání z osoby. Přes getter, kdyby ho Topola někdy dostala, jinak ze surového JSONu —
 * `JsonIndiDetails` si ho drží v `this.json` a my ho tam poslali.
 */
function occupationOf(indi) {
  if (typeof indi.getOccupation === "function") {
    return indi.getOccupation();
  }

  return (indi.json && indi.json.occupation) || null;
}

/**
 * Volby zobrazení (Services/TreeViewSettings.cs) na konstrukty Topoly. Neznámá hodnota
 * spadne na výchozí — nastavení z cizí nebo starší verze nesmí položit vykreslení rodokmenu.
 */
function chartTypeOf(name) {
  switch (name) {
    case "hourglass":
      return topola.HourglassChart;
    case "ancestors":
      return topola.AncestorChart;
    case "descendants":
      return topola.DescendantChart;
    default:
      return topola.RelativesChart;
  }
}

function colorsOf(name) {
  if (!topola.ChartColors) {
    return undefined; // jiná verze Topoly — ať si obarví po svém
  }

  switch (name) {
    case "byGeneration":
      return topola.ChartColors.COLOR_BY_GENERATION;
    case "none":
      return topola.ChartColors.NO_COLOR;
    default:
      return topola.ChartColors.COLOR_BY_SEX;
  }
}

/** @type {Map<string, { svg: SVGSVGElement, container: HTMLElement }>} */
const instances = new Map();

/**
 * @param {string} elementId id kontejnerového divu
 * @param {string} dataJson JsonGedcomData ({indis, fams}) z TopolaDataMapper
 * @param {string|null} rootId výchozí kořenová osoba (null = topola vybere sama)
 * @param {string|null} viewJson volby zobrazení z TreeViewSettings.ToJson() (null = výchozí)
 * @param {object} dotNetRef DotNetObjectReference na TopolaTree
 */
export function init(elementId, dataJson, rootId, viewJson, dotNetRef) {
  destroy(elementId); // opakovaný init na tomtéž elementu nesmí hromadit instance

  const container = document.getElementById(elementId);
  if (!container) {
    throw new Error(`KorenyTopola.init: element '${elementId}' not found`);
  }

  const data = JSON.parse(dataJson);
  if (!data || !Array.isArray(data.indis) || data.indis.length === 0) {
    return; // prázdný dokument — není co kreslit
  }

  const viewOptions = viewJson ? JSON.parse(viewJson) : {};

  patchDetailsRendering(); // až tady: dřív nemusí být bundle `topola` na window

  const svgId = `${elementId}-svg`;
  container.innerHTML = `<svg id="${svgId}"></svg>`;

  const chart = topola.createChart({
    json: data,
    chartType: chartTypeOf(viewOptions.chartType),
    renderer: topola.DetailedRenderer,
    svgSelector: `#${svgId}`,
    // Topola si data vypisuje sama; bez locale by z „3 MAR 1901" nakreslila anglické „3 Mar 1901"
    // a kvalifikátory („abt") by nechala, jak jsou. S „cs" vyjde „3. 3. 1901" a „okolo 1850" —
    // tedy totéž, co kreslí náhled přes Services/GedcomDate.cs.
    locale: "cs",
    animate: false, // synchronní render — funguje i ve skrytém dokumentu
    horizontal: !!viewOptions.horizontal,
    colors: colorsOf(viewOptions.colors),
    indiCallback: (info) => {
      if (info && info.id && dotNetRef) {
        dotNetRef.invokeMethodAsync("OnPersonClicked", info.id);
      }
    },
  });

  const rootExists = rootId && data.indis.some((i) => i.id === rootId);
  chart.render(rootExists ? { startIndi: rootId } : undefined);

  const svg = document.getElementById(svgId);
  const root = svg ? svg.querySelector("g") : null;
  if (!svg || !root) {
    return;
  }

  // Obal topola root <g> do <g class="view"> pro zoom + export (topola po renderu DOM nemění).
  const view = document.createElementNS(SVG_NS, "g");
  view.setAttribute("class", "view");
  svg.insertBefore(view, root);
  view.appendChild(root);

  // SVG vyplní kontejner; viewBox z bbox obsahu dá počáteční „fit" celého stromu.
  const bbox = view.getBBox();
  svg.setAttribute(
    "viewBox",
    `${bbox.x - EXPORT_PADDING} ${bbox.y - EXPORT_PADDING} ${bbox.width + 2 * EXPORT_PADDING} ${bbox.height + 2 * EXPORT_PADDING}`);
  svg.setAttribute("width", "100%");
  svg.setAttribute("height", "100%");
  svg.style.display = "block";
  svg.style.width = "100%";
  svg.style.height = "100%";
  svg.style.cursor = "grab";

  const d3view = d3.select(view);
  const zoom = d3.zoom()
    .scaleExtent([0.1, 4])
    .on("zoom", (event) => d3view.attr("transform", event.transform));
  d3.select(svg).call(zoom);

  instances.set(elementId, { svg, container });
}

export function destroy(elementId) {
  const inst = instances.get(elementId);
  if (inst) {
    d3.select(inst.svg).on(".zoom", null);
    instances.delete(elementId);
  }

  const container = document.getElementById(elementId);
  if (container) {
    container.innerHTML = "";
  }
}

/**
 * Export celého rodokmenu jako samostatný SVG string, nezávislý na pan/zoom.
 * Topola vkládá svoje CSS do embedded <style> uvnitř svg, které se klonuje se stromem,
 * takže není potřeba žádná injektáž CSS (sdílený svg-export se volá bez cssText).
 *
 * @param {string} elementId id kontejneru předaný do init
 * @returns {string} serializované SVG
 */
export function exportSvg(elementId) {
  const container = document.getElementById(elementId);
  const svg = container ? container.querySelector("svg") : null;
  if (!svg) {
    throw new Error(`KorenyTopola.exportSvg: rodokmen v elementu '${elementId}' nenalezen`);
  }

  return exportSvgString(svg, { viewSelector: "g.view" });
}
