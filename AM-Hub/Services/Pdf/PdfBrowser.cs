using Microsoft.Playwright;

namespace AMHub.Services.Pdf;

// One browser per application; each PDF owns and disposes its isolated context.
public sealed class PdfBrowser : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private bool _disposed;

    public async Task<IBrowserContext> CreateContextAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_browser is null || !_browser.IsConnected)
            {
                await CloseBrowserAsync();
                _playwright = await Playwright.CreateAsync();
                try
                {
                    _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
                }
                catch
                {
                    _playwright.Dispose();
                    _playwright = null;
                    throw;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            return await _browser.NewContextAsync(new()
            {
                ViewportSize = new ViewportSize { Width = 1240, Height = 1754 },
                DeviceScaleFactor = 3
            });
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task CloseBrowserAsync()
    {
        try
        {
            if (_browser is { IsConnected: true })
                await _browser.CloseAsync();
        }
        finally
        {
            _browser = null;
            _playwright?.Dispose();
            _playwright = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_disposed)
                return;
            _disposed = true;
            await CloseBrowserAsync();
        }
        finally
        {
            _gate.Release();
        }
    }
}
