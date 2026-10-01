namespace AMHub.Services.WorkOrders;

// Scoped to one Blazor circuit; never stores the accountmanager authorization lookup.
public sealed class WorkOrderDataCache(TimeProvider clock)
{
    private sealed record Entry(object Value, DateTimeOffset Expires, int Rows);
    private readonly Dictionary<(string Scope, string Url, Type Type), Entry> entries = [];
    private readonly object gate = new();
    private int rows;
    private int generation;

    public int Generation { get { lock (gate) return generation; } }

    public bool TryGet<T>(string scope, Uri uri, out List<T> result)
    {
        lock (gate)
        {
            var key = (scope, uri.AbsoluteUri, typeof(T));
            if (entries.TryGetValue(key, out var entry))
            {
                if (entry.Expires > clock.GetUtcNow())
                {
                    result = (List<T>)entry.Value;
                    return true;
                }
                entries.Remove(key);
                rows -= entry.Rows;
            }
        }
        result = [];
        return false;
    }

    public void Set<T>(string scope, Uri uri, List<T> result, int seconds, int requestGeneration)
    {
        if (seconds <= 0 || result.Count > 50000) return;
        lock (gate)
        {
            if (requestGeneration != generation) return;
            // Bound retained memory even when a user visits many periods/managers.
            if (entries.Count >= 128 || rows + result.Count > 50000)
            {
                entries.Clear();
                rows = 0;
            }
            var key = (scope, uri.AbsoluteUri, typeof(T));
            if (entries.Remove(key, out var old)) rows -= old.Rows;
            entries[key] = new Entry(result, clock.GetUtcNow().AddSeconds(seconds), result.Count);
            rows += result.Count;
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            entries.Clear();
            rows = 0;
            generation++;
        }
    }
}
