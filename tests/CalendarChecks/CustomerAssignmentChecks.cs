using System.Net;
using System.Security.Claims;
using System.Text.Json;
using AMHub.Configuration;
using AMHub.Services.WorkOrders;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

static class CustomerAssignmentChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static readonly DateOnly Start = new(2026, 9, 1);

    public static async Task RunAsync()
    {
        var clock = new TestClock();
        using var assignments = new CustomerAssignmentCache(clock);
        using var handler = new AssignmentHandler();
        var service = CreateService(handler, assignments, clock);
        var initial = await Collect(service);
        Check(initial.Count == 1 && initial[0].Items.Single().Order.CustomerNumber == "C1", "Cold list incorrect");
        await Collect(service);
        Check(handler.CustomerRequests == 1, "Fresh mapping queried BC");

        // A second circuit shares assignments, while authorizing its own request.
        await Collect(CreateService(handler, assignments, clock));
        Check(handler.CustomerRequests == 1 && handler.AuthRequests == 3, "Circuit sharing bypassed auth or reloaded customers");

        clock.Advance(TimeSpan.FromMinutes(5));
        handler.Customers = ["C2"];
        handler.CustomerWait = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using (var updates = service.GetUpdatesAsync(Start, Start.AddDays(7), default).GetAsyncEnumerator())
        {
            Check(await updates.MoveNextAsync(), "No immediate stale result");
            Check(updates.Current.Items.Single().Order.CustomerNumber == "C1" && handler.CustomerRequests == 1,
                "Revalidation started before initial result was consumed");
            var next = updates.MoveNextAsync().AsTask();
            Check(!next.IsCompleted && handler.CustomerRequests == 2, "Refresh did not run behind the initial result");
            handler.CustomerWait.SetResult();
            Check(await next.WaitAsync(TimeSpan.FromSeconds(5)), "Changed assignments did not update the calendar");
            Check(updates.Current.Items.Single().Order.CustomerNumber == "C2", "Departed customer remained visible/new customer missing");
            Check(!await updates.MoveNextAsync(), "Unexpected repeated update");
        }
        handler.CustomerWait = null;

        clock.Advance(TimeSpan.FromMinutes(5));
        var beforeOrders = handler.OrderRequests;
        var unchanged = await Collect(service);
        Check(unchanged.Count == 1 && handler.CustomerRequests == 3 && handler.OrderRequests == beforeOrders + 1,
            "Unchanged mapping reloaded the calendar twice");

        // Failed background refresh must neither replace nor renew the old snapshot.
        clock.Advance(TimeSpan.FromMinutes(5));
        handler.FailCustomers = true;
        await using (var updates = service.GetUpdatesAsync(Start, Start.AddDays(7), default).GetAsyncEnumerator())
        {
            Check(await updates.MoveNextAsync() && updates.Current.CustomerCount == 1, "Stale result was lost on refresh failure");
            try { await updates.MoveNextAsync(); throw new Exception("Background failure was hidden"); }
            catch (HttpRequestException) { }
        }
        clock.Advance(TimeSpan.FromHours(24));
        await using (var updates = service.GetUpdatesAsync(Start, Start.AddDays(7), default).GetAsyncEnumerator())
        {
            try { await updates.MoveNextAsync(); throw new Exception("Mapping older than 24 hours was displayed"); }
            catch (HttpRequestException) { }
        }
        handler.FailCustomers = false;
        await Collect(service);
        handler.Customers = [];
        var refreshed = await Collect(service, refresh: true);
        Check(refreshed.Count == 1 && refreshed[0].CustomerCount == 0 && refreshed[0].Items.Count == 0,
            "Manual refresh reused a fresh cached mapping");

        handler.Blocked = true;
        var beforeCustomers = handler.CustomerRequests;
        try { await Collect(service); throw new Exception("Shared mapping bypassed authorization"); }
        catch (ManagerLookupException) { }
        Check(handler.CustomerRequests == beforeCustomers, "Blocked user queried customers");
        await CheckCoalescingAndKeys(clock);
        Console.WriteLine("Customer assignment checks passed: 5-minute revalidation after initial result, 24-hour hard expiry, changed/unchanged/empty lists, refresh failure, manual refresh, circuit sharing, live authorization, coalescing and cancellation.");
    }

    private static async Task CheckCoalescingAndKeys(TestClock clock)
    {
        using var cache = new CustomerAssignmentCache(clock);
        var key = new CustomerAssignmentCache.Key(new Uri("https://bc.test/Company('A')/customers?manager=LL"), "credentials-A", 7000);
        var requests = 0;
        Task<IEnumerable<string>> Load(CancellationToken token) { requests++; return Task.FromResult<IEnumerable<string>>(["C1", "C1", ""]); }
        var first = await cache.GetAsync(key, Load, default);
        Check(first.Numbers.SetEquals(["C1"]), "Customer numbers were not normalized");
        clock.Advance(TimeSpan.FromMinutes(5));
        var release = new TaskCompletionSource<IEnumerable<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<IEnumerable<string>> SlowLoad(CancellationToken token) { requests++; return release.Task.WaitAsync(token); }
        using var cancel = new CancellationTokenSource();
        var a = cache.RevalidateAsync(key, SlowLoad, cancel.Token);
        var b = cache.RevalidateAsync(key, SlowLoad, default);
        Check(requests == 2, "Concurrent refreshes were not coalesced");
        cancel.Cancel();
        try { await a; throw new Exception("Cancelled waiter completed"); } catch (OperationCanceledException) { }
        release.SetResult(["C2"]);
        Check((await b).Numbers.SetEquals(["C2"]), "One cancellation stopped other waiters");
        await cache.GetAsync(key, Load, default);
        Check(requests == 2, "Fresh result not reused");
        foreach (var otherKey in new[]
        {
            key with { Query = new Uri("https://bc.test/Company('B')/customers?manager=LL") },
            key with { Query = new Uri("https://bc.test/Company('A')/customers?manager=AW") },
            key with { CredentialFingerprint = "credentials-B" }
        }) await cache.GetAsync(otherKey, Load, default);
        Check(requests == 5, "Cache crossed company/manager/credential boundaries");
        clock.Advance(TimeSpan.FromHours(24));
        var expired = cache.GetAsync(key, SlowLoad, default);
        await expired;
        Check(requests == 6, "Exactly 24-hour-old snapshot was reused");
    }

    private static WorkOrderCalendar CreateService(AssignmentHandler handler, CustomerAssignmentCache cache, TimeProvider clock) =>
        new(new HttpClient(handler, disposeHandler: false), new AssignmentAuth(),
            Options.Create(new BusinessCentralOptions { BaseUrl = "https://bc.test/ODataV4/", Company = "Test", Username = "test", Password = "test" }),
            Options.Create(new WorkOrderOptions()), NullLogger<WorkOrderCalendar>.Instance, new WorkOrderDataCache(clock), cache);

    private static async Task<List<CalendarResult>> Collect(WorkOrderCalendar service, bool refresh = false)
    {
        var results = new List<CalendarResult>();
        await foreach (var result in service.GetUpdatesAsync(Start, Start.AddDays(7), default, refresh: refresh)) results.Add(result);
        return results;
    }

    private sealed class AssignmentAuth : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Email, "test@example.test")], "test"))));
    }

    private sealed class AssignmentHandler : HttpMessageHandler
    {
        public string[] Customers { get; set; } = ["C1"];
        public bool Blocked { get; set; }
        public bool FailCustomers { get; set; }
        public TaskCompletionSource? CustomerWait { get; set; }
        public int CustomerRequests { get; private set; }
        public int OrderRequests { get; private set; }
        public int AuthRequests { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            object value;
            if (request.RequestUri!.AbsolutePath.EndsWith("SalesPersonCard"))
            {
                AuthRequests++;
                value = new[] { new { Code = "LL", E_Mail = "test@example.test", Blocked } };
            }
            else if (request.RequestUri.AbsolutePath.EndsWith("AppCustomerCard"))
            {
                CustomerRequests++;
                if (CustomerWait is not null) await CustomerWait.Task.WaitAsync(token);
                if (FailCustomers) return new(HttpStatusCode.ServiceUnavailable);
                value = Customers.Select(number => new { No = number }).ToArray();
            }
            else
            {
                OrderRequests++;
                value = new[] { "C1", "C2" }.Select(number => new { No = "W-" + number, Bill_to_Customer_No = number, Start_Date = "2026-09-01", Status = "Released" }).ToArray();
            }
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { value }), System.Text.Encoding.UTF8, "application/json") };
        }
    }
}
