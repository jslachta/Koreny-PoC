/**
 * KorenyF3 — interop modul pro family-chart (f3).
 * Očekává globály `f3` a `d3` (vendorované v lib/, načtené v index.html).
 *
 * init(elementId, dataJson, rootId, dotNetRef) — vytvoří graf v elementu.
 * destroy(elementId) — uklidí instanci a vyprázdní kontejner.
 * exportSvg(elementId) — vrátí samostatný SVG string celého stromu.
 * Klik na kartu volá dotNetRef.invokeMethodAsync("OnPersonClicked", id).
 */

import { exportSvgString } from "./svg-export.js";

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
 * Export celého stromu jako samostatný SVG string, nezávislý na aktuálním pan/zoom.
 * Klon/reset-zoom/bbox/serializace řeší sdílený svg-export.js; f3-specifická je jen
 * injektáž vendorovaného f3 CSS (fetch, jediný zdroj pravdy) a třída "f3" na kořeni,
 * aby `.f3 …` selektory platily i mimo původní rodičovský div (karty f3 se stylují CSS,
 * ne inline atributy — na rozdíl od FullGraphu).
 *
 * @param {string} elementId id kontejneru předaný do init
 * @returns {Promise<string>} serializované SVG
 */
export async function exportSvg(elementId) {
  const container = document.getElementById(elementId);
  const svg = container ? container.querySelector("svg.main_svg") : null;
  if (!svg) {
    throw new Error(`KorenyF3.exportSvg: strom v elementu '${elementId}' nenalezen`);
  }

  const cssResponse = await fetch(F3_CSS_URL);
  if (!cssResponse.ok) {
    throw new Error(`KorenyF3.exportSvg: nelze načíst ${F3_CSS_URL} (${cssResponse.status})`);
  }

  const cssText = await cssResponse.text();
  return exportSvgString(svg, { viewSelector: "g.view", cssText, rootClass: "f3" });
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
