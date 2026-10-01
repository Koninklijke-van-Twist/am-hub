using Microsoft.Extensions.Caching.Memory;

namespace AMHub.Services.Pdf;

// Only static template assets are shared. File changes invalidate them immediately.
public sealed class PdfAssetCache : IDisposable
{
    private sealed record Asset(DateTime Modified, long Length, string Content);
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 64 * 1024 * 1024 });

    public Task<string> ReadTextAsync(string path, CancellationToken token) => ReadAsync(path, false, token);
    public Task<string> ReadImageAsync(string path, CancellationToken token) => ReadAsync(path, true, token);

    private async Task<string> ReadAsync(string path, bool image, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var file = new FileInfo(path);
        if (image && !file.Exists) return "";
        var key = (file.FullName, image);
        var modified = file.LastWriteTimeUtc;
        var length = file.Length;
        if (cache.TryGetValue<Asset>(key, out var asset) && asset!.Modified == modified && asset.Length == length)
            return asset.Content;

        string content;
        if (image)
        {
            var mime = file.Extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".svg" => "image/svg+xml",
                ".webp" => "image/webp",
                _ => "image/png"
            };
            content = $"data:{mime};base64," + Convert.ToBase64String(await File.ReadAllBytesAsync(path, token));
        }
        else content = await File.ReadAllTextAsync(path, token);

        cache.Set(key, new Asset(modified, length, content), new MemoryCacheEntryOptions
        {
            Size = Math.Max(1, (long)content.Length * sizeof(char)),
            SlidingExpiration = TimeSpan.FromMinutes(30)
        });
        return content;
    }

    public void Dispose() => cache.Dispose();
}
