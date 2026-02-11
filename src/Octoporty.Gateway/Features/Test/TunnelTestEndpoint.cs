// TunnelTestEndpoint.cs
// Test endpoint for verifying tunnel connectivity without Caddy/ACME.
// Sends a test request through the tunnel and returns the Agent's response.
// Used for deployment verification and debugging.

using System.Text;
using System.Net.WebSockets;
using Octoporty.Gateway.Services;
using Octoporty.Shared.Contracts;

namespace Octoporty.Gateway.Features.Test;

public static class TunnelTestEndpoints
{
    public static void MapTunnelTestEndpoints(this WebApplication app)
    {
        // GET /test/tunnel - Check if tunnel is connected
        app.MapGet("/test/tunnel", (ITunnelConnectionManager connectionManager) =>
        {
            var connection = connectionManager.ActiveConnection;
            if (connection == null)
            {
                return Results.Json(new
                {
                    connected = false,
                    message = "No Agent connected",
                    timestamp = DateTime.UtcNow
                }, statusCode: 503);
            }

            return Results.Ok(new
            {
                connected = true,
                connectionId = connection.ConnectionId,
                agentVersion = connection.AgentVersion,
                agentSupportsWebSocketProxy = connection.AgentSupportsWebSocketProxy,
                mappingCount = connection.Mappings.Count,
                mappings = connection.Mappings.Values.Select(m => new
                {
                    id = m.Id,
                    externalDomain = m.ExternalDomain,
                    internalTarget = $"{m.InternalHost}:{m.InternalPort}",
                    enabled = m.IsEnabled
                }),
                timestamp = DateTime.UtcNow
            });
        });

        // POST /test/tunnel/echo - Send test request through tunnel to Agent's echo endpoint
        app.MapPost("/test/tunnel/echo", async (
            ITunnelConnectionManager connectionManager,
            HttpRequest httpRequest,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var connection = connectionManager.ActiveConnection;
            if (connection == null)
            {
                return Results.Json(new
                {
                    success = false,
                    error = "No Agent connected"
                }, statusCode: 503);
            }

            // Get first enabled mapping (for test purposes)
            var mapping = connection.Mappings.Values.FirstOrDefault(m => m.IsEnabled);
            if (mapping == null)
            {
                return Results.Json(new
                {
                    success = false,
                    error = "No enabled mappings configured"
                }, statusCode: 400);
            }

            // Read request body if present
            byte[]? body = null;
            if (httpRequest.ContentLength > 0)
            {
                using var ms = new MemoryStream();
                await httpRequest.Body.CopyToAsync(ms, ct);
                body = ms.ToArray();
            }

            // Build test request to Agent's echo endpoint
            var testRequest = new RequestMessage
            {
                RequestId = Guid.NewGuid().ToString("N"),
                MappingId = mapping.Id,
                Method = "POST",
                Path = "/api/v1/test/echo",
                Headers = new Dictionary<string, string[]>
                {
                    ["Content-Type"] = ["application/json"],
                    ["X-Test-Request"] = ["true"],
                    ["X-Forwarded-By"] = ["Octoporty.Gateway.Test"]
                },
                Body = body ?? Encoding.UTF8.GetBytes("{\"test\":true,\"timestamp\":\"" + DateTime.UtcNow.ToString("O") + "\"}")
            };

            logger.LogInformation("Sending test request {RequestId} through tunnel to mapping {MappingId}",
                testRequest.RequestId, mapping.Id);

            var response = await connectionManager.ForwardRequestAsync(
                testRequest,
                TimeSpan.FromSeconds(30),
                ct);

            if (response == null)
            {
                return Results.Json(new
                {
                    success = false,
                    error = "No response from Agent (tunnel may have disconnected)"
                }, statusCode: 504);
            }

            return Results.Json(new
            {
                success = true,
                statusCode = response.StatusCode,
                headers = response.Headers,
                body = response.Body != null ? Encoding.UTF8.GetString(response.Body) : null,
                roundTripMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        });

        // POST /test/tunnel/forward - Forward arbitrary request through tunnel
        app.MapPost("/test/tunnel/forward/{mappingId}", async (
            Guid mappingId,
            ITunnelConnectionManager connectionManager,
            HttpRequest httpRequest,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            var connection = connectionManager.ActiveConnection;
            if (connection == null)
            {
                return Results.Json(new { error = "No Agent connected" }, statusCode: 503);
            }

            if (!connection.Mappings.TryGetValue(mappingId, out var mapping))
            {
                return Results.Json(new { error = "Mapping not found" }, statusCode: 404);
            }

            // Read body
            byte[]? body = null;
            if (httpRequest.ContentLength > 0)
            {
                using var ms = new MemoryStream();
                await httpRequest.Body.CopyToAsync(ms, ct);
                body = ms.ToArray();
            }

            // Get path from query
            var targetPath = httpRequest.Query["path"].FirstOrDefault() ?? "/";

            var request = new RequestMessage
            {
                RequestId = Guid.NewGuid().ToString("N"),
                MappingId = mappingId,
                Method = httpRequest.Query["method"].FirstOrDefault() ?? "GET",
                Path = targetPath,
                Headers = new Dictionary<string, string[]>
                {
                    ["Host"] = [mapping.ExternalDomain],
                    ["X-Forwarded-By"] = ["Octoporty.Gateway.Test"]
                },
                Body = body
            };

            logger.LogInformation("Test forward: {Method} {Path} via mapping {MappingId}",
                request.Method, request.Path, mappingId);

            var response = await connectionManager.ForwardRequestAsync(request, TimeSpan.FromSeconds(30), ct);

            if (response == null)
            {
                return Results.Json(new { error = "No response from Agent" }, statusCode: 504);
            }

            return Results.Json(new
            {
                statusCode = response.StatusCode,
                headers = response.Headers,
                bodyLength = response.Body?.Length ?? 0,
                body = response.Body != null ? Encoding.UTF8.GetString(response.Body) : null
            });
        });

        // GET /test/tunnel/ws-echo - WebSocket round-trip through Agent websocket test endpoint
        app.MapGet("/test/tunnel/ws-echo", async (
            HttpContext context,
            ITunnelConnectionManager connectionManager,
            CancellationToken ct) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("WebSocket connection required", ct);
                return;
            }

            var connection = connectionManager.ActiveConnection;
            if (connection == null)
            {
                context.Response.StatusCode = 503;
                await context.Response.WriteAsync("No Agent connected", ct);
                return;
            }

            if (!connection.AgentSupportsWebSocketProxy)
            {
                context.Response.StatusCode = 501;
                await context.Response.WriteAsync("Connected Agent does not support websocket proxy forwarding", ct);
                return;
            }

            var mapping = connection.Mappings.Values.FirstOrDefault(m => m.IsEnabled);
            if (mapping == null)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("No enabled mappings configured", ct);
                return;
            }

            var sessionId = Guid.NewGuid().ToString("N");
            var openResult = await connection.OpenWebSocketAsync(new WebSocketOpenMessage
            {
                SessionId = sessionId,
                RequestId = Guid.NewGuid().ToString("N"),
                MappingId = mapping.Id,
                Path = "/api/v1/test/ws-echo",
                Headers = new Dictionary<string, string[]>(),
                Subprotocols = []
            }, TimeSpan.FromSeconds(30), ct);

            if (openResult == null || !openResult.Accepted)
            {
                context.Response.StatusCode = openResult?.StatusCode ?? 502;
                await context.Response.WriteAsync(openResult?.Reason ?? "Failed to open upstream websocket", ct);
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync(openResult.SelectedSubprotocol);
            using var relayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            var inTask = RelayClientToTunnelAsync(connection, socket, sessionId, relayCts.Token);
            var outTask = RelayTunnelToClientAsync(connection, socket, sessionId, relayCts.Token);

            await Task.WhenAny(inTask, outTask);
            await relayCts.CancelAsync();
            await inTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            await outTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        });
    }

    private static async Task RelayClientToTunnelAsync(
        ITunnelConnection connection,
        WebSocket clientSocket,
        string sessionId,
        CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];

        while (!ct.IsCancellationRequested && clientSocket.State == WebSocketState.Open)
        {
            var result = await clientSocket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await connection.SendWebSocketCloseAsync(new WebSocketCloseMessage
                {
                    SessionId = sessionId,
                    CloseStatus = (int?)result.CloseStatus,
                    Description = result.CloseStatusDescription,
                    Initiator = "GatewayTestClient"
                }, ct);
                return;
            }

            await connection.SendWebSocketFrameAsync(new WebSocketFrameMessage
            {
                SessionId = sessionId,
                FrameType = result.MessageType == WebSocketMessageType.Text ? WebSocketFrameType.Text : WebSocketFrameType.Binary,
                IsFinal = result.EndOfMessage,
                Payload = buffer.AsSpan(0, result.Count).ToArray()
            }, ct);
        }
    }

    private static async Task RelayTunnelToClientAsync(
        ITunnelConnection connection,
        WebSocket clientSocket,
        string sessionId,
        CancellationToken ct)
    {
        await foreach (var message in connection.ReceiveWebSocketMessagesAsync(sessionId, ct))
        {
            if (message is WebSocketFrameMessage frame)
            {
                await clientSocket.SendAsync(
                    frame.Payload,
                    frame.FrameType == WebSocketFrameType.Text ? WebSocketMessageType.Text : WebSocketMessageType.Binary,
                    frame.IsFinal,
                    ct);
                continue;
            }

            if (message is WebSocketCloseMessage close)
            {
                if (clientSocket.State == WebSocketState.Open || clientSocket.State == WebSocketState.CloseReceived)
                {
                    var closeStatus = close.CloseStatus.HasValue && Enum.IsDefined(typeof(WebSocketCloseStatus), close.CloseStatus.Value)
                        ? (WebSocketCloseStatus)close.CloseStatus.Value
                        : WebSocketCloseStatus.NormalClosure;

                    await clientSocket.CloseAsync(closeStatus, close.Description, ct);
                }
                return;
            }
        }
    }
}
