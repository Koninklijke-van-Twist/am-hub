using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using AMHub.Configuration;
using AMHub.Services.ServiceCoordination;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

static class ReportHttpChecks
{
    public static async Task RunAsync()
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, HeaderAuth>("test", _ => { });
        builder.Services.AddAuthorization();
        var bc = new BcHandler();
        using var bcHttp = new HttpClient(bc);
        var settings = Options.Create(new SharePointReportOptions());
        var tokens = new TestToken();
        using var pdfHandler = new PdfHandler();
        using var pdfHttp = new HttpClient(pdfHandler);
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton<IReportAccessToken>(tokens);
        builder.Services.AddSingleton(new SharePointReportPdf(pdfHttp, tokens, settings));
        builder.Services.AddSingleton(new CoordinatorReportService(bcHttp, new UnusedAuth(), Options.Create(new BusinessCentralOptions
            { BaseUrl = "https://bc.test/ODataV4/", Company = "Test", Username = "test", Password = "test" })));
        await using var app = builder.Build();
        app.UsePathBase("/base");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapCoordinatorReportEndpoints();
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            const string path = "/base/sc/werkorders/WO1/rapport.pdf";
            using (var anonymous = await client.GetAsync(path))
                Check(anonymous.StatusCode == HttpStatusCode.Unauthorized && bc.Requests == 0 && pdfHandler.Requests == 0, "Anonymous report access");
            client.DefaultRequestHeaders.Add("X-Test-User", "sc@example.test");
            var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\nHTTP test");
            pdfHandler.Replies.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
            using (var response = await client.GetAsync(path))
            {
                Check(response.StatusCode == HttpStatusCode.OK && response.Content.Headers.ContentType?.MediaType == "application/pdf", "PDF endpoint failed");
                Check((await response.Content.ReadAsByteArrayAsync()).SequenceEqual(bytes), "HTTP PDF content changed");
                Check(response.Headers.CacheControl?.NoStore == true && response.Content.Headers.ContentDisposition?.DispositionType == "inline", "PDF response headers missing");
            }
            pdfHandler.Replies.Enqueue(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
            using (var request = new HttpRequestMessage(HttpMethod.Get, path))
            {
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 4);
                using var response = await client.SendAsync(request);
                Check(response.StatusCode == HttpStatusCode.PartialContent && await response.Content.ReadAsStringAsync() == "%PDF-", "PDF range request failed");
            }
            var count = pdfHandler.Requests;
            bc.Coordinator = "OTHER";
            using (var denied = await client.GetAsync(path))
                Check(denied.StatusCode == HttpStatusCode.NotFound && pdfHandler.Requests == count, "Unauthorized workorder reached SharePoint");
            bc.Coordinator = "MDM";
            bc.Status = "Checked";
            using (var checkedOrder = await client.GetAsync(path))
                Check(checkedOrder.StatusCode == HttpStatusCode.NotFound && pdfHandler.Requests == count, "Checked workorder still accessible");
            bc.Status = "Completed";
            pdfHandler.Replies.Enqueue(new HttpResponseMessage(HttpStatusCode.Forbidden));
            using (var forbidden = await client.GetAsync(path))
                Check(forbidden.StatusCode == HttpStatusCode.Forbidden && forbidden.Headers.CacheControl?.NoStore == true, "SharePoint failure not handled");
            Console.WriteLine("Report HTTP checks passed: authentication, workorder access, PDF response, range requests, errors and path base.");
        }
        finally { await app.StopAsync(); }
    }

    sealed class HeaderAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var email = Request.Headers["X-Test-User"].ToString();
            return Task.FromResult(string.IsNullOrWhiteSpace(email) ? AuthenticateResult.NoResult()
                : AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("preferred_username", email)], Scheme.Name)), Scheme.Name)));
        }
    }
    sealed class UnusedAuth : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => throw new Exception("HTTP endpoints must use HttpContext.User");
    }
    sealed class BcHandler : HttpMessageHandler
    {
        public string Coordinator { get; set; } = "MDM";
        public string Status { get; set; } = "Completed";
        public int Requests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            var json = request.RequestUri!.AbsolutePath.EndsWith("SalesPersonCard")
                ? """{"value":[{"Code":"OWN","E_Mail":"sc@example.test"}]}"""
                : System.Text.Json.JsonSerializer.Serialize(new { value = new[] { new { No = "WO1", Status, KVT_Service_Coordinator = Coordinator,
                    KVT_URL_Workorder_Report_PDF = "https://kvtnl.sharepoint.com/sites/BCDocumentRepository/report.pdf" } } });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}
