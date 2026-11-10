using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AMHub.Configuration;
using AMHub.Services.ServiceCoordination;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;

static class FamilyChecks
{
    public static async Task RunAsync()
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        var data = new List<ReportWorkOrder>
        {
            new() { Number = "P0", Status = "Completed", Coordinator = "OWN" },
            new() { Number = "P'1", Status = "Released", Coordinator = "OTHER" },
            new() { Number = "C0", ParentNumber = "P0", Status = "Open", Coordinator = "OTHER" },
            new() { Number = "C1", ParentNumber = "P'1", Status = "Completed", Coordinator = "OWN" },
            new() { Number = "C2", ParentNumber = "P'1", Status = "Checked", Coordinator = "OTHER" },
            new() { Number = "GRANDCHILD", ParentNumber = "C1", Status = "Open", Coordinator = "OTHER" },
            new() { Number = "UNRELATED", Status = "Released", Coordinator = "OTHER" }
        };
        var auth = new FamilyAuth();
        var handler = new FamilyHandler(data);
        using var http = new HttpClient(handler);
        var service = new CoordinatorReportService(http, auth, Options.Create(new BusinessCentralOptions
            { BaseUrl = "https://bc.test/ODataV4/", Company = "Test", Username = "test", Password = "test" }));
        var result = await service.GetAsync(default);
        Check(result.Select(order => order.Number).ToHashSet().SetEquals(["P0", "P'1", "C0", "C1", "C2"]), "Direct parent/child closure incorrect");
        Check(result.Count(order => string.IsNullOrWhiteSpace(order.ParentNumber)) == 2, "Roots must be deduplicated");
        Check(handler.Queries.Count == 4, "Expected identity, selection, batched parents and batched children only");
        Check(handler.Queries[2].Contains("No eq 'P''1'") && !handler.Queries[2].Contains("Status eq"), "Parent query must escape codes and omit status filter");
        Check(handler.Queries[3].Contains("Parent_Work_Order_No eq 'P0'") && handler.Queries[3].Contains("Parent_Work_Order_No eq 'P''1'")
            && !handler.Queries[3].Contains("Status eq") && !handler.Queries[3].Contains("KVT_Service_Coordinator eq"), "Children must include all statuses/coordinators and use batches");
        var principal = (await auth.GetAuthenticationStateAsync()).User;
        Check((await service.GetDetailAsync("P'1", principal, default))?.Status == "Released", "Related non-completed parent detail denied");
        Check((await service.GetDetailAsync("C2", principal, default))?.Status == "Checked", "Related sibling detail denied");
        Check((await service.GetDetailAsync("C0", principal, default))?.Status == "Open", "Child of selected parent denied");
        Check(await service.GetDetailAsync("UNRELATED", principal, default) is null, "Unrelated workorder exposed");
        Check(await service.GetDetailAsync("GRANDCHILD", principal, default) is null, "Indirect child exposed");
        data.RemoveAll(order => order.Number == "C1");
        Check(await service.GetDetailAsync("C2", principal, default) is null, "Access survived removal of completed anchor");
        data.Add(new() { Number = "ORPHAN", ParentNumber = "MISSING", Status = "Completed", Coordinator = "OWN" });
        try { await service.GetAsync(default); throw new Exception("Missing parent silently hidden"); }
        catch (CoordinatorLookupException) { }
        Console.WriteLine("Family checks passed: direct parents/children, all statuses/coordinators, batching, escaping, deduplication, detail access and missing parents.");
    }

    sealed class FamilyAuth : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("email", "sc@example.test")], "test"))));
    }
    sealed class FamilyHandler(List<ReportWorkOrder> data) : HttpMessageHandler
    {
        public List<string> Queries { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var query = Uri.UnescapeDataString(request.RequestUri!.Query);
            Queries.Add(query);
            if (request.RequestUri.AbsolutePath.EndsWith("SalesPersonCard"))
                return Task.FromResult(Reply("""{"value":[{"Code":"OWN","E_Mail":"sc@example.test"}]}"""));
            static HashSet<string> Values(string query, string field) => Regex.Matches(query, @"(?<![A-Za-z_])" + field + @" eq '((?:[^']|'')*)'")
                .Select(match => match.Groups[1].Value.Replace("''", "'")).ToHashSet(StringComparer.Ordinal);
            var numbers = Values(query, "No");
            var parents = Values(query, "Parent_Work_Order_No");
            var codes = Values(query, "KVT_Service_Coordinator");
            var rows = data.Where(order => (numbers.Count == 0 || numbers.Contains(order.Number))
                && (parents.Count == 0 || parents.Contains(order.ParentNumber))
                && (codes.Count == 0 || codes.Contains(order.Coordinator))
                && (!query.Contains("Status eq 'Completed'") || order.Status == "Completed"));
            return Task.FromResult(Reply(JsonSerializer.Serialize(new { value = rows })));
        }
        private static HttpResponseMessage Reply(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
