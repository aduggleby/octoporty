// ImportExportApiTests.cs
// E2E tests for Agent Import/Export endpoints:
// - JSON export/import of definitions
// - SQLite backup download

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Octoporty.Tests.E2E;

[TestFixture]
public class ImportExportApiTests : TestBase
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
        var loginRequest = new { password = TestPassword };

        var content = new StringContent(
            JsonSerializer.Serialize(loginRequest),
            Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync($"{AgentUrl}/api/v1/auth/login", content);
        if (!response.IsSuccessStatusCode)
            return null;

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("token", out var token) ? token.GetString() : null;
    }

    private HttpClient CreateAuthenticatedClient()
    {
        var client = new HttpClient();
        if (!string.IsNullOrEmpty(_authToken))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
        return client;
    }

    [Test]
    public async Task ImportExport_ExportImport_RoundTripsMappings()
    {
        if (string.IsNullOrEmpty(_authToken))
        {
            Assert.Ignore("No auth token available");
            return;
        }

        using var client = CreateAuthenticatedClient();

        // Create a mapping we can verify via ID afterwards.
        var domain = $"import-export-{Guid.NewGuid():N}.local";
        var newMapping = new
        {
            externalDomain = domain,
            externalPort = 443,
            internalHost = "192.168.1.100",
            internalPort = 8080,
            internalUseTls = false,
            allowSelfSignedCerts = false,
            isEnabled = true,
            description = "ImportExportApiTests"
        };

        var createContent = new StringContent(JsonSerializer.Serialize(newMapping), Encoding.UTF8, "application/json");
        var createResponse = await client.PostAsync($"{AgentUrl}/api/v1/mappings", createContent);
        Assert.That(createResponse.IsSuccessStatusCode, Is.True, $"Create should succeed, got {createResponse.StatusCode}");

        using var createDoc = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var id = createDoc.RootElement.GetProperty("id").GetString();
        Assert.That(id, Is.Not.Null.And.Not.Empty);

        // Export JSON
        var exportResponse = await client.GetAsync($"{AgentUrl}/api/v1/import-export/export");
        Assert.That(exportResponse.IsSuccessStatusCode, Is.True, $"Export should succeed, got {exportResponse.StatusCode}");

        var exportJson = await exportResponse.Content.ReadAsStringAsync();
        using var exportDoc = JsonDocument.Parse(exportJson);

        Assert.That(exportDoc.RootElement.TryGetProperty("mappings", out var mappingsEl), Is.True);
        Assert.That(mappingsEl.ValueKind, Is.EqualTo(JsonValueKind.Array));

        // Modify the exported payload: change the mapping port.
        var mappings = mappingsEl.EnumerateArray().Select(e => e.Clone()).ToList();
        var found = false;
        for (var i = 0; i < mappings.Count; i++)
        {
            if (mappings[i].TryGetProperty("externalDomain", out var d) &&
                string.Equals(d.GetString(), domain, StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                // Rebuild object with internalPort changed (JsonElement is immutable).
                var obj = new Dictionary<string, object?>();
                foreach (var p in mappings[i].EnumerateObject())
                    obj[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.Deserialize<object?>();
                obj["internalPort"] = 9090;
                mappings[i] = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(obj));
                break;
            }
        }

        Assert.That(found, Is.True, "Export should include the created mapping");

        var importPayload = new Dictionary<string, object?>
        {
            ["schemaVersion"] = exportDoc.RootElement.GetProperty("schemaVersion").GetString() ?? "1",
            ["mappings"] = mappings,
            ["landingPageHtml"] = exportDoc.RootElement.TryGetProperty("landingPageHtml", out var lp) ? lp.Deserialize<object?>() : null,
            ["landingPageIsDefault"] = exportDoc.RootElement.TryGetProperty("landingPageIsDefault", out var lpd) ? lpd.GetBoolean() : (bool?)null
        };

        var importContent = new StringContent(JsonSerializer.Serialize(importPayload), Encoding.UTF8, "application/json");
        var importResponse = await client.PostAsync($"{AgentUrl}/api/v1/import-export/import", importContent);
        Assert.That(importResponse.IsSuccessStatusCode, Is.True, $"Import should succeed, got {importResponse.StatusCode}");

        var imported = JsonDocument.Parse(await importResponse.Content.ReadAsStringAsync());
        Assert.That(imported.RootElement.TryGetProperty("success", out var ok) && ok.GetBoolean(), Is.True);

        // Verify mapping got updated
        var getResponse = await client.GetAsync($"{AgentUrl}/api/v1/mappings/{id}");
        Assert.That(getResponse.IsSuccessStatusCode, Is.True);

        var getBody = await getResponse.Content.ReadAsStringAsync();
        Assert.That(getBody, Does.Contain("9090"), "Mapping should be updated by import");
    }

    [Test]
    public async Task ImportExport_DownloadSqlite_ReturnsSqliteHeader()
    {
        if (string.IsNullOrEmpty(_authToken))
        {
            Assert.Ignore("No auth token available");
            return;
        }

        using var client = CreateAuthenticatedClient();
        var response = await client.GetAsync($"{AgentUrl}/api/v1/import-export/sqlite");
        Assert.That(response.IsSuccessStatusCode, Is.True, $"SQLite backup should succeed, got {response.StatusCode}");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.That(bytes.Length, Is.GreaterThan(64));

        var header = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 16));
        Assert.That(header, Does.StartWith("SQLite format 3"), "Backup should be a SQLite database file");
    }
}

