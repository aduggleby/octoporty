namespace Octoporty.Agent.Features.Status;

public record AgentStatusResponse
{
    public required string ConnectionStatus { get; init; }
    public required string GatewayUrl { get; init; }
    public DateTime? LastConnected { get; init; }
    public DateTime? LastDisconnected { get; init; }
    public int ReconnectAttempts { get; init; }
    public double? UptimeSeconds { get; init; }
    public required string Version { get; init; }
    public int ActiveMappings { get; init; }
    public string? GatewayVersion { get; init; }
    public string? LastError { get; init; }
}
