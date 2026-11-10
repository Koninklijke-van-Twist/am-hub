using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;

namespace AMHub.Services.ServiceCoordination;

public sealed class ReportPdfException(string message, int statusCode = 502) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public interface IReportAccessToken
{
    Task<string> GetAsync(ClaimsPrincipal user, CancellationToken token);
}

public sealed class ReportAccessToken(IServiceProvider services, IConfiguration configuration,
    IOptions<SharePointReportOptions> options) : IReportAccessToken
{
    public async Task<string> GetAsync(ClaimsPrincipal user, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!SharePointReportOptions.HasClientCredential(configuration))
            throw new ReportPdfException("De SharePoint-koppeling is nog niet ingesteld: configureer een client secret of certificaat voor de hub-appregistratie. Je kunt het rapport wel in SharePoint openen.", 503);
        return await services.GetRequiredService<ITokenAcquisition>().GetAccessTokenForUserAsync(
            [options.Value.Scope], user: user);
    }

    public static bool NeedsSignIn(Exception exception) => exception is MicrosoftIdentityWebChallengeUserException or MsalUiRequiredException;
}

public sealed class SharePointReportPdf(HttpClient http, IReportAccessToken tokens, IOptions<SharePointReportOptions> options)
{
    public Uri GetFileUri(string? url)
    {
        var site = options.Value.Site;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var file)
            || file.Scheme != Uri.UriSchemeHttps || file.Authority != site.Authority
            || file.UserInfo.Length != 0 || file.Query.Length != 0 || file.Fragment.Length != 0)
            throw new ReportPdfException("De rapportlink verwijst niet naar een geldig PDF-bestand op de ingestelde SharePoint-site.", 422);
        var path = Uri.UnescapeDataString(file.AbsolutePath);
        var sitePath = Uri.UnescapeDataString(site.AbsolutePath).TrimEnd('/') + "/";
        if (!path.StartsWith(sitePath, StringComparison.OrdinalIgnoreCase)
            || !path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            || path.Contains('\\') || path.Any(char.IsControl)
            || path.Split('/').Any(part => part is "." or ".."))
            throw new ReportPdfException("De rapportlink valt buiten de ingestelde SharePoint-site of verwijst niet naar een PDF.", 422);
        return file;
    }

    public Uri BuildContentUri(string url)
    {
        var file = GetFileUri(url);
        // Use a parameter alias to keep apostrophes, # and % in filenames out of the OData expression.
        var pathLiteral = "'" + Uri.UnescapeDataString(file.AbsolutePath).Replace("'", "''") + "'";
        return new Uri(options.Value.Site.AbsoluteUri.TrimEnd('/')
            + "/_api/web/GetFileByServerRelativePath(decodedUrl=@path)/$value?@path=" + Uri.EscapeDataString(pathLiteral));
    }

    public async Task<byte[]> GetAsync(ReportWorkOrder order, ClaimsPrincipal user, CancellationToken token)
    {
        var uri = BuildContentUri(order.PdfUrl);
        var accessToken = await tokens.GetAsync(user, token);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/pdf"));
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new ReportPdfException("Het PDF-rapport is niet gevonden in SharePoint.", 404);
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            throw new ReportPdfException("SharePoint geeft geen toegang tot dit rapport. Controleer je rechten en de toestemming voor de app.", 403);
        if (!response.IsSuccessStatusCode)
            throw new ReportPdfException("SharePoint kon het PDF-rapport niet leveren. Probeer het later opnieuw.");
        if (options.Value.MaxPdfMegabytes is < 1 or > 100)
            throw new InvalidOperationException("Ongeldige maximale PDF-grootte.");
        var limit = options.Value.MaxPdfMegabytes * 1024 * 1024;
        if (response.Content.Headers.ContentLength > limit)
            throw new ReportPdfException("Het PDF-rapport is te groot om in de hub te openen.", 413);
        await using var source = await response.Content.ReadAsStreamAsync(token);
        using var target = new MemoryStream();
        var buffer = new byte[64 * 1024];
        int read;
        while ((read = await source.ReadAsync(buffer, token)) > 0)
        {
            if (target.Length + read > limit)
                throw new ReportPdfException("Het PDF-rapport is te groot om in de hub te openen.", 413);
            await target.WriteAsync(buffer.AsMemory(0, read), token);
        }
        var bytes = target.ToArray();
        if (bytes.Length < 5 || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            throw new ReportPdfException("SharePoint heeft geen geldig PDF-bestand teruggegeven.");
        return bytes;
    }
}
