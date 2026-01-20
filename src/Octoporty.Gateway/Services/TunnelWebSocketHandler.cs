using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Octoporty.Shared.Contracts;
using Octoporty.Shared.Options;

namespace Octoporty.Gateway.Services;

public class TunnelWebSocketHandler
{
    private readonly TunnelConnectionManager _connectionManager;
    private readonly ICaddyAdminClient _caddyClient;
    private readonly ILogger<TunnelWebSocketHandler> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly GatewayOptions _options;

    private const string GatewayVersion = "1.0.0";

    public TunnelWebSocketHandler(
        TunnelConnectionManager connectionManager,
        ICaddyAdminClient caddyClient,
        IOptions<GatewayOptions> options,
        ILogger<TunnelWebSocketHandler> logger,
        ILoggerFactory loggerFactory)
    {
        _connectionManager = connectionManager;
        _caddyClient = caddyClient;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _options = options.Value;
    }

    public async Task HandleConnectionAsync(WebSocket webSocket, CancellationToken ct)
    {
        var connection = new TunnelConnection(webSocket, _loggerFactory.CreateLogger<TunnelConnection>());
        _logger.LogInformation("New tunnel connection {ConnectionId}", connection.ConnectionId);

        try
        {
            // Wait for authentication
            var authenticated = await WaitForAuthenticationAsync(connection, ct);
            if (!authenticated)
            {
                _logger.LogWarning("Authentication failed for connection {ConnectionId}", connection.ConnectionId);
                return;
            }

            // Set as active connection (replaces any existing)
            _connectionManager.SetActiveConnection(connection);

            // Start message processing
            connection.StartProcessing(msg => HandleMessageAsync(connection, msg, ct));

            // Wait for connection to close
            while (webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                await Task.Delay(1000, ct);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Connection {ConnectionId} cancelled", connection.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling connection {ConnectionId}", connection.ConnectionId);
        }
        finally
        {
            await _connectionManager.RemoveConnectionAsync(connection.ConnectionId);
            _logger.LogInformation("Connection {ConnectionId} closed", connection.ConnectionId);
        }
    }

    private async Task<bool> WaitForAuthenticationAsync(TunnelConnection connection, CancellationToken ct)
    {
        var buffer = new byte[4096];
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            var result = await connection.ReceiveRawAsync(buffer, linkedCts.Token);
            if (result.MessageType == WebSocketMessageType.Close)
                return false;

            var message = TunnelSerializer.Deserialize(buffer.AsMemory(0, result.Count));

            if (message is AuthMessage authMessage)
            {
                var isValid = ValidateApiKey(authMessage.ApiKey);

                var response = new AuthResultMessage
                {
                    Success = isValid,
                    Error = isValid ? null : "Invalid API key",
                    GatewayVersion = GatewayVersion
                };

                await connection.SendRawAsync(TunnelSerializer.Serialize(response), linkedCts.Token);

                if (isValid)
                {
                    connection.SetAgentVersion(authMessage.AgentVersion);
                    _logger.LogInformation("Connection {ConnectionId} authenticated (Agent v{Version})",
                        connection.ConnectionId, authMessage.AgentVersion);
                }

                return isValid;
            }

            _logger.LogWarning("Expected AuthMessage but got {MessageType}", message.GetType().Name);
            return false;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Authentication timeout for connection {ConnectionId}", connection.ConnectionId);
            return false;
        }
    }

    private bool ValidateApiKey(string providedKey)
    {
        if (string.IsNullOrEmpty(_options.ApiKey))
        {
            _logger.LogWarning("No API key configured - rejecting all connections");
            return false;
        }

        // Constant-time comparison to prevent timing attacks
        var expected = Encoding.UTF8.GetBytes(_options.ApiKey);
        var provided = Encoding.UTF8.GetBytes(providedKey);

        return CryptographicOperations.FixedTimeEquals(expected, provided);
    }

    private async Task HandleMessageAsync(TunnelConnection connection, TunnelMessage message, CancellationToken ct)
    {
        switch (message)
        {
            case ConfigSyncMessage configSync:
                await HandleConfigSyncAsync(connection, configSync, ct);
                break;

            case HeartbeatMessage heartbeat:
                await HandleHeartbeatAsync(connection, heartbeat, ct);
                break;

            case ResponseMessage response:
                connection.CompleteRequest(response);
                break;

            case ResponseBodyChunkMessage chunk:
                connection.HandleResponseBodyChunk(chunk);
                break;

            case DisconnectMessage disconnect:
                _logger.LogInformation("Agent requested disconnect: {Reason}", disconnect.Reason);
                break;

            default:
                _logger.LogWarning("Unhandled message type: {MessageType}", message.GetType().Name);
                break;
        }
    }

    private async Task HandleConfigSyncAsync(TunnelConnection connection, ConfigSyncMessage configSync, CancellationToken ct)
    {
        _logger.LogInformation("Received config sync with {Count} mappings (hash: {Hash})",
            configSync.Mappings.Length, configSync.ConfigHash[..8]);

        try
        {
            // Update mappings in connection
            connection.UpdateMappings(configSync.Mappings);

            // Configure Caddy routes
            foreach (var mapping in configSync.Mappings.Where(m => m.IsEnabled))
            {
                await _caddyClient.EnsureRouteExistsAsync(mapping, ct);
            }

            // Remove routes for disabled mappings
            var enabledIds = configSync.Mappings.Where(m => m.IsEnabled).Select(m => m.Id).ToHashSet();
            await _caddyClient.RemoveStaleRoutesAsync(enabledIds, ct);

            await connection.SendAsync(new ConfigAckMessage
            {
                Success = true,
                ConfigHash = configSync.ConfigHash
            }, ct);

            _logger.LogInformation("Config sync completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Config sync failed");
            await connection.SendAsync(new ConfigAckMessage
            {
                Success = false,
                Error = ex.Message,
                ConfigHash = configSync.ConfigHash
            }, ct);
        }
    }

    private async Task HandleHeartbeatAsync(TunnelConnection connection, HeartbeatMessage heartbeat, CancellationToken ct)
    {
        await connection.SendAsync(new HeartbeatAckMessage
        {
            Timestamp = heartbeat.Timestamp,
            ServerTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        }, ct);
    }
}

public static class TunnelConnectionExtensions
{
    public static async Task<WebSocketReceiveResult> ReceiveRawAsync(this TunnelConnection connection, byte[] buffer, CancellationToken ct)
    {
        // Access underlying WebSocket for initial auth handshake
        var webSocketField = typeof(TunnelConnection).GetField("_webSocket", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var webSocket = (WebSocket)webSocketField!.GetValue(connection)!;
        return await webSocket.ReceiveAsync(buffer, ct);
    }

    public static async Task SendRawAsync(this TunnelConnection connection, byte[] data, CancellationToken ct)
    {
        var webSocketField = typeof(TunnelConnection).GetField("_webSocket", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var webSocket = (WebSocket)webSocketField!.GetValue(connection)!;
        await webSocket.SendAsync(data, WebSocketMessageType.Binary, true, ct);
    }
}
