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
    public required int ExternalPort { get; init; }

    [Key(3)]
    public required string InternalHost { get; init; }

    [Key(4)]
    public required int InternalPort { get; init; }

    [Key(5)]
    public required bool InternalUseTls { get; init; }

    [Key(6)]
    public required bool AllowSelfSignedCerts { get; init; }

    [Key(7)]
    public required bool IsEnabled { get; init; }
}
