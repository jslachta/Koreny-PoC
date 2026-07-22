/**
 * Sdílená serializace živého SVG do samostatného stringu, nezávislého na pan/zoom.
 * Dva konzumenti: f3-view.js (s injektáží f3 CSS) a graph-view.js (inline atributy, bez CSS).
 * Originál se nedotýká — pracuje se s klonem, takže overlay zůstává interaktivní.
 */

const EXPORT_PADDING = 20;

/**
 * @param {SVGSVGElement} svg živé SVG v DOM (musí být v DOM kvůli getBBox)
 * @param {object} [opts]
 * @param {string} [opts.viewSelector="g.view"] skupina nesoucí zoom transformaci
 * @param {string|null} [opts.cssText=null] CSS vložené do <style> klonu (jen f3 cesta)
 * @param {string|null} [opts.rootClass=null] třída přidaná na kořenové <svg> klonu (kvůli CSS selektorům)
 * @returns {string} serializované SVG
 */
export function exportSvgString(svg, { viewSelector = "g.view", cssText = null, rootClass = null } = {}) {
  const view = svg.querySelector(viewSelector);
  if (!view) {
    throw new Error(`svg-export: '${viewSelector}' nenalezeno`);
  }

  // getBBox nad ORIGINÁLEM (klon mimo DOM getBBox neumí). getBBox ignoruje vlastní
  // transformaci elementu, takže vrací nezoomované souřadnice obsahu — přesně to,
  // co export potřebuje.
  const bbox = view.getBBox();

  const clone = svg.cloneNode(true);
  const cloneView = clone.querySelector(viewSelector);
  cloneView.style.transform = "";       // f3 drží zoom jako inline style
  cloneView.removeAttribute("transform"); // graph drží zoom jako SVG atribut

  if (rootClass) {
    clone.classList.add(rootClass);
  }

  const w = bbox.width + 2 * EXPORT_PADDING;
  const h = bbox.height + 2 * EXPORT_PADDING;
  clone.setAttribute("viewBox", `${bbox.x - EXPORT_PADDING} ${bbox.y - EXPORT_PADDING} ${w} ${h}`);
  clone.setAttribute("width", String(w));
  clone.setAttribute("height", String(h));
  clone.setAttribute("xmlns", "http://www.w3.org/2000/svg");

  if (cssText) {
    const styleEl = document.createElementNS("http://www.w3.org/2000/svg", "style");
    styleEl.textContent = cssText;
    clone.insertBefore(styleEl, clone.firstChild);
  }

  return new XMLSerializer().serializeToString(clone);
}
