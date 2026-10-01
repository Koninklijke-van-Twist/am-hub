using System.Collections.Immutable;
using Microsoft.Extensions.Caching.Memory;

namespace AMHub.Services.WorkOrders;

// Shared BC service-account data only. Callers must authorize the user before use.
public sealed class CustomerAssignmentCache(TimeProvider clock) : IDisposable
{
    public static readonly TimeSpan RefreshAfter = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MaximumAge = TimeSpan.FromHours(24);
    public sealed record Key(Uri Query, string CredentialFingerprint, int MaxUrlLength);
    public sealed record Snapshot(ImmutableHashSet<string> Numbers, DateTimeOffset CheckedAt);

    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 100000 });
    private readonly Dictionary<Key, Task<Snapshot>> pending = [];
    private readonly object gate = new();
    private readonly CancellationTokenSource shutdown = new();

    public bool NeedsRevalidation(Snapshot snapshot) => clock.GetUtcNow() - snapshot.CheckedAt >= RefreshAfter;

    public Task<Snapshot> GetAsync(Key key, Func<CancellationToken, Task<IEnumerable<string>>> load,
        CancellationToken token, bool refresh = false) =>
        ReadAsync(key, load, refresh ? TimeSpan.Zero : MaximumAge, token);

    public Task<Snapshot> RevalidateAsync(Key key, Func<CancellationToken, Task<IEnumerable<string>>> load,
        CancellationToken token) => ReadAsync(key, load, RefreshAfter, token);

    private async Task<Snapshot> ReadAsync(Key key, Func<CancellationToken, Task<IEnumerable<string>>> load,
        TimeSpan maximumAge, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Task<Snapshot> task;
        TaskCompletionSource<Snapshot>? owner = null;
        lock (gate)
        {
            if (cache.TryGetValue<Snapshot>(key, out var snapshot) && clock.GetUtcNow() - snapshot!.CheckedAt < maximumAge)
                return snapshot;
            if (!pending.TryGetValue(key, out task!))
            {
                owner = new(TaskCreationOptions.RunContinuationsAsynchronously);
                pending[key] = task = owner.Task;
                // Observe failures even if every caller cancels its own wait.
                _ = task.ContinueWith(failed => _ = failed.Exception, CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }
        if (owner is not null) _ = LoadAsync(key, load, owner);
        // One departing page must not cancel a request used by another user.
        return await task.WaitAsync(token);
    }

    private async Task LoadAsync(Key key, Func<CancellationToken, Task<IEnumerable<string>>> load,
        TaskCompletionSource<Snapshot> completion)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(90));
            var numbers = (await load(timeout.Token)).Where(n => !string.IsNullOrWhiteSpace(n)).ToImmutableHashSet(StringComparer.Ordinal);
            timeout.Token.ThrowIfCancellationRequested();
            var snapshot = new Snapshot(numbers, clock.GetUtcNow());
            lock (gate)
            {
                cache.Set(key, snapshot, new MemoryCacheEntryOptions
                {
                    // At most 100 small lists, or 100,000 customer numbers. Larger lists
                    // still load in full; they simply do not fit in the retained cache.
                    Size = Math.Max(1000, numbers.Count), AbsoluteExpirationRelativeToNow = MaximumAge
                });
                pending.Remove(key);
                completion.TrySetResult(snapshot);
            }
        }
        catch (Exception exception)
        {
            lock (gate)
            {
                pending.Remove(key);
                completion.TrySetException(exception);
            }
        }
    }

    public void Dispose()
    {
        shutdown.Cancel();
        shutdown.Dispose();
        cache.Dispose();
    }
}
