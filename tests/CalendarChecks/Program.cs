using System.Net;
using System.Security.Claims;
using System.Text;
using AMHub.Configuration;
using AMHub.Services.WorkOrders;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

var start = new DateOnly(2026, 9, 1);
var end = start.AddDays(42);
void Check(bool value, string message) { if (!value) throw new Exception(message); }
var handler = new FakeHandler();
var auth = new FakeAuth();
var options = new WorkOrderOptions();
var service = new WorkOrderCalendar(new HttpClient(handler), auth,
    Options.Create(new BusinessCentralOptions { BaseUrl = "https://bc.test/ODataV4/", Company = "Test", Username = "test", Password = "test" }),
    Options.Create(options), NullLogger<WorkOrderCalendar>.Instance);
try { await service.GetAsync(start, end, default); throw new Exception("Missing mapping accepted"); }
catch (ManagerLookupException) { Check(handler.Urls.Count == 0, "Request without mapping"); }
auth.Email = "person@example.test";
handler.Responses.Enqueue("""{"value":[{"No":"C'1"}],"@odata.nextLink":"?$skiptoken=page2"}""");
handler.Responses.Enqueue("""{"value":[{"No":"C'1"},{"No":"C2"}]}""");
handler.Responses.Enqueue("""{"value":[{"No":"W1","KVT_Maintenance_Workorder_No":"MW1","Status":"Open","Bill_to_Customer_No":"C2","LVS_Starting_Date":"2030-01-01","LVS_Ending_Date":"0001-01-01"},{"No":"W2","Bill_to_Customer_No":"OTHER","LVS_Starting_Date":"2026-09-01"},{"No":"W-OPEN","KVT_Maintenance_Workorder_No":"MW2","Bill_to_Customer_No":"C2","LVS_Starting_Date":"2026-09-01","Status":"Open"}],"@odata.nextLink":"?$skiptoken=more"}""");
handler.Responses.Enqueue("""{"value":[{"No":"W1","KVT_Maintenance_Workorder_No":"MW1","Status":"Open","Bill_to_Customer_No":"C2","LVS_Starting_Date":"2030-01-01","LVS_Ending_Date":"0001-01-01"}]}""");
handler.Responses.Enqueue("""{"value":[{"No":"MW1","Start_Date":"2026-08-15","End_Date":"0001-01-01","Status":"Released","Main_Entity":"MAIN-1","Component_No":"COMP-1","Component_Description":"Noodstroomaggregaat"},{"No":"MW2","Start_Date":"2026-09-01","End_Date":"2026-09-10","Status":"Open"}]}""");
var result = await service.GetAsync(start, end, default);
var items = result.Items;
Check(result.CustomerCount == 2 && result.SalespersonCode == "A'B" && result.CustomerManagerField == "LVS_After_Sales_Person_Code", "Customer summary incorrect");
Check(items.Count == 1 && items[0].Start == start && items[0].EndExclusive == end, "Isolation/deduplication/clipping failed");
Check(handler.Urls.Count == 5, "Pagination failed");
Check(items[0].Order.Status == "Released", "Linked status must override project status");
Check(items[0].Order.Start == new DateOnly(2026, 8, 15), "Workorder date must override project date");
Check(!Uri.UnescapeDataString(handler.Urls[2]).Contains("LVS_Starting_Date") &&
    !Uri.UnescapeDataString(handler.Urls[2]).Contains("LVS_Ending_Date"), "Projects must not filter or select dates");
Check(Uri.UnescapeDataString(handler.Urls[4]).Contains("Start_Date lt 2026-10-13") &&
    Uri.UnescapeDataString(handler.Urls[4]).Contains("End_Date ge 2026-09-01"), "Date filter must target workorders");
Check(items[0].Order.MaintenanceWorkorderNumber == "MW1" && items[0].Order.Number == "W1", "Project/workorder identities mixed");
Check(items[0].Order.MainEntity == "MAIN-1" && items[0].Order.ComponentNumber == "COMP-1" &&
    items[0].Order.ComponentDescription == "Noodstroomaggregaat", "Workorder component fields missing");
Check(Uri.UnescapeDataString(handler.Urls[4]).Contains("No,Status,Main_Entity,Component_No,Component_Description"), "Component select missing");
Check(Uri.UnescapeDataString(handler.Urls[4]).Contains("No eq 'MW1' or No eq 'MW2'"), "Statuses must be fetched in one grouped request");
Check(Uri.UnescapeDataString(handler.Urls[0]).Contains("LVS_After_Sales_Person_Code eq 'A''B'"), "Manager escaping failed");
Check(Uri.UnescapeDataString(handler.Urls[2]).Contains("Bill_to_Customer_No eq 'C''1'"), "Customer escaping failed");
Check(!new WorkOrder { Start = end }.Overlaps(start, end), "Exclusive end failed");
Check(new WorkOrder { Start = start.AddDays(-5), End = start }.Overlaps(start, end), "Inclusive end failed");
Check(!new WorkOrder { Start = DateOnly.MinValue }.Overlaps(start, end), "Blank start accepted");
handler.Responses.Enqueue("""{"value":[],"@odata.nextLink":"https://evil.test/steal"}""");
var count = handler.Urls.Count;
try { await service.GetAsync(start, end, default); throw new Exception("Unsafe nextLink accepted"); }
catch (InvalidOperationException) { Check(handler.Urls.Count == count + 1, "Credentials sent to foreign host"); }
handler.Responses.Enqueue("""{"value":[]}""");
var empty = await service.GetAsync(start, end, default);
Check(empty.Items.Count == 0 && empty.CustomerCount == 0, "Empty customers failed");
using var cancelled = new CancellationTokenSource();
cancelled.Cancel();
try { await service.GetAsync(start, end, cancelled.Token); throw new Exception("Cancellation ignored"); }
catch (OperationCanceledException) { }
Console.WriteLine("Calendar checks passed: mapping, isolation, pagination, escaping, dates, nextLink safety, empty results, cancellation.");
options.MaxUrlLength = 1000;
var manyCustomers = Enumerable.Range(0, 100).Select(i => new { No = $"CUSTOMER-{i:D4}" });
handler.Responses.Enqueue(System.Text.Json.JsonSerializer.Serialize(new { value = manyCustomers }));
for (var i = 0; i < 100; i++) handler.Responses.Enqueue("""{"value":[]}""");
count = handler.Urls.Count;
handler.DelayMilliseconds = 30;
await service.GetAsync(start, end, default);
handler.DelayMilliseconds = 0;
Check(handler.PeakConcurrent > 1 && handler.PeakConcurrent <= options.MaxConcurrentRequests, "Chunk concurrency outside bounds");
var chunkRequests = handler.Urls.Skip(count + 1).ToList();
Check(chunkRequests.Count > 1 && chunkRequests.Count < 100, "Chunking must group customers");
Check(chunkRequests.All(url => url.Length <= 1000), "URL limit exceeded");
Console.WriteLine("Chunking checks passed.");
foreach (var invalid in new[] { """{"value":[]}""", """{"value":[{"Code":"A","E_Mail":"person@example.test","Blocked":true}]}""", """{"value":[{"Code":"A","E_Mail":"person@example.test"},{"Code":"B","E_Mail":"person@example.test"}]}""" })
{
    handler.SalespersonResponse = invalid;
    count = handler.Urls.Count;
    try { await service.GetAsync(start, end, default); throw new Exception("Invalid salesperson accepted"); }
    catch (ManagerLookupException) { Check(handler.Urls.Count == count, "Customer query after failed lookup"); }
}
Console.WriteLine("Email lookup rejection checks passed.");
auth.Email = "LLISSENBERG@KVT.NL";
handler.SalespersonResponse = """{"value":[{"Code":"LL","E_Mail":"llissenberg@kvt.nl"}]}""";
handler.Responses.Clear();
handler.Responses.Enqueue("""{"value":[]}""");
count = handler.Urls.Count;
await service.GetAsync(start, end, default);
Check(handler.Urls.Count == count + 1 && Uri.UnescapeDataString(handler.Urls[^1]).Contains("LVS_After_Sales_Person_Code eq 'LL'"), "Own manager default failed");
foreach (var manager in WorkOrderCalendar.SelectableManagers)
{
    handler.Responses.Enqueue("""{"value":[]}""");
    var choice = await service.GetAsync(start, end, default, manager);
    Check(choice.SalespersonCode == manager && choice.OwnSalespersonCode == "LL", "Manager choice/default failed");
    Check(Uri.UnescapeDataString(handler.Urls[^1]).Contains($"LVS_After_Sales_Person_Code eq '{manager}'"), "Chosen manager filter failed");
}
count = handler.Urls.Count;
try { await service.GetAsync(start, end, default, "INVALID"); throw new Exception("Unknown manager accepted"); }
catch (ManagerLookupException) { Check(handler.Urls.Count == count, "Unknown manager queried"); }
auth.Email = null;
try { await service.GetAsync(start, end, default, "NVD"); throw new Exception("Missing identity accepted"); }
catch (ManagerLookupException) { Check(handler.Urls.Count == count, "Unauthenticated choice queried"); }
Console.WriteLine("Own manager default and all nine selections passed; invalid choices rejected.");

sealed class FakeAuth : AuthenticationStateProvider
{
    public string? Email { get; set; }
    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(
        new ClaimsPrincipal(new ClaimsIdentity(Email is null ? [] : [new Claim(ClaimTypes.Email, Email)], "test"))));
}
sealed class FakeHandler : HttpMessageHandler
{
    public string? SalespersonResponse { get; set; }
    public Queue<string> Responses { get; } = new();
    public List<string> Urls { get; } = [];
    public int DelayMilliseconds { get; set; }
    public int PeakConcurrent { get; private set; }
    private int active;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (request.RequestUri!.AbsolutePath.EndsWith("/SalesPersonCard"))
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(SalespersonResponse ?? """{"value":[{"Code":"A'B","E_Mail":"person@example.test"}]}""", Encoding.UTF8, "application/json") };
        string response;
        lock (Urls)
        {
            Urls.Add(request.RequestUri!.AbsoluteUri);
            response = Responses.Dequeue();
            PeakConcurrent = Math.Max(PeakConcurrent, ++active);
        }
        try
        {
            if (DelayMilliseconds > 0) await Task.Delay(DelayMilliseconds, token);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
        finally { lock (Urls) active--; }
    }
}
