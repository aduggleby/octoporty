// GetAgentLogsEndpoint.cs
// API endpoint to retrieve historical Agent logs from the in-memory AgentLogBuffer.
// Supports pagination via beforeId for infinite scroll in the UI.

using FastEndpoints;
using Octoporty.Agent.Services;

namespace Octoporty.Agent.Features.AgentLogs;

public class GetAgentLogsResponse
{
    public required bool Success { get; init; }
    public string? Error { get; init; }
    public required AgentLogItem[] Logs { get; init; }
    public bool HasMore { get; init; }
}

public class AgentLogItem
{
    public long Id { get; init; }
    public DateTime Timestamp { get; init; }
    public required string Level { get; init; }
    public required string Message { get; init; }
}

public class GetAgentLogsEndpoint : EndpointWithoutRequest<GetAgentLogsResponse>
{
    private readonly AgentLogBuffer _buffer;

    public GetAgentLogsEndpoint(AgentLogBuffer buffer)
    {
        _buffer = buffer;
    }

    public override void Configure()
    {
        Get("/api/v1/agent/logs");
        Description(d => d
            .WithSummary("Get Agent Logs")
            .WithDescription("Retrieves historical logs from the Agent process (buffered in memory) with pagination support"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var beforeIdStr = Query<string>("beforeId", isRequired: false);
        var countStr = Query<string>("count", isRequired: false);

        long beforeId = 0;
        if (!string.IsNullOrEmpty(beforeIdStr) && long.TryParse(beforeIdStr, out var parsedBeforeId))
        {
            beforeId = parsedBeforeId;
        }

        int count = 1000;
        if (!string.IsNullOrEmpty(countStr) && int.TryParse(countStr, out var parsedCount))
        {
            count = Math.Clamp(parsedCount, 1, 5000);
        }

        var (logs, hasMore) = _buffer.GetLogs(beforeId > 0 ? beforeId : null, count);

        await Send.OkAsync(new GetAgentLogsResponse
        {
            Success = true,
            Logs = logs.Select(l => new AgentLogItem
            {
                Id = l.Id,
                Timestamp = l.Timestamp,
                Level = l.Level.ToString(),
                Message = l.Message
            }).ToArray(),
            HasMore = hasMore
        }, ct);
    }
}

