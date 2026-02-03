// GetCaddyConfigEndpoint.cs
// API endpoint to retrieve the current Caddy configuration from the Gateway.
// Used for debugging and monitoring the reverse proxy state.

using FastEndpoints;
using Octoporty.Agent.Services;

namespace Octoporty.Agent.Features.Gateway;

public class GetCaddyConfigResponse
{
    public required bool Success { get; init; }
    public string? Error { get; init; }
    public string? ConfigJson { get; init; }
}

public class GetCaddyConfigEndpoint : EndpointWithoutRequest<GetCaddyConfigResponse>
{
    private readonly TunnelClient _tunnelClient;
    private readonly ILogger<GetCaddyConfigEndpoint> _logger;

    public GetCaddyConfigEndpoint(TunnelClient tunnelClient, ILogger<GetCaddyConfigEndpoint> logger)
    {
        _tunnelClient = tunnelClient;
        _logger = logger;
    }

    public override void Configure()
    {
        Get("/api/v1/gateway/caddy-config");
        Description(d => d
            .WithSummary("Get Caddy Configuration")
            .WithDescription("Retrieves the current Caddy reverse proxy configuration from the Gateway"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Check if connected
        if (_tunnelClient.State != TunnelClientState.Connected)
        {
            _logger.LogWarning("Cannot get Caddy config - not connected to Gateway");
            await Send.OkAsync(new GetCaddyConfigResponse
            {
                Success = false,
                Error = "Agent is not currently connected to the Gateway"
            }, ct);
            return;
        }

        try
        {
            var response = await _tunnelClient.GetCaddyConfigAsync(ct);

            await Send.OkAsync(new GetCaddyConfigResponse
            {
                Success = response.Success,
                Error = response.Error,
                ConfigJson = response.ConfigJson
            }, ct);
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Timeout waiting for Caddy config response");
            await Send.OkAsync(new GetCaddyConfigResponse
            {
                Success = false,
                Error = "Gateway did not respond within 30 seconds"
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Caddy config");
            await Send.OkAsync(new GetCaddyConfigResponse
            {
                Success = false,
                Error = ex.Message
            }, ct);
        }
    }
}
