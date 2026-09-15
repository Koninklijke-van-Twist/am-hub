using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AMHub.Configuration;
using AMHub.Models.BusinessCentral;
using Microsoft.Extensions.Options;

namespace AMHub.Services.BusinessCentral;

public class BusinessCentralClient : IBusinessCentralClient
{
    private readonly HttpClient _httpClient;
    private readonly BusinessCentralOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;

    public BusinessCentralClient(
        HttpClient httpClient,
        IOptions<BusinessCentralOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;

        var authBytes = Encoding.ASCII.GetBytes(
            $"{_options.Username}:{_options.Password}");

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(authBytes));

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<List<T>> GetAsync<T>(
        string webService,
        ODataQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(webService, query);

        using var response = await _httpClient.GetAsync(
            url,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream =
            await response.Content.ReadAsStreamAsync(cancellationToken);

        var result = await JsonSerializer.DeserializeAsync<ODataResponse<T>>(
            stream,
            _jsonOptions,
            cancellationToken);

        return result?.Value ?? [];
    }

    private string BuildUrl(string webService, ODataQuery? query)
    {
        var company = Uri.EscapeDataString(_options.Company)
            .Replace("%20", "%20");

        var url =
            $"Company('{company}')/{webService}";

        if (query is null)
            return url;

        var parameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(query.Filter))
        {
            parameters.Add(
                $"$filter={Uri.EscapeDataString(query.Filter)}");
        }

        if (!string.IsNullOrWhiteSpace(query.Select))
        {
            parameters.Add(
                $"$select={Uri.EscapeDataString(query.Select)}");
        }

        if (!string.IsNullOrWhiteSpace(query.OrderBy))
        {
            parameters.Add(
                $"$orderby={Uri.EscapeDataString(query.OrderBy)}");
        }

        if (query.Top.HasValue)
        {
            parameters.Add($"$top={query.Top.Value}");
        }

        if (!string.IsNullOrWhiteSpace(query.Expand))
        {
            parameters.Add(
                $"$expand={Uri.EscapeDataString(query.Expand)}");
        }

        return parameters.Count == 0
            ? url
            : $"{url}?{string.Join("&", parameters)}";
    }
}