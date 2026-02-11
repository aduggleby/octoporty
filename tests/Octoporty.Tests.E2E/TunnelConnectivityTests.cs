// TunnelConnectivityTests.cs
// E2E tests for tunnel connectivity between Agent and Gateway.
// Tests the full request round-trip through the WebSocket tunnel.

using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Octoporty.Tests.E2E;

[TestFixture]
public class TunnelConnectivityTests : TestBase
{
    private async Task<bool> IsTunnelConnectedAsync()
    {
        using var statusClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        try
        {
            var statusResponse = await statusClient.GetAsync($"{GatewayUrl}/test/tunnel");
            var statusContent = await statusResponse.Content.ReadAsStringAsync();
            return statusContent.Contains("\"connected\":true");
        }
        catch
        {
            return false;
        }
    }

    [Test]
    public async Task Gateway_Health_ReturnsStatus()
    {
        using var client = new HttpClient();
        var response = await client.GetAsync($"{GatewayUrl}/health");

        Assert.That(response.IsSuccessStatusCode, Is.True,
            $"Gateway health should return success, got {response.StatusCode}");

        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("status"),
            "Health response should contain status field");
    }

    [Test]
    public async Task Gateway_TunnelStatus_ShowsConnection()
    {
        // Wait for Agent to connect
        await Task.Delay(3000);

        using var client = new HttpClient();
        var response = await client.GetAsync($"{GatewayUrl}/test/tunnel");

        Assert.That(response.IsSuccessStatusCode, Is.True,
            $"Tunnel status should return success, got {response.StatusCode}");

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        Assert.That(json.RootElement.TryGetProperty("connected", out var connected), Is.True,
            "Response should have 'connected' property");
    }

    [Test]
    public async Task Agent_Echo_Endpoint_DirectAccess()
    {
        using var client = new HttpClient();
        var requestBody = new { data = new { message = "test" } };
        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync($"{AgentUrl}/api/v1/test/echo", content);

        Assert.That(response.IsSuccessStatusCode, Is.True,
            $"Echo endpoint should return success, got {response.StatusCode}");

        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.That(responseContent, Does.Contain("success"),
            "Echo response should indicate success");
    }

    [Test]
    public async Task Agent_Echo_Get_Endpoint()
    {
        using var client = new HttpClient();
        var response = await client.GetAsync($"{AgentUrl}/api/v1/test/echo");

        Assert.That(response.IsSuccessStatusCode, Is.True,
            $"Echo GET should return success, got {response.StatusCode}");

        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Does.Contain("Octoporty Agent"),
            "Echo response should mention Octoporty Agent");
    }

    [Test]
    public async Task Tunnel_Echo_RequestGoesThrough()
    {
        // Wait for connection
        await Task.Delay(3000);

        using var client = new HttpClient();

        // First verify tunnel is connected
        var statusResponse = await client.GetAsync($"{GatewayUrl}/test/tunnel");
        var statusContent = await statusResponse.Content.ReadAsStringAsync();

        if (!statusContent.Contains("\"connected\":true"))
        {
            Assert.Ignore("Tunnel not connected - Agent may not be running");
            return;
        }

        // Send echo through tunnel
        var requestBody = new { data = new { message = "tunnel test" } };
        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync($"{GatewayUrl}/test/tunnel/echo", content);

        Assert.That(response.IsSuccessStatusCode, Is.True,
            $"Tunnel echo should return success, got {response.StatusCode}");

        var responseContent = await response.Content.ReadAsStringAsync();
        Assert.That(responseContent, Does.Contain("success"),
            "Tunnel echo response should indicate success");
        Assert.That(responseContent, Does.Contain("statusCode"),
            "Response should contain status code from Agent");
    }

    [Test]
    public async Task Tunnel_Connected_GatewayHealthy()
    {
        // Wait for connection
        await Task.Delay(3000);

        using var client = new HttpClient();
        var response = await client.GetAsync($"{GatewayUrl}/health");

        var content = await response.Content.ReadAsStringAsync();

        // If Agent is connected, Gateway should be healthy
        var tunnelStatus = await client.GetAsync($"{GatewayUrl}/test/tunnel");
        var tunnelContent = await tunnelStatus.Content.ReadAsStringAsync();

        if (tunnelContent.Contains("\"connected\":true"))
        {
            Assert.That(content, Does.Contain("healthy"),
                "Gateway should report healthy when Agent is connected");
        }
        else
        {
            Assert.That(content, Does.Contain("degraded"),
                "Gateway should report degraded when no Agent connected");
        }
    }

    [Test]
    public async Task Tunnel_Status_ShowsMappings()
    {
        await Task.Delay(3000);

        using var client = new HttpClient();
        var response = await client.GetAsync($"{GatewayUrl}/test/tunnel");

        if (!response.IsSuccessStatusCode)
        {
            Assert.Ignore("Tunnel endpoint not accessible");
            return;
        }

        var content = await response.Content.ReadAsStringAsync();

        if (content.Contains("\"connected\":true"))
        {
            Assert.That(content, Does.Contain("mappingCount"),
                "Connected tunnel should report mapping count");
            Assert.That(content, Does.Contain("mappings"),
                "Connected tunnel should include mappings array");
        }
    }

    [Test]
    public async Task Tunnel_WebSocket_Echo_RoundTrip_Works()
    {
        await Task.Delay(3000);

        if (!await IsTunnelConnectedAsync())
        {
            Assert.Ignore("Tunnel not connected - websocket round-trip test skipped");
            return;
        }

        var wsUrl = GatewayUrl.Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase)
            .Replace("https://", "wss://", StringComparison.OrdinalIgnoreCase)
            + "/test/tunnel/ws-echo";

        using var socket = new ClientWebSocket();
        using var wsTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await socket.ConnectAsync(new Uri(wsUrl), wsTimeout.Token);

        var payload = Encoding.UTF8.GetBytes("octoporty-websocket-test");
        await socket.SendAsync(payload, WebSocketMessageType.Text, true, wsTimeout.Token);

        var buffer = new byte[256];
        var result = await socket.ReceiveAsync(buffer, wsTimeout.Token);
        var echoed = Encoding.UTF8.GetString(buffer, 0, result.Count);

        Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Text),
            "Expected echoed text websocket frame");
        Assert.That(echoed, Is.EqualTo("octoporty-websocket-test"),
            "Echoed websocket payload should match sent payload");

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "test complete", wsTimeout.Token);
    }

    [Test]
    public async Task Tunnel_WebSocket_Binary_RoundTrip_Works()
    {
        await Task.Delay(3000);

        if (!await IsTunnelConnectedAsync())
        {
            Assert.Ignore("Tunnel not connected - websocket binary test skipped");
            return;
        }

        var wsUrl = GatewayUrl.Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase)
            .Replace("https://", "wss://", StringComparison.OrdinalIgnoreCase)
            + "/test/tunnel/ws-echo";

        using var socket = new ClientWebSocket();
        using var wsTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await socket.ConnectAsync(new Uri(wsUrl), wsTimeout.Token);

        var payload = new byte[] { 1, 2, 3, 4, 5, 250, 251, 252, 253, 254, 255 };
        await socket.SendAsync(payload, WebSocketMessageType.Binary, true, wsTimeout.Token);

        var buffer = new byte[256];
        var result = await socket.ReceiveAsync(buffer, wsTimeout.Token);
        var echoed = buffer.AsSpan(0, result.Count).ToArray();

        Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Binary),
            "Expected echoed binary websocket frame");
        Assert.That(echoed, Is.EqualTo(payload),
            "Echoed binary payload should match sent payload");

        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "test complete", wsTimeout.Token);
    }

    [Test]
    public async Task Tunnel_WebSocket_Close_Handshake_Propagates()
    {
        await Task.Delay(3000);

        if (!await IsTunnelConnectedAsync())
        {
            Assert.Ignore("Tunnel not connected - websocket close test skipped");
            return;
        }

        var wsUrl = GatewayUrl.Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase)
            .Replace("https://", "wss://", StringComparison.OrdinalIgnoreCase)
            + "/test/tunnel/ws-echo";

        using var socket = new ClientWebSocket();
        using var wsTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await socket.ConnectAsync(new Uri(wsUrl), wsTimeout.Token);

        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "client-close", wsTimeout.Token);

        var buffer = new byte[64];
        var result = await socket.ReceiveAsync(buffer, wsTimeout.Token);

        Assert.That(result.MessageType, Is.EqualTo(WebSocketMessageType.Close),
            "Expected close frame acknowledgment from tunnel websocket endpoint");
        Assert.That(result.CloseStatus, Is.EqualTo(WebSocketCloseStatus.NormalClosure));
    }
}
