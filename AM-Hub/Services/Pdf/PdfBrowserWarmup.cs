namespace AMHub.Services.Pdf;

public sealed class PdfBrowserWarmup(PdfBrowser browser, ILogger<PdfBrowserWarmup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var context = await browser.CreateContextAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            // A failed warmup must not prevent startup; the next PDF request retries.
            logger.LogWarning(exception, "Vooraf starten van de PDF-browser mislukt; opnieuw proberen bij de volgende offerte.");
        }
    }
}
