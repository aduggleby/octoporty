using System.Diagnostics;
using Octoporty.Shared.Contracts;

namespace Octoporty.Gateway.Services;

public sealed class RequestRoutingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ITunnelConnectionManager _connectionManager;
    private readonly ICaddyAdminClient _caddyClient;
    private readonly ILogger<RequestRoutingMiddleware> _logger;

    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
    private const int MaxBodySize = 10 * 1024 * 1024; // 10MB

    public RequestRoutingMiddleware(
        RequestDelegate next,
        ITunnelConnectionManager connectionManager,
        ICaddyAdminClient caddyClient,
        ILogger<RequestRoutingMiddleware> logger)
    {
        _next = next;
        _connectionManager = connectionManager;
        _caddyClient = caddyClient;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip internal endpoints
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/health") || path.StartsWith("/tunnel"))
        {
            await _next(context);
            return;
        }

        // Check for Octoporty mapping header (set by Caddy)
        if (!context.Request.Headers.TryGetValue("X-Octoporty-Mapping-Id", out var mappingIdHeader) ||
            !Guid.TryParse(mappingIdHeader.FirstOrDefault(), out var mappingId))
        {
            // Try to find mapping by host header
            var host = context.Request.Host.Value ?? "";
            var mapping = _connectionManager.GetMappingByHost(host);

            if (mapping == null)
            {
                _logger.LogWarning("No mapping found for host {Host}", host);
                await WriteErrorResponse(context, 503, "Service Unavailable", "No tunnel configured for this host");
                return;
            }

            mappingId = mapping.Id;
        }

        await ForwardRequestAsync(context, mappingId);
    }

    private async Task ForwardRequestAsync(HttpContext context, Guid mappingId)
    {
        var requestId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Read request body
            byte[]? body = null;
            if (context.Request.ContentLength > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding"))
            {
                if (context.Request.ContentLength > MaxBodySize)
                {
                    await WriteErrorResponse(context, 413, "Payload Too Large", "Request body exceeds maximum size");
                    return;
                }

                using var ms = new MemoryStream();
                await context.Request.Body.CopyToAsync(ms, context.RequestAborted);
                body = ms.ToArray();
            }

            // Build request message
            var headers = new Dictionary<string, string[]>();
            foreach (var (key, values) in context.Request.Headers)
            {
                headers[key] = values.Select(v => v ?? "").ToArray();
            }

            var request = new RequestMessage
            {
                RequestId = requestId,
                MappingId = mappingId,
                Method = context.Request.Method,
                Path = context.Request.Path + context.Request.QueryString,
                Headers = headers,
                Body = body,
                HasMoreBody = false
            };

            _logger.LogDebug("Forwarding request {RequestId}: {Method} {Path} (mapping: {MappingId})",
                requestId, request.Method, request.Path, mappingId);

            // Forward through tunnel
            var response = await _connectionManager.ForwardRequestAsync(request, DefaultTimeout, context.RequestAborted);

            if (response == null)
            {
                await HandleTunnelUnavailable(context, mappingId);
                return;
            }

            // Write response
            context.Response.StatusCode = response.StatusCode;

            // Copy headers (excluding hop-by-hop headers)
            var hopByHopHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Connection", "Keep-Alive", "Proxy-Authenticate", "Proxy-Authorization",
                "TE", "Trailer", "Transfer-Encoding", "Upgrade"
            };

            foreach (var (key, values) in response.Headers)
            {
                if (hopByHopHeaders.Contains(key))
                    continue;

                context.Response.Headers[key] = values.Select(v => v ?? "").ToArray();
            }

            // Write body
            if (response.Body != null && response.Body.Length > 0)
            {
                await context.Response.Body.WriteAsync(response.Body, context.RequestAborted);
            }

            stopwatch.Stop();
            _logger.LogInformation("Request {RequestId} completed: {StatusCode} ({Duration}ms)",
                requestId, response.StatusCode, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            // Client disconnected
            _logger.LogDebug("Request {RequestId} cancelled by client", requestId);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Request {RequestId} timed out", requestId);
            await WriteErrorResponse(context, 504, "Gateway Timeout", "The upstream server did not respond in time");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forwarding request {RequestId}", requestId);
            await WriteErrorResponse(context, 502, "Bad Gateway", "An error occurred while processing the request");
        }
    }

    private async Task HandleTunnelUnavailable(HttpContext context, Guid mappingId)
    {
        _logger.LogWarning("Tunnel unavailable for mapping {MappingId}, triggering self-healing", mappingId);

        // Self-healing: remove the route from Caddy since tunnel is down
        try
        {
            await _caddyClient.RemoveRouteAsync(mappingId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove Caddy route during self-healing for {MappingId}", mappingId);
        }

        await WriteErrorResponse(context, 503, "Service Unavailable", "The tunnel connection is not available");
    }

    private static async Task WriteErrorResponse(HttpContext context, int statusCode, string status, string message)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new
        {
            status,
            message,
            timestamp = DateTime.UtcNow
        });
    }
}

public static class RequestRoutingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestRouting(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestRoutingMiddleware>();
    }
}
