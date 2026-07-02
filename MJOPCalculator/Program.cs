using MJOP.Calculator.Components;
using MJOP.Calculator.Services;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add custom services
builder.Services.AddScoped<MJOPCalculatorService>();
builder.Services.AddSingleton<EquipmentModelService>();
builder.Services.AddScoped<BusinessCentralApiService>();
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization();




builder.Services.Configure<ForwardedHeadersOptions>(options =>

{

    options.ForwardedHeaders =

        ForwardedHeaders.XForwardedFor |

        ForwardedHeaders.XForwardedProto |

        ForwardedHeaders.XForwardedHost;



    // Required on Linux behind a proxy — otherwise headers are ignored

    options.KnownNetworks.Clear();

    options.KnownProxies.Clear();

});






var app = builder.Build();

var pathBase = builder.Configuration["ASPNETCORE_PATHBASE"];
if (!string.IsNullOrWhiteSpace(pathBase))
{
    app.UsePathBase(pathBase);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();




app.UseForwardedHeaders();




app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();