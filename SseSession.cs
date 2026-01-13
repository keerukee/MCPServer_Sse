using Microsoft.AspNetCore.Http;

namespace McpSseServer;

/// <summary>
/// Thread-safe SSE Session for sending events to connected clients.
/// </summary>
public class SseSession : IDisposable
{
    private readonly HttpResponse _response;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _isDisposed;

    public SseSession(HttpResponse response) => _response = response;

    public async Task SendEventAsync(string eventType, string data)
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(SseSession));

        await _lock.WaitAsync();
        try
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(SseSession));

            var message = $"event: {eventType}\ndata: {data}\n\n";
            await _response.WriteAsync(message);
            await _response.Body.FlushAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _isDisposed = true;
        _lock.Dispose();
    }
}
