# PDF.js

Version: **6.5.438**, Mozilla PDF.js, Apache-2.0 (see LICENSE).

Source: https://github.com/mozilla/pdf.js/releases/tag/v6.5.438
Archive: `pdfjs-6.5.438-dist.zip` from that release.

The production bundles (`build/pdf.mjs`, `pdf.worker.mjs`, `pdf.renderer.mjs`) and supporting `web/cmaps`, `standard_fonts`, `wasm`, and `iccs` are hosted locally. No runtime CDN or external document service is used. `text_layer.css` contains the `.textLayer` styles extracted from the release's `web/viewer.css` to support text selection without the standard viewer UI. The archive's viewer HTML, scripts and toolbar are not used.

When upgrading, keep the library, worker, resources and text-layer styles on the same version and run the viewer browser checks.
