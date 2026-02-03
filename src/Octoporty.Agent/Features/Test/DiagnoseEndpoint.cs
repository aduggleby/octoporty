// DiagnoseEndpoint.cs
// Diagnostics endpoint that probes a URL through the Gateway and directly against the Agent's internal target.
// Returns both responses and Gateway logs correlated by request ID.

using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Octoporty.Agent.Data;
using Octoporty.Agent.Services;

namespace Octoporty.Agent.Features.Test;

public class DiagnoseRequest
{
    public required string Url { get; init; }
    public int MaxBodyBytes { get; init; } = 128 * 1024;
}

public class DiagnoseResponse
{
    public required bool Success { get; init; }
    public string? Error { get; init; }
    public required string RequestId { get; init; }
    public required string Url { get; init; }
    public MappingInfo? Mapping { get; init; }
    public ProbeResult? Gateway { get; init; }
    public ProbeResult? Agent { get; init; }
    public required GatewayLogEntry[] GatewayLogs { get; init; }
}

public class MappingInfo
{
    public required string Id { get; init; }
    public required string ExternalDomain { get; init; }
    public required string InternalTarget { get; init; }
}

public class ProbeResult
{
    public required bool Success { get; init; }
    public string? Error { get; init; }
    public int? StatusCode { get; init; }
    public long? DurationMs { get; init; }
    public Dictionary<string, string[]>? Headers { get; init; }
    public string? ContentType { get; init; }
    public int? BodySize { get; init; }
    public bool BodyTruncated { get; init; }
    public string? BodyText { get; init; }
    public string? BodyBase64 { get; init; }
}

public class GatewayLogEntry
{
    public long Id { get; init; }
    public DateTime Timestamp { get; init; }
    public required string Level { get; init; }
    public required string Message { get; init; }
}

public class DiagnoseEndpoint : Endpoint<DiagnoseRequest, DiagnoseResponse>
{
    private readonly OctoportyDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TunnelClient _tunnelClient;
    private readonly ILogger<DiagnoseEndpoint> _logger;

    public DiagnoseEndpoint(
        OctoportyDbContext db,
        IHttpClientFactory httpClientFactory,
        TunnelClient tunnelClient,
        ILogger<DiagnoseEndpoint> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _tunnelClient = tunnelClient;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/api/v1/test/diagnose");
        Description(d => d
            .WithSummary("Diagnose Gateway vs Agent response")
            .WithDescription("Probes a URL via Gateway and direct internal target, returning responses and Gateway logs"));
    }

    public override async Task HandleAsync(DiagnoseRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Url) || !Uri.TryCreate(req.Url, UriKind.Absolute, out var uri))
        {
            await Send.OkAsync(new DiagnoseResponse
            {
                Success = false,
                Error = "Invalid URL",
                RequestId = "invalid",
                Url = req.Url ?? string.Empty,
                GatewayLogs = []
            }, ct);
            return;
        }

        if (!uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            await Send.OkAsync(new DiagnoseResponse
            {
                Success = false,
                Error = "Only http and https URLs are supported",
                RequestId = "invalid",
                Url = req.Url,
                GatewayLogs = []
            }, ct);
            return;
        }

        var requestId = $"diag-{Guid.NewGuid():N}";
        var maxBodyBytes = Math.Clamp(req.MaxBodyBytes, 1024, 512 * 1024);

        var mapping = await _db.PortMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ExternalDomain == uri.Host, ct);

        MappingInfo? mappingInfo = null;
        if (mapping != null)
        {
            mappingInfo = new MappingInfo
            {
                Id = mapping.Id.ToString(),
                ExternalDomain = mapping.ExternalDomain,
                InternalTarget = $"{mapping.InternalHost}:{mapping.InternalPort}"
            };
        }

        var gatewayProbe = await ProbeAsync(
            () => BuildGatewayRequest(uri, requestId),
            maxBodyBytes,
            ct);

        ProbeResult? agentProbe = null;
        if (mapping == null)
        {
            agentProbe = new ProbeResult
            {
                Success = false,
                Error = "No matching mapping for host"
            };
        }
        else if (!mapping.IsEnabled)
        {
            agentProbe = new ProbeResult
            {
                Success = false,
                Error = "Mapping is disabled"
            };
        }
        else
        {
            var internalUri = BuildInternalUri(uri, mapping);
            var clientName = mapping.AllowSelfSignedCerts ? "InternalServices-Insecure" : "InternalServices";
            agentProbe = await ProbeAsync(
                () => new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, internalUri),
                maxBodyBytes,
                ct,
                clientName);
        }

        var gatewayLogs = await TryGetGatewayLogsAsync(requestId, ct);

        await Send.OkAsync(new DiagnoseResponse
        {
            Success = true,
            RequestId = requestId,
            Url = req.Url,
            Mapping = mappingInfo,
            Gateway = gatewayProbe,
            Agent = agentProbe,
            GatewayLogs = gatewayLogs
        }, ct);
    }

    private HttpRequestMessage BuildGatewayRequest(Uri uri, string requestId)
    {
        var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("X-Octoporty-Request-Id", requestId);
        request.Headers.TryAddWithoutValidation("X-Octoporty-Debug", "true");
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
        return request;
    }

    private static Uri BuildInternalUri(Uri externalUri, Octoporty.Shared.Entities.PortMapping mapping)
    {
        var scheme = mapping.InternalUseTls ? "https" : "http";
        var builder = new UriBuilder(scheme, mapping.InternalHost, mapping.InternalPort)
        {
            Path = externalUri.AbsolutePath,
            Query = externalUri.Query
        };
        return builder.Uri;
    }

    private async Task<ProbeResult> ProbeAsync(
        Func<HttpRequestMessage> requestFactory,
        int maxBodyBytes,
        CancellationToken ct,
        string clientName = "InternalServices")
    {
        var client = _httpClientFactory.CreateClient(clientName);
        var request = requestFactory();

        try
        {
            var sw = Stopwatch.StartNew();
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            sw.Stop();

            var headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, values) in response.Headers)
            {
                headers[key] = values.ToArray();
            }
            foreach (var (key, values) in response.Content.Headers)
            {
                headers[key] = values.ToArray();
            }

            var contentType = response.Content.Headers.ContentType?.ToString();
            var (bodyBytes, truncated) = await ReadLimitedBodyAsync(response, maxBodyBytes, ct);
            var bodySize = bodyBytes.Length;

            string? bodyText = null;
            string? bodyBase64 = null;
            if (bodySize > 0)
            {
                if (IsTextual(contentType))
                {
                    bodyText = Encoding.UTF8.GetString(bodyBytes);
                }
                else
                {
                    bodyBase64 = Convert.ToBase64String(bodyBytes);
                }
            }

            return new ProbeResult
            {
                Success = true,
                StatusCode = (int)response.StatusCode,
                DurationMs = sw.ElapsedMilliseconds,
                Headers = headers,
                ContentType = contentType,
                BodySize = bodySize,
                BodyTruncated = truncated,
                BodyText = bodyText,
                BodyBase64 = bodyBase64
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Probe failed");
            return new ProbeResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private static async Task<(byte[] Data, bool Truncated)> ReadLimitedBodyAsync(
        HttpResponseMessage response,
        int maxBodyBytes,
        CancellationToken ct)
    {
        if (response.Content == null)
        {
            return ([], false);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[8192];
        var ms = new MemoryStream();
        var totalRead = 0;
        var truncated = false;

        while (true)
        {
            var read = await stream.ReadAsync(buffer, ct);
            if (read == 0)
                break;

            var remaining = maxBodyBytes - totalRead;
            if (remaining <= 0)
            {
                truncated = true;
                break;
            }

            var toWrite = Math.Min(read, remaining);
            ms.Write(buffer, 0, toWrite);
            totalRead += toWrite;

            if (toWrite < read)
            {
                truncated = true;
                break;
            }
        }

        return (ms.ToArray(), truncated);
    }

    private static bool IsTextual(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return true;
        }

        var ct = contentType.ToLowerInvariant();
        return ct.StartsWith("text/")
               || ct.Contains("json")
               || ct.Contains("xml")
               || ct.Contains("javascript")
               || ct.Contains("x-www-form-urlencoded")
               || ct.Contains("svg");
    }

    private async Task<GatewayLogEntry[]> TryGetGatewayLogsAsync(string requestId, CancellationToken ct)
    {
        if (_tunnelClient.State != TunnelClientState.Connected)
        {
            return [];
        }

        try
        {
            var response = await _tunnelClient.GetGatewayLogsAsync(0, 500, ct);
            return response.Logs
                .Where(l => l.Message.Contains(requestId, StringComparison.OrdinalIgnoreCase))
                .Select(l => new GatewayLogEntry
                {
                    Id = l.Id,
                    Timestamp = l.Timestamp,
                    Level = l.Level.ToString(),
                    Message = l.Message
                })
                .OrderBy(l => l.Timestamp)
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve Gateway logs for diagnose request");
            return [];
        }
    }
}
