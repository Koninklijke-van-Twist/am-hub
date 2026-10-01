using System.Diagnostics;
using AMHub.Models.Documents;
using AMHub.Services.Documents;
using AMHub.Services.Pdf;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

await DocumentChecks.RunAsync();
if (args.Contains("--documents-only")) return;

var root = Path.GetFullPath("AM-Hub");
var environment = new TestEnvironment { ContentRootPath = root };
using var assets = new PdfAssetCache();
using var cache = new PdfRenderCache();
var renderer = new OfferteHtmlRenderer(environment, assets);
await using var browser = new PdfBrowser();
var documents = new TestDocuments();
var service = new OffertePdfService(documents, renderer, browser, cache, NullLogger<OffertePdfService>.Instance);
byte[]? firstPdf = null;
for (var i = 0; i < 3; i++)
{
    var timer = Stopwatch.StartNew();
    var pdf = await service.GenerateAsync("PERF-TEST");
    if (!pdf.AsSpan(0, 5).SequenceEqual("%PDF-"u8)) throw new Exception("Invalid PDF");
    if (firstPdf is not null && !ReferenceEquals(firstPdf, pdf)) throw new Exception("Identical PDF was regenerated");
    firstPdf = pdf;
    Console.WriteLine($"PDF {i + 1}: {timer.ElapsedMilliseconds} ms, {pdf.Length} bytes");
    Directory.CreateDirectory(".artifacts/pdf-checks");
    await File.WriteAllBytesAsync($".artifacts/pdf-checks/{(args.FirstOrDefault() ?? "current")}-{i}.pdf", pdf);
}
if (documents.Calls != 3) throw new Exception("PDF cache skipped current document data");
var freshTimer = Stopwatch.StartNew();
var changed = await service.GenerateAsync("CHANGED-QUOTE");
Console.WriteLine($"Changed PDF (warm browser, new content): {freshTimer.ElapsedMilliseconds} ms");
if (ReferenceEquals(firstPdf, changed)) throw new Exception("Stale PDF after content change");
var concurrent = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => service.GenerateAsync("CONCURRENT")));
if (concurrent.Any(pdf => !ReferenceEquals(pdf, concurrent[0]))) throw new Exception("Concurrent PDF renders were not coalesced");
using var cancelled = new CancellationTokenSource();
cancelled.Cancel();
try { await service.GenerateAsync("CANCELLED", cancelled.Token); throw new Exception("Cancellation ignored"); }
catch (OperationCanceledException) { }
var assetPath = Path.GetFullPath(".artifacts/pdf-checks/asset.txt");
await File.WriteAllTextAsync(assetPath, "first");
if (await assets.ReadTextAsync(assetPath, default) != "first") throw new Exception("Asset read failed");
await File.WriteAllTextAsync(assetPath, "updated template");
if (await assets.ReadTextAsync(assetPath, default) != "updated template") throw new Exception("Stale template cache");
Console.WriteLine("PDF checks passed: real Chromium output, fresh data, content invalidation, concurrent reuse, cancellation, asset invalidation.");

sealed class TestDocuments : IOfferteDocumentService
{
    public int Calls { get; private set; }
    public Task<OfferteDocumentModel?> GetAsync(string number, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        return Task.FromResult<OfferteDocumentModel?>(new()
        {
            OfferteNummer = number, KlantNaam = "Testklant", AccountmanagerCode = "AW",
            AccountmanagerNaam = "Test Accountmanager", AccountmanagerFunctie = "Accountmanager",
            AccountmanagerEmail = "test@example.test", AccountmanagerTelefoon = "0123456789",
            Components = Enumerable.Range(1, 12).Select(i => new OfferteComponent
            {
                ComponentNo = $"NSA-{i}", Omschrijving = $"Noodstroominstallatie {i}", Locatie = "Testlocatie",
                Werkzaamheden = [new() { Code = "TEST", Omschrijving = "Preventief onderhoud", Aantal = 1, Prijs = 1234.56m }]
            }).ToList()
        });
    }
}

sealed class TestEnvironment : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "PdfChecks";
    public string EnvironmentName { get; set; } = "Development";
    public string WebRootPath { get; set; } = "";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
