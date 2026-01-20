using FastEndpoints;
using Octoporty.Agent.Services;

namespace Octoporty.Agent.Features.Status;

public class ReconnectEndpoint : EndpointWithoutRequest
{
    private readonly TunnelClient _tunnelClient;
    private readonly ILogger<ReconnectEndpoint> _logger;

    public ReconnectEndpoint(TunnelClient tunnelClient, ILogger<ReconnectEndpoint> logger)
    {
        _tunnelClient = tunnelClient;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/api/v1/reconnect");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        _logger.LogInformation("Manual reconnection requested");

        // Trigger a config resync if connected
        if (_tunnelClient.State == TunnelClientState.Connected)
        {
            await _tunnelClient.ResyncConfigurationAsync(ct);
        }

        await Send.OkAsync(new { message = "Reconnection triggered" }, ct);
    }
}
