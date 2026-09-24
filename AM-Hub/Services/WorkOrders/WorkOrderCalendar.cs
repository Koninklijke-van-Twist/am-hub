using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AMHub.Configuration;
using AMHub.Models.BusinessCentral;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;

namespace AMHub.Services.WorkOrders;

public sealed class WorkOrderOptions
{
    public string CustomerManagerField { get; set; } = "LVS_After_Sales_Person_Code";
    public int MaxUrlLength { get; set; } = 7000;
    public int MaxConcurrentRequests { get; set; } = 3;
}

public sealed class WorkOrder
{
    [JsonPropertyName("No")] public string Number { get; set; } = "";
    [JsonPropertyName("Task_Description")] public string Description { get; set; } = "";
    [JsonPropertyName("Bill_to_Customer_No")] public string CustomerNumber { get; set; } = "";
    [JsonPropertyName("Bill_to_Name")] public string CustomerName { get; set; } = "";
    [JsonPropertyName("Start_Date")] public DateOnly Start { get; set; }
    [JsonPropertyName("End_Date")] public DateOnly? End { get; set; }
    public string Status { get; set; } = "";
    [JsonIgnore] public string MaintenanceWorkorderNumber => Number;
    [JsonPropertyName("Main_Entity")] public string MainEntity { get; set; } = "";
    [JsonPropertyName("Main_Entity_Description")] public string MainEntityDescription { get; set; } = "";
    [JsonPropertyName("Component_No")] public string ComponentNumber { get; set; } = "";
    [JsonPropertyName("Component_Description")] public string ComponentDescription { get; set; } = "";
    [JsonPropertyName("KVT_Document_Status")] public string DocumentStatus { get; set; } = "";
    [JsonPropertyName("Project_Manager")] public string ProjectManager { get; set; } = "";
    public bool OpenEnded => End is null || End == DateOnly.MinValue;
    public bool Overlaps(DateOnly start, DateOnly end) => Start != DateOnly.MinValue && Start < end && (OpenEnded || End >= start);
}

public sealed record CalendarItem(WorkOrder Order, DateOnly Start, DateOnly EndExclusive);
public sealed record CalendarLoadStep(string Name, TimeSpan Duration);
public sealed record CalendarResult(string SalespersonCode, string CustomerManagerField, int CustomerCount, List<CalendarItem> Items, string OwnSalespersonCode)
{
    public TimeSpan LoadDuration { get; init; }
    public IReadOnlyList<CalendarLoadStep> LoadSteps { get; init; } = [];
}
public sealed class ManagerLookupException(string message) : Exception(message);

public sealed class WorkOrderCalendar(HttpClient http, AuthenticationStateProvider authentication,
    IOptions<BusinessCentralOptions> bcOptions, IOptions<WorkOrderOptions> calendarOptions,
    ILogger<WorkOrderCalendar> logger)
{
    public static IReadOnlyList<string> SelectableManagers { get; } = Array.AsReadOnly(new[] { "AW", "LL", "FR", "YB", "NVD", "NZ", "FJ", "MS", "WM" });
    private sealed class Customer
    {
        [JsonPropertyName("No")] public string Number { get; set; } = "";
    }
    private sealed class Page<T>
    {
        [JsonPropertyName("value")] public List<T> Value { get; set; } = [];
        [JsonPropertyName("@odata.nextLink")] public string? Next { get; set; }
    }
    private static string Escape(string value) => value.Replace("'", "''");
    private const string WorkorderFields = "No,Task_Description,Bill_to_Customer_No,Bill_to_Name,KVT_Document_Status,Status,Main_Entity,Main_Entity_Description,Component_No,Component_Description,Start_Date,End_Date";

    public async Task<CalendarResult> GetAsync(DateOnly start, DateOnly end, CancellationToken token, string? selectedManager = null)
    {
        var timer = Stopwatch.StartNew();
        var steps = new List<CalendarLoadStep>();
        var previous = TimeSpan.Zero;
        void CompleteStep(string name)
        {
            var elapsed = timer.Elapsed;
            steps.Add(new CalendarLoadStep(name, elapsed - previous));
            previous = elapsed;
        }

        if (end <= start || end.DayNumber - start.DayNumber > 42)
            throw new ArgumentException("Ongeldige kalenderperiode.");
        var user = (await authentication.GetAuthenticationStateAsync()).User;
        var email = new[] { ClaimTypes.Email, "email", "preferred_username", ClaimTypes.Upn, "upn" }
            .Select(user.FindFirstValue).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
        var options = calendarOptions.Value;
        if (user.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ManagerLookupException("Je aanmelding bevat geen bruikbaar e-mailadres. Neem contact op met de beheerder.");
        if (selectedManager is not null && !SelectableManagers.Contains(selectedManager.Trim().ToUpperInvariant()))
            throw new ManagerLookupException("Deze accountmanagercode kan niet worden gekozen.");
        if (options.CustomerManagerField is not ("Salesperson_Code" or "LVS_After_Sales_Person_Code" or "KVT_Service_coördinator"))
            throw new InvalidOperationException("Ongeldig accountmanagerveld.");
        if (options.MaxUrlLength is < 1000 or > 16000)
            throw new InvalidOperationException("Ongeldige maximale URL-lengte.");
        if (options.MaxConcurrentRequests is < 1 or > 6)
            throw new InvalidOperationException("Ongeldig aantal gelijktijdige aanvragen.");

        CompleteStep("Aanmelding en instellingen controleren");
        var salespeople = await ReadAsync<SalesPersonCard>(Build("SalesPersonCard", "Code,E_Mail,Blocked,Privacy_Blocked",
            $"E_Mail eq '{Escape(email)}'"), token);
        var matches = salespeople.Where(person => string.Equals(person.Email?.Trim(), email, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0)
            throw new ManagerLookupException("Er is geen accountmanager in Business Central gevonden met jouw e-mailadres. Laat E_Mail op de verkoperskaart controleren.");
        if (matches.Count != 1)
            throw new ManagerLookupException("Er zijn meerdere accountmanagers met jouw e-mailadres. Laat dit in Business Central corrigeren.");
        var person = matches[0];
        if (person.Blocked || person.PrivacyBlocked || string.IsNullOrWhiteSpace(person.Code))
            throw new ManagerLookupException("Je accountmanagerkaart in Business Central is geblokkeerd of heeft geen code. Neem contact op met de beheerder.");
        var ownManager = person.Code.Trim().ToUpperInvariant();
        var manager = selectedManager is null ? ownManager : selectedManager.Trim().ToUpperInvariant();
        CompleteStep("Accountmanager ophalen en controleren");
        var customers = await ReadAsync<Customer>(Build("AppCustomerCard", "No",
            $"{options.CustomerManagerField} eq '{Escape(manager)}'"), token);
        var numbers = customers.Select(c => c.Number).Where(n => !string.IsNullOrWhiteSpace(n)).ToHashSet(StringComparer.Ordinal);
        CompleteStep($"Klanten ophalen ({numbers.Count})");
        logger.LogInformation("Werkorderkalender: {CustomerCount} klanten via {ManagerField}; periode {Start} tot {End} (exclusief).",
            numbers.Count, options.CustomerManagerField, start, end);
        var orders = new Dictionary<string, WorkOrder>(StringComparer.Ordinal);
        var received = 0;
        var rejectedCustomer = 0;
        var rejectedDate = 0;
        string WorkorderFilter(IEnumerable<string> parts) => $"({string.Join(" or ", parts)}) and Start_Date ne 0001-01-01 and Start_Date lt {end:yyyy-MM-dd} and (End_Date ge {start:yyyy-MM-dd} or End_Date eq 0001-01-01 or End_Date eq null)";
        var workorderQueries = BuildChunks("LVS_MainWorkOrderCard", WorkorderFields,
            numbers.Order(StringComparer.Ordinal).Select(number => $"Bill_to_Customer_No eq '{Escape(number)}'"), WorkorderFilter);
        CompleteStep($"Werkorderaanvragen voorbereiden ({workorderQueries.Count} groepen)");
        var fetchedOrders = await ReadChunksAsync<WorkOrder>(workorderQueries, token);
        CompleteStep("Werkorders ophalen (inclusief vervolgpagina's)");
        foreach (var order in fetchedOrders)
        {
            received++;
            if (!numbers.Contains(order.CustomerNumber)) { rejectedCustomer++; continue; }
            if (!order.Overlaps(start, end))
            {
                rejectedDate++;
                continue;
            }
            if (string.IsNullOrWhiteSpace(order.Status) ||
                string.Equals(order.Status.Trim(), "Open", StringComparison.OrdinalIgnoreCase))
                continue;
            orders[order.Number] = order;
        }
        logger.LogInformation("Werkorderkalender geladen: {Count} items.", orders.Count);
        logger.LogInformation("Werkorderkalender: {Received} werkorders ontvangen; {RejectedCustomer} uitgesloten op klant; {RejectedDate} uitgesloten op datum.",
            received, rejectedCustomer, rejectedDate);
        var items = orders.Values.OrderBy(o => o.Start).ThenBy(o => o.Number).Select(o => new CalendarItem(o,
            o.Start < start ? start : o.Start,
            o.OpenEnded || o.End >= end.AddDays(-1) ? end : o.End!.Value.AddDays(1))).ToList();
        CompleteStep($"Werkorders filteren en kalender opbouwen ({items.Count} items)");
        return new CalendarResult(manager, options.CustomerManagerField, numbers.Count, items, ownManager)
        {
            LoadDuration = previous,
            LoadSteps = steps.AsReadOnly()
        };
    }

    private List<Uri> BuildChunks(string endpoint, string select, IEnumerable<string> clauses,
        Func<IEnumerable<string>, string> filter)
    {
        var result = new List<Uri>();
        var chunk = new List<string>();
        foreach (var clause in clauses)
        {
            if (Build(endpoint, select, filter(chunk.Append(clause))).AbsoluteUri.Length > calendarOptions.Value.MaxUrlLength)
            {
                if (chunk.Count > 0) result.Add(Build(endpoint, select, filter(chunk)));
                chunk.Clear();
                if (Build(endpoint, select, filter([clause])).AbsoluteUri.Length > calendarOptions.Value.MaxUrlLength)
                    throw new InvalidOperationException("Filter is te lang.");
            }
            chunk.Add(clause);
        }
        if (chunk.Count > 0) result.Add(Build(endpoint, select, filter(chunk)));
        return result;
    }

    private async Task<IEnumerable<T>> ReadChunksAsync<T>(List<Uri> queries, CancellationToken token)
    {
        var results = new List<T>[queries.Count];
        await Parallel.ForEachAsync(Enumerable.Range(0, queries.Count), new ParallelOptions
        {
            MaxDegreeOfParallelism = calendarOptions.Value.MaxConcurrentRequests,
            CancellationToken = token
        }, async (index, cancellation) => results[index] = await ReadAsync<T>(queries[index], cancellation));
        return results.SelectMany(result => result);
    }

    private Uri Build(string endpoint, string select, string filter)
    {
        var options = bcOptions.Value;
        return new Uri(new Uri(options.BaseUrl.TrimEnd('/') + "/"),
            $"Company('{Uri.EscapeDataString(Escape(options.Company))}')/{endpoint}?$select={Uri.EscapeDataString(select)}&$filter={Uri.EscapeDataString(filter)}");
    }

    private async Task<List<T>> ReadAsync<T>(Uri first, CancellationToken token)
    {
        var options = bcOptions.Value;
        var result = new List<T>();
        var visited = new HashSet<string>();
        Uri? next = first;
        while (next is not null)
        {
            token.ThrowIfCancellationRequested();
            // Never send credentials to a different origin or endpoint supplied by nextLink.
            if (next.GetLeftPart(UriPartial.Path) != first.GetLeftPart(UriPartial.Path) ||
                next.UserInfo.Length != 0 || next.AbsoluteUri.Length > calendarOptions.Value.MaxUrlLength ||
                !visited.Add(next.AbsoluteUri) || visited.Count > 10000)
                throw new InvalidOperationException("Ongeldige OData-vervolgpagina.");
            using var request = new HttpRequestMessage(HttpMethod.Get, next);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($"{options.Username}:{options.Password}")));
            using var response = await http.SendAsync(request, token);
            response.EnsureSuccessStatusCode();
            var page = await response.Content.ReadFromJsonAsync<Page<T>>(cancellationToken: token)
                ?? throw new JsonException("Lege OData-response.");
            result.AddRange(page.Value);
            next = string.IsNullOrWhiteSpace(page.Next) ? null : new Uri(next, page.Next);
        }
        return result;
    }
}
