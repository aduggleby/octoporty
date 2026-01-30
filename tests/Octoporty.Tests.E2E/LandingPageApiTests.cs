// LandingPageApiTests.cs
// E2E tests for the landing page settings REST API.
// Tests get/update/reset operations with proper authentication.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Octoporty.Tests.E2E;

[TestFixture]
public class LandingPageApiTests : TestBase
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
            username = TestUsername,
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
    public async Task LandingPage_Get_ReturnsHtmlAndHash()
    {
        if (string.IsNullOrEmpty(_authToken))
        {
            Assert.Ignore("No auth token available");
            return;
        }

        using var client = CreateAuthenticatedClient();
        var response = await client.GetAsync($"{AgentUrl}/api/v1/settings/landing-page");

        Assert.That(response.IsSuccessStatusCode, Is.True,
            $"Get landing page should succeed, got {response.StatusCode}");

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        Assert.That(json.RootElement.TryGetProperty("html", out var html), Is.True,
            "Response should contain 'html' property");
        Assert.That(json.RootElement.TryGetProperty("hash", out var hash), Is.True,
            "Response should contain 'hash' property");
        Assert.That(json.RootElement.TryGetProperty("isDefault", out var isDefault), Is.True,
            "Response should contain 'isDefault' property");

        Assert.That(html.GetString(), Is.Not.Empty,
            "HTML should not be empty");
        Assert.That(hash.GetString()?.Length, Is.EqualTo(32),
            "Hash should be 32 characters (MD5 hex)");
    }

    [Test]
    public async Task LandingPage_Get_Unauthenticated_Returns401()
    {
        using var client = new HttpClient(); // No auth token
        var response = await client.GetAsync($"{AgentUrl}/api/v1/settings/landing-page");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized),
            "Unauthenticated request should return 401");
    }

    [Test]
    public async Task LandingPage_Update_ChangesContent()
    {
        if (string.IsNullOrEmpty(_authToken))
        {
            Assert.Ignore("No auth token available");
            return;
        }

        using var client = CreateAuthenticatedClient();

        // Get original landing page
        var getResponse = await client.GetAsync($"{AgentUrl}/api/v1/settings/landing-page");
        var originalContent = await getResponse.Content.ReadAsStringAsync();
        var originalJson = JsonDocument.Parse(originalContent);
        var originalHash = originalJson.RootElement.GetProperty("hash").GetString();

        // Update with custom HTML
        var customHtml = $"<!DOCTYPE html><html><body><h1>Test Landing Page {Guid.NewGuid():N}</h1></body></html>";
        var updateRequest = new { html = customHtml };
        var updateContent = new StringContent(
            JsonSerializer.Serialize(updateRequest),
            Encoding.UTF8,
            "application/json");

        var updateResponse = await client.PutAsync($"{AgentUrl}/api/v1/settings/landing-page", updateContent);

        Assert.That(updateResponse.IsSuccessStatusCode, Is.True,
            $"Update landing page should succeed, got {updateResponse.StatusCode}");

        var updateResponseContent = await updateResponse.Content.ReadAsStringAsync();
        var updateJson = JsonDocument.Parse(updateResponseContent);
        var newHash = updateJson.RootElement.GetProperty("hash").GetString();

        Assert.That(newHash, Is.Not.EqualTo(originalHash),
            "Hash should change after update");

        // Verify the update persisted
        var verifyResponse = await client.GetAsync($"{AgentUrl}/api/v1/settings/landing-page");
        var verifyContent = await verifyResponse.Content.ReadAsStringAsync();
        var verifyJson = JsonDocument.Parse(verifyContent);

        Assert.That(verifyJson.RootElement.GetProperty("html").GetString(), Does.Contain("Test Landing Page"),
            "Updated HTML should be returned");
        Assert.That(verifyJson.RootElement.GetProperty("isDefault").GetBoolean(), Is.False,
            "isDefault should be false after custom update");
    }

    [Test]
    public async Task LandingPage_Reset_RestoresDefault()
    {
        if (string.IsNullOrEmpty(_authToken))
        {
            Assert.Ignore("No auth token available");
            return;
        }

        using var client = CreateAuthenticatedClient();

        // First set a custom landing page
        var customHtml = $"<!DOCTYPE html><html><body><h1>Custom {Guid.NewGuid():N}</h1></body></html>";
        var updateRequest = new { html = customHtml };
        var updateContent = new StringContent(
            JsonSerializer.Serialize(updateRequest),
            Encoding.UTF8,
            "application/json");

        await client.PutAsync($"{AgentUrl}/api/v1/settings/landing-page", updateContent);

        // Now reset to default
        var resetResponse = await client.DeleteAsync($"{AgentUrl}/api/v1/settings/landing-page");

        Assert.That(resetResponse.IsSuccessStatusCode, Is.True,
            $"Reset landing page should succeed, got {resetResponse.StatusCode}");

        var resetContent = await resetResponse.Content.ReadAsStringAsync();
        var resetJson = JsonDocument.Parse(resetContent);

        Assert.That(resetJson.RootElement.GetProperty("isDefault").GetBoolean(), Is.True,
            "isDefault should be true after reset");
        Assert.That(resetJson.RootElement.GetProperty("html").GetString(), Does.Contain("Octoporty Gateway"),
            "Default HTML should contain 'Octoporty Gateway'");
    }

    [Test]
    public async Task LandingPage_Update_EmptyHtml_Returns400()
    {
        if (string.IsNullOrEmpty(_authToken))
        {
            Assert.Ignore("No auth token available");
            return;
        }

        using var client = CreateAuthenticatedClient();

        var updateRequest = new { html = "" };
        var updateContent = new StringContent(
            JsonSerializer.Serialize(updateRequest),
            Encoding.UTF8,
            "application/json");

        var updateResponse = await client.PutAsync($"{AgentUrl}/api/v1/settings/landing-page", updateContent);

        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
            "Empty HTML should return 400 Bad Request");
    }
}
