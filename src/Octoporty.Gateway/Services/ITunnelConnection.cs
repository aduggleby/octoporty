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
    bool AgentSupportsWebSocketProxy { get; }
    IReadOnlyDictionary<Guid, PortMappingDto> Mappings { get; }

    Task SendAsync(TunnelMessage message, CancellationToken ct);
    Task<ResponseMessage?> SendRequestAsync(RequestMessage request, TimeSpan timeout, CancellationToken ct);
    IAsyncEnumerable<StreamingResponse> SendStreamingRequestAsync(RequestMessage request, TimeSpan timeout, CancellationToken ct);
    Task<WebSocketOpenResultMessage?> OpenWebSocketAsync(WebSocketOpenMessage request, TimeSpan timeout, CancellationToken ct);
    Task SendWebSocketFrameAsync(WebSocketFrameMessage frame, CancellationToken ct);
    Task SendWebSocketCloseAsync(WebSocketCloseMessage close, CancellationToken ct);
    IAsyncEnumerable<TunnelMessage> ReceiveWebSocketMessagesAsync(string sessionId, CancellationToken ct);
}

public readonly record struct StreamingResponse(
    ResponseMessage? InitialResponse,
    ResponseBodyChunkMessage? Chunk,
    bool IsComplete);
