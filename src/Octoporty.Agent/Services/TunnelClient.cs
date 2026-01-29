// TunnelClient.cs
// BackgroundService that maintains a persistent WebSocket connection to the Gateway.
// State machine: Disconnected → Connecting → Authenticating → Syncing → Connected.
// Implements automatic reconnection with exponential backoff via ReconnectionPolicy.
// Handles heartbeats (30s interval) and forwards requests to internal services.

using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Octoporty.Agent.Data;
using Octoporty.Shared.Contracts;
using Octoporty.Shared.Options;

namespace Octoporty.Agent.Services;

public class TunnelClient : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TunnelClient> _logger;
    private readonly AgentOptions _options;
    private readonly ReconnectionPolicy _reconnectionPolicy = new();
    private Channel<TunnelMessage> _outboundChannel;

    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _connectionCts;
    private Task? _receiveTask;
    private Task? _sendTask;
    private Task? _heartbeatTask;
    private TaskCompletionSource<ConfigAckMessage>? _pendingConfigAck;
    private TaskCompletionSource<UpdateResponseMessage>? _pendingUpdateResponse;
    private TaskCompletionSource<GetLogsResponseMessage>? _pendingLogsResponse;

    private const string AgentVersion = "1.0.0";
    private const int HeartbeatIntervalSeconds = 30;

    public TunnelClientState State { get; private set; } = TunnelClientState.Disconnected;
    public DateTime? LastConnectedAt { get; private set; }
    public string? LastError { get; private set; }
    public string? GatewayVersion { get; private set; }

    /// <summary>
    /// Gateway uptime in seconds, reported by the Gateway in heartbeat acks.
    /// Updated every heartbeat cycle (30 seconds).
    /// </summary>
    public long? GatewayUptimeSeconds { get; private set; }

    /// <summary>
    /// Indicates whether the Agent version is newer than the Gateway version.
    /// When true, the user can trigger a Gateway update from the UI.
    /// </summary>
    public bool GatewayUpdateAvailable { get; private set; }

    public event Action<TunnelClientState>? StateChanged;

    public TunnelClient(
        IServiceProvider serviceProvider,
        IOptions<AgentOptions> options,
        ILogger<TunnelClient> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
        _outboundChannel = CreateOutboundChannel();
    }

    private static Channel<TunnelMessage> CreateOutboundChannel()
    {
        return Channel.CreateBounded<TunnelMessage>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Tunnel client starting, connecting to {Url}", _options.GatewayUrl);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndRunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                _logger.LogError(ex, "Tunnel connection error");
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                SetState(TunnelClientState.Reconnecting);
                var delay = _reconnectionPolicy.GetNextDelay();
                _logger.LogInformation("Reconnecting in {Delay:F1}s (attempt {Attempt})",
                    delay.TotalSeconds, _reconnectionPolicy.CurrentAttempt);
                await Task.Delay(delay, stoppingToken);
            }
        }

        _logger.LogInformation("Tunnel client stopped");
    }

    private async Task ConnectAndRunAsync(CancellationToken ct)
    {
        SetState(TunnelClientState.Connecting);

        // Recreate the outbound channel for each connection attempt.
        // The channel may have been completed by a previous CleanupConnectionAsync call.
        _outboundChannel = CreateOutboundChannel();

        _webSocket = new ClientWebSocket();
        _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // CRITICAL-02: Send API key header for pre-connection authentication
        // Gateway validates this BEFORE accepting the WebSocket upgrade
        _webSocket.Options.SetRequestHeader("X-Api-Key", _options.ApiKey);

        try
        {
            await _webSocket.ConnectAsync(new Uri(_options.GatewayUrl), _connectionCts.Token);
            _logger.LogInformation("WebSocket connected to Gateway");

            // Authenticate
            SetState(TunnelClientState.Authenticating);
            var authSuccess = await AuthenticateAsync(_connectionCts.Token);
            if (!authSuccess)
            {
                throw new InvalidOperationException("Authentication failed");
            }

            // Sync configuration
            SetState(TunnelClientState.Syncing);
            await SyncConfigurationAsync(_connectionCts.Token);

            // Connected successfully
            SetState(TunnelClientState.Connected);
            LastConnectedAt = DateTime.UtcNow;
            _reconnectionPolicy.Reset();

            // Start processing tasks
            _receiveTask = ReceiveLoopAsync(_connectionCts.Token);
            _sendTask = SendLoopAsync(_connectionCts.Token);
            _heartbeatTask = HeartbeatLoopAsync(_connectionCts.Token);

            // Wait for any task to complete (indicating disconnect)
            await Task.WhenAny(_receiveTask, _sendTask, _heartbeatTask);

            _logger.LogInformation("Tunnel connection closed");
        }
        finally
        {
            SetState(TunnelClientState.Disconnected);
            await CleanupConnectionAsync();
        }
    }

    private async Task<bool> AuthenticateAsync(CancellationToken ct)
    {
        var authMessage = new AuthMessage
        {
            ApiKey = _options.ApiKey,
            AgentVersion = AgentVersion
        };

        await SendMessageAsync(authMessage, ct);

        // Wait for auth response
        var buffer = new byte[4096];
        var result = await _webSocket!.ReceiveAsync(buffer, ct);

        if (result.MessageType == WebSocketMessageType.Close)
        {
            _logger.LogWarning("Gateway closed connection during authentication");
            return false;
        }

        var response = TunnelSerializer.Deserialize(buffer.AsMemory(0, result.Count));

        if (response is AuthResultMessage authResult)
        {
            if (authResult.Success)
            {
                GatewayVersion = authResult.GatewayVersion;
                _logger.LogInformation("Authenticated successfully (Gateway v{Version})", GatewayVersion);

                // Compare versions to determine if Gateway update is available
                // Agent can trigger update if it has a newer version than Gateway
                GatewayUpdateAvailable = CompareVersions(AgentVersion, GatewayVersion) > 0;
                if (GatewayUpdateAvailable)
                {
                    _logger.LogInformation(
                        "Gateway update available: Agent v{AgentVersion} > Gateway v{GatewayVersion}",
                        AgentVersion, GatewayVersion);
                }

                return true;
            }

            LastError = authResult.Error;
            _logger.LogWarning("Authentication failed: {Error}", authResult.Error);
            return false;
        }

        _logger.LogWarning("Unexpected response during authentication: {Type}", response.GetType().Name);
        return false;
    }

    private async Task SyncConfigurationAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OctoportyDbContext>();

        var mappings = await db.PortMappings
            .Where(m => m.IsEnabled)
            .Select(m => new PortMappingDto
            {
                Id = m.Id,
                ExternalDomain = m.ExternalDomain,
                InternalHost = m.InternalHost,
                InternalPort = m.InternalPort,
                InternalUseTls = m.InternalUseTls,
                AllowSelfSignedCerts = m.AllowSelfSignedCerts,
                IsEnabled = m.IsEnabled
            })
            .ToArrayAsync(ct);

        var configHash = ComputeConfigHash(mappings);

        var syncMessage = new ConfigSyncMessage
        {
            Mappings = mappings,
            ConfigHash = configHash
        };

        _logger.LogDebug("Sending ConfigSyncMessage with {Count} mappings, hash {Hash}",
            mappings.Length, configHash[..8]);
        await SendMessageAsync(syncMessage, ct);
        _logger.LogDebug("ConfigSyncMessage sent, waiting for ack...");

        // Wait for ack - loop to handle other messages (like HeartbeatAck) that may arrive
        var buffer = new byte[4096];
        var timeout = TimeSpan.FromSeconds(30);
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        while (!linkedCts.Token.IsCancellationRequested)
        {
            var result = await _webSocket!.ReceiveAsync(buffer, linkedCts.Token);

            _logger.LogDebug("Received response during config sync: MessageType={Type}, Count={Count}",
                result.MessageType, result.Count);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                _logger.LogWarning("Gateway closed connection during config sync. CloseStatus={Status}, CloseDescription={Description}",
                    result.CloseStatus, result.CloseStatusDescription);
                throw new InvalidOperationException("Gateway closed connection during config sync");
            }

            var response = TunnelSerializer.Deserialize(buffer.AsMemory(0, result.Count));
            _logger.LogDebug("Config sync response type: {Type}", response.GetType().Name);

            if (response is ConfigAckMessage ackMessage)
            {
                if (ackMessage.Success)
                {
                    _logger.LogInformation("Configuration synced successfully ({Count} mappings)", mappings.Length);
                }
                else
                {
                    throw new InvalidOperationException($"Config sync failed: {ackMessage.Error}");
                }
                return; // Got our ack, done
            }

            // Handle other message types that may arrive while waiting
            if (response is HeartbeatAckMessage)
            {
                _logger.LogDebug("Received HeartbeatAck while waiting for ConfigAck, continuing to wait...");
                continue;
            }

            _logger.LogWarning("Unexpected message during config sync: {Type}, continuing to wait...", response.GetType().Name);
        }

        throw new TimeoutException("Timeout waiting for ConfigAckMessage");
    }

    public async Task ResyncConfigurationAsync(CancellationToken ct)
    {
        if (State != TunnelClientState.Connected)
        {
            _logger.LogWarning("Cannot resync configuration - not connected");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OctoportyDbContext>();

        var mappings = await db.PortMappings
            .Where(m => m.IsEnabled)
            .Select(m => new PortMappingDto
            {
                Id = m.Id,
                ExternalDomain = m.ExternalDomain,
                InternalHost = m.InternalHost,
                InternalPort = m.InternalPort,
                InternalUseTls = m.InternalUseTls,
                AllowSelfSignedCerts = m.AllowSelfSignedCerts,
                IsEnabled = m.IsEnabled
            })
            .ToArrayAsync(ct);

        var configHash = ComputeConfigHash(mappings);

        var syncMessage = new ConfigSyncMessage
        {
            Mappings = mappings,
            ConfigHash = configHash
        };

        _logger.LogDebug("Resyncing configuration with {Count} mappings, hash {Hash}",
            mappings.Length, configHash[..8]);

        // Set up the pending ack before sending
        _pendingConfigAck = new TaskCompletionSource<ConfigAckMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Send through the outbound channel (will be picked up by SendLoopAsync)
        await _outboundChannel.Writer.WriteAsync(syncMessage, ct);

        // Wait for the ack with timeout
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            var ackMessage = await _pendingConfigAck.Task.WaitAsync(linkedCts.Token);

            if (ackMessage.Success)
            {
                _logger.LogInformation("Configuration resynced successfully ({Count} mappings)", mappings.Length);
            }
            else
            {
                throw new InvalidOperationException($"Config resync failed: {ackMessage.Error}");
            }
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException("Timeout waiting for ConfigAckMessage during resync");
        }
        finally
        {
            _pendingConfigAck = null;
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        var messageBuffer = new MemoryStream();

        try
        {
            while (!ct.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
            {
                var result = await _webSocket.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("Received close from Gateway");
                    break;
                }

                messageBuffer.Write(buffer, 0, result.Count);

                if (result.EndOfMessage)
                {
                    var data = messageBuffer.ToArray();
                    messageBuffer.SetLength(0);

                    try
                    {
                        var message = TunnelSerializer.Deserialize(data);
                        await HandleMessageAsync(message, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to handle message");
                    }
                }
            }
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "WebSocket receive error");
        }
    }

    private async Task SendLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var message in _outboundChannel.Reader.ReadAllAsync(ct))
            {
                if (_webSocket?.State != WebSocketState.Open)
                    break;

                var data = TunnelSerializer.Serialize(message);
                await _webSocket.SendAsync(data, WebSocketMessageType.Binary, true, ct);
            }
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "WebSocket send error");
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
        {
            await Task.Delay(TimeSpan.FromSeconds(HeartbeatIntervalSeconds), ct);

            if (_webSocket?.State == WebSocketState.Open)
            {
                await _outboundChannel.Writer.WriteAsync(new HeartbeatMessage
                {
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                }, ct);
            }
        }
    }

    private async Task HandleMessageAsync(TunnelMessage message, CancellationToken ct)
    {
        switch (message)
        {
            case RequestMessage request:
                await HandleRequestAsync(request, ct);
                break;

            case HeartbeatAckMessage heartbeatAck:
                var latency = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - heartbeatAck.Timestamp;
                GatewayUptimeSeconds = heartbeatAck.GatewayUptimeSeconds;
                _logger.LogDebug("Heartbeat ack received (latency: {Latency}ms, gateway uptime: {Uptime}s)",
                    latency, GatewayUptimeSeconds);
                break;

            case ConfigAckMessage configAck:
                _logger.LogDebug("ConfigAck received: Success={Success}", configAck.Success);
                _pendingConfigAck?.TrySetResult(configAck);
                break;

            case UpdateResponseMessage updateResponse:
                _logger.LogInformation(
                    "Update response received: Accepted={Accepted}, Status={Status}",
                    updateResponse.Accepted, updateResponse.Status);
                _pendingUpdateResponse?.TrySetResult(updateResponse);
                break;

            case GatewayLogMessage gatewayLog:
                await HandleGatewayLogAsync(gatewayLog);
                break;

            case GetLogsResponseMessage logsResponse:
                _logger.LogDebug("GetLogsResponse received: {Count} logs", logsResponse.Logs.Length);
                _pendingLogsResponse?.TrySetResult(logsResponse);
                break;

            case DisconnectMessage disconnect:
                _logger.LogWarning("Gateway requested disconnect: {Reason}", disconnect.Reason);
                await _connectionCts!.CancelAsync();
                break;

            default:
                _logger.LogWarning("Unhandled message type: {Type}", message.GetType().Name);
                break;
        }
    }

    private async Task HandleRequestAsync(RequestMessage request, CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var forwarder = scope.ServiceProvider.GetRequiredService<RequestForwarder>();

        try
        {
            // Use streaming for potentially large responses
            await foreach (var message in forwarder.ForwardStreamingAsync(request, ct))
            {
                await _outboundChannel.Writer.WriteAsync(message, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to forward request {RequestId}", request.RequestId);

            await _outboundChannel.Writer.WriteAsync(new ResponseMessage
            {
                RequestId = request.RequestId,
                StatusCode = 502,
                Headers = new Dictionary<string, string[]>
                {
                    ["Content-Type"] = ["text/plain"]
                },
                Body = Encoding.UTF8.GetBytes($"Bad Gateway: {ex.Message}")
            }, ct);
        }
    }

    private async Task HandleGatewayLogAsync(GatewayLogMessage log)
    {
        using var scope = _serviceProvider.CreateScope();
        var notifier = scope.ServiceProvider.GetRequiredService<StatusNotifier>();

        await notifier.NotifyGatewayLogAsync(
            log.Timestamp,
            log.Level.ToString(),
            log.Message);
    }

    private async Task SendMessageAsync(TunnelMessage message, CancellationToken ct)
    {
        var data = TunnelSerializer.Serialize(message);
        await _webSocket!.SendAsync(data, WebSocketMessageType.Binary, true, ct);
    }

    private async Task CleanupConnectionAsync()
    {
        _connectionCts?.Cancel();
        _outboundChannel.Writer.TryComplete();

        if (_receiveTask != null)
            await _receiveTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (_sendTask != null)
            await _sendTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (_heartbeatTask != null)
            await _heartbeatTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        if (_webSocket?.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Shutdown", CancellationToken.None);
            }
            catch { /* Ignore close errors */ }
        }

        _webSocket?.Dispose();
        _connectionCts?.Dispose();
    }

    private void SetState(TunnelClientState newState)
    {
        if (State != newState)
        {
            State = newState;
            _logger.LogDebug("State changed to {State}", newState);
            StateChanged?.Invoke(newState);
        }
    }

    private static string ComputeConfigHash(PortMappingDto[] mappings)
    {
        var json = JsonSerializer.Serialize(mappings.OrderBy(m => m.Id));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash)[..16];
    }

    /// <summary>
    /// Requests the Gateway to update itself to the Agent's version.
    /// The Gateway will write a signal file for the host watcher to process.
    /// </summary>
    /// <returns>The update response from the Gateway.</returns>
    /// <exception cref="InvalidOperationException">If not connected or no update is available.</exception>
    public async Task<UpdateResponseMessage> RequestGatewayUpdateAsync(CancellationToken ct = default)
    {
        if (State != TunnelClientState.Connected)
        {
            throw new InvalidOperationException("Cannot request update - not connected to Gateway");
        }

        if (!GatewayUpdateAvailable)
        {
            throw new InvalidOperationException("No Gateway update available - versions are the same or Gateway is newer");
        }

        _logger.LogInformation("Requesting Gateway update to version {Version}", AgentVersion);

        // Set up the pending response before sending
        _pendingUpdateResponse = new TaskCompletionSource<UpdateResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var updateRequest = new UpdateRequestMessage
        {
            TargetVersion = AgentVersion,
            RequestedBy = $"Agent@{Environment.MachineName}"
        };

        // Send through the outbound channel
        await _outboundChannel.Writer.WriteAsync(updateRequest, ct);

        // Wait for response with timeout
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            var response = await _pendingUpdateResponse.Task.WaitAsync(linkedCts.Token);

            if (response.Accepted && response.Status == UpdateStatus.Queued)
            {
                // Update is queued - Gateway will restart soon
                // The GatewayUpdateAvailable flag will be re-evaluated on reconnect
                _logger.LogInformation("Gateway update queued successfully. Gateway will restart soon.");
            }

            return response;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException("Timeout waiting for update response from Gateway");
        }
        finally
        {
            _pendingUpdateResponse = null;
        }
    }

    /// <summary>
    /// Requests historical logs from the Gateway.
    /// </summary>
    /// <param name="beforeId">Return logs with ID less than this value. Use 0 for latest.</param>
    /// <param name="count">Maximum number of logs to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The logs response from the Gateway.</returns>
    public async Task<GetLogsResponseMessage> GetGatewayLogsAsync(long beforeId, int count, CancellationToken ct = default)
    {
        if (State != TunnelClientState.Connected)
        {
            throw new InvalidOperationException("Cannot request logs - not connected to Gateway");
        }

        _logger.LogDebug("Requesting Gateway logs: beforeId={BeforeId}, count={Count}", beforeId, count);

        // Set up the pending response before sending
        _pendingLogsResponse = new TaskCompletionSource<GetLogsResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var request = new GetLogsRequestMessage
        {
            RequestId = Guid.NewGuid().ToString("N")[..8],
            BeforeId = beforeId,
            Count = count
        };

        // Send through the outbound channel
        await _outboundChannel.Writer.WriteAsync(request, ct);

        // Wait for response with timeout
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            return await _pendingLogsResponse.Task.WaitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new TimeoutException("Timeout waiting for logs response from Gateway");
        }
        finally
        {
            _pendingLogsResponse = null;
        }
    }

    /// <summary>
    /// Compares two semantic version strings.
    /// Returns positive if version1 > version2, negative if version1 < version2, zero if equal.
    /// </summary>
    private static int CompareVersions(string? version1, string? version2)
    {
        if (string.IsNullOrEmpty(version1) && string.IsNullOrEmpty(version2))
            return 0;
        if (string.IsNullOrEmpty(version1))
            return -1;
        if (string.IsNullOrEmpty(version2))
            return 1;

        // Parse semantic version (major.minor.patch)
        var v1Parts = version1.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();
        var v2Parts = version2.Split('.').Select(p => int.TryParse(p, out var n) ? n : 0).ToArray();

        // Ensure both have at least 3 parts
        Array.Resize(ref v1Parts, 3);
        Array.Resize(ref v2Parts, 3);

        // Compare major, minor, patch in order
        for (int i = 0; i < 3; i++)
        {
            if (v1Parts[i] > v2Parts[i])
                return 1;
            if (v1Parts[i] < v2Parts[i])
                return -1;
        }

        return 0;
    }

    /// <summary>
    /// Gets the current Agent version.
    /// </summary>
    public static string Version => AgentVersion;
}
