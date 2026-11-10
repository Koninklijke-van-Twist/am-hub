using System.Net;
using System.Security.Claims;
using System.Text;
using AMHub.Services.ServiceCoordination;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
using var handler = new PdfHandler();
using var http = new HttpClient(handler);
var tokens = new TestToken();
var settings = new SharePointReportOptions { MaxPdfMegabytes = 1 };
var service = new SharePointReportPdf(http, tokens, Options.Create(settings));
var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("preferred_username", "sc@example.test")], "test"));
const string root = "https://kvtnl.sharepoint.com/sites/BCDocumentRepository";
const string file = root + "/Equipment/123/100_SERV_REP_PDF/Rapport%20van%20O%27Brien%20%231%20100%25.pdf";
var rest = service.BuildContentUri(file);
Check(rest.Host == "kvtnl.sharepoint.com" && rest.AbsolutePath.Contains("GetFileByServerRelativePath"), "Wrong REST API");
Check(Uri.UnescapeDataString(rest.Query).Contains("@path='/sites/BCDocumentRepository/Equipment/123/100_SERV_REP_PDF/Rapport van O''Brien #1 100%.pdf'"), "Special characters not preserved");
Check(settings.Scope == "https://kvtnl.sharepoint.com/AllSites.Read", "Wrong resource scope");
foreach (var invalid in new[] { "https://evil.test/report.pdf", "http://kvtnl.sharepoint.com/report.pdf",
    root + "-other/report.pdf", root + "/../other/report.pdf", root + "/%2e%2e/other/report.pdf",
    root + "/report.xlsm", root + "/report.pdf?download=1", "https://name:password@kvtnl.sharepoint.com/sites/BCDocumentRepository/report.pdf" })
{
    try { service.BuildContentUri(invalid); throw new Exception("Invalid URL accepted: " + invalid); }
    catch (ReportPdfException) { }
}
Check(handler.Requests == 0 && tokens.Calls == 0, "URL validation must happen before network or token requests");
var order = new ReportWorkOrder { Number = "WO1", PdfUrl = file };
var expected = Encoding.ASCII.GetBytes("%PDF-1.7\nTest document");
handler.Replies.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(expected) });
var actual = await service.GetAsync(order, user, default);
Check(actual.SequenceEqual(expected), "PDF bytes changed");
Check(handler.Authorization == "Bearer test-access-token" && ReferenceEquals(tokens.User, user), "Token must belong to the requesting user");
foreach (var (status, result) in new[] { (HttpStatusCode.NotFound, 404), (HttpStatusCode.Forbidden, 403),
    (HttpStatusCode.Unauthorized, 403), (HttpStatusCode.Redirect, 502), (HttpStatusCode.TooManyRequests, 502) })
{
    handler.Replies.Enqueue(new HttpResponseMessage(status) { Content = new StringContent("private downstream message") });
    try { await service.GetAsync(order, user, default); throw new Exception("HTTP failure accepted"); }
    catch (ReportPdfException exception)
    {
        Check(exception.StatusCode == result && !exception.Message.Contains("private downstream"), "Incorrect or leaking error");
    }
}
handler.Replies.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>Sign in</html>") });
try { await service.GetAsync(order, user, default); throw new Exception("HTML accepted as PDF"); }
catch (ReportPdfException) { }
// Unknown-length content must also respect the cap while being copied.
handler.Replies.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new NonSeekableStream(new byte[1024 * 1024 + 1])) });
try { await service.GetAsync(order, user, default); throw new Exception("Oversize stream accepted"); }
catch (ReportPdfException exception) { Check(exception.StatusCode == 413, "Wrong size error"); }

using var services = new ServiceCollection().BuildServiceProvider();
var emptyConfiguration = new ConfigurationBuilder().Build();
var missingCredential = new ReportAccessToken(services, emptyConfiguration, Options.Create(settings));
try { await missingCredential.GetAsync(user, default); throw new Exception("Missing credential accepted"); }
catch (ReportPdfException exception) { Check(exception.StatusCode == 503, "Missing credential must be actionable"); }
Console.WriteLine("SharePoint checks passed: REST paths, URL restrictions, delegated token, PDF bytes, errors, size limit and missing credentials.");
if (args.Contains("--http")) await ReportHttpChecks.RunAsync();
if (args.Contains("--viewer")) await ViewerBrowserChecks.RunAsync();

sealed class TestToken : IReportAccessToken
{
    public int Calls { get; private set; }
    public ClaimsPrincipal? User { get; private set; }
    public Task<string> GetAsync(ClaimsPrincipal user, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); Calls++; User = user;
        return Task.FromResult("test-access-token");
    }
}
sealed class PdfHandler : HttpMessageHandler
{
    public Queue<HttpResponseMessage> Replies { get; } = new();
    public int Requests { get; private set; }
    public string? Authorization { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests++; Authorization = request.Headers.Authorization?.ToString();
        return Task.FromResult(Replies.Dequeue());
    }
}
sealed class NonSeekableStream(byte[] data) : MemoryStream(data)
{
    public override bool CanSeek => false;
}
