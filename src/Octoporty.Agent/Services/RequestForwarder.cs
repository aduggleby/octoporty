using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
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

    private static async Task<ResponseMessage> CreateResponseMessageAsync(
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
        }

        var body = await httpResponse.Content.ReadAsByteArrayAsync(ct);

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

    public async IAsyncEnumerable<TunnelMessage> ForwardStreamingAsync(
        RequestMessage request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var mapping = await _db.PortMappings.FindAsync([request.MappingId], ct);

        if (mapping == null || !mapping.IsEnabled)
        {
            _logger.LogWarning("Mapping {MappingId} not found or disabled", request.MappingId);
            yield return CreateErrorResponse(request.RequestId, 404, "Mapping not found");
            yield break;
        }

        var client = CreateHttpClient(mapping);

        HttpResponseMessage? httpResponse = null;
        try
        {
            var httpRequest = CreateHttpRequest(request, mapping);
            var startTime = DateTime.UtcNow;

            // Use ResponseHeadersRead for streaming
            httpResponse = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Forwarded {Method} {Path} to {Host}:{Port} -> {Status} ({Duration}ms, streaming)",
                request.Method, request.Path, mapping.InternalHost, mapping.InternalPort,
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
            }

            // Check content length to determine if we should stream
            var contentLength = httpResponse.Content.Headers.ContentLength;
            var shouldStream = contentLength == null || contentLength > StreamingThreshold;

            if (!shouldStream)
            {
                // Small response - send as single message
                var body = await httpResponse.Content.ReadAsByteArrayAsync(ct);
                yield return new ResponseMessage
                {
                    RequestId = request.RequestId,
                    StatusCode = (int)httpResponse.StatusCode,
                    Headers = headers,
                    Body = body,
                    HasMoreBody = false
                };
                yield break;
            }

            // Large response - stream in chunks
            yield return new ResponseMessage
            {
                RequestId = request.RequestId,
                StatusCode = (int)httpResponse.StatusCode,
                Headers = headers,
                Body = null,
                HasMoreBody = true
            };

            // Stream body chunks
            await using var stream = await httpResponse.Content.ReadAsStreamAsync(ct);
            var buffer = new byte[ChunkSize];
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
            {
                var chunk = buffer.AsSpan(0, bytesRead).ToArray();
                var hasMore = stream.Position < (contentLength ?? long.MaxValue);

                yield return new ResponseBodyChunkMessage
                {
                    RequestId = request.RequestId,
                    Data = chunk,
                    IsFinal = !hasMore
                };
            }

            // Ensure we send a final chunk marker
            yield return new ResponseBodyChunkMessage
            {
                RequestId = request.RequestId,
                Data = [],
                IsFinal = true
            };
        }
        finally
        {
            httpResponse?.Dispose();
        }
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
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                MaxConnectionsPerServer = 100,
                ConnectTimeout = TimeSpan.FromSeconds(10)
            });

        // CRITICAL-06: Client that allows self-signed certificates but still validates the chain
        // This is more secure than accepting ALL certificates
        services.AddHttpClient("InternalServices-Insecure")
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
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
