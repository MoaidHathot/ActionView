using System.Diagnostics;
using System.Net.WebSockets;
using Microsoft.Extensions.Hosting;

namespace ActionView.Api;

/// <summary>
/// Development middleware that launches the Vite dev server as a child process
/// and reverse-proxies all non-API requests to it. Handles both HTTP and WebSocket
/// connections (needed for Vite HMR).
/// </summary>
public sealed class ViteDevMiddleware : IDisposable
{
    private readonly RequestDelegate _next;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ViteDevMiddleware> _logger;
    private readonly Process? _viteProcess;
    private readonly string _viteBaseUrl;
    private bool _disposed;

    public ViteDevMiddleware(RequestDelegate next, ILogger<ViteDevMiddleware> logger, IHostApplicationLifetime lifetime, string clientDir, int vitePort = 5174)
    {
        _next = next;
        _logger = logger;
        _viteBaseUrl = $"http://localhost:{vitePort}";

        _httpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false
        })
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        // Launch Vite dev server
        _viteProcess = StartVite(clientDir, vitePort);

        // Ensure Vite is killed when the host is shutting down (Ctrl+C).
        // UseMiddleware<T>() does not dispose middleware instances, so without
        // this the child process would be orphaned.
        lifetime.ApplicationStopping.Register(Dispose);
    }

    private Process? StartVite(string clientDir, int port)
    {
        if (!Directory.Exists(clientDir))
        {
            _logger.LogError("Client directory not found: {ClientDir}", clientDir);
            return null;
        }

        // On Windows, npm is a batch script (npm.cmd) so we must invoke it via cmd.exe
        var isWindows = OperatingSystem.IsWindows();
        var psi = new ProcessStartInfo
        {
            FileName = isWindows ? "cmd.exe" : "npm",
            Arguments = isWindows ? "/c npm run dev" : "run dev",
            WorkingDirectory = clientDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.Environment["PORT"] = port.ToString();

        try
        {
            var process = Process.Start(psi);
            if (process is null)
            {
                _logger.LogError("Failed to start Vite dev server");
                return null;
            }

            // Log Vite output in background
            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    _logger.LogInformation("[Vite] {Output}", e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    _logger.LogInformation("[Vite] {Output}", e.Data);
            };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            _logger.LogInformation("Vite dev server starting at {Url} (client: {Dir})", _viteBaseUrl, clientDir);
            return process;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Vite dev server");
            return null;
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";

        // Let API and SignalR hub requests pass through to .NET
        if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/hubs", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // WebSocket requests (Vite HMR)
        if (context.WebSockets.IsWebSocketRequest)
        {
            await ProxyWebSocket(context);
            return;
        }

        // Proxy HTTP request to Vite
        await ProxyHttp(context);
    }

    private async Task ProxyHttp(HttpContext context)
    {
        var targetUrl = $"{_viteBaseUrl}{context.Request.Path}{context.Request.QueryString}";

        try
        {
            var requestMessage = new HttpRequestMessage
            {
                Method = new HttpMethod(context.Request.Method),
                RequestUri = new Uri(targetUrl)
            };

            // Copy relevant request headers
            foreach (var header in context.Request.Headers)
            {
                if (header.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
                    continue;
                requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }

            if (context.Request.ContentLength > 0 || context.Request.Body.CanRead)
            {
                requestMessage.Content = new StreamContent(context.Request.Body);
                if (context.Request.ContentType is not null)
                    requestMessage.Content.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType);
            }

            var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);

            context.Response.StatusCode = (int)response.StatusCode;

            // Copy response headers
            foreach (var header in response.Headers)
                context.Response.Headers[header.Key] = header.Value.ToArray();
            foreach (var header in response.Content.Headers)
                context.Response.Headers[header.Key] = header.Value.ToArray();

            // Remove transfer-encoding since Kestrel handles it
            context.Response.Headers.Remove("transfer-encoding");

            await response.Content.CopyToAsync(context.Response.Body);
        }
        catch (HttpRequestException)
        {
            // Vite probably hasn't started yet, retry after a brief wait
            _logger.LogDebug("Vite not ready yet, waiting...");
            await Task.Delay(1000);

            try
            {
                var retryResponse = await _httpClient.GetAsync(targetUrl);
                context.Response.StatusCode = (int)retryResponse.StatusCode;

                foreach (var header in retryResponse.Headers)
                    context.Response.Headers[header.Key] = header.Value.ToArray();
                foreach (var header in retryResponse.Content.Headers)
                    context.Response.Headers[header.Key] = header.Value.ToArray();

                context.Response.Headers.Remove("transfer-encoding");

                await retryResponse.Content.CopyToAsync(context.Response.Body);
            }
            catch
            {
                context.Response.StatusCode = 503;
                await context.Response.WriteAsync("Vite dev server is starting, please refresh in a moment...");
            }
        }
    }

    private async Task ProxyWebSocket(HttpContext context)
    {
        var wsUri = new Uri($"ws{_viteBaseUrl[4..]}{context.Request.Path}{context.Request.QueryString}");

        using var clientWs = new ClientWebSocket();
        foreach (var subProtocol in context.WebSockets.WebSocketRequestedProtocols)
            clientWs.Options.AddSubProtocol(subProtocol);

        try
        {
            await clientWs.ConnectAsync(wsUri, CancellationToken.None);
        }
        catch
        {
            context.Response.StatusCode = 502;
            return;
        }

        using var serverWs = await context.WebSockets.AcceptWebSocketAsync();

        var cts = new CancellationTokenSource();

        var clientToServer = Task.Run(async () =>
        {
            var buffer = new byte[4096];
            while (!cts.Token.IsCancellationRequested)
            {
                var result = await clientWs.ReceiveAsync(buffer, cts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await serverWs.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                    break;
                }
                await serverWs.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, cts.Token);
            }
        });

        var serverToClient = Task.Run(async () =>
        {
            var buffer = new byte[4096];
            while (!cts.Token.IsCancellationRequested)
            {
                var result = await serverWs.ReceiveAsync(buffer, cts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await clientWs.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                    break;
                }
                await clientWs.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, cts.Token);
            }
        });

        await Task.WhenAny(clientToServer, serverToClient);
        cts.Cancel();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_viteProcess is not null && !_viteProcess.HasExited)
        {
            try
            {
                _viteProcess.Kill(entireProcessTree: true);
                _viteProcess.WaitForExit(3000);
                _viteProcess.Dispose();
                _logger.LogInformation("Vite dev server stopped");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping Vite dev server");
            }
        }

        _httpClient.Dispose();
    }
}

public static class ViteDevMiddlewareExtensions
{
    /// <summary>
    /// Adds Vite dev server reverse proxy middleware. Only use in Development.
    /// Launches Vite and proxies all non-API requests to it.
    /// </summary>
    public static IApplicationBuilder UseViteDev(this IApplicationBuilder app, string clientDir)
    {
        return app.UseMiddleware<ViteDevMiddleware>(clientDir);
    }
}
