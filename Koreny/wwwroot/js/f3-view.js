/**
 * KorenyF3 — interop modul pro family-chart (f3).
 * Očekává globály `f3` a `d3` (vendorované v lib/, načtené v index.html).
 *
 * init(elementId, dataJson, rootId, dotNetRef) — vytvoří graf v elementu.
 * destroy(elementId) — uklidí instanci a vyprázdní kontejner.
 * exportSvg(elementId) — vrátí samostatný SVG string celého stromu.
 * Klik na kartu volá dotNetRef.invokeMethodAsync("OnPersonClicked", id).
 */

const EXPORT_PADDING = 20;
const F3_CSS_URL = "lib/family-chart/family-chart.css";

/** @type {Map<string, { chart: object, dotNetRef: object }>} */
const instances = new Map();

/**
 * @param {string} elementId id kontejneru (div.f3)
 * @param {string} dataJson JSON pole osob ve formátu f3 (viz F3DataMapper)
 * @param {string|null} rootId výchozí hlavní osoba (null = první v datech)
 * @param {object} dotNetRef DotNetObjectReference na F3Tree
 */
export function init(elementId, dataJson, rootId, dotNetRef) {
  destroy(elementId); // opakovaný init na tomtéž elementu nesmí hromadit instance

  const container = document.getElementById(elementId);
  if (!container) {
    throw new Error(`KorenyF3.init: element '${elementId}' not found`);
  }

  const data = JSON.parse(dataJson);
  if (!Array.isArray(data) || data.length === 0) {
    return; // prázdný dokument — není co kreslit
  }

  const chart = f3.createChart(container, data);
  // Ve skrytém dokumentu (background tab, headless verifikace) rAF nikdy neběží,
  // takže d3 přechody by zůstaly viset na startovních pozicích. Bez animací +
  // timerFlush níže se strom položí na finální pozice synchronně.
  chart.setTransitionTime(document.hidden ? 0 : 400);
  chart.setCardYSpacing(140);
  chart.setCardXSpacing(220);
  chart.setSingleParentEmptyCard(false);

  const card = chart.setCardSvg();
  card.setCardDisplay([
    d => fullName(personData(d)),
    d => personData(d)["years"] || "",
  ]);
  card.setMiniTree(true);
  card.setOnCardClick((e, d) => {
    const id = d && d.data ? d.data.id : null;
    if (id && dotNetRef) {
      dotNetRef.invokeMethodAsync("OnPersonClicked", id);
    }
  });

  if (rootId && data.some(p => p.id === rootId)) {
    chart.store.updateMainId(rootId);
  }

  chart.updateTree({ initial: true, tree_position: "fit" });
  if (document.hidden) {
    d3.timerFlush(); // dokonči duration-0 přechody bez čekání na rAF
  }

  instances.set(elementId, { chart, dotNetRef });
}

export function destroy(elementId) {
  instances.delete(elementId);
  const container = document.getElementById(elementId);
  if (container) {
    container.innerHTML = "";
  }
}

/**
 * Export celého stromu jako samostatný SVG string, nezávislý na aktuálním
 * pan/zoom stavu. Originální DOM se nemění — pracuje se s klonem.
 *
 * Poznámky k implementaci (viz docs/session-5-vysledky.md):
 * - zoom drží f3 jako INLINE CSS `style="transform: …"` na g.view (ne SVG atribut),
 *   reset v klonu tedy maže style.transform;
 * - bounding box se čte getBBox() nad g.view ORIGINÁLU (klon mimo DOM getBBox
 *   neumí); getBBox ignoruje vlastní CSS transform elementu, takže vrací
 *   nezoomované souřadnice obsahu — přesně to, co export potřebuje;
 * - f3 CSS se načítá fetch-em z vendorovaného souboru (jediný zdroj pravdy)
 *   a vkládá do <style> v klonu; klonu se přidává třída "f3", aby selektory
 *   `.f3 …` platily i bez původního rodičovského divu.
 *
 * @param {string} elementId id kontejneru předaný do init
 * @returns {Promise<string>} serializované SVG
 */
export async function exportSvg(elementId) {
  const container = document.getElementById(elementId);
  const svg = container ? container.querySelector("svg.main_svg") : null;
  const view = svg ? svg.querySelector("g.view") : null;
  if (!svg || !view) {
    throw new Error(`KorenyF3.exportSvg: strom v elementu '${elementId}' nenalezen`);
  }

  const bbox = view.getBBox();

  const clone = svg.cloneNode(true);
  const cloneView = clone.querySelector("g.view");
  cloneView.style.transform = "";
  cloneView.removeAttribute("transform");

  clone.classList.add("f3");
  const w = bbox.width + 2 * EXPORT_PADDING;
  const h = bbox.height + 2 * EXPORT_PADDING;
  clone.setAttribute("viewBox", `${bbox.x - EXPORT_PADDING} ${bbox.y - EXPORT_PADDING} ${w} ${h}`);
  clone.setAttribute("width", String(w));
  clone.setAttribute("height", String(h));
  clone.setAttribute("xmlns", "http://www.w3.org/2000/svg");

  const cssResponse = await fetch(F3_CSS_URL);
  if (!cssResponse.ok) {
    throw new Error(`KorenyF3.exportSvg: nelze načíst ${F3_CSS_URL} (${cssResponse.status})`);
  }

  const styleEl = document.createElementNS("http://www.w3.org/2000/svg", "style");
  styleEl.textContent = await cssResponse.text();
  clone.insertBefore(styleEl, clone.firstChild);

  return new XMLSerializer().serializeToString(clone);
}

/** card_display dostává TreeDatum i Datum podle kontextu — sáhni na osobní data oběma cestami. */
function personData(d) {
  if (d && d.data && d.data.data) {
    return d.data.data;
  }

  if (d && d.data) {
    return d.data;
  }

  return d || {};
}

function fullName(pd) {
  const name = [pd["first name"], pd["last name"]].filter(Boolean).join(" ").trim();
  return name.length > 0 ? name : "?";
}
