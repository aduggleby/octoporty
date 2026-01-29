// StatusHub.cs
// SignalR hub for real-time status updates to the web UI.
// Requires JWT authentication - only logged-in users receive updates.
// Broadcasts tunnel connection state and mapping status changes.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Octoporty.Agent.Hubs;

[Authorize]
public class StatusHub : Hub<IStatusHubClient>
{
    private readonly ILogger<StatusHub> _logger;

    public StatusHub(ILogger<StatusHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected to StatusHub: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected from StatusHub: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

public interface IStatusHubClient
{
    Task StatusUpdate(StatusUpdateMessage update);
    Task MappingStatusUpdate(MappingStatusUpdateMessage update);
    Task GatewayLog(GatewayLogMessageDto log);
}

public record GatewayLogMessageDto(
    DateTime Timestamp,
    string Level,
    string Message);

public record StatusUpdateMessage(
    string ConnectionStatus,
    DateTime Timestamp,
    string? Message = null);

public record MappingStatusUpdateMessage(
    Guid MappingId,
    string Status,
    DateTime? LastRequestAt = null,
    string? ErrorMessage = null);
