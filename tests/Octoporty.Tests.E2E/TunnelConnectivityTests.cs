// TunnelConnectivityTests.cs
// E2E tests for tunnel connectivity between Agent and Gateway.
// Tests the full request round-trip through the WebSocket tunnel.

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Octoporty.Tests.E2E;

[TestFixture]
public class TunnelConnectivityTests : TestBase
{
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
}
