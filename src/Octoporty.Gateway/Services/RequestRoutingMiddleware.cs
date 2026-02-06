// RequestRoutingMiddleware.cs
// ASP.NET Core middleware that forwards incoming HTTP requests through the tunnel to the Agent.
// Routes by X-Octoporty-Mapping-Id header (set by Caddy) or Host header lookup.
// Implements self-healing: removes Caddy routes when tunnel is unavailable.
// Strips hop-by-hop headers and enforces 10MB max body size.

using System.Diagnostics;
using Microsoft.AspNetCore.StaticFiles;
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

    // Used to infer Content-Type from file extension when upstream doesn't provide one
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

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
        // Skip internal endpoints (health, tunnel, and test endpoints)
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/health") || path.StartsWith("/tunnel") || path.StartsWith("/test"))
        {
            await _next(context);
            return;
        }

        // Check for Octoporty mapping header (set by Caddy)
        PortMappingDto? mapping = null;
        if (!context.Request.Headers.TryGetValue("X-Octoporty-Mapping-Id", out var mappingIdHeader) ||
            !Guid.TryParse(mappingIdHeader.FirstOrDefault(), out var mappingId))
        {
            // Try to find mapping by host header
            var host = context.Request.Host.Value ?? "";
            mapping = _connectionManager.GetMappingByHost(host);

            if (mapping == null)
            {
                _logger.LogWarning("No mapping found for host {Host}", host);
                await WriteErrorResponse(context, 503, "Service Unavailable", "No tunnel configured for this host");
                return;
            }

            mappingId = mapping.Id;
        }
        else
        {
            mapping = _connectionManager.GetMappingById(mappingId);
        }

        await ForwardRequestAsync(context, mappingId, mapping);
    }

    private async Task ForwardRequestAsync(HttpContext context, Guid mappingId, PortMappingDto? mapping)
    {
        var requestId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        if (context.Request.Headers.TryGetValue("X-Octoporty-Request-Id", out var requestIdHeader))
        {
            var headerId = requestIdHeader.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerId) && headerId.Length <= 64)
            {
                requestId = headerId;
            }
        }
        var stopwatch = Stopwatch.StartNew();
        var mappingName = mapping?.Name ?? "(unknown)";
        var mappingDomain = mapping?.ExternalDomain ?? "(unknown)";

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

            var gotAnyResponse = false;
            var headersApplied = false;

            await foreach (var streamingResponse in _connectionManager.ForwardStreamingRequestAsync(
                request,
                DefaultTimeout,
                context.RequestAborted))
            {
                gotAnyResponse = true;

                if (streamingResponse.InitialResponse != null && !headersApplied)
                {
                    var response = streamingResponse.InitialResponse;

                    // Write response
                    context.Response.StatusCode = response.StatusCode;

                    // Copy headers (excluding hop-by-hop headers)
                    var hopByHopHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "Connection", "Keep-Alive", "Proxy-Authenticate", "Proxy-Authorization",
                        "TE", "Trailer", "Transfer-Encoding", "Upgrade"
                    };

                    _logger.LogDebug("Response {RequestId} has {HeaderCount} headers", requestId, response.Headers.Count);

                    string? contentType = null;

                    foreach (var (key, values) in response.Headers)
                    {
                        if (hopByHopHeaders.Contains(key))
                            continue;

                        // Content-Type needs special handling in ASP.NET Core.
                        // Setting it via Headers[] collection may not work correctly.
                        if (string.Equals(key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                        {
                            contentType = values.FirstOrDefault();
                            _logger.LogDebug("Response {RequestId} Content-Type from upstream: '{ContentType}'", requestId, contentType ?? "(null)");
                            continue;
                        }

                        // Content-Length is automatically handled by ASP.NET Core based on body size
                        if (string.Equals(key, "Content-Length", StringComparison.OrdinalIgnoreCase))
                            continue;

                        context.Response.Headers[key] = values.Select(v => v ?? "").ToArray();
                    }

                    // Set Content-Type, inferring from file extension if upstream didn't provide one.
                    // This is critical for JavaScript modules which browsers reject without proper MIME type.
                    if (!string.IsNullOrEmpty(contentType))
                    {
                        context.Response.ContentType = contentType;
                        _logger.LogDebug("Response {RequestId} Content-Type set to: {ContentType}", requestId, contentType);
                    }
                    else
                    {
                        // Try to infer Content-Type from the request path
                        var path = context.Request.Path.Value ?? "";
                        if (ContentTypeProvider.TryGetContentType(path, out var inferredContentType))
                        {
                            context.Response.ContentType = inferredContentType;
                            _logger.LogDebug("Response {RequestId} Content-Type inferred from path: {ContentType}", requestId, inferredContentType);
                        }
                        else
                        {
                            _logger.LogWarning("Response {RequestId} [{Host}] (mapping: {MappingName} - {MappingDomain}) has no Content-Type and could not infer from path: {Path}",
                                requestId, context.Request.Host.Value, mappingName, mappingDomain, path);
                        }
                    }

                    headersApplied = true;

                    // Write body if present in initial response
                    if (response.Body != null && response.Body.Length > 0)
                    {
                        _logger.LogDebug("Response {RequestId} writing initial body of {Length} bytes", requestId, response.Body.Length);
                        await context.Response.Body.WriteAsync(response.Body, context.RequestAborted);
                    }
                }

                if (streamingResponse.Chunk != null && streamingResponse.Chunk.Data.Length > 0)
                {
                    await context.Response.Body.WriteAsync(streamingResponse.Chunk.Data, context.RequestAborted);
                }
            }

            if (!gotAnyResponse)
            {
                await HandleTunnelUnavailable(context, mappingId);
                return;
            }

            stopwatch.Stop();
            _logger.LogInformation("Request {RequestId} [{Host}] (mapping: {MappingName} - {MappingDomain}) completed: {StatusCode} ({Duration}ms)",
                requestId, context.Request.Host.Value, mappingName, mappingDomain, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
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
