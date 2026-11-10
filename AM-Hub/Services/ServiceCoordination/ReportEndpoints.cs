using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;

namespace AMHub.Services.ServiceCoordination;

public static class ReportEndpoints
{
    public static void MapCoordinatorReportEndpoints(this WebApplication app)
    {
        app.MapGet("/sc/werkorders/{number}/rapport.pdf", PdfAsync).RequireAuthorization();
        app.MapGet("/sc/werkorders/{number}/aanmelden", SignInAsync).RequireAuthorization();
    }

    private static async Task<IResult> PdfAsync(string number, HttpContext context,
        CoordinatorReportService reports, SharePointReportPdf pdf, ILoggerFactory logs)
    {
        PrivateResponse(context);
        try
        {
            var order = await reports.GetDetailAsync(number, context.User, context.RequestAborted);
            if (order is null) return Results.Text("Deze werkorder is niet beschikbaar in jouw werkvoorraad.", statusCode: 404);
            if (string.IsNullOrWhiteSpace(order.PdfUrl)) return Results.Text("Bij deze werkorder ontbreekt het PDF-rapport.", statusCode: 404);
            var bytes = await pdf.GetAsync(order, context.User, context.RequestAborted);
            context.Response.Headers.ContentDisposition = "inline";
            // No download filename: let the browser display the PDF inline. Range requests support its viewer.
            return Results.File(bytes, "application/pdf", enableRangeProcessing: true);
        }
        catch (CoordinatorLookupException exception) { return Results.Text(exception.Message, statusCode: 403); }
        catch (ReportPdfException exception) { return Results.Text(exception.Message, statusCode: exception.StatusCode); }
        catch (Exception exception) when (ReportAccessToken.NeedsSignIn(exception))
        {
            return Results.Text("Open de rapportdetailpagina en kies 'Aanmelden voor SharePoint' om je toegang te vernieuwen.", statusCode: 401);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { return Results.StatusCode(499); }
        catch (Exception exception)
        {
            // Do not log report URLs, credentials, tokens or downstream response bodies.
            logs.CreateLogger("SC.ReportPdf").LogWarning("PDF ophalen mislukt ({ExceptionType}).", exception.GetType().Name);
            return Results.Text("Het PDF-rapport kon niet worden geladen. Open het in SharePoint of probeer het later opnieuw.", statusCode: 502);
        }
    }

    private static async Task<IResult> SignInAsync(string number, HttpContext context, CoordinatorReportService reports,
        SharePointReportPdf pdf, IReportAccessToken tokens, IOptions<SharePointReportOptions> options)
    {
        PrivateResponse(context);
        try
        {
            var order = await reports.GetDetailAsync(number, context.User, context.RequestAborted);
            if (order is null) return Results.NotFound();
            pdf.GetFileUri(order.PdfUrl);
            string? claims = null;
            try { await tokens.GetAsync(context.User, context.RequestAborted); }
            catch (MicrosoftIdentityWebChallengeUserException exception) { claims = exception.MsalUiRequiredException.Claims; }
            catch (MsalUiRequiredException exception) { claims = exception.Claims; }
            var properties = new OpenIdConnectChallengeProperties
            {
                RedirectUri = context.Request.PathBase + "/sc/werkorders/" + Uri.EscapeDataString(number),
                Scope = new[] { "openid", "profile", "offline_access", options.Value.Scope }
            };
            if (!string.IsNullOrWhiteSpace(claims)) properties.Items["claims"] = claims;
            return Results.Challenge(properties, [OpenIdConnectDefaults.AuthenticationScheme]);
        }
        catch (CoordinatorLookupException exception) { return Results.Text(exception.Message, statusCode: 403); }
        catch (ReportPdfException exception) { return Results.Text(exception.Message, statusCode: exception.StatusCode); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { return Results.StatusCode(499); }
        catch (Exception)
        {
            return Results.Text("De SharePoint-aanmelding kon niet worden gestart. Controleer de appconfiguratie.", statusCode: 503);
        }
    }

    private static void PrivateResponse(HttpContext context)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    }
}
