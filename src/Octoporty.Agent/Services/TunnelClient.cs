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
    private readonly Channel<TunnelMessage> _outboundChannel;

    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _connectionCts;
    private Task? _receiveTask;
    private Task? _sendTask;
    private Task? _heartbeatTask;

    private const string AgentVersion = "1.0.0";
    private const int HeartbeatIntervalSeconds = 30;

    public TunnelClientState State { get; private set; } = TunnelClientState.Disconnected;
    public DateTime? LastConnectedAt { get; private set; }
    public string? LastError { get; private set; }
    public string? GatewayVersion { get; private set; }

    public event Action<TunnelClientState>? StateChanged;

    public TunnelClient(
        IServiceProvider serviceProvider,
        IOptions<AgentOptions> options,
        ILogger<TunnelClient> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
        _outboundChannel = Channel.CreateBounded<TunnelMessage>(new BoundedChannelOptions(1000)
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

        _webSocket = new ClientWebSocket();
        _connectionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

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
                ExternalPort = m.ExternalPort,
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

        await SendMessageAsync(syncMessage, ct);

        // Wait for ack
        var buffer = new byte[4096];
        var result = await _webSocket!.ReceiveAsync(buffer, ct);

        if (result.MessageType == WebSocketMessageType.Close)
        {
            throw new InvalidOperationException("Gateway closed connection during config sync");
        }

        var response = TunnelSerializer.Deserialize(buffer.AsMemory(0, result.Count));

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
        }
        else
        {
            throw new InvalidOperationException($"Unexpected response during config sync: {response.GetType().Name}");
        }
    }

    public async Task ResyncConfigurationAsync(CancellationToken ct)
    {
        if (State != TunnelClientState.Connected)
        {
            _logger.LogWarning("Cannot resync configuration - not connected");
            return;
        }

        await SyncConfigurationAsync(ct);
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
                _logger.LogDebug("Heartbeat ack received (latency: {Latency}ms)", latency);
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
}
