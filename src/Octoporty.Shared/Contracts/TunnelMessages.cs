// TunnelMessages.cs
// MessagePack-serializable message types for the WebSocket tunnel protocol.
// Uses Union attribute for polymorphic deserialization of TunnelMessage subtypes.
// Message flow: Auth → ConfigSync → Heartbeat loop + Request/Response forwarding.

using MessagePack;

namespace Octoporty.Shared.Contracts;

[Union(0, typeof(AuthMessage))]
[Union(1, typeof(AuthResultMessage))]
[Union(2, typeof(ConfigSyncMessage))]
[Union(3, typeof(ConfigAckMessage))]
[Union(4, typeof(HeartbeatMessage))]
[Union(5, typeof(HeartbeatAckMessage))]
[Union(6, typeof(RequestMessage))]
[Union(7, typeof(ResponseMessage))]
[Union(8, typeof(DisconnectMessage))]
[Union(9, typeof(ErrorMessage))]
[Union(10, typeof(RequestBodyChunkMessage))]
[Union(11, typeof(ResponseBodyChunkMessage))]
[Union(12, typeof(UpdateRequestMessage))]
[Union(13, typeof(UpdateResponseMessage))]
[Union(14, typeof(GatewayLogMessage))]
[Union(15, typeof(GetLogsRequestMessage))]
[Union(16, typeof(GetLogsResponseMessage))]
[Union(17, typeof(GetCaddyConfigRequestMessage))]
[Union(18, typeof(GetCaddyConfigResponseMessage))]
public abstract class TunnelMessage
{
    [IgnoreMember]
    public abstract MessageType Type { get; }
}

[MessagePackObject]
public sealed class AuthMessage : TunnelMessage
{
    [IgnoreMember]
    public override MessageType Type => MessageType.Auth;

    [Key(0)]
    public required string ApiKey { get; init; }

    [Key(1)]
    public required string AgentVersion { get; init; }
}

[MessagePackObject]
public sealed class AuthResultMessage : TunnelMessage
{
    [IgnoreMember]
    public override MessageType Type => MessageType.AuthResult;

    [Key(0)]
    public required bool Success { get; init; }

    [Key(1)]
    public string? Error { get; init; }

    [Key(2)]
    public string? GatewayVersion { get; init; }

    /// <summary>
    /// MD5 hash of the Gateway's current landing page HTML.
    /// Agent compares this with its own hash to determine if sync is needed.
    /// </summary>
    [Key(3)]
    public string? LandingPageHtmlHash { get; init; }
}

[MessagePackObject]
public sealed class ConfigSyncMessage : TunnelMessage
{
    [IgnoreMember]
    public override MessageType Type => MessageType.ConfigSync;

    [Key(0)]
    public required PortMappingDto[] Mappings { get; init; }

    [Key(1)]
    public required string ConfigHash { get; init; }

    /// <summary>
    /// Full landing page HTML content. Only sent when Agent's hash differs from Gateway's.
    /// </summary>
    [Key(2)]
    public string? LandingPageHtml { get; init; }

    /// <summary>
    /// MD5 hash of the landing page HTML for validation.
    /// </summary>
    [Key(3)]
    public string? LandingPageHtmlHash { get; init; }

    /// <summary>
    /// Gateway FQDN extracted from Agent's GatewayUrl setting.
    /// Gateway uses this for landing page routing if not manually configured.
    /// </summary>
    [Key(4)]
    public string? GatewayFqdn { get; init; }
}

[MessagePackObject]
public sealed class ConfigAckMessage : TunnelMessage
{
    [IgnoreMember]
    public override MessageType Type => MessageType.ConfigAck;

    [Key(0)]
    public required bool Success { get; init; }

    [Key(1)]
    public string? Error { get; init; }

    [Key(2)]
    public required string ConfigHash { get; init; }
}

[MessagePackObject]
public sealed class HeartbeatMessage : TunnelMessage
{
    [IgnoreMember]
    public override MessageType Type => MessageType.Heartbeat;

    [Key(0)]
    public required long Timestamp { get; init; }
}

[MessagePackObject]
public sealed class HeartbeatAckMessage : TunnelMessage
{
    [IgnoreMember]
    public override MessageType Type => MessageType.HeartbeatAck;

    [Key(0)]
    public required long Timestamp { get; init; }

    [Key(1)]
    public required long ServerTimestamp { get; init; }

    /// <summary>
    /// Gateway uptime in seconds since start.
    /// </summary>
    [Key(2)]
    public long? GatewayUptimeSeconds { get; init; }
}

[MessagePackObject]
public sealed class RequestMessage : TunnelMessage
{
    [IgnoreMember]
    public override MessageType Type => MessageType.Request;

    [Key(0)]
    public required string RequestId { get; init; }

    [Key(1)]
    public required Guid MappingId { get; init; }

    [Key(2)]
    public required string Method { get; init; }

    [Key(3)]
    public required string Path { get; init; }

    [Key(4)]
    public required Dictionary<string, string[]> Headers { get; init; }

    [Key(5)]
    public byte[]? Body { get; init; }

    [Key(6)]
    public bool HasMoreBody { get; init; }
}

[MessagePackObject]
public sealed class ResponseMessage : TunnelMessage
{
    [IgnoreMember]
    public override MessageType Type => MessageType.Response;

    [Key(0)]
    public required string RequestId { get; init; }

    [Key(1)]
    public required int StatusCode { get; init; }

    [Key(2)]
    public required Dictionary<string, string[]> Headers { get; init; }

    [Key(3)]
    public byte[]? Body { get; init; }

    [Key(4)]
    public bool HasMoreBody { get; init; }
}

[MessagePackObject]
public sealed class DisconnectMessage : TunnelMessage
{
    [IgnoreMember]
    public override MessageType Type => MessageType.Disconnect;

    [Key(0)]
    public required string Reason { get; init; }
}

[MessagePackObject]
public sealed class ErrorMessage : TunnelMessage
{
    [IgnoreMember]
    public override MessageType Type => MessageType.Error;

    [Key(0)]
    public string? RequestId { get; init; }

    [Key(1)]
    public required string Error { get; init; }

    [Key(2)]
    public required int Code { get; init; }
}

[MessagePackObject]
public sealed class RequestBodyChunkMessage : TunnelMessage
{
    [IgnoreMember]
    public override MessageType Type => MessageType.RequestBodyChunk;

    [Key(0)]
    public required string RequestId { get; init; }

    [Key(1)]
    public required byte[] Data { get; init; }

    [Key(2)]
    public required bool IsFinal { get; init; }
}

[MessagePackObject]
public sealed class ResponseBodyChunkMessage : TunnelMessage
{
    [IgnoreMember]
    public override MessageType Type => MessageType.ResponseBodyChunk;

    [Key(0)]
    public required string RequestId { get; init; }

    [Key(1)]
    public required byte[] Data { get; init; }

    [Key(2)]
    public required bool IsFinal { get; init; }
}

[MessagePackObject]
public sealed class PortMappingDto
{
    [Key(0)]
    public required Guid Id { get; init; }

    [Key(1)]
    public required string ExternalDomain { get; init; }

    [Key(2)]
    public required string InternalHost { get; init; }

    [Key(3)]
    public required int InternalPort { get; init; }

    [Key(4)]
    public required bool InternalUseTls { get; init; }

    [Key(5)]
    public required bool AllowSelfSignedCerts { get; init; }

    [Key(6)]
    public required bool IsEnabled { get; init; }
}

/// <summary>
/// Request from Agent to trigger a Gateway self-update.
/// Sent when Agent detects it has a newer version than Gateway.
/// </summary>
[MessagePackObject]
public sealed class UpdateRequestMessage : TunnelMessage
{
    [IgnoreMember]
    public override MessageType Type => MessageType.UpdateRequest;

    [Key(0)]
    public required string TargetVersion { get; init; }

    [Key(1)]
    public required string RequestedBy { get; init; }
}

/// <summary>
/// Response to an update request from the Gateway.
/// </summary>
[MessagePackObject]
public sealed class UpdateResponseMessage : TunnelMessage
{
    [IgnoreMember]
    public override MessageType Type => MessageType.UpdateResponse;

    [Key(0)]
    public required bool Accepted { get; init; }

    [Key(1)]
    public string? Error { get; init; }

    [Key(2)]
    public required string CurrentVersion { get; init; }

    [Key(3)]
    public required UpdateStatus Status { get; init; }
}

/// <summary>
/// Status of an update request.
/// </summary>
public enum UpdateStatus : byte
{
    /// <summary>Update has been queued and will be processed by host watcher.</summary>
    Queued = 0,

    /// <summary>Update was rejected (disabled, same version, or other error).</summary>
    Rejected = 1,

    /// <summary>An update is already queued and pending.</summary>
    AlreadyQueued = 2
}

/// <summary>
/// Log message from the Gateway forwarded to the Agent for UI display.
/// Allows real-time log streaming from the Gateway to the Agent's web UI.
/// </summary>
[MessagePackObject]
public sealed class GatewayLogMessage : TunnelMessage
{
    [IgnoreMember]
    public override MessageType Type => MessageType.GatewayLog;

    [Key(0)]
    public required DateTime Timestamp { get; init; }

    [Key(1)]
    public required GatewayLogLevel Level { get; init; }

    [Key(2)]
    public required string Message { get; init; }
}

/// <summary>
/// Log levels for gateway log messages.
/// </summary>
public enum GatewayLogLevel : byte
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3
}

/// <summary>
/// Request from Agent to get historical Gateway logs.
/// Used for initial log loading and infinite scroll.
/// </summary>
[MessagePackObject]
public sealed class GetLogsRequestMessage : TunnelMessage
{
    [IgnoreMember]
    public override MessageType Type => MessageType.GetLogsRequest;

    /// <summary>
    /// Unique request ID for correlation.
    /// </summary>
    [Key(0)]
    public required string RequestId { get; init; }

    /// <summary>
    /// Return logs with ID less than this value.
    /// Use 0 for the latest logs.
    /// </summary>
    [Key(1)]
    public long BeforeId { get; init; }

    /// <summary>
    /// Maximum number of logs to return.
    /// </summary>
    [Key(2)]
    public int Count { get; init; }
}

/// <summary>
/// Response containing historical Gateway logs.
/// </summary>
[MessagePackObject]
public sealed class GetLogsResponseMessage : TunnelMessage
{
    [IgnoreMember]
    public override MessageType Type => MessageType.GetLogsResponse;

    /// <summary>
    /// Request ID for correlation.
    /// </summary>
    [Key(0)]
    public required string RequestId { get; init; }

    /// <summary>
    /// Log entries in reverse chronological order (newest first).
    /// </summary>
    [Key(1)]
    public required GatewayLogDto[] Logs { get; init; }

    /// <summary>
    /// Whether there are more older logs available.
    /// </summary>
    [Key(2)]
    public bool HasMore { get; init; }
}

/// <summary>
/// DTO for a single log entry with ID for pagination.
/// </summary>
[MessagePackObject]
public sealed class GatewayLogDto
{
    [Key(0)]
    public long Id { get; init; }

    [Key(1)]
    public DateTime Timestamp { get; init; }

    [Key(2)]
    public GatewayLogLevel Level { get; init; }

    [Key(3)]
    public required string Message { get; init; }
}

/// <summary>
/// Request from Agent to get the current Caddy configuration from the Gateway.
/// Used for debugging and monitoring the reverse proxy state.
/// </summary>
[MessagePackObject]
public sealed class GetCaddyConfigRequestMessage : TunnelMessage
{
    [IgnoreMember]
    public override MessageType Type => MessageType.GetCaddyConfigRequest;

    /// <summary>
    /// Unique request ID for correlation.
    /// </summary>
    [Key(0)]
    public required string RequestId { get; init; }
}

/// <summary>
/// Response containing the current Caddy configuration.
/// </summary>
[MessagePackObject]
public sealed class GetCaddyConfigResponseMessage : TunnelMessage
{
    [IgnoreMember]
    public override MessageType Type => MessageType.GetCaddyConfigResponse;

    /// <summary>
    /// Request ID for correlation.
    /// </summary>
    [Key(0)]
    public required string RequestId { get; init; }

    /// <summary>
    /// Whether the request was successful.
    /// </summary>
    [Key(1)]
    public required bool Success { get; init; }

    /// <summary>
    /// Error message if the request failed.
    /// </summary>
    [Key(2)]
    public string? Error { get; init; }

    /// <summary>
    /// The Caddy configuration as JSON string.
    /// </summary>
    [Key(3)]
    public string? ConfigJson { get; init; }
}
