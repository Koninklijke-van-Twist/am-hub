namespace AMHub.Configuration;

public class BusinessCentralOptions
{
    public const string SectionName = "BusinessCentral";

    public string BaseUrl { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}