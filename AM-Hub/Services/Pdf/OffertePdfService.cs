using AMHub.Services.Documents;
using Microsoft.Playwright;

namespace AMHub.Services.Pdf;

public class OffertePdfService : IOffertePdfService
{
    private readonly IOfferteDocumentService _documents;
    private readonly IOfferteHtmlRenderer _renderer;
    private readonly PdfBrowser _browser;

    public OffertePdfService(
        IOfferteDocumentService documents,
        IOfferteHtmlRenderer renderer,
        PdfBrowser browser)
    {
        _documents = documents;
        _renderer = renderer;
        _browser = browser;
    }

    public async Task<byte[]> GenerateAsync(
        string offerteNummer,
        CancellationToken cancellationToken = default)
    {
        var model = await _documents.GetAsync(
            offerteNummer,
            cancellationToken);

        if (model is null)
        {
            throw new InvalidOperationException(
                $"Offerte {offerteNummer} niet gevonden.");
        }

        var html = await _renderer.RenderAsync(
            model,
            cancellationToken);

        await using var context = await _browser.CreateContextAsync(cancellationToken);
        var page = await context.NewPageAsync();

        await page.SetContentAsync(
            html,
            new PageSetContentOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle
            });

        await page.EmulateMediaAsync(
            new PageEmulateMediaOptions
            {
                Media = Media.Print
            });

        await PrepareCoverFooterAsync(page);

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
            });
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
