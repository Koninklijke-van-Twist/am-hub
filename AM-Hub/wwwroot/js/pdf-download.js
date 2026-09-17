// Delegation also handles pages inserted by Blazor enhanced navigation.
document.addEventListener("click", async event => {
    const link = event.target.closest("button[data-pdf-download]");
    if (!link) return;
    event.preventDefault();
    if (link.dataset.busy === "true") return;

    const status = link.parentElement.querySelector(".pdf-download-status");
    const label = link.textContent;
    link.dataset.busy = "true";
    link.disabled = true;
    link.setAttribute("aria-disabled", "true");
    link.setAttribute("aria-busy", "true");
    link.textContent = "PDF wordt gemaakt…";
    status.textContent = "Even geduld, je download wordt voorbereid.";

    try {
        const response = await fetch(new URL(link.dataset.url, document.baseURI), { credentials: "same-origin" });
        if (!response.ok || !response.headers.get("content-type")?.toLowerCase().includes("application/pdf")) {
            throw new Error("PDF request failed");
        }
        const blob = await response.blob();
        const url = URL.createObjectURL(blob);
        const download = document.createElement("a");
        download.href = url;
        download.download = link.dataset.filename;
        document.body.appendChild(download);
        download.click();
        download.remove();
        setTimeout(() => URL.revokeObjectURL(url), 60000);
        status.textContent = "De PDF is klaar en de download is gestart.";
    } catch {
        status.textContent = "Downloaden is mislukt. Probeer opnieuw of vernieuw de pagina.";
    } finally {
        delete link.dataset.busy;
        link.disabled = false;
        link.removeAttribute("aria-disabled");
        link.removeAttribute("aria-busy");
        link.textContent = label;
    }
});
