/**
 * KorenyTopola — interop modul pro Topola (PeWu/topola), Relatives chart.
 * Očekává globály `topola` (vendorovaný self-contained bundle) a `d3` (vendorované 7.9.0).
 *
 * init(elementId, dataJson, rootId, dotNetRef) — vyrenderuje rodokmen do kontejneru.
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

/** @type {Map<string, { svg: SVGSVGElement, container: HTMLElement }>} */
const instances = new Map();

/**
 * @param {string} elementId id kontejnerového divu
 * @param {string} dataJson JsonGedcomData ({indis, fams}) z TopolaDataMapper
 * @param {string|null} rootId výchozí kořenová osoba (null = topola vybere sama)
 * @param {object} dotNetRef DotNetObjectReference na TopolaTree
 */
export function init(elementId, dataJson, rootId, dotNetRef) {
  destroy(elementId); // opakovaný init na tomtéž elementu nesmí hromadit instance

  const container = document.getElementById(elementId);
  if (!container) {
    throw new Error(`KorenyTopola.init: element '${elementId}' not found`);
  }

  const data = JSON.parse(dataJson);
  if (!data || !Array.isArray(data.indis) || data.indis.length === 0) {
    return; // prázdný dokument — není co kreslit
  }

  const svgId = `${elementId}-svg`;
  container.innerHTML = `<svg id="${svgId}"></svg>`;

  const chart = topola.createChart({
    json: data,
    chartType: topola.RelativesChart,
    renderer: topola.DetailedRenderer,
    svgSelector: `#${svgId}`,
    animate: false, // synchronní render — funguje i ve skrytém dokumentu
    colors: topola.ChartColors ? topola.ChartColors.COLOR_BY_SEX : undefined,
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
