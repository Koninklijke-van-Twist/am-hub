using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using AMHub.Configuration;
using AMHub.Models.BusinessCentral;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;

namespace AMHub.Services.ServiceCoordination;

public sealed class CoordinatorLookupException(string message) : Exception(message);

public sealed class CoordinatorReportService(HttpClient http, AuthenticationStateProvider authentication,
    IOptions<BusinessCentralOptions> configuration)
{
    public static IReadOnlyList<string> SelectableCoordinators { get; } = Array.AsReadOnly(new[] { "MDM", "RK", "SH", "VBR" });

    private sealed class Page<T>
    {
        [JsonPropertyName("value")] public List<T> Value { get; set; } = [];
        [JsonPropertyName("@odata.nextLink")] public string? Next { get; set; }
    }

    public async Task<List<ReportWorkOrder>> GetAsync(CancellationToken token, string? selectedCoordinator = null)
    {
        token.ThrowIfCancellationRequested();
        var selection = selectedCoordinator?.Trim().ToUpperInvariant();
        if (selection is not null && !SelectableCoordinators.Contains(selection))
            throw new CoordinatorLookupException("Deze servicecoördinatorcode kan niet worden gekozen.");
        var user = (await authentication.GetAuthenticationStateAsync()).User;
        var ownCode = await GetOwnCodeAsync(user, token);
        var code = selection ?? ownCode;
        var orders = await ReadAsync<ReportWorkOrder>(Build("LVS_MainWorkOrderCard", ReportFields,
            $"Status eq 'Completed' and KVT_Service_Coordinator eq '{Escape(code)}'"), token);
        return orders.Where(order => order.Status == "Completed"
                && string.Equals(order.Coordinator?.Trim(), code, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(order => order.Number).OrderByDescending(order => order.Date).ThenBy(order => order.Number).ToList();
    }

    private const string ReportFields = "No,Task_Code,Task_Description,Resource_Name,Start_Date,Status,KVT_Service_Coordinator,KVT_URL_Workorder_Report_PDF";

    public async Task<ReportWorkOrder?> GetDetailAsync(string number, ClaimsPrincipal user, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var ownCode = await GetOwnCodeAsync(user, token);
        if (string.IsNullOrWhiteSpace(number) || number.Length > 100) return null;
        var orders = await ReadAsync<ReportWorkOrder>(Build("LVS_MainWorkOrderCard", ReportFields,
            $"No eq '{Escape(number)}' and Status eq 'Completed'"), token);
        return orders.FirstOrDefault(order => order.Number == number && order.Status == "Completed"
            && (string.Equals(order.Coordinator?.Trim(), ownCode, StringComparison.OrdinalIgnoreCase)
                || SelectableCoordinators.Contains(order.Coordinator?.Trim().ToUpperInvariant())));
    }

    private async Task<string> GetOwnCodeAsync(ClaimsPrincipal user, CancellationToken token)
    {
        var email = new[] { ClaimTypes.Email, "email", "preferred_username", ClaimTypes.Upn, "upn" }
            .Select(user.FindFirstValue).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
        if (user.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new CoordinatorLookupException("Je aanmelding bevat geen bruikbaar e-mailadres. Neem contact op met de beheerder.");
        var people = await ReadAsync<SalesPersonCard>(Build("SalesPersonCard", "Code,E_Mail,Blocked,Privacy_Blocked",
            $"E_Mail eq '{Escape(email)}'"), token);
        var matches = people.Where(person => string.Equals(person.Email?.Trim(), email, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count != 1)
            throw new CoordinatorLookupException(matches.Count == 0
                ? "Er is geen medewerkerscode gevonden bij jouw e-mailadres. Laat E_Mail op de verkoperskaart in BC controleren."
                : "Er zijn meerdere medewerkers met jouw e-mailadres. Laat dit in BC corrigeren.");
        var person = matches[0];
        if (person.Blocked || person.PrivacyBlocked || string.IsNullOrWhiteSpace(person.Code))
            throw new CoordinatorLookupException("Je medewerkerskaart is geblokkeerd of heeft geen code. Neem contact op met de beheerder.");
        return person.Code.Trim();
    }

    private static string Escape(string value) => value.Replace("'", "''");

    private Uri Build(string endpoint, string fields, string filter)
    {
        var options = configuration.Value;
        return new Uri(new Uri(options.BaseUrl.TrimEnd('/') + "/"),
            $"Company('{Uri.EscapeDataString(Escape(options.Company))}')/{endpoint}?$select={Uri.EscapeDataString(fields)}&$filter={Uri.EscapeDataString(filter)}");
    }

    private async Task<List<T>> ReadAsync<T>(Uri first, CancellationToken token)
    {
        var options = configuration.Value;
        var result = new List<T>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        Uri? next = first;
        while (next is not null)
        {
            token.ThrowIfCancellationRequested();
            if (next.GetLeftPart(UriPartial.Path) != first.GetLeftPart(UriPartial.Path)
                || next.UserInfo.Length != 0 || !visited.Add(next.AbsoluteUri) || visited.Count > 10000)
                throw new InvalidOperationException("Ongeldige OData-vervolgpagina.");
            using var request = new HttpRequestMessage(HttpMethod.Get, next);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($"{options.Username}:{options.Password}")));
            using var response = await http.SendAsync(request, token);
            response.EnsureSuccessStatusCode();
            var page = await response.Content.ReadFromJsonAsync<Page<T>>(cancellationToken: token)
                ?? throw new InvalidOperationException("Lege OData-response.");
            result.AddRange(page.Value);
            next = string.IsNullOrWhiteSpace(page.Next) ? null : new Uri(next, page.Next);
        }
        return result;
    }
}
