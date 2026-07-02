using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace MJOP.Calculator.Services;

public class BusinessCentralApiService
{
    private readonly IConfiguration _configuration;

    public BusinessCentralApiService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<string> GetBCFilteredDataAsync(string subUrl, string filter)
    {
        if (string.IsNullOrWhiteSpace(subUrl))
            throw new ArgumentException("SubUrl is verplicht.", nameof(subUrl));

        if (string.IsNullOrWhiteSpace(filter))
            throw new ArgumentException("Filter is verplicht.", nameof(filter));

        var baseUrl = _configuration["BusinessCentral:BaseUrl"];
        var username = _configuration["BusinessCentral:Username"];
        var password = _configuration["BusinessCentral:Password"];

        if (string.IsNullOrWhiteSpace(baseUrl) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new Exception("BusinessCentral configuratie is niet volledig ingevuld.");
        }

        var fullUrl =
            $"{baseUrl}Company('Koninklijke%20van%20Twist')/{subUrl}?$filter={Uri.EscapeDataString(filter)}";

        using var client = new HttpClient();

        var credentials = Convert.ToBase64String(
            System.Text.Encoding.ASCII.GetBytes($"{username}:{password}")
        );

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

        var response = await client.GetAsync(fullUrl);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Status: {(int)response.StatusCode} ({response.StatusCode})\n{body}");
        }

        return body;
    }
}