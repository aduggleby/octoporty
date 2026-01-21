// MappingsApiTests.cs
// E2E tests for the port mappings REST API.
// Tests CRUD operations on port mappings with proper authentication.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Octoporty.Tests.E2E;

[TestFixture]
public class MappingsApiTests : TestBase
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
    public async Task Auth_Login_ReturnsToken()
    {
        Assert.That(_authToken, Is.Not.Null.And.Not.Empty,
            "Login should return an auth token");
    }

    [Test]
    public async Task Mappings_List_ReturnsArray()
    {
        if (string.IsNullOrEmpty(_authToken))
        {
            Assert.Ignore("No auth token available");
            return;
        }

        using var client = CreateAuthenticatedClient();
        var response = await client.GetAsync($"{AgentUrl}/api/v1/mappings");

        Assert.That(response.IsSuccessStatusCode, Is.True,
            $"List mappings should succeed, got {response.StatusCode}");

        var content = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(content);

        Assert.That(json.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array),
            "Response should be an array");
    }

    [Test]
    public async Task Mappings_Create_Succeeds()
    {
        if (string.IsNullOrEmpty(_authToken))
        {
            Assert.Ignore("No auth token available");
            return;
        }

        using var client = CreateAuthenticatedClient();

        var newMapping = new
        {
            externalDomain = $"test-{Guid.NewGuid():N}.local",
            externalPort = 443,
            internalHost = "192.168.1.100",
            internalPort = 8080,
            internalUseTls = false,
            allowSelfSignedCerts = false,
            isEnabled = true,
            description = "Test mapping from E2E tests"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(newMapping),
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync($"{AgentUrl}/api/v1/mappings", content);

        Assert.That(response.IsSuccessStatusCode, Is.True,
            $"Create mapping should succeed, got {response.StatusCode}");

        var responseContent = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(responseContent);

        Assert.That(json.RootElement.TryGetProperty("id", out _), Is.True,
            "Created mapping should have an ID");
    }

    [Test]
    public async Task Mappings_Get_ReturnsMapping()
    {
        if (string.IsNullOrEmpty(_authToken))
        {
            Assert.Ignore("No auth token available");
            return;
        }

        using var client = CreateAuthenticatedClient();

        // First create a mapping
        var newMapping = new
        {
            externalDomain = $"get-test-{Guid.NewGuid():N}.local",
            externalPort = 443,
            internalHost = "192.168.1.100",
            internalPort = 9090,
            internalUseTls = false,
            allowSelfSignedCerts = false,
            isEnabled = true
        };

        var createContent = new StringContent(
            JsonSerializer.Serialize(newMapping),
            Encoding.UTF8,
            "application/json");

        var createResponse = await client.PostAsync($"{AgentUrl}/api/v1/mappings", createContent);

        if (!createResponse.IsSuccessStatusCode)
        {
            Assert.Ignore("Could not create mapping for get test");
            return;
        }

        var createJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var id = createJson.RootElement.GetProperty("id").GetString();

        // Now get it
        var getResponse = await client.GetAsync($"{AgentUrl}/api/v1/mappings/{id}");

        Assert.That(getResponse.IsSuccessStatusCode, Is.True,
            $"Get mapping should succeed, got {getResponse.StatusCode}");

        var getContent = await getResponse.Content.ReadAsStringAsync();
        Assert.That(getContent, Does.Contain(newMapping.externalDomain),
            "Retrieved mapping should contain the domain we created");
    }

    [Test]
    public async Task Mappings_Delete_RemovesMapping()
    {
        if (string.IsNullOrEmpty(_authToken))
        {
            Assert.Ignore("No auth token available");
            return;
        }

        using var client = CreateAuthenticatedClient();

        // First create a mapping
        var newMapping = new
        {
            externalDomain = $"delete-test-{Guid.NewGuid():N}.local",
            externalPort = 443,
            internalHost = "192.168.1.100",
            internalPort = 9091,
            internalUseTls = false,
            allowSelfSignedCerts = false,
            isEnabled = true
        };

        var createContent = new StringContent(
            JsonSerializer.Serialize(newMapping),
            Encoding.UTF8,
            "application/json");

        var createResponse = await client.PostAsync($"{AgentUrl}/api/v1/mappings", createContent);

        if (!createResponse.IsSuccessStatusCode)
        {
            Assert.Ignore("Could not create mapping for delete test");
            return;
        }

        var createJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var id = createJson.RootElement.GetProperty("id").GetString();

        // Delete it
        var deleteResponse = await client.DeleteAsync($"{AgentUrl}/api/v1/mappings/{id}");

        Assert.That(deleteResponse.IsSuccessStatusCode, Is.True,
            $"Delete mapping should succeed, got {deleteResponse.StatusCode}");

        // Verify it's gone
        var getResponse = await client.GetAsync($"{AgentUrl}/api/v1/mappings/{id}");

        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
            "Deleted mapping should return 404");
    }

    [Test]
    public async Task Mappings_Update_ModifiesMapping()
    {
        if (string.IsNullOrEmpty(_authToken))
        {
            Assert.Ignore("No auth token available");
            return;
        }

        using var client = CreateAuthenticatedClient();

        // First create a mapping
        var domain = $"update-test-{Guid.NewGuid():N}.local";
        var newMapping = new
        {
            externalDomain = domain,
            externalPort = 443,
            internalHost = "192.168.1.100",
            internalPort = 9092,
            internalUseTls = false,
            allowSelfSignedCerts = false,
            isEnabled = true,
            description = "Original"
        };

        var createContent = new StringContent(
            JsonSerializer.Serialize(newMapping),
            Encoding.UTF8,
            "application/json");

        var createResponse = await client.PostAsync($"{AgentUrl}/api/v1/mappings", createContent);

        if (!createResponse.IsSuccessStatusCode)
        {
            Assert.Ignore("Could not create mapping for update test");
            return;
        }

        var createJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var id = createJson.RootElement.GetProperty("id").GetString();

        // Update it
        var updateMapping = new
        {
            externalDomain = domain,
            externalPort = 443,
            internalHost = "192.168.1.100",
            internalPort = 9999, // Changed
            internalUseTls = false,
            allowSelfSignedCerts = false,
            isEnabled = true,
            description = "Updated"
        };

        var updateContent = new StringContent(
            JsonSerializer.Serialize(updateMapping),
            Encoding.UTF8,
            "application/json");

        var updateResponse = await client.PutAsync($"{AgentUrl}/api/v1/mappings/{id}", updateContent);

        Assert.That(updateResponse.IsSuccessStatusCode, Is.True,
            $"Update mapping should succeed, got {updateResponse.StatusCode}");

        // Verify the update
        var getResponse = await client.GetAsync($"{AgentUrl}/api/v1/mappings/{id}");
        var getContent = await getResponse.Content.ReadAsStringAsync();

        Assert.That(getContent, Does.Contain("9999"),
            "Updated mapping should have new port");
        Assert.That(getContent, Does.Contain("Updated"),
            "Updated mapping should have new description");
    }
}
