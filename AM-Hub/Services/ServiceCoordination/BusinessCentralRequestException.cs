using System.Text.Json;

namespace AMHub.Services.ServiceCoordination;

public sealed class BusinessCentralRequestException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken token)
    {
        if (response.IsSuccessStatusCode) return;
        string? code = null, message = null;
        try
        {
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(token), cancellationToken: token);
            if (json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                if (error.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String) code = c.GetString();
                if (error.TryGetProperty("message", out var m))
                {
                    if (m.ValueKind == JsonValueKind.String) message = m.GetString();
                    else if (m.ValueKind == JsonValueKind.Object && m.TryGetProperty("value", out var value)
                        && value.ValueKind == JsonValueKind.String) message = value.GetString();
                }
            }
        }
        catch (JsonException) { } // A proxy may return HTML; never display that body as a BC error.
        var status = (int)response.StatusCode;
        var detail = string.IsNullOrWhiteSpace(message) ? status switch
        {
            401 => "BC heeft de aanmeldgegevens geweigerd.",
            403 => "Het gebruikte BC-account heeft geen toestemming voor dit verzoek.",
            404 => "De webservice of werkorder is niet gevonden in BC.",
            405 => "Deze BC-webservice staat deze bewerking niet toe.",
            412 => "De werkorder is ondertussen gewijzigd. Vernieuw de pagina en probeer opnieuw.",
            _ => "BC heeft geen leesbare foutmelding teruggegeven."
        } : message;
        throw new BusinessCentralRequestException(status,
            $"Business Central (HTTP {status}{(string.IsNullOrWhiteSpace(code) ? "" : $", {code}")}): {detail}");
    }
}
