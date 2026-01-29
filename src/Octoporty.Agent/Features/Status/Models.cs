// Models.cs
// Response DTO for the agent status endpoint.
// Exposes connection state, uptime, and active mapping count.

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

    /// <summary>
    /// Indicates whether the Agent version is newer than the Gateway version.
    /// When true, the user can trigger a Gateway update from the UI.
    /// </summary>
    public bool GatewayUpdateAvailable { get; init; }

    /// <summary>
    /// Gateway uptime in seconds, as reported by the Gateway.
    /// Updated with each heartbeat cycle (every 30 seconds).
    /// Null if not connected or uptime not yet received.
    /// </summary>
    public long? GatewayUptime { get; init; }
}
