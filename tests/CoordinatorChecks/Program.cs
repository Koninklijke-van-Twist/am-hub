using System.Net;
using System.Security.Claims;
using System.Text;
using AMHub.Configuration;
using AMHub.Services.ServiceCoordination;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;

void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
var handler = new FakeHandler();
var auth = new FakeAuth();
using var client = new HttpClient(handler);
var service = new CoordinatorReportService(client, auth, Options.Create(new BusinessCentralOptions
    { BaseUrl = "https://bc.test/ODataV4/", Company = "Test", Username = "test", Password = "test" }));
async Task ExpectLookupFailure(string response)
{
    handler.Responses.Enqueue(response);
    var count = handler.Urls.Count;
    try { await service.GetAsync(default); throw new Exception("Invalid identity mapping accepted"); }
    catch (CoordinatorLookupException) { Check(handler.Urls.Count == count + 1, "Workorders requested for invalid mapping"); }
}

try { await service.GetAsync(default); throw new Exception("Anonymous lookup accepted"); }
catch (CoordinatorLookupException) { Check(handler.Urls.Count == 0, "Anonymous BC request"); }
auth.Email = "sc@example.test";
await ExpectLookupFailure("""{"value":[]}""");
await ExpectLookupFailure("""{"value":[{"Code":"SC","E_Mail":"sc@example.test","Blocked":true}]}""");
await ExpectLookupFailure("""{"value":[{"Code":"SC","E_Mail":"sc@example.test","Privacy_Blocked":true}]}""");
await ExpectLookupFailure("""{"value":[{"Code":"SC","E_Mail":"sc@example.test"},{"Code":"OTHER","E_Mail":"sc@example.test"}]}""");
const string person = """{"value":[{"Code":"S'C","E_Mail":"sc@example.test"}]}""";
handler.Responses.Enqueue(person);
handler.Responses.Enqueue("""{"value":[{"No":"OLD","Status":"Completed","KVT_Service_Coordinator":"S'C","Start_Date":"2025-01-01"},{"No":"WRONG-PERSON","Status":"Completed","KVT_Service_Coordinator":"OTHER"},{"No":"CHECKED","Status":"Checked","KVT_Service_Coordinator":"S'C"}],"@odata.nextLink":"?$skiptoken=next"}""");
handler.Responses.Enqueue("""{"value":[{"No":"NEW","Status":"Completed","KVT_Service_Coordinator":"S'C","Start_Date":"2026-11-08","Task_Code":"CO","Task_Description":"Onderhoud","Resource_Name":"Monteur","KVT_URL_Workorder_Report_PDF":"https://sharepoint.test/report.pdf"},{"No":"OLD","Status":"Completed","KVT_Service_Coordinator":"S'C","Start_Date":"2025-01-01"}]}""");
var first = handler.Urls.Count;
var orders = await service.GetAsync(default);
Check(orders.Count == 2 && orders[0].Number == "NEW" && orders[1].Number == "OLD", "Backlog, deduplication, ordering or isolation failed");
Check(handler.Urls.Count == first + 4, "Pagination or direct children query failed");
var query = Uri.UnescapeDataString(handler.Urls[first + 1]);
Check(query.Contains("Status eq 'Completed' and KVT_Service_Coordinator eq 'S''C'"), "Server-side filter or escaping incorrect");
Check(!query.Contains("Start_Date eq") && !query.Contains("Start_Date ge"), "Old backlog excluded");
Check(orders[0].TaskCode == "CO" && orders[0].ResourceName == "Monteur" && orders[0].SafePdfUrl is not null, "Field mapping failed");
Check(orders[1].SafePdfUrl is null, "Missing PDF not handled");
Check(new ReportWorkOrder { PdfUrl = "javascript:alert(1)" }.SafePdfUrl is null, "Unsafe PDF accepted");
foreach (var coordinator in new[] { "MDM", "RK", "SH", "VBR" })
{
    handler.Responses.Enqueue(person);
    handler.Responses.Enqueue(System.Text.Json.JsonSerializer.Serialize(new
    {
        value = new[]
        {
            new { No = "SELECTED", Status = "Completed", KVT_Service_Coordinator = coordinator },
            new { No = "OWN", Status = "Completed", KVT_Service_Coordinator = "S'C" },
            new { No = "CHECKED", Status = "Checked", KVT_Service_Coordinator = coordinator }
        }
    }));
    first = handler.Urls.Count;
    var selected = await service.GetAsync(default, " " + coordinator.ToLowerInvariant() + " ");
    Check(selected.Count == 1 && selected[0].Number == "SELECTED", "Selected coordinator isolation failed");
    Check(Uri.UnescapeDataString(handler.Urls[first + 1]).Contains($"Status eq 'Completed' and KVT_Service_Coordinator eq '{coordinator}'"), "Selected coordinator server filter failed");
    Check(Uri.UnescapeDataString(handler.Urls[first]).Contains("E_Mail eq 'sc@example.test'"), "Own identity not revalidated");
}
first = handler.Urls.Count;
try { await service.GetAsync(default, "OTHER"); throw new Exception("Arbitrary coordinator accepted"); }
catch (CoordinatorLookupException) { Check(handler.Urls.Count == first, "Request sent for invalid selection"); }
handler.Responses.Enqueue("""{"value":[{"Code":"SC","E_Mail":"sc@example.test","Blocked":true}]}""");
first = handler.Urls.Count;
try { await service.GetAsync(default, "MDM"); throw new Exception("Blocked user may select other coordinator"); }
catch (CoordinatorLookupException) { Check(handler.Urls.Count == first + 1, "Selected workorders requested for blocked user"); }
handler.Responses.Enqueue(person);
handler.Responses.Enqueue("""{"value":[{"No":"OWN-AGAIN","Status":"Completed","KVT_Service_Coordinator":"S'C"}]}""");
var ownAgain = await service.GetAsync(default);
Check(ownAgain.Count == 1 && ownAgain[0].Number == "OWN-AGAIN", "Return to own reports failed");
handler.Responses.Enqueue(person);
handler.Responses.Enqueue("""{"value":[],"@odata.nextLink":"https://evil.test/steal"}""");
first = handler.Urls.Count;
try { await service.GetAsync(default); throw new Exception("Unsafe nextLink accepted"); }
catch (InvalidOperationException) { Check(handler.Urls.Count == first + 2, "Credentials sent to foreign host"); }
using var cancellation = new CancellationTokenSource();
cancellation.Cancel();
first = handler.Urls.Count;
try { await service.GetAsync(cancellation.Token); throw new Exception("Cancellation ignored"); }
catch (OperationCanceledException) { Check(handler.Urls.Count == first, "Request sent after cancellation"); }
var principal = (await auth.GetAuthenticationStateAsync()).User;
foreach (var (body, expected) in new[]
{
    ("""{"error":{"code":"BadRequest","message":"Status Checked is niet toegestaan."}}""", "Status Checked is niet toegestaan."),
    ("""{"error":{"message":{"lang":"nl","value":"Wijzigen niet toegestaan."}}}""", "Wijzigen niet toegestaan."),
    ("<html>proxy failure</html>", "BC heeft geen leesbare foutmelding teruggegeven."),
    ("{}", "BC heeft geen leesbare foutmelding teruggegeven.")
})
{
    using var response = new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(body) };
    try { await BusinessCentralRequestException.EnsureSuccessAsync(response, default); throw new Exception("BC failure accepted"); }
    catch (BusinessCentralRequestException exception)
    {
        Check(exception.StatusCode == 400 && exception.Message.Contains("HTTP 400") && exception.Message.Contains(expected), "BC error details lost");
        Check(!exception.Message.Contains("<html>"), "Raw proxy body exposed");
    }
}
foreach (var coordinator in new[] { "S'C", "MDM", "RK", "SH", "VBR", "OTHER", "" })
{
    handler.Responses.Enqueue(person);
    handler.Responses.Enqueue(System.Text.Json.JsonSerializer.Serialize(new
    {
        value = new[] { new { No = "DETAIL'1", Status = "Completed", KVT_Service_Coordinator = coordinator } }
    }));
    first = handler.Urls.Count;
    var detail = await service.GetDetailAsync("DETAIL'1", principal, default);
    Check((detail is not null) == (coordinator is not ("OTHER" or "")), "Detail authorization incorrect");
    Check(Uri.UnescapeDataString(handler.Urls[first + 1]).Contains("No eq 'DETAIL''1'"), "Detail must query one workorder with escaped number");
}
handler.Responses.Enqueue(person);
handler.Responses.Enqueue("""{"value":[{"No":"CHECKED","Status":"Checked","KVT_Service_Coordinator":"MDM"}]}""");
Check(await service.GetDetailAsync("CHECKED", principal, default) is null, "Completed status bypassed on detail");
handler.Responses.Enqueue(person);
handler.Responses.Enqueue("""{"value":[{"No":"WO'1","Status":"Completed","KVT_Service_Coordinator":"MDM","@odata.etag":"W/\"version1\""}]}""");
handler.Responses.Enqueue("""{"value":[{"id":"71445ea7-90fe-f011-ac80-00155d0a3441","no":"WO'1","status":"Completed","@odata.etag":"W/\"api1\""}]}""");
handler.Responses.Enqueue("{}");
await service.MarkCheckedAsync("WO'1", principal, default);
Check(handler.PatchUri == "https://bc.test/api/logicVision/serviceApp/v2.0/maintenanceworkorders(71445ea7-90fe-f011-ac80-00155d0a3441)?company=Test", "PATCH must target LogicVision API, configured environment, company and GUID");
Check(handler.Urls.Any(url => Uri.UnescapeDataString(url).Contains("no eq 'WO''1'")), "API lookup must escape workorder number");
Check(handler.PatchBody == "{\"status\":\"Checked\"}" && handler.IfMatch == "W/\"api1\"", "PATCH must send only lowercase status and API ETag");
handler.Responses.Enqueue(person);
handler.Responses.Enqueue("""{"value":[{"No":"WO1","Status":"Completed","KVT_Service_Coordinator":"MDM","@odata.etag":"W/\"version2\""}]}""");
handler.Responses.Enqueue("""{"value":[{"id":"71445ea7-90fe-f011-ac80-00155d0a3441","no":"WO1","status":"Completed","@odata.etag":"W/\"api2\""}]}""");
handler.Responses.Enqueue("{}");
await service.ChangeStatusAsync("WO1", "Signed", principal, default);
Check(handler.PatchBody == "{\"status\":\"Signed\"}" && handler.IfMatch == "W/\"api2\"", "Signed PATCH payload or ETag incorrect");
foreach (var status in new[] { "Open", "Completed" })
{
    handler.Responses.Enqueue(person);
    handler.Responses.Enqueue(System.Text.Json.JsonSerializer.Serialize(new { value = new[] {
        new { No = "WO2", Status = status, KVT_Service_Coordinator = "MDM", Parent_Work_Order_No = "WO'1" } } }));
    if (status == "Open")
        handler.Responses.Enqueue("""{"value":[{"No":"WO'1","Status":"Completed","KVT_Service_Coordinator":"MDM"}]}""");
    else
        handler.Responses.Enqueue("""{"value":[{"id":"71445ea7-90fe-f011-ac80-00155d0a3441","no":"WO2","status":"Completed"}]}""");
    var patches = handler.PatchCount;
    try { await service.MarkCheckedAsync("WO2", principal, default); throw new Exception("Invalid transition or missing ETag accepted"); }
    catch (CoordinatorLookupException) { Check(handler.PatchCount == patches, "Invalid update sent to BC"); }
}
Console.WriteLine("Coordinator checks passed, including Checked write target, payload, ETag and transition validation.");
await FamilyChecks.RunAsync();

sealed class FakeAuth : AuthenticationStateProvider
{
    public string? Email { get; set; }
    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(
        new ClaimsPrincipal(Email is null ? new ClaimsIdentity() : new ClaimsIdentity([new Claim("preferred_username", Email)], "test"))));
}
sealed class FakeHandler : HttpMessageHandler
{
    public Queue<string> Responses { get; } = new();
    public List<string> Urls { get; } = [];
    public string? PatchUri, PatchBody, IfMatch;
    public int PatchCount;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Urls.Add(request.RequestUri!.AbsoluteUri);
        if (request.Method == HttpMethod.Patch)
        {
            PatchCount++;
            PatchUri = request.RequestUri.AbsoluteUri;
            PatchBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            IfMatch = request.Headers.GetValues("If-Match").Single();
        }
        if (Uri.UnescapeDataString(request.RequestUri.Query).Contains("Parent_Work_Order_No eq"))
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"value\":[]}", Encoding.UTF8, "application/json") };
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Responses.Dequeue(), Encoding.UTF8, "application/json") };
    }
}
