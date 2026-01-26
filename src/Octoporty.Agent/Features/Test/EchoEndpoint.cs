// EchoEndpoint.cs
// Test endpoint that echoes back request details.
// Used for verifying tunnel connectivity and request forwarding.
// Returns request headers, body, and Agent metadata.

using System.Text.Json;
using FastEndpoints;
using Octoporty.Agent.Services;

namespace Octoporty.Agent.Features.Test;

public class EchoRequest
{
    public JsonElement? Data { get; set; }
}

public class EchoResponse
{
    public required bool Success { get; init; }
    public required string Message { get; init; }
    public required EchoRequestInfo Request { get; init; }
    public required EchoAgentInfo Agent { get; init; }
    public required DateTime Timestamp { get; init; }
}

public class EchoRequestInfo
{
    public required string Method { get; init; }
    public required string Path { get; init; }
    public required Dictionary<string, string[]> Headers { get; init; }
    public required string? Body { get; init; }
    public required string RemoteIp { get; init; }
}

public class EchoAgentInfo
{
    public required string Version { get; init; }
    public required string ConnectionStatus { get; init; }
    public required string? GatewayVersion { get; init; }
    public required string MachineName { get; init; }
}

public class EchoEndpoint : Endpoint<EchoRequest, EchoResponse>
{
    private readonly TunnelClient _tunnelClient;

    public EchoEndpoint(TunnelClient tunnelClient)
    {
        _tunnelClient = tunnelClient;
    }

    public override void Configure()
    {
        Post("/api/v1/test/echo");
        AllowAnonymous(); // Allow unauthenticated for testing tunnel flow
    }

    public override async Task HandleAsync(EchoRequest req, CancellationToken ct)
    {
        // Collect request headers
        var headers = new Dictionary<string, string[]>();
        foreach (var (key, values) in HttpContext.Request.Headers)
        {
            headers[key] = values.Select(v => v ?? "").ToArray();
        }

        // Use the already-parsed request data
        string? rawBody = null;
        if (req.Data.HasValue)
        {
            rawBody = req.Data.Value.GetRawText();
        }

        await Send.OkAsync(new EchoResponse
        {
            Success = true,
            Message = "Echo from Octoporty Agent - tunnel is working!",
            Request = new EchoRequestInfo
            {
                Method = HttpContext.Request.Method,
                Path = HttpContext.Request.Path + HttpContext.Request.QueryString,
                Headers = headers,
                Body = rawBody,
                RemoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            },
            Agent = new EchoAgentInfo
            {
                Version = "1.0.0",
                ConnectionStatus = _tunnelClient.State.ToString(),
                GatewayVersion = _tunnelClient.GatewayVersion,
                MachineName = Environment.MachineName
            },
            Timestamp = DateTime.UtcNow
        }, ct);
    }
}

// GET variant for simpler testing
public class EchoGetEndpoint : EndpointWithoutRequest<EchoResponse>
{
    private readonly TunnelClient _tunnelClient;

    public EchoGetEndpoint(TunnelClient tunnelClient)
    {
        _tunnelClient = tunnelClient;
    }

    public override void Configure()
    {
        Get("/api/v1/test/echo");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var headers = new Dictionary<string, string[]>();
        foreach (var (key, values) in HttpContext.Request.Headers)
        {
            headers[key] = values.Select(v => v ?? "").ToArray();
        }

        await Send.OkAsync(new EchoResponse
        {
            Success = true,
            Message = "Echo from Octoporty Agent - tunnel is working!",
            Request = new EchoRequestInfo
            {
                Method = HttpContext.Request.Method,
                Path = HttpContext.Request.Path + HttpContext.Request.QueryString,
                Headers = headers,
                Body = null,
                RemoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            },
            Agent = new EchoAgentInfo
            {
                Version = "1.0.0",
                ConnectionStatus = _tunnelClient.State.ToString(),
                GatewayVersion = _tunnelClient.GatewayVersion,
                MachineName = Environment.MachineName
            },
            Timestamp = DateTime.UtcNow
        }, ct);
    }
}
