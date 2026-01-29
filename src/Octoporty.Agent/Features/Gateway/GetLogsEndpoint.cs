// GetLogsEndpoint.cs
// API endpoint to retrieve historical Gateway logs.
// Supports pagination via beforeId for infinite scroll in the UI.
// Returns logs in reverse chronological order (newest first).

using FastEndpoints;
using Octoporty.Agent.Services;
using Octoporty.Shared.Contracts;

namespace Octoporty.Agent.Features.Gateway;

public class GetLogsRequest
{
    /// <summary>
    /// Return logs with ID less than this value.
    /// Use 0 or omit for the latest logs.
    /// </summary>
    public long BeforeId { get; set; }

    /// <summary>
    /// Maximum number of logs to return (default: 1000, max: 5000).
    /// </summary>
    public int Count { get; set; } = 1000;
}

public class GetLogsResponse
{
    public required bool Success { get; init; }
    public string? Error { get; init; }
    public required GatewayLogItem[] Logs { get; init; }
    public bool HasMore { get; init; }
}

public class GatewayLogItem
{
    public long Id { get; init; }
    public DateTime Timestamp { get; init; }
    public required string Level { get; init; }
    public required string Message { get; init; }
}

public class GetLogsEndpoint : Endpoint<GetLogsRequest, GetLogsResponse>
{
    private readonly TunnelClient _tunnelClient;
    private readonly ILogger<GetLogsEndpoint> _logger;

    public GetLogsEndpoint(TunnelClient tunnelClient, ILogger<GetLogsEndpoint> logger)
    {
        _tunnelClient = tunnelClient;
        _logger = logger;
    }

    public override void Configure()
    {
        Get("/api/v1/gateway/logs");
        Description(d => d
            .WithSummary("Get Gateway Logs")
            .WithDescription("Retrieves historical logs from the Gateway with pagination support"));
    }

    public override async Task HandleAsync(GetLogsRequest req, CancellationToken ct)
    {
        // Validate and clamp count
        var count = Math.Clamp(req.Count, 1, 5000);

        // Check if connected
        if (_tunnelClient.State != TunnelClientState.Connected)
        {
            _logger.LogWarning("Cannot get logs - not connected to Gateway");
            await Send.OkAsync(new GetLogsResponse
            {
                Success = false,
                Error = "Agent is not currently connected to the Gateway",
                Logs = [],
                HasMore = false
            }, ct);
            return;
        }

        try
        {
            var response = await _tunnelClient.GetGatewayLogsAsync(req.BeforeId, count, ct);

            await Send.OkAsync(new GetLogsResponse
            {
                Success = true,
                Logs = response.Logs.Select(l => new GatewayLogItem
                {
                    Id = l.Id,
                    Timestamp = l.Timestamp,
                    Level = l.Level.ToString(),
                    Message = l.Message
                }).ToArray(),
                HasMore = response.HasMore
            }, ct);
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Timeout waiting for Gateway logs response");
            await Send.OkAsync(new GetLogsResponse
            {
                Success = false,
                Error = "Gateway did not respond within 30 seconds",
                Logs = [],
                HasMore = false
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Gateway logs");
            await Send.OkAsync(new GetLogsResponse
            {
                Success = false,
                Error = ex.Message,
                Logs = [],
                HasMore = false
            }, ct);
        }
    }
}
