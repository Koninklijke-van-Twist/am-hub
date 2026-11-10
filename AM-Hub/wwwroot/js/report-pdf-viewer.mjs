const assets = new URL("../lib/pdfjs/", import.meta.url);
let library;
function loadLibrary() {
    return library ??= import(new URL("pdf.mjs", assets).href).then(pdfjs => {
        pdfjs.GlobalWorkerOptions.workerSrc = new URL("pdf.worker.mjs", assets).href;
        return pdfjs;
    });
}

class HubPdfViewer extends HTMLElement {
    connectedCallback() {
        if (this.controller) return;
        this.controller = new AbortController();
        this.firstPage = 1;
        this.columns = 1;
        this.zoom = 1;
        this.revision = 0;
        this.tasks = new Set();
        this.renderQueue = Promise.resolve();
        this.innerHTML = `
            <div class="hub-pdf-toolbar" role="group" aria-label="Rapportbediening">
                <div class="hub-pdf-navigation">
                    <button type="button" data-action="previous" aria-label="Vorige pagina’s" disabled>‹ <span>Vorige</span></button>
                    <span class="hub-pdf-position" role="status" aria-live="polite">Rapport laden…</span>
                    <button type="button" data-action="next" aria-label="Volgende pagina’s" disabled><span>Volgende</span> ›</button>
                </div>
                <div class="hub-pdf-external"></div>
                <div class="hub-pdf-zoom">
                    <button type="button" data-action="out" aria-label="Uitzoomen" disabled>−</button>
                    <button type="button" data-action="fit" title="Terug naar 100%" disabled>100%</button>
                    <button type="button" data-action="in" aria-label="Inzoomen" disabled>+</button>
                </div>
            </div>
            <p class="hub-pdf-message" role="status">Rapport laden…</p>
            <div class="hub-pdf-stage" tabindex="0" aria-label="Rapportpagina’s"></div>`;
        this.stage = this.querySelector(".hub-pdf-stage");
        this.message = this.querySelector(".hub-pdf-message");
        this.position = this.querySelector(".hub-pdf-position");
        if (this.dataset.sharepointUrl) {
            try {
                const url = new URL(this.dataset.sharepointUrl);
                if (url.protocol === "https:" && !url.username && !url.password) {
                    const link = document.createElement("a");
                    link.href = url.href;
                    link.target = "_blank";
                    link.rel = "noopener noreferrer";
                    link.textContent = "Open in SharePoint ↗";
                    this.querySelector(".hub-pdf-external").append(link);
                }
            } catch { /* An invalid optional link must not prevent rendering the PDF. */ }
        }
        this.addEventListener("click", event => {
            const button = event.target.closest("button[data-action]");
            if (!button || button.disabled || !this.pdf) return;
            switch (button.dataset.action) {
                case "previous": this.firstPage = Math.max(1, this.firstPage - this.columns); break;
                case "next": this.firstPage = Math.min(this.pdf.numPages, this.firstPage + this.columns); break;
                case "out": this.zoom = Math.max(0.75, this.zoom - 0.25); break;
                case "in": this.zoom = Math.min(2, this.zoom + 0.25); break;
                case "fit": this.zoom = 1; break;
            }
            this.scheduleRender();
        }, { signal: this.controller.signal });
        this.stage.addEventListener("keydown", event => {
            if (event.key !== "ArrowLeft" && event.key !== "ArrowRight") return;
            const action = event.key === "ArrowLeft" ? "previous" : "next";
            this.querySelector(`[data-action="${action}"]`).click();
            event.preventDefault();
        }, { signal: this.controller.signal });
        this.observer = new ResizeObserver(() => {
            // Scrollbars change clientWidth/clientHeight while rendering, not the viewer's
            // actual allocated space. Observing those changes can create a redraw loop.
            const bounds = this.stage.getBoundingClientRect();
            const width = Math.round(bounds.width);
            const height = Math.round(bounds.height);
            if (width === this.width && height === this.height) return;
            this.width = width;
            this.height = height;
            if (this.pdf) this.scheduleRender();
        });
        this.observer.observe(this.stage, { box: "border-box" });
        this.load(this.controller.signal);
    }

    async load(signal) {
        try {
            const source = new URL(this.dataset.pdfUrl, document.baseURI);
            if (source.origin !== location.origin) throw new Error("Ongeldige rapportlocatie.");
            const [pdfjs, response] = await Promise.all([
                loadLibrary(),
                fetch(source, { credentials: "same-origin", mode: "same-origin", redirect: "error", cache: "no-store", signal })
            ]);
            if (signal.aborted) return;
            if (!response.ok) {
                const text = response.headers.get("content-type")?.startsWith("text/plain") ? await response.text() : "";
                throw new Error(text.slice(0, 500) || "Het rapport kon niet worden opgehaald. Open het rechtstreeks in SharePoint.");
            }
            if (!response.headers.get("content-type")?.includes("application/pdf"))
                throw new Error("De server heeft geen PDF teruggegeven. Vernieuw de pagina en meld je zo nodig opnieuw aan.");
            const data = new Uint8Array(await response.arrayBuffer());
            if (signal.aborted) return;
            this.pdfjs = pdfjs;
            this.loadingTask = pdfjs.getDocument({
                data,
                cMapUrl: new URL("cmaps/", assets).href,
                cMapPacked: true,
                standardFontDataUrl: new URL("standard_fonts/", assets).href,
                wasmUrl: new URL("wasm/", assets).href,
                isEvalSupported: false,
                enableXfa: false
            });
            this.pdf = await this.loadingTask.promise;
            if (!signal.aborted) this.scheduleRender();
        } catch (error) {
            if (!signal.aborted) this.showError(error);
        }
    }

    scheduleRender() {
        const revision = ++this.revision;
        clearTimeout(this.timer);
        for (const task of this.tasks) task.cancel();
        this.timer = setTimeout(() => {
            this.renderQueue = this.renderQueue.then(() => this.renderPages(revision)).catch(error => {
                if (revision === this.revision && !this.controller?.signal.aborted) this.showError(error);
            });
        }, 80);
    }

    async renderPages(revision) {
        if (revision !== this.revision || !this.pdf || !this.isConnected) return;
        const signal = this.controller.signal;
        const width = Math.max(1, this.stage.clientWidth - 40);
        const height = Math.max(1, this.stage.clientHeight - 40);
        // Two pages are only shown when both fit at the current zoom level.
        // Account for each page's aspect ratio, including mixed portrait/landscape reports.
        this.firstPage = Math.max(1, Math.min(this.firstPage, this.pdf.numPages));
        const pairStart = this.firstPage - (this.firstPage - 1) % 2;
        const left = await this.pdf.getPage(pairStart);
        const right = await this.pdf.getPage(Math.min(pairStart + 1, this.pdf.numPages));
        if (revision !== this.revision || signal.aborted) return;
        const leftSize = left.getViewport({ scale: 1 });
        const rightSize = right.getViewport({ scale: 1 });
        const pairWidth = height * (leftSize.width / leftSize.height + rightSize.width / rightSize.height) * this.zoom + 20;
        this.columns = this.pdf.numPages > 1 && width >= Math.max(920, pairWidth) ? 2 : 1;
        if (this.columns === 2) this.firstPage -= (this.firstPage - 1) % 2;
        const last = Math.min(this.firstPage + this.columns - 1, this.pdf.numPages);
        const visible = await Promise.all(Array.from({ length: last - this.firstPage + 1 }, (_, index) => this.pdf.getPage(this.firstPage + index)));
        if (revision !== this.revision || signal.aborted) return;
        const sizes = visible.map(page => page.getViewport({ scale: 1 }));
        const fitScale = Math.min(
            (width - (visible.length - 1) * 20) / sizes.reduce((sum, size) => sum + size.width, 0),
            height / Math.max(...sizes.map(size => size.height))
        );
        this.updateControls(last);
        this.setAttribute("aria-busy", "true");
        this.message.hidden = this.stage.childElementCount > 0;
        this.message.textContent = "Pagina’s laden…";
        // Render the complete spread off-screen. Keep the existing pages visible
        // until both canvases and their text layers are ready to replace them.
        const spread = document.createElement("div");
        spread.className = "hub-pdf-spread";
        try {
            // Only the visible one or two pages are rendered. No full-document canvas allocation.
            for (let number = this.firstPage; number <= last; number++) {
                if (revision !== this.revision || signal.aborted) return;
                const page = await this.pdf.getPage(number);
                if (revision !== this.revision || signal.aborted) return;
                const viewport = page.getViewport({ scale: fitScale * this.zoom });
                const sheet = document.createElement("section");
                sheet.className = "hub-pdf-sheet";
                sheet.setAttribute("aria-label", `Pagina ${number} van ${this.pdf.numPages}`);
                sheet.style.width = `${viewport.width}px`;
                sheet.style.height = `${viewport.height}px`;
                sheet.style.setProperty("--total-scale-factor", viewport.scale);
                sheet.style.setProperty("--scale-round-x", "1px");
                sheet.style.setProperty("--scale-round-y", "1px");
                const canvas = document.createElement("canvas");
                canvas.setAttribute("aria-hidden", "true");
                const outputScale = Math.min(window.devicePixelRatio || 1, 2, Math.sqrt(12_000_000 / (viewport.width * viewport.height)));
                canvas.width = Math.floor(viewport.width * outputScale);
                canvas.height = Math.floor(viewport.height * outputScale);
                sheet.append(canvas);
                spread.append(sheet);
                const rendering = page.render({
                    canvasContext: canvas.getContext("2d"),
                    viewport,
                    transform: outputScale === 1 ? null : [outputScale, 0, 0, outputScale, 0, 0],
                    background: "white"
                });
                this.tasks.add(rendering);
                try { await rendering.promise; }
                finally { this.tasks.delete(rendering); }
                if (revision !== this.revision || signal.aborted) return;
                const textContainer = document.createElement("div");
                textContainer.className = "textLayer";
                sheet.append(textContainer);
                const text = new this.pdfjs.TextLayer({ textContentSource: page.streamTextContent(), container: textContainer, viewport });
                this.tasks.add(text);
                try { await text.render(); }
                finally { this.tasks.delete(text); }
                page.cleanup();
            }
            if (revision === this.revision) {
                this.stage.replaceChildren(spread);
                this.message.hidden = true;
                this.setAttribute("aria-busy", "false");
            }
        } catch (error) {
            if (revision === this.revision && !signal.aborted) throw error;
        }
    }

    updateControls(last) {
        const total = this.pdf.numPages;
        this.position.textContent = this.firstPage === last ? `Pagina ${this.firstPage} van ${total}` : `Pagina ${this.firstPage}–${last} van ${total}`;
        this.querySelector('[data-action="previous"]').disabled = this.firstPage <= 1;
        this.querySelector('[data-action="next"]').disabled = last >= total;
        this.querySelector('[data-action="out"]').disabled = this.zoom <= 0.75;
        this.querySelector('[data-action="in"]').disabled = this.zoom >= 2;
        const fit = this.querySelector('[data-action="fit"]');
        fit.disabled = false;
        fit.textContent = `${Math.round(this.zoom * 100)}%`;
    }

    showError(error) {
        this.setAttribute("aria-busy", "false");
        this.stage.replaceChildren();
        this.message.hidden = false;
        this.message.setAttribute("role", "alert");
        this.message.textContent = error instanceof Error && !["TypeError", "InvalidPDFException", "PasswordException"].includes(error.name)
            ? error.message
            : "Het rapport kon niet worden weergegeven. Open de PDF via de link hierboven of vernieuw de pagina.";
        this.position.textContent = "Rapport niet beschikbaar";
        this.querySelectorAll("button").forEach(button => button.disabled = true);
    }

    disconnectedCallback() {
        clearTimeout(this.timer);
        this.revision++;
        this.controller?.abort();
        this.observer?.disconnect();
        for (const task of this.tasks || []) task.cancel();
        this.loadingTask?.destroy().catch(() => {});
        this.pdf = null;
        this.controller = null;
    }
}

if (!customElements.get("hub-pdf-viewer")) customElements.define("hub-pdf-viewer", HubPdfViewer);
