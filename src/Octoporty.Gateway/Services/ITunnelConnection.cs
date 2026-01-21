// ITunnelConnection.cs
// Interface representing an active tunnel connection from an Agent.
// Exposes connection metadata and methods for sending/receiving tunnel messages.
// Supports both request-response and streaming patterns.

using Octoporty.Shared.Contracts;

namespace Octoporty.Gateway.Services;

public interface ITunnelConnection
{
    string ConnectionId { get; }
    bool IsConnected { get; }
    DateTime ConnectedAt { get; }
    string? AgentVersion { get; }
    IReadOnlyDictionary<Guid, PortMappingDto> Mappings { get; }

    Task SendAsync(TunnelMessage message, CancellationToken ct);
    Task<ResponseMessage?> SendRequestAsync(RequestMessage request, TimeSpan timeout, CancellationToken ct);
    IAsyncEnumerable<StreamingResponse> SendStreamingRequestAsync(RequestMessage request, TimeSpan timeout, CancellationToken ct);
}

public readonly record struct StreamingResponse(
    ResponseMessage? InitialResponse,
    ResponseBodyChunkMessage? Chunk,
    bool IsComplete);
