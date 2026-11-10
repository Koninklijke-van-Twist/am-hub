using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

static class ViewerBrowserChecks
{
    public static async Task RunAsync()
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        var root = Directory.GetCurrentDirectory();
        var webRoot = Path.Combine(root, "AM-Hub", "wwwroot");
        if (!Directory.Exists(webRoot)) throw new Exception("Run viewer checks from the repository root.");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { WebRootPath = webRoot });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        app.UsePathBase("/base");
        app.UseStaticFiles();
        var requests = 0;
        app.MapGet("/fixture.pdf", () => { requests++; return Results.File(CreatePdf(), "application/pdf"); });
        app.MapGet("/denied.pdf", () => Results.Text("Geen toegang tot dit testdocument.", statusCode: 403));
        app.MapGet("/detail.css", () => Results.Content(File.ReadAllText(Path.Combine(root, "AM-Hub", "Components", "Pages", "ServiceCoordination", "ReportDetails.razor.css")).Replace("::deep ", ""), "text/css"));
        app.MapGet("/fixture", (bool? denied) => Results.Content("""
            <!DOCTYPE html><html lang="nl"><head><meta charset="utf-8"><base href="/base/">
            <link rel="stylesheet" href="app.css"><link rel="stylesheet" href="detail.css"><link rel="stylesheet" href="lib/pdfjs/text_layer.css"><link rel="stylesheet" href="css/report-pdf-viewer.css">
            <style>body{margin:0;background:#f3f8fb;font:16px Arial;color:#002b68}.test-topbar{height:4.5rem;background:#eaf7fc}main{margin-left:250px;padding:1.1rem 24px 0}.test-sidebar{position:absolute;top:0;left:0;width:250px;height:100vh;background:#003b70}@media(max-width:640px){main{margin:0;padding-inline:12px}.test-sidebar{display:none}}</style>
            </head><body><div class="test-sidebar"></div><div class="test-topbar"></div><main>
            <section class="report-detail"><div class="report-layout"><div class="workorder-column"><header class="workorder-intro"><a class="back">Rapporten</a><h1>Werkorder WO2605670</h1><p>Rapport en werkordergegevens.</p></header>
            <aside class="workorder-information"><h2>Werkordergegevens</h2><dl class="order-summary"><div><dt>Taak</dt><dd>CO</dd></div><div><dt>Taakomschrijving</dt><dd>Correctief onderhoud</dd></div><div><dt>Monteur</dt><dd>Test monteur</dd></div></dl></aside></div>
            <section class="pdf-panel"><div class="pdf-heading"><h2>Monteursrapport</h2><div class="actions"><a>Open in SharePoint</a></div></div>
            <hub-pdf-viewer data-pdf-url="SOURCE"></hub-pdf-viewer><p class="viewer-help">Open de PDF rechtstreeks in SharePoint als alternatief.</p></section></div></section></main>
            <script type="module" src="js/report-pdf-viewer.mjs"></script></body></html>
            """.Replace("SOURCE", denied == true ? "denied.pdf" : "fixture.pdf"), "text/html"));
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            var page = await browser.NewPageAsync(new() { ViewportSize = new() { Width = 2560, Height = 1080 }, DeviceScaleFactor = 1 });
            var errors = new List<string>();
            page.PageError += (_, error) => errors.Add(error);
            await page.GotoAsync(address + "/base/fixture");
            await page.WaitForFunctionAsync("document.querySelector('hub-pdf-viewer')?.getAttribute('aria-busy') === 'false'", null, new() { Timeout = 30000 });
            Check(await page.Locator(".hub-pdf-sheet").CountAsync() == 2, "Wide viewer must show two pages: " + await page.Locator(".hub-pdf-message").InnerTextAsync());
            Check((await page.Locator(".hub-pdf-position").InnerTextAsync()).Contains("1–2"), "Wide pagination wrong");
            Check((await page.Locator(".textLayer").First.InnerTextAsync()).Contains("Rapportpagina 1"), "PDF text layer missing");
            Check(await page.Locator("iframe").CountAsync() == 0, "Browser PDF UI still present");
            const string wholePages = """
                (() => {
                    const stage = document.querySelector('.hub-pdf-stage');
                    return stage.scrollHeight <= stage.clientHeight + 1 && stage.scrollWidth <= stage.clientWidth + 1 &&
                        [...document.querySelectorAll('.hub-pdf-sheet')].every(sheet => {
                            const rect = sheet.getBoundingClientRect();
                            return rect.top >= 0 && rect.bottom <= innerHeight && rect.left >= 0 && rect.right <= innerWidth;
                        });
                })()
                """;
            Check(await page.EvaluateAsync<bool>(wholePages), "Ultrawide pages must fit completely without scrolling");
            Check(await page.EvaluateAsync<bool>("document.querySelector('.workorder-information').getBoundingClientRect().right < document.querySelector('.pdf-panel').getBoundingClientRect().left"), "Workorder information must be left of the report");
            Directory.CreateDirectory(Path.Combine(root, ".artifacts", "viewer"));
            await page.ScreenshotAsync(new() { Path = Path.Combine(root, ".artifacts", "viewer", "wide.png"), FullPage = true });
            await page.Locator("[data-action='next']").ClickAsync();
            await page.WaitForFunctionAsync("document.querySelector('hub-pdf-viewer').getAttribute('aria-busy') === 'false' && document.querySelector('.hub-pdf-position').textContent.includes('3 van')");
            Check(await page.Locator(".hub-pdf-sheet").CountAsync() == 1 && await page.Locator("[data-action='next']").IsDisabledAsync(), "Odd last page not handled");
            await page.SetViewportSizeAsync(1440, 1000);
            await page.WaitForFunctionAsync("document.querySelector('hub-pdf-viewer').getAttribute('aria-busy') === 'false' && document.querySelector('hub-pdf-viewer').columns === 1");
            await page.Locator("[data-action='previous']").ClickAsync();
            await page.WaitForFunctionAsync("document.querySelector('hub-pdf-viewer').getAttribute('aria-busy') === 'false' && document.querySelector('.hub-pdf-position').textContent.includes('2 van')");
            Check(await page.Locator(".hub-pdf-sheet").CountAsync() == 1, "Narrow viewer must show one page");
            Check(await page.EvaluateAsync<bool>(wholePages), "Single page must fit completely without scrolling");
            await page.Locator("[data-action='in']").ClickAsync();
            await page.WaitForFunctionAsync("document.querySelector('hub-pdf-viewer').getAttribute('aria-busy') === 'false' && document.querySelector('[data-action=fit]').textContent.includes('125%')");
            await page.Locator("[data-action='fit']").ClickAsync();
            await page.WaitForFunctionAsync("document.querySelector('hub-pdf-viewer').getAttribute('aria-busy') === 'false' && document.querySelector('[data-action=fit]').textContent === 'Hele pagina'");
            Check(await page.EvaluateAsync<bool>(wholePages), "Fit button must restore whole-page display");
            await page.SetViewportSizeAsync(1440, 650);
            await page.WaitForTimeoutAsync(500);
            Check(await page.EvaluateAsync<bool>(wholePages), "Height-only resize must refit the pages");
            Check(requests == 1, "Navigation/resize/zoom must not refetch the PDF");
            await page.SetViewportSizeAsync(390, 844);
            await page.WaitForTimeoutAsync(500);
            Check(await page.Locator(".hub-pdf-sheet").CountAsync() == 1, "Mobile page count incorrect");
            Check(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"), "Mobile viewport overflows");
            await page.ScreenshotAsync(new() { Path = Path.Combine(root, ".artifacts", "viewer", "mobile.png"), FullPage = true });
            await page.SetViewportSizeAsync(2560, 1080);
            await page.WaitForFunctionAsync("document.querySelector('hub-pdf-viewer').getAttribute('aria-busy') === 'false' && document.querySelectorAll('.hub-pdf-sheet').length === 2");
            await page.GotoAsync(address + "/base/fixture?denied=true");
            await page.WaitForFunctionAsync("document.querySelector('.hub-pdf-message')?.getAttribute('role') === 'alert'");
            Check((await page.Locator(".hub-pdf-message").InnerTextAsync()).Contains("Geen toegang"), "Endpoint error not shown");
            Check(errors.Count == 0, "Browser errors: " + string.Join("; ", errors));
            Console.WriteLine("Viewer browser checks passed: two/one pages, text selection layer, page navigation, odd last page, resizing, zoom, single fetch, mobile layout, errors and path base.");
        }
        finally { await app.StopAsync(); }
    }

    private static byte[] CreatePdf()
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R 5 0 R 7 0 R] /Count 3 >>"
        };
        for (var number = 1; number <= 3; number++)
        {
            var content = $"0.0 0.23 0.44 rg BT /F1 25 Tf 45 780 Td (Rapportpagina {number}) Tj 0 -45 Td /F1 12 Tf (Werkorder WO2605670 - Test monteur) Tj 0 -28 Td (Taak: Correctief onderhoud) Tj ET 0.82 0.9 0.95 RG 45 675 505 1 re S";
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 9 0 R >> >> /Contents {objects.Count + 2} 0 R >>");
            objects.Add($"<< /Length {content.Length} >>\nstream\n{content}\nendstream");
        }
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        var xref = pdf.Length;
        pdf.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1)) pdf.Append($"{offset:D10} 00000 n \n");
        pdf.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
