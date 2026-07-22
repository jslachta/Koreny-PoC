# Vendored Topola

Self-contained browser bundle of [Topola](https://github.com/PeWu/topola) (Relatives chart).

- **topola version:** 3.10.3 (pinned in `tools/bundle-topola/package.json` + `package-lock.json`)
- **License:** Apache-2.0 (see `LICENSE`)
- **File:** `topola.bundle.js` — IIFE, global `topola`, **all dependencies inlined**
  (d3-array, d3-hierarchy, d3-selection, d3-transition, d3-flextree, parse-gedcom,
  array-flat-polyfill). No CDN import, no runtime `require`, works offline.
- **sha256(topola.bundle.js):** `fd5c68cde4a1ccd9e5395c095d49abb1a16dd31393c80da351b1d6b7699ba3a2`

Zoom/pan is **not** provided by Topola; `js/topola-view.js` attaches `d3.zoom` from the
separately vendored d3 7.9.0 (`lib/d3/d3.min.js`).

## Regenerating (author-time only)

The app runs from the committed `topola.bundle.js` — this is **not** part of `dotnet run`.
To rebuild after a version bump (edit `tools/bundle-topola/package.json`), with Node.js installed:

```
npm ci      --prefix tools/bundle-topola
npm run build --prefix tools/bundle-topola
```

`build.mjs` (esbuild, IIFE, `globalName: "topola"`) writes this file and prints its sha256;
update the hash above when it changes.
