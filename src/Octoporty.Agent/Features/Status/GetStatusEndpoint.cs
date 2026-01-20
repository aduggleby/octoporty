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

        await Send.OkAsync(new AgentStatusResponse
        {
            ConnectionStatus = _tunnelClient.State.ToString(),
            GatewayUrl = _options.GatewayUrl,
            LastConnected = _tunnelClient.LastConnectedAt,
            LastDisconnected = null, // Could track this if needed
            ReconnectAttempts = 0, // Could expose from TunnelClient if needed
            UptimeSeconds = uptimeSeconds,
            Version = "1.0.0",
            ActiveMappings = activeMappings,
            GatewayVersion = _tunnelClient.GatewayVersion,
            LastError = _tunnelClient.LastError
        }, ct);
    }
}
