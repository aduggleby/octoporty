// GetStatusEndpoint.cs
// Returns agent connection status for the dashboard.
// Redacts sensitive data (Gateway URL credentials, error details).
// Authenticated endpoint - requires valid JWT.

using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Octoporty.Agent.Data;
using Octoporty.Agent.Services;
using Octoporty.Shared.Options;

namespace Octoporty.Agent.Features.Status;

public class GetStatusEndpoint : EndpointWithoutRequest<AgentStatusResponse>
{
    private readonly TunnelClient _tunnelClient;
    private readonly OctoportyDbContext _db;
    private readonly AgentOptions _options;

    public GetStatusEndpoint(
        TunnelClient tunnelClient,
        OctoportyDbContext db,
        IOptions<AgentOptions> options)
    {
        _tunnelClient = tunnelClient;
        _db = db;
        _options = options.Value;
    }

    public override void Configure()
    {
        Get("/api/v1/status");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var activeMappings = await _db.PortMappings.CountAsync(m => m.IsEnabled, ct);

        double? uptimeSeconds = null;
        if (_tunnelClient.LastConnectedAt.HasValue && _tunnelClient.State == TunnelClientState.Connected)
        {
            uptimeSeconds = (DateTime.UtcNow - _tunnelClient.LastConnectedAt.Value).TotalSeconds;
        }

        // MEDIUM-01: Authenticated endpoint can return more info, but still redact sensitive data
        await Send.OkAsync(new AgentStatusResponse
        {
            ConnectionStatus = _tunnelClient.State.ToString(),
            GatewayUrl = RedactUrl(_options.GatewayUrl), // Redact credentials if present
            LastConnected = _tunnelClient.LastConnectedAt,
            LastDisconnected = null,
            ReconnectAttempts = 0,
            UptimeSeconds = uptimeSeconds,
            Version = TunnelClient.Version,
            ActiveMappings = activeMappings,
            GatewayVersion = _tunnelClient.GatewayVersion,
            LastError = null, // Don't expose error details
            GatewayUpdateAvailable = _tunnelClient.GatewayUpdateAvailable
        }, ct);
    }

    private static string RedactUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            // Return only scheme, host, port - no credentials or path
            return $"{uri.Scheme}://{uri.Host}:{uri.Port}";
        }
        catch
        {
            return "[redacted]";
        }
    }
}
