/**
 * KorenyF3 — interop modul pro family-chart (f3).
 * Očekává globály `f3` a `d3` (vendorované v lib/, načtené v index.html).
 *
 * init(elementId, dataJson, rootId, dotNetRef) — vytvoří graf v elementu.
 * destroy(elementId) — uklidí instanci a vyprázdní kontejner.
 * Klik na kartu volá dotNetRef.invokeMethodAsync("OnPersonClicked", id).
 */

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
  chart.setTransitionTime(400);
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
  instances.set(elementId, { chart, dotNetRef });
}

export function destroy(elementId) {
  instances.delete(elementId);
  const container = document.getElementById(elementId);
  if (container) {
    container.innerHTML = "";
  }
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
