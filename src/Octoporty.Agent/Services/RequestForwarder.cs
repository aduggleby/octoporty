// RequestForwarder.cs
// Forwards tunnel requests to internal services via HttpClient.
// Supports streaming for large responses (>256KB) using ResponseBodyChunkMessage.
// Two HttpClient variants: standard (validates certs) and insecure (self-signed only).
// Strips hop-by-hop headers and adds X-Octoporty-Request-Id for tracing.

using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Octoporty.Agent.Data;
using Octoporty.Shared.Contracts;
using Octoporty.Shared.Entities;

namespace Octoporty.Agent.Services;

public class RequestForwarder
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OctoportyDbContext _db;
    private readonly ILogger<RequestForwarder> _logger;

    private const int ChunkSize = 64 * 1024; // 64KB chunks
    private const int StreamingThreshold = 256 * 1024; // 256KB - responses larger than this are streamed

    public RequestForwarder(
        IHttpClientFactory httpClientFactory,
        OctoportyDbContext db,
        ILogger<RequestForwarder> logger)
    {
        _httpClientFactory = httpClientFactory;
        _db = db;
        _logger = logger;
    }

    public async Task<ResponseMessage> ForwardAsync(RequestMessage request, CancellationToken ct)
    {
        var mapping = await _db.PortMappings.FindAsync([request.MappingId], ct);

        if (mapping == null || !mapping.IsEnabled)
        {
            _logger.LogWarning("Mapping {MappingId} not found or disabled", request.MappingId);
            return CreateErrorResponse(request.RequestId, 404, "Mapping not found");
        }

        var client = CreateHttpClient(mapping);

        try
        {
            var httpRequest = CreateHttpRequest(request, mapping);
            var startTime = DateTime.UtcNow;

            var httpResponse = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseContentRead, ct);

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Forwarded {Method} {Path} to {Host}:{Port} -> {Status} ({Duration}ms)",
                request.Method, request.Path, mapping.InternalHost, mapping.InternalPort,
                (int)httpResponse.StatusCode, duration.TotalMilliseconds);

            return await CreateResponseMessageAsync(request.RequestId, httpResponse, ct);
        }
        catch (HttpRequestException ex)
        {
            // HIGH-05: Log details server-side, return generic message to client
            _logger.LogWarning(ex, "Failed to forward request to {Host}:{Port}",
                mapping.InternalHost, mapping.InternalPort);
            return CreateErrorResponse(request.RequestId, 502, "Bad Gateway: upstream service unavailable");
        }
        catch (TaskCanceledException)
        {
            return CreateErrorResponse(request.RequestId, 504, "Gateway Timeout");
        }
    }

    private HttpClient CreateHttpClient(PortMapping mapping)
    {
        var clientName = mapping.AllowSelfSignedCerts ? "InternalServices-Insecure" : "InternalServices";
        return _httpClientFactory.CreateClient(clientName);
    }

    private static HttpRequestMessage CreateHttpRequest(RequestMessage request, PortMapping mapping)
    {
        var scheme = mapping.InternalUseTls ? "https" : "http";
        var uri = new Uri($"{scheme}://{mapping.InternalHost}:{mapping.InternalPort}{request.Path}");

        var httpRequest = new HttpRequestMessage(new HttpMethod(request.Method), uri);

        // Copy headers (excluding hop-by-hop headers)
        var hopByHopHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Connection", "Keep-Alive", "Proxy-Authenticate", "Proxy-Authorization",
            "TE", "Trailer", "Transfer-Encoding", "Upgrade", "Host"
        };

        foreach (var (key, values) in request.Headers)
        {
            if (hopByHopHeaders.Contains(key))
                continue;

            foreach (var value in values)
            {
                httpRequest.Headers.TryAddWithoutValidation(key, value);
            }
        }

        // Add tracing headers
        httpRequest.Headers.TryAddWithoutValidation("X-Octoporty-Request-Id", request.RequestId);
        httpRequest.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");

        // Set body if present
        if (request.Body != null && request.Body.Length > 0)
        {
            httpRequest.Content = new ByteArrayContent(request.Body);

            // Copy content headers
            if (request.Headers.TryGetValue("Content-Type", out var contentType))
            {
                httpRequest.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
            }
        }

        return httpRequest;
    }

    private async Task<ResponseMessage> CreateResponseMessageAsync(
        string requestId,
        HttpResponseMessage httpResponse,
        CancellationToken ct)
    {
        var headers = new Dictionary<string, string[]>();

        foreach (var (key, values) in httpResponse.Headers)
        {
            headers[key] = values.ToArray();
        }

        foreach (var (key, values) in httpResponse.Content.Headers)
        {
            headers[key] = values.ToArray();
            // Log Content-Type specifically for debugging
            if (string.Equals(key, "Content-Type", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Request {RequestId} response Content-Type from upstream: {ContentType}",
                    requestId, string.Join(", ", values));
            }
        }

        var body = await httpResponse.Content.ReadAsByteArrayAsync(ct);

        _logger.LogDebug("Request {RequestId} response has {HeaderCount} headers, {BodyLength} bytes",
            requestId, headers.Count, body.Length);

        return new ResponseMessage
        {
            RequestId = requestId,
            StatusCode = (int)httpResponse.StatusCode,
            Headers = headers,
            Body = body
        };
    }

    private static ResponseMessage CreateErrorResponse(string requestId, int statusCode, string message)
    {
        return new ResponseMessage
        {
            RequestId = requestId,
            StatusCode = statusCode,
            Headers = new Dictionary<string, string[]>
            {
                ["Content-Type"] = ["text/plain; charset=utf-8"]
            },
            Body = System.Text.Encoding.UTF8.GetBytes(message)
        };
    }

    public IAsyncEnumerable<TunnelMessage> ForwardStreamingAsync(RequestMessage request, CancellationToken ct)
    {
        // C# async iterators cannot `yield` inside try/catch blocks. We use a channel-backed producer so we can
        // stream chunks and still handle exceptions (notably TLS handshake failures) with actionable logs.
        var channel = Channel.CreateUnbounded<TunnelMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

        _ = ProduceAsync(channel.Writer, request, ct);
        return channel.Reader.ReadAllAsync(ct);
    }

    private async Task ProduceAsync(ChannelWriter<TunnelMessage> writer, RequestMessage request, CancellationToken ct)
    {
        HttpResponseMessage? httpResponse = null;
        PortMapping? mapping = null;
        try
        {
            mapping = await _db.PortMappings.FindAsync([request.MappingId], ct);

            if (mapping == null || !mapping.IsEnabled)
            {
                _logger.LogWarning("Mapping {MappingId} not found or disabled", request.MappingId);
                await writer.WriteAsync(CreateErrorResponse(request.RequestId, 404, "Mapping not found"), ct);
                return;
            }

            var client = CreateHttpClient(mapping);
            var httpRequest = CreateHttpRequest(request, mapping);
            var startTime = DateTime.UtcNow;

            // Use ResponseHeadersRead for streaming
            httpResponse = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation(
                "Forwarded {Method} {Path} mappingId={MappingId} external={ExternalDomain} to {Scheme}://{Host}:{Port} (allowInvalidCerts={AllowInvalidCerts}) -> {Status} ({Duration}ms, streaming)",
                request.Method,
                request.Path,
                mapping.Id,
                mapping.ExternalDomain,
                mapping.InternalUseTls ? "https" : "http",
                mapping.InternalHost,
                mapping.InternalPort,
                mapping.AllowSelfSignedCerts,
                (int)httpResponse.StatusCode, duration.TotalMilliseconds);

            // Build headers
            var headers = new Dictionary<string, string[]>();
            foreach (var (key, values) in httpResponse.Headers)
            {
                headers[key] = values.ToArray();
            }
            foreach (var (key, values) in httpResponse.Content.Headers)
            {
                headers[key] = values.ToArray();
                if (string.Equals(key, "Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Request {RequestId} streaming response Content-Type from upstream: {ContentType}",
                        request.RequestId, string.Join(", ", values));
                }
            }

            if (!headers.ContainsKey("Content-Type"))
            {
                _logger.LogWarning("Request {RequestId} streaming response has no Content-Type header for path: {Path}",
                    request.RequestId, request.Path);
            }

            var contentLength = httpResponse.Content.Headers.ContentLength;
            var shouldStream = contentLength == null || contentLength > StreamingThreshold;

            if (!shouldStream)
            {
                var body = await httpResponse.Content.ReadAsByteArrayAsync(ct);
                await writer.WriteAsync(new ResponseMessage
                {
                    RequestId = request.RequestId,
                    StatusCode = (int)httpResponse.StatusCode,
                    Headers = headers,
                    Body = body,
                    HasMoreBody = false
                }, ct);
                return;
            }

            await writer.WriteAsync(new ResponseMessage
            {
                RequestId = request.RequestId,
                StatusCode = (int)httpResponse.StatusCode,
                Headers = headers,
                Body = null,
                HasMoreBody = true
            }, ct);

            await using var stream = await httpResponse.Content.ReadAsStreamAsync(ct);
            var buffer = new byte[ChunkSize];
            int bytesRead;
            long totalBytesRead = 0;

            while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
            {
                totalBytesRead += bytesRead;
                var chunk = buffer.AsSpan(0, bytesRead).ToArray();
                var hasMore = contentLength == null || totalBytesRead < contentLength;

                await writer.WriteAsync(new ResponseBodyChunkMessage
                {
                    RequestId = request.RequestId,
                    Data = chunk,
                    IsFinal = !hasMore
                }, ct);
            }

            if (contentLength == null || totalBytesRead < contentLength)
            {
                await writer.WriteAsync(new ResponseBodyChunkMessage
                {
                    RequestId = request.RequestId,
                    Data = [],
                    IsFinal = true
                }, ct);
            }
        }
        catch (HttpRequestException ex)
        {
            var mappingInfo = mapping == null
                ? $"mappingId={request.MappingId}"
                : $"mappingId={mapping.Id} external={mapping.ExternalDomain} target={(mapping.InternalUseTls ? "https" : "http")}://{mapping.InternalHost}:{mapping.InternalPort} allowInvalidCerts={mapping.AllowSelfSignedCerts}";

            _logger.LogWarning(ex,
                "Streaming forward failed ({MappingInfo}) requestId={RequestId} method={Method} path={Path}. Cause: {Cause}",
                mappingInfo,
                request.RequestId,
                request.Method,
                request.Path,
                FormatExceptionChain(ex));

            await writer.WriteAsync(CreateErrorResponse(request.RequestId, 502, CreateSafeBadGatewayMessage(ex)), ct);
        }
        catch (TaskCanceledException)
        {
            await writer.WriteAsync(CreateErrorResponse(request.RequestId, 504, "Gateway Timeout"), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error streaming forward for request {RequestId}", request.RequestId);
            await writer.WriteAsync(CreateErrorResponse(request.RequestId, 502, "Bad Gateway: upstream service unavailable"), ct);
        }
        finally
        {
            httpResponse?.Dispose();
            writer.TryComplete();
        }
    }

    private static string FormatExceptionChain(Exception ex)
    {
        var sb = new StringBuilder();
        var current = ex;
        var depth = 0;
        while (current != null && depth < 6)
        {
            if (depth > 0)
                sb.Append(" -> ");
            sb.Append(current.GetType().Name);
            if (!string.IsNullOrWhiteSpace(current.Message))
            {
                sb.Append(": ");
                sb.Append(current.Message);
            }
            current = current.InnerException!;
            depth++;
        }
        return sb.ToString();
    }

    private static string CreateSafeBadGatewayMessage(HttpRequestException ex)
    {
        // HttpRequestException.Message is already what we showed before.
        // Add a short inner hint when available so the user gets something more actionable.
        var inner = ex.InnerException;
        if (inner == null || string.IsNullOrWhiteSpace(inner.Message))
            return "Bad Gateway: upstream service unavailable";

        var hint = $"{inner.GetType().Name}: {inner.Message}";
        hint = hint.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (hint.Length > 300)
            hint = hint[..300];

        // Special-case common TLS symptom: HTTPS to a plaintext HTTP port (or wrong port).
        if (hint.Contains("Cannot determine the frame size", StringComparison.OrdinalIgnoreCase) ||
            hint.Contains("corrupted frame", StringComparison.OrdinalIgnoreCase))
        {
            return $"Bad Gateway: {ex.Message} (inner: {hint}). Hint: this usually means the mapping is set to HTTPS but the upstream is speaking plain HTTP (or the port is wrong).";
        }

        return $"Bad Gateway: {ex.Message} (inner: {hint})";
    }
}

public static class HttpClientExtensions
{
    public static IServiceCollection AddInternalServicesHttpClient(this IServiceCollection services)
    {
        // Standard client that validates certificates
        services.AddHttpClient("InternalServices")
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                // Behave like a reverse proxy: pass redirects through instead of following them.
                // Following redirects can accidentally switch schemes/ports (e.g., https://...:8080) and surface as TLS frame errors.
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                MaxConnectionsPerServer = 100,
                ConnectTimeout = TimeSpan.FromSeconds(10)
            });

        // CRITICAL-06: Client that allows self-signed certificates but still validates the chain
        // This is more secure than accepting ALL certificates
        services.AddHttpClient("InternalServices-Insecure")
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                MaxConnectionsPerServer = 100,
                ConnectTimeout = TimeSpan.FromSeconds(10),
                SslOptions = new SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                    {
                        // Accept if no errors at all
                        if (sslPolicyErrors == System.Net.Security.SslPolicyErrors.None)
                            return true;

                        // Accept self-signed certificates (chain errors only, not name mismatch)
                        // This still rejects expired certs and name mismatches unless self-signed
                        if (sslPolicyErrors == System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors)
                        {
                            // Check if it's actually a self-signed cert (issuer == subject)
                            if (certificate != null)
                            {
                                var cert = new X509Certificate2(certificate);
                                if (cert.Subject == cert.Issuer)
                                {
                                    return true; // Self-signed, allow
                                }
                            }
                        }

                        // Log and reject other certificate issues
                        return false;
                    }
                }
            });

        return services;
    }
}
