/**
 * KorenyGraph — interop modul pro živý FullGraph ("Celý graf").
 * Očekává globál `d3` (vendorované d3 7.9.0, načtené v index.html).
 *
 * init(elementId, svgString, dotNetRef) — vloží SVG do kontejneru, připne pan/zoom a klik.
 * destroy(elementId) — odpojí listenery a vyprázdní kontejner.
 * exportSvg(elementId) — vrátí samostatný SVG string celého grafu (nezávislý na pan/zoom).
 *
 * SVG se vkládá přes innerHTML (ne Blazor MarkupString): JS vlastní celý SVG podstrom,
 * takže re-render Blazoru na něj nesahá a neshodí d3 zoom/click handlery. Stejný model
 * vlastnictví kontejneru jako f3-view.js.
 *
 * Klik na skupinu osoby (g[data-person-id]) volá dotNetRef.invokeMethodAsync("OnPersonClicked", id).
 */

import { exportSvgString } from "./svg-export.js";

/** @type {Map<string, { svg: SVGSVGElement, container: HTMLElement, onClick: Function }>} */
const instances = new Map();

/**
 * @param {string} elementId id kontejnerového divu
 * @param {string} svgString SVG vygenerované FullGraphem (obsahuje g.view a g[data-person-id])
 * @param {object} dotNetRef DotNetObjectReference na FullGraphView
 */
export function init(elementId, svgString, dotNetRef) {
  destroy(elementId); // opakovaný init na tomtéž elementu nesmí hromadit instance

  const container = document.getElementById(elementId);
  if (!container) {
    throw new Error(`KorenyGraph.init: element '${elementId}' not found`);
  }

  container.innerHTML = svgString;

  const svg = container.querySelector("svg");
  const view = svg ? svg.querySelector("g.view") : null;
  if (!svg || !view) {
    return; // prázdný/placeholder graf — není co ovládat
  }

  // SVG vyplní kontejner; viewBox z FullGraphu dá počáteční „fit" celého grafu.
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

  const onClick = (event) => {
    const g = event.target.closest("g[data-person-id]");
    if (g && dotNetRef) {
      dotNetRef.invokeMethodAsync("OnPersonClicked", g.getAttribute("data-person-id"));
    }
  };
  container.addEventListener("click", onClick);

  instances.set(elementId, { svg, container, onClick });
}

export function destroy(elementId) {
  const inst = instances.get(elementId);
  if (inst) {
    d3.select(inst.svg).on(".zoom", null);
    inst.container.removeEventListener("click", inst.onClick);
    instances.delete(elementId);
  }

  const container = document.getElementById(elementId);
  if (container) {
    container.innerHTML = "";
  }
}

/**
 * FullGraph se styluje inline atributy (fill/stroke/font-size), takže export nepotřebuje
 * žádnou injektáž CSS — na rozdíl od f3 cesty. Ověřeno čtením generátoru FullGraph.razor.
 *
 * @param {string} elementId id kontejneru předaný do init
 * @returns {string} serializované SVG
 */
export function exportSvg(elementId) {
  const container = document.getElementById(elementId);
  const svg = container ? container.querySelector("svg") : null;
  if (!svg) {
    throw new Error(`KorenyGraph.exportSvg: graf v elementu '${elementId}' nenalezen`);
  }

  return exportSvgString(svg, { viewSelector: "g.view" });
}
