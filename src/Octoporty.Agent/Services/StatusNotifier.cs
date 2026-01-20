using Microsoft.AspNetCore.SignalR;
using Octoporty.Agent.Hubs;

namespace Octoporty.Agent.Services;

public class StatusNotifier
{
    private readonly IHubContext<StatusHub, IStatusHubClient> _hubContext;
    private readonly ILogger<StatusNotifier> _logger;

    public StatusNotifier(
        IHubContext<StatusHub, IStatusHubClient> hubContext,
        ILogger<StatusNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyStatusChangeAsync(TunnelClientState state, string? message = null)
    {
        var update = new StatusUpdateMessage(
            ConnectionStatus: state.ToString(),
            Timestamp: DateTime.UtcNow,
            Message: message);

        _logger.LogDebug("Broadcasting status update: {Status}", state);
        await _hubContext.Clients.All.StatusUpdate(update);
    }

    public async Task NotifyMappingStatusAsync(Guid mappingId, string status, DateTime? lastRequestAt = null, string? errorMessage = null)
    {
        var update = new MappingStatusUpdateMessage(
            MappingId: mappingId,
            Status: status,
            LastRequestAt: lastRequestAt,
            ErrorMessage: errorMessage);

        await _hubContext.Clients.All.MappingStatusUpdate(update);
    }
}
