using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace AMHub.Services.Pdf;

// The complete rendered HTML is the key: fresh BC data and template changes always
// produce a different key. No quote data is served just because a TTL has not elapsed.
public sealed class PdfRenderCache : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 32 * 1024 * 1024 });
    private readonly SemaphoreSlim[] gates = Enumerable.Range(0, 16).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public async Task<byte[]> GetOrCreateAsync(string html, Func<CancellationToken, Task<byte[]>> render, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var hash = SHA256.HashData(MemoryMarshal.AsBytes(html.AsSpan()));
        var key = Convert.ToHexString(hash);
        if (cache.TryGetValue<byte[]>(key, out var pdf)) return pdf!;
        // Fixed lock stripes bound memory and coalesce identical simultaneous requests.
        var gate = gates[hash[0] % gates.Length];
        await gate.WaitAsync(token);
        try
        {
            if (cache.TryGetValue<byte[]>(key, out pdf)) return pdf!;
            pdf = await render(token);
            token.ThrowIfCancellationRequested();
            cache.Set(key, pdf, new MemoryCacheEntryOptions
            {
                Size = pdf.Length,
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            });
            return pdf;
        }
        finally { gate.Release(); }
    }

    public void Dispose()
    {
        cache.Dispose();
        foreach (var gate in gates) gate.Dispose();
    }
}
