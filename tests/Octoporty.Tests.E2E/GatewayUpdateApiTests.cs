// GatewayUpdateApiTests.cs
// E2E tests for the Gateway Self-Update feature.
// Tests the update trigger API endpoint, status reporting, and UI banner.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Octoporty.Tests.E2E;

[TestFixture]
public class GatewayUpdateApiTests : TestBase
{
    private string? _authToken;

    [SetUp]
    public async Task SetUpAuth()
    {
        await base.SetUpTest();
        _authToken = await GetAuthTokenAsync();
    }

    private async Task<string?> GetAuthTokenAsync()
    {
        using var client = new HttpClient();
        var loginRequest = new
        {
            password = TestPassword
        };

        var content = new StringContent(
            JsonSerializer.Serialize(loginRequest),
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync($"{AgentUrl}/api/v1/auth/login", content);

        if (!response.IsSuccessStatusCode)
            return null;

        var responseContent = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(responseContent);

        if (json.RootElement.TryGetProperty("token", out var token))
            return token.GetString();

        return null;
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = new HttpClient();
        if (!string.IsNullOrEmpty(_authToken))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _authToken);
        }
        return client;
    }

    [Test]
    public async Task Status_IncludesGatewayUpdateAvailable()
    {
        if (string.IsNullOrEmpty(_authToken))
        {
            Assert.Ignore("No auth token available");
            return;
        }

        using var client = CreateAuthenticatedClient();
        var response = await client.GetAsync($"{AgentUrl}/api/v1/status");

        Assert.That(response.IsSuccessStatusCode, Is.True,
            $"Status endpoint should succeed, got {response.StatusCode}");

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        // Verify the response includes gatewayUpdateAvailable field
        Assert.That(json.RootElement.TryGetProperty("gatewayUpdateAvailable", out var updateAvailable), Is.True,
            "Status response should include gatewayUpdateAvailable field");

        // The value should be a boolean
        Assert.That(updateAvailable.ValueKind, Is.EqualTo(JsonValueKind.True).Or.EqualTo(JsonValueKind.False),
            "gatewayUpdateAvailable should be a boolean");
    }

    [Test]
    public async Task Status_IncludesGatewayVersion()
    {
        if (string.IsNullOrEmpty(_authToken))
        {
            Assert.Ignore("No auth token available");
            return;
        }

        using var client = CreateAuthenticatedClient();
        var response = await client.GetAsync($"{AgentUrl}/api/v1/status");

        Assert.That(response.IsSuccessStatusCode, Is.True,
            $"Status endpoint should succeed, got {response.StatusCode}");

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        // Gateway version may be null if not connected, but the field should exist
        Assert.That(json.RootElement.TryGetProperty("gatewayVersion", out _), Is.True,
            "Status response should include gatewayVersion field");
    }

    [Test]
    public async Task GatewayUpdate_RequiresAuthentication()
    {
        using var client = new HttpClient(); // No auth token

        var content = new StringContent(
            JsonSerializer.Serialize(new { force = false }),
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync($"{AgentUrl}/api/v1/gateway/update", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
            "Gateway update endpoint should require authentication");
    }

    [Test]
    public async Task GatewayUpdate_ReturnsStructuredResponse()
    {
        if (string.IsNullOrEmpty(_authToken))
        {
            Assert.Ignore("No auth token available");
            return;
        }

        using var client = CreateAuthenticatedClient();

        var content = new StringContent(
            JsonSerializer.Serialize(new { force = false }),
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync($"{AgentUrl}/api/v1/gateway/update", content);

        // The request may succeed or fail depending on connection state,
        // but should always return a structured response
        Assert.That(response.IsSuccessStatusCode, Is.True,
            $"Gateway update endpoint should return OK, got {response.StatusCode}");

        var responseContent = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(responseContent);

        // Verify response structure
        Assert.That(json.RootElement.TryGetProperty("success", out var success), Is.True,
            "Response should include 'success' field");
        Assert.That(success.ValueKind, Is.EqualTo(JsonValueKind.True).Or.EqualTo(JsonValueKind.False),
            "'success' should be a boolean");

        Assert.That(json.RootElement.TryGetProperty("message", out var message), Is.True,
            "Response should include 'message' field");
        Assert.That(message.ValueKind, Is.EqualTo(JsonValueKind.String),
            "'message' should be a string");

        Assert.That(json.RootElement.TryGetProperty("agentVersion", out var agentVersion), Is.True,
            "Response should include 'agentVersion' field");
        Assert.That(agentVersion.ValueKind, Is.EqualTo(JsonValueKind.String),
            "'agentVersion' should be a string");
    }

    [Test]
    public async Task GatewayUpdate_WhenNotConnected_ReturnsError()
    {
        if (string.IsNullOrEmpty(_authToken))
        {
            Assert.Ignore("No auth token available");
            return;
        }

        // This test assumes Agent may not be connected to Gateway during tests
        using var client = CreateAuthenticatedClient();

        // First check if connected
        var statusResponse = await client.GetAsync($"{AgentUrl}/api/v1/status");
        var statusContent = await statusResponse.Content.ReadAsStringAsync();
        var statusJson = JsonDocument.Parse(statusContent);

        var connectionStatus = statusJson.RootElement.GetProperty("connectionStatus").GetString();

        var content = new StringContent(
            JsonSerializer.Serialize(new { force = false }),
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync($"{AgentUrl}/api/v1/gateway/update", content);
        var responseContent = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(responseContent);

        if (connectionStatus != "Connected")
        {
            // If not connected, should report failure
            Assert.That(json.RootElement.GetProperty("success").GetBoolean(), Is.False,
                "Update should fail when not connected to Gateway");

            var message = json.RootElement.GetProperty("message").GetString();
            Assert.That(message, Does.Contain("Not connected").IgnoreCase,
                "Error message should indicate not connected");
        }
        else
        {
            // If connected, the response depends on version comparison
            // Either success (update queued) or failure (no update available)
            Assert.That(json.RootElement.TryGetProperty("success", out _), Is.True,
                "Response should have success field when connected");
        }
    }

    [Test]
    public async Task GatewayUpdate_ForceFlag_AcceptedInRequest()
    {
        if (string.IsNullOrEmpty(_authToken))
        {
            Assert.Ignore("No auth token available");
            return;
        }

        using var client = CreateAuthenticatedClient();

        // Test with force=true
        var content = new StringContent(
            JsonSerializer.Serialize(new { force = true }),
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync($"{AgentUrl}/api/v1/gateway/update", content);

        // Should accept the request (may still fail if not connected, but shouldn't error on the force flag)
        Assert.That(response.IsSuccessStatusCode, Is.True,
            "Endpoint should accept force flag in request");
    }
}
