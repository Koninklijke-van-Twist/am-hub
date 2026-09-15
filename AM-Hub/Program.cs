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
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile(
    "appsettings.Local.json",
    optional: true,
    reloadOnChange: true);

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
builder.Services.AddScoped< IAppCustomerService,AppCustomerService>();
builder.Services.AddScoped<ISalesPersonService, SalesPersonService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

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