namespace AMHub.Services.ServiceCoordination;

public sealed class SharePointReportOptions
{
    public string SiteUrl { get; set; } = "https://kvtnl.sharepoint.com/sites/BCDocumentRepository";
    public int MaxPdfMegabytes { get; set; } = 40;

    public Uri Site
    {
        get
        {
            if (!Uri.TryCreate(SiteUrl.TrimEnd('/'), UriKind.Absolute, out var site)
                || site.Scheme != Uri.UriSchemeHttps || !site.IsDefaultPort
                || !site.Host.EndsWith(".sharepoint.com", StringComparison.OrdinalIgnoreCase)
                || site.UserInfo.Length != 0 || site.Query.Length != 0 || site.Fragment.Length != 0)
                throw new InvalidOperationException("Ongeldige SharePoint-siteconfiguratie.");
            return site;
        }
    }

    public string Scope => Site.GetLeftPart(UriPartial.Authority) + "/AllSites.Read";

    public static bool HasClientCredential(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["AzureAd:ClientSecret"])
        || configuration.GetSection("AzureAd:ClientCredentials").GetChildren().Any()
        || configuration.GetSection("AzureAd:ClientCertificates").GetChildren().Any();
}
