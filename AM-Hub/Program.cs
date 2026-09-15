using AM_Hub.Components;
using AMHub.Configuration;
using AMHub.Services.BusinessCentral;
using AMHub.Services.ConfigurationRules;
using AMHub.Services.Customers;
using AMHub.Services.Documents;
using AMHub.Services.Pdf;
using AMHub.Services.SalesPersons;
using AMHub.Services.SalesQuotes;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;

// systemd often starts `dotnet /path/AM-Hub.dll` with cwd != the app directory.
// Local `dotnet run` keeps the project directory (where appsettings.Local.json lives).
var cwdSettings = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
var appSettings = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
var contentRoot = !File.Exists(cwdSettings) && File.Exists(appSettings)
    ? AppContext.BaseDirectory
    : Directory.GetCurrentDirectory();

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = contentRoot
});

builder.Configuration.AddJsonFile(
    "appsettings.Local.json",
    optional: true,
    reloadOnChange: true);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;

    // Apache on sleutels.kvt.nl terminates TLS and proxies to Kestrel.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.Configure<BusinessCentralOptions>(
    builder.Configuration.GetSection(
        BusinessCentralOptions.SectionName));

builder.Services.AddHttpClient<IBusinessCentralClient, BusinessCentralClient>(
    (serviceProvider, client) =>
    {
        var options = serviceProvider
            .GetRequiredService<IOptions<BusinessCentralOptions>>()
            .Value;

        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(60);
    });

builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(
        builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<ISalesQuoteService, SalesQuoteService>();
builder.Services.AddScoped<IConfigurationRuleService, ConfigurationRuleService>();
builder.Services.AddScoped<IOfferteDocumentService, OfferteDocumentService>();
builder.Services.AddScoped<IOfferteHtmlRenderer, OfferteHtmlRenderer>();
builder.Services.AddScoped<IOffertePdfService, OffertePdfService>();
builder.Services.AddScoped<IAppCustomerService, AppCustomerService>();
builder.Services.AddScoped<ISalesPersonService, SalesPersonService>();

var app = builder.Build();

app.Logger.LogInformation(
    "AM-Hub starting. ContentRoot={ContentRoot} PathBase={PathBase}",
    app.Environment.ContentRootPath,
    app.Configuration["ASPNETCORE_PATHBASE"] ?? "(none)");

// Last known-good host (8 Jul) applied forwarded headers + PathBase before mapping endpoints.
app.UseForwardedHeaders();

var pathBase = builder.Configuration["ASPNETCORE_PATHBASE"];

if (!string.IsNullOrWhiteSpace(pathBase))
{
    app.UsePathBase(pathBase);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet(
    "/api/offertes/{offerteNummer}/pdf",
    async (
        string offerteNummer,
        bool? download,
        IOffertePdfService pdfService,
        CancellationToken cancellationToken) =>
    {
        var pdf = await pdfService.GenerateAsync(
            offerteNummer,
            cancellationToken);

        if (download == true)
        {
            return Results.File(
                pdf,
                "application/pdf",
                $"Offerte-{offerteNummer}.pdf");
        }

        return Results.File(
            pdf,
            "application/pdf",
            enableRangeProcessing: true);
    })
    .RequireAuthorization();

app.Run();
