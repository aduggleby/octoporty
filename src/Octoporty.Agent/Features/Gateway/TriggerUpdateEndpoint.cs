// TriggerUpdateEndpoint.cs
// API endpoint to trigger a Gateway self-update from the Agent.
// Sends an update request to the Gateway when version mismatch is detected.
// The Gateway will write a signal file for the host watcher to process.

using FastEndpoints;
using Octoporty.Agent.Services;
using Octoporty.Shared.Contracts;

namespace Octoporty.Agent.Features.Gateway;

public class TriggerUpdateRequest
{
    /// <summary>
    /// Optional: Force the update request even if versions appear equal.
    /// Useful for re-deploying the same version.
    /// </summary>
    public bool Force { get; set; }
}

public class TriggerUpdateResponse
{
    public required bool Success { get; init; }
    public required string Message { get; init; }
    public string? Error { get; init; }
    public required string AgentVersion { get; init; }
    public string? GatewayVersion { get; init; }
    public UpdateStatus? Status { get; init; }
}

public class TriggerUpdateEndpoint : Endpoint<TriggerUpdateRequest, TriggerUpdateResponse>
{
    private readonly TunnelClient _tunnelClient;
    private readonly ILogger<TriggerUpdateEndpoint> _logger;

    public TriggerUpdateEndpoint(TunnelClient tunnelClient, ILogger<TriggerUpdateEndpoint> logger)
    {
        _tunnelClient = tunnelClient;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/api/v1/gateway/update");
        Description(d => d
            .WithSummary("Trigger Gateway Update")
            .WithDescription("Requests the Gateway to update itself to the Agent's version"));
    }

    public override async Task HandleAsync(TriggerUpdateRequest req, CancellationToken ct)
    {
        _logger.LogInformation("Gateway update requested via API (Force={Force})", req.Force);

        // Check if connected
        if (_tunnelClient.State != TunnelClientState.Connected)
        {
            _logger.LogWarning("Cannot trigger update - not connected to Gateway");
            await Send.OkAsync(new TriggerUpdateResponse
            {
                Success = false,
                Message = "Not connected to Gateway",
                Error = "Agent is not currently connected to the Gateway",
                AgentVersion = TunnelClient.Version,
                GatewayVersion = _tunnelClient.GatewayVersion
            }, ct);
            return;
        }

        // Check if update is available (unless force is set)
        if (!_tunnelClient.GatewayUpdateAvailable && !req.Force)
        {
            _logger.LogInformation("No Gateway update available");
            await Send.OkAsync(new TriggerUpdateResponse
            {
                Success = false,
                Message = "No update available",
                Error = "Gateway is already running the same or newer version",
                AgentVersion = TunnelClient.Version,
                GatewayVersion = _tunnelClient.GatewayVersion
            }, ct);
            return;
        }

        try
        {
            var response = await _tunnelClient.RequestGatewayUpdateAsync(ct);

            if (response.Accepted)
            {
                var message = response.Status switch
                {
                    UpdateStatus.Queued => "Gateway update queued successfully. Gateway will restart soon.",
                    UpdateStatus.AlreadyQueued => "Gateway update was already queued.",
                    _ => "Update request accepted."
                };

                _logger.LogInformation("Gateway update request successful: {Status}", response.Status);
                await Send.OkAsync(new TriggerUpdateResponse
                {
                    Success = true,
                    Message = message,
                    AgentVersion = TunnelClient.Version,
                    GatewayVersion = response.CurrentVersion,
                    Status = response.Status
                }, ct);
            }
            else
            {
                _logger.LogWarning("Gateway update request rejected: {Error}", response.Error);
                await Send.OkAsync(new TriggerUpdateResponse
                {
                    Success = false,
                    Message = "Update request rejected by Gateway",
                    Error = response.Error,
                    AgentVersion = TunnelClient.Version,
                    GatewayVersion = response.CurrentVersion,
                    Status = response.Status
                }, ct);
            }
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Timeout waiting for Gateway update response");
            await Send.OkAsync(new TriggerUpdateResponse
            {
                Success = false,
                Message = "Request timed out",
                Error = "Gateway did not respond within 30 seconds",
                AgentVersion = TunnelClient.Version,
                GatewayVersion = _tunnelClient.GatewayVersion
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request Gateway update");
            await Send.OkAsync(new TriggerUpdateResponse
            {
                Success = false,
                Message = "Update request failed",
                Error = ex.Message,
                AgentVersion = TunnelClient.Version,
                GatewayVersion = _tunnelClient.GatewayVersion
            }, ct);
        }
    }
}
