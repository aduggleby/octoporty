// TunnelConnection.cs
// Manages a single WebSocket connection from an Agent.
// Handles bidirectional message flow: receive loop reads incoming messages, send loop writes outbound.
// Uses channels for backpressure and TaskCompletionSource for request-response correlation.
// Supports streaming responses via Channel<StreamingResponse>.

using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Octoporty.Shared.Contracts;

namespace Octoporty.Gateway.Services;

public sealed class TunnelConnection : ITunnelConnection, IAsyncDisposable
{
    private readonly WebSocket _webSocket;
    private readonly ILogger<TunnelConnection> _logger;
    private readonly Channel<TunnelMessage> _outboundChannel;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ResponseMessage>> _pendingRequests = new();
    private readonly ConcurrentDictionary<string, Channel<StreamingResponse>> _streamingRequests = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<WebSocketOpenResultMessage>> _pendingWebSocketOpens = new();
    private readonly ConcurrentDictionary<string, Channel<TunnelMessage>> _webSocketSessions = new();
    private readonly ConcurrentDictionary<Guid, PortMappingDto> _mappings = new();
    private readonly CancellationTokenSource _cts = new();

    private Task? _receiveTask;
    private Task? _sendTask;

    public string ConnectionId { get; } = Guid.NewGuid().ToString("N")[..8];
    public bool IsConnected => _webSocket.State == WebSocketState.Open;
    public DateTime ConnectedAt { get; } = DateTime.UtcNow;
    public string? AgentVersion { get; private set; }
    public bool AgentSupportsWebSocketProxy { get; private set; }
    public IReadOnlyDictionary<Guid, PortMappingDto> Mappings => _mappings;

    public TunnelConnection(WebSocket webSocket, ILogger<TunnelConnection> logger)
    {
        _webSocket = webSocket;
        _logger = logger;
        _outboundChannel = Channel.CreateBounded<TunnelMessage>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    public void SetAgentVersion(string version) => AgentVersion = version;
    public void SetAgentCapabilities(bool supportsWebSocketProxy) => AgentSupportsWebSocketProxy = supportsWebSocketProxy;

    public void UpdateMappings(IEnumerable<PortMappingDto> mappings)
    {
        _mappings.Clear();
        foreach (var mapping in mappings)
        {
            _mappings[mapping.Id] = mapping;
        }
    }

    public void StartProcessing(Func<TunnelMessage, Task> onMessageReceived)
    {
        _logger.LogDebug("StartProcessing called for connection {ConnectionId}, WebSocket state: {State}",
            ConnectionId, _webSocket.State);
        _receiveTask = ReceiveLoopAsync(onMessageReceived, _cts.Token);
        _sendTask = SendLoopAsync(_cts.Token);
        _logger.LogDebug("Receive and send loops started for connection {ConnectionId}", ConnectionId);
    }

    public async Task SendAsync(TunnelMessage message, CancellationToken ct)
    {
        await _outboundChannel.Writer.WriteAsync(message, ct);
    }

    public async Task<ResponseMessage?> SendRequestAsync(RequestMessage request, TimeSpan timeout, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<ResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[request.RequestId] = tcs;

        try
        {
            await SendAsync(request, ct);

            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            try
            {
                return await tcs.Task.WaitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                _logger.LogWarning("Request {RequestId} timed out after {Timeout}ms", request.RequestId, timeout.TotalMilliseconds);
                return null;
            }
        }
        finally
        {
            _pendingRequests.TryRemove(request.RequestId, out _);
        }
    }

    public void CompleteRequest(ResponseMessage response)
    {
        // Check if it's a streaming request first
        if (_streamingRequests.TryGetValue(response.RequestId, out var channel))
        {
            var streamingResponse = new StreamingResponse(response, null, !response.HasMoreBody);
            channel.Writer.TryWrite(streamingResponse);

            if (!response.HasMoreBody)
            {
                channel.Writer.Complete();
                _streamingRequests.TryRemove(response.RequestId, out _);
            }
            return;
        }

        // Regular non-streaming request
        if (_pendingRequests.TryRemove(response.RequestId, out var tcs))
        {
            tcs.TrySetResult(response);
        }
        else
        {
            _logger.LogWarning("Received response for unknown request {RequestId}", response.RequestId);
        }
    }

    public void HandleResponseBodyChunk(ResponseBodyChunkMessage chunk)
    {
        if (_streamingRequests.TryGetValue(chunk.RequestId, out var channel))
        {
            var streamingResponse = new StreamingResponse(null, chunk, chunk.IsFinal);
            channel.Writer.TryWrite(streamingResponse);

            if (chunk.IsFinal)
            {
                channel.Writer.Complete();
                _streamingRequests.TryRemove(chunk.RequestId, out _);
            }
        }
        else
        {
            _logger.LogWarning("Received body chunk for unknown request {RequestId}", chunk.RequestId);
        }
    }

    public async IAsyncEnumerable<StreamingResponse> SendStreamingRequestAsync(
        RequestMessage request,
        TimeSpan timeout,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateBounded<StreamingResponse>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        _streamingRequests[request.RequestId] = channel;

        try
        {
            await SendAsync(request, ct);

            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            await foreach (var response in channel.Reader.ReadAllAsync(linkedCts.Token))
            {
                yield return response;

                if (response.IsComplete)
                    break;
            }
        }
        finally
        {
            _streamingRequests.TryRemove(request.RequestId, out _);
        }
    }

    public async Task<WebSocketOpenResultMessage?> OpenWebSocketAsync(
        WebSocketOpenMessage request,
        TimeSpan timeout,
        CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<WebSocketOpenResultMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var sessionChannel = Channel.CreateBounded<TunnelMessage>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        _pendingWebSocketOpens[request.SessionId] = tcs;
        _webSocketSessions[request.SessionId] = sessionChannel;

        try
        {
            await SendAsync(request, ct);

            using var timeoutCts = new CancellationTokenSource(timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            try
            {
                var openResult = await tcs.Task.WaitAsync(linkedCts.Token);
                if (!openResult.Accepted)
                {
                    sessionChannel.Writer.TryComplete();
                    _webSocketSessions.TryRemove(request.SessionId, out _);
                }

                return openResult;
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                _logger.LogWarning("WebSocket open {SessionId} timed out after {Timeout}ms", request.SessionId, timeout.TotalMilliseconds);
                sessionChannel.Writer.TryComplete();
                _webSocketSessions.TryRemove(request.SessionId, out _);
                return null;
            }
        }
        finally
        {
            _pendingWebSocketOpens.TryRemove(request.SessionId, out _);
        }
    }

    public async Task SendWebSocketFrameAsync(WebSocketFrameMessage frame, CancellationToken ct)
    {
        await SendAsync(frame, ct);
    }

    public async Task SendWebSocketCloseAsync(WebSocketCloseMessage close, CancellationToken ct)
    {
        await SendAsync(close, ct);
    }

    public async IAsyncEnumerable<TunnelMessage> ReceiveWebSocketMessagesAsync(
        string sessionId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (!_webSocketSessions.TryGetValue(sessionId, out var channel))
        {
            yield break;
        }

        await foreach (var message in channel.Reader.ReadAllAsync(ct))
        {
            yield return message;

            if (message is WebSocketCloseMessage)
                break;
        }
    }

    public void CompleteWebSocketOpen(WebSocketOpenResultMessage result)
    {
        if (_pendingWebSocketOpens.TryRemove(result.SessionId, out var tcs))
        {
            tcs.TrySetResult(result);
            return;
        }

        _logger.LogWarning("Received WebSocket open result for unknown session {SessionId}", result.SessionId);
    }

    public void HandleWebSocketFrame(WebSocketFrameMessage frame)
    {
        if (_webSocketSessions.TryGetValue(frame.SessionId, out var channel))
        {
            channel.Writer.TryWrite(frame);
            return;
        }

        _logger.LogWarning("Received WebSocket frame for unknown session {SessionId}", frame.SessionId);
    }

    public void HandleWebSocketClose(WebSocketCloseMessage close)
    {
        if (_webSocketSessions.TryRemove(close.SessionId, out var channel))
        {
            channel.Writer.TryWrite(close);
            channel.Writer.TryComplete();
            return;
        }

        _logger.LogWarning("Received WebSocket close for unknown session {SessionId}", close.SessionId);
    }

    private async Task ReceiveLoopAsync(Func<TunnelMessage, Task> onMessageReceived, CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        var messageBuffer = new MemoryStream();

        try
        {
            while (!ct.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("WebSocket close received for connection {ConnectionId}", ConnectionId);
                    break;
                }

                messageBuffer.Write(buffer, 0, result.Count);

                if (result.EndOfMessage)
                {
                    var data = messageBuffer.ToArray();
                    messageBuffer.SetLength(0);

                    _logger.LogDebug("Received complete message ({Bytes} bytes) on connection {ConnectionId}",
                        data.Length, ConnectionId);

                    try
                    {
                        var message = TunnelSerializer.Deserialize(data);
                        _logger.LogDebug("Deserialized message type {MessageType} on connection {ConnectionId}",
                            message.GetType().Name, ConnectionId);
                        await onMessageReceived(message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to deserialize message ({Bytes} bytes) on connection {ConnectionId}",
                            data.Length, ConnectionId);
                    }
                }
            }
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "WebSocket error on connection {ConnectionId}", ConnectionId);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }

    private async Task SendLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var message in _outboundChannel.Reader.ReadAllAsync(ct))
            {
                if (_webSocket.State != WebSocketState.Open)
                    break;

                var data = TunnelSerializer.Serialize(message);
                await _webSocket.SendAsync(data, WebSocketMessageType.Binary, true, ct);
            }
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "WebSocket send error on connection {ConnectionId}", ConnectionId);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _outboundChannel.Writer.Complete();

        foreach (var pending in _pendingRequests.Values)
        {
            pending.TrySetCanceled();
        }
        _pendingRequests.Clear();

        foreach (var channel in _streamingRequests.Values)
        {
            channel.Writer.TryComplete();
        }
        _streamingRequests.Clear();

        foreach (var pendingOpen in _pendingWebSocketOpens.Values)
        {
            pendingOpen.TrySetCanceled();
        }
        _pendingWebSocketOpens.Clear();

        foreach (var session in _webSocketSessions.Values)
        {
            session.Writer.TryComplete();
        }
        _webSocketSessions.Clear();

        if (_receiveTask != null)
            await _receiveTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (_sendTask != null)
            await _sendTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        if (_webSocket.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Shutdown", CancellationToken.None);
            }
            catch { /* Ignore close errors */ }
        }

        _webSocket.Dispose();
        _cts.Dispose();
    }
}
