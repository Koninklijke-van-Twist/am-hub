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
        var selected = orders.Where(order => order.Status == "Completed"
                && string.Equals(order.Coordinator?.Trim(), code, StringComparison.OrdinalIgnoreCase))
            .Where(order => !string.IsNullOrWhiteSpace(order.Number))
            .DistinctBy(order => order.Number).ToDictionary(order => order.Number, StringComparer.Ordinal);
        if (selected.Count == 0) return [];

        var family = new Dictionary<string, ReportWorkOrder>(selected, StringComparer.Ordinal);
        var parentNumbers = selected.Values.Where(order => !string.IsNullOrWhiteSpace(order.ParentNumber))
            .Select(order => order.ParentNumber).ToHashSet(StringComparer.Ordinal);
        foreach (var parent in await ReadRelatedAsync("No", parentNumbers.Where(number => !family.ContainsKey(number)), token))
            family[parent.Number] = parent;
        foreach (var number in parentNumbers)
        {
            if (!family.TryGetValue(number, out var parent))
                throw new CoordinatorLookupException($"De bovenliggende werkorder {number} kon niet worden gevonden. Controleer de koppeling in BC.");
            if (!string.IsNullOrWhiteSpace(parent.ParentNumber))
                throw new CoordinatorLookupException($"Werkorder {number} heeft zelf een bovenliggende werkorder. Dit overzicht ondersteunt één parent met directe children.");
        }
        var roots = family.Values.Where(order => string.IsNullOrWhiteSpace(order.ParentNumber))
            .Select(order => order.Number).ToHashSet(StringComparer.Ordinal);
        foreach (var child in await ReadRelatedAsync("Parent_Work_Order_No", roots, token))
            family[child.Number] = child;
        return family.Values.OrderByDescending(order => order.Date).ThenBy(order => order.Number).ToList();
    }

    private const string ReportFields = "No,Parent_Work_Order_No,Task_Code,Task_Description,Main_Entity_Description,Resource_Name,Start_Date,Status,KVT_Service_Coordinator,KVT_URL_Workorder_Report_PDF";

    private async Task<List<ReportWorkOrder>> ReadRelatedAsync(string field, IEnumerable<string> numbers, CancellationToken token)
    {
        var results = new List<ReportWorkOrder>();
        // Bounded same-field OR queries avoid one HTTP request per workorder and long OData URLs.
        foreach (var chunk in numbers.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Chunk(30))
        {
            var wanted = chunk.ToHashSet(StringComparer.Ordinal);
            var filter = string.Join(" or ", chunk.Select(number => $"{field} eq '{Escape(number)}'"));
            var rows = await ReadAsync<ReportWorkOrder>(Build("LVS_MainWorkOrderCard", ReportFields, filter), token);
            results.AddRange(rows.Where(order => !string.IsNullOrWhiteSpace(order.Number)
                && wanted.Contains(field == "No" ? order.Number : order.ParentNumber)));
        }
        return results;
    }

    public async Task<ReportWorkOrder?> GetDetailAsync(string number, ClaimsPrincipal user, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var ownCode = await GetOwnCodeAsync(user, token);
        if (string.IsNullOrWhiteSpace(number) || number.Length > 100) return null;
        var order = (await ReadRelatedAsync("No", [number], token)).FirstOrDefault();
        if (order is null) return null;
        bool IsEntry(ReportWorkOrder item) => item.Status == "Completed"
            && (string.Equals(item.Coordinator?.Trim(), ownCode, StringComparison.OrdinalIgnoreCase)
                || SelectableCoordinators.Contains(item.Coordinator?.Trim().ToUpperInvariant()));
        if (IsEntry(order)) return order;
        var parent = order;
        if (!string.IsNullOrWhiteSpace(order.ParentNumber))
        {
            parent = (await ReadRelatedAsync("No", [order.ParentNumber], token)).FirstOrDefault();
            if (parent is null || !string.IsNullOrWhiteSpace(parent.ParentNumber)) return null;
            if (IsEntry(parent)) return order;
        }
        var codes = SelectableCoordinators.Append(ownCode).Distinct(StringComparer.OrdinalIgnoreCase);
        var filter = $"Parent_Work_Order_No eq '{Escape(parent.Number)}' and Status eq 'Completed' and ("
            + string.Join(" or ", codes.Select(code => $"KVT_Service_Coordinator eq '{Escape(code)}'")) + ")";
        var children = await ReadAsync<ReportWorkOrder>(Build("LVS_MainWorkOrderCard", ReportFields, filter), token);
        return children.Any(child => child.ParentNumber == parent.Number && IsEntry(child)) ? order : null;
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
