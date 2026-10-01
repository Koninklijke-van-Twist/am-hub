using AMHub.Services.Documents;
using Microsoft.Playwright;
using System.Diagnostics;

namespace AMHub.Services.Pdf;

public class OffertePdfService : IOffertePdfService
{
    private readonly IOfferteDocumentService _documents;
    private readonly IOfferteHtmlRenderer _renderer;
    private readonly PdfBrowser _browser;
    private readonly PdfRenderCache _cache;
    private readonly ILogger<OffertePdfService> _logger;

    public OffertePdfService(
        IOfferteDocumentService documents,
        IOfferteHtmlRenderer renderer,
        PdfBrowser browser,
        PdfRenderCache cache,
        ILogger<OffertePdfService> logger)
    {
        _documents = documents;
        _renderer = renderer;
        _browser = browser;
        _cache = cache;
        _logger = logger;
    }

    public async Task<byte[]> GenerateAsync(
        string offerteNummer,
        CancellationToken cancellationToken = default)
    {
        var timer = Stopwatch.StartNew();
        var model = await _documents.GetAsync(
            offerteNummer,
            cancellationToken);

        if (model is null)
        {
            throw new InvalidOperationException(
                $"Offerte {offerteNummer} niet gevonden.");
        }

        var dataDuration = timer.Elapsed;
        var html = await _renderer.RenderAsync(
            model,
            cancellationToken);

        var htmlDuration = timer.Elapsed - dataDuration;
        var pdf = await _cache.GetOrCreateAsync(html, token => RenderPdfAsync(html, token), cancellationToken);
        _logger.LogInformation("Offerte-pdf: gegevens {DataMs} ms, HTML {HtmlMs} ms, PDF/cache {PdfMs} ms, totaal {TotalMs} ms.",
            dataDuration.TotalMilliseconds, htmlDuration.TotalMilliseconds,
            (timer.Elapsed - dataDuration - htmlDuration).TotalMilliseconds, timer.Elapsed.TotalMilliseconds);
        return pdf;
    }

    private async Task<byte[]> RenderPdfAsync(string html, CancellationToken cancellationToken)
    {
        await using var context = await _browser.CreateContextAsync(cancellationToken);
        // Disposing this isolated context also stops outstanding browser work on cancellation.
        var page = await context.NewPageAsync().WaitAsync(cancellationToken);

        await page.EmulateMediaAsync(
            new PageEmulateMediaOptions { Media = Media.Print }).WaitAsync(cancellationToken);

        await page.SetContentAsync(
            html,
            new PageSetContentOptions
            {
                WaitUntil = WaitUntilState.Load
            }).WaitAsync(cancellationToken);

        await PrepareCoverFooterAsync(page).WaitAsync(cancellationToken);

        return await page.PdfAsync(
            new PagePdfOptions
            {
                Format = "A4",
                PrintBackground = true,
                PreferCSSPageSize = true,

                // CSS verzorgt de header en paginanummers, met een uitzondering voor de cover.
                DisplayHeaderFooter = false,

                // De CSS stelt de bovenmarge in, inclusief de uitzondering voor de eerste pagina.
                Margin = new Margin
                {
                    Top = "14mm",
                    Right = "14mm",
                    Bottom = "18mm",
                    Left = "14mm"
                }
            }).WaitAsync(cancellationToken);
    }

    internal static async Task PrepareCoverFooterAsync(IPage page)
    {
        // Chromium kan HTML niet als inhoud van een paginamarge gebruiken.
        // Render de kaart op hoge resolutie en plaats haar uitsluitend in @page :first.
        var footer = page.Locator(".cover-footer");
        await page.EvaluateAsync("document.fonts.ready");
        var bounds = await footer.BoundingBoxAsync()
            ?? throw new InvalidOperationException("De offertefooter kon niet worden gemeten.");
        var image = await footer.ScreenshotAsync(new LocatorScreenshotOptions
        {
            Type = ScreenshotType.Png,
            OmitBackground = true,
            Scale = ScreenshotScale.Device
        });

        var heightMm = bounds.Height * 25.4 / 96;
        var bottomMargin = (heightMm + 24).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        var imageUri = "data:image/png;base64," + Convert.ToBase64String(image);

        await page.Locator(".cover-footer-anchor").EvaluateAsync("element => element.remove()");
        await page.AddStyleTagAsync(new PageAddStyleTagOptions
        {
            Content = $$"""
                @page :first {
                    margin-bottom: {{bottomMargin}}mm;
                    @bottom-left {
                        content: "";
                        width: 100%;
                        margin-top: 6mm;
                        margin-bottom: 18mm;
                        background-image: url('{{imageUri}}');
                        background-repeat: no-repeat;
                        background-position: left bottom;
                        background-size: 100% auto;
                    }
                }
                """
        });
    }
}
