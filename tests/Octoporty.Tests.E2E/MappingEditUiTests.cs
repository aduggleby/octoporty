// MappingEditUiTests.cs
// E2E test that creates a mapping via the UI and then loads + edits it.
// Verifies GET /api/v1/mappings/{id} succeeds (regression test for 400 deserialization errors).

using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NUnit.Framework;

namespace Octoporty.Tests.E2E;

public class MappingEditUiTests : TestBase
{
    [Test]
    public async Task Mapping_Create_Then_Edit_Works()
    {
        await LoginAsync();

        // Go to create page
        var navigated = await NavigateWithAuthAsync("/mappings/new");
        if (!navigated)
            Assert.Ignore("Could not authenticate to access new mapping page");

        var unique = Guid.NewGuid().ToString("N")[..8];
        var domain = $"e2e-edit-{unique}.test.local";
        var name = $"E2E Edit {unique}";

        await Page.FillAsync("input[placeholder='e.g., My Web App']", name);
        await Page.FillAsync("input[placeholder='app.example.com']", domain);
        await Page.SelectOptionAsync("select", new SelectOptionValue { Value = "Http" });
        await Page.FillAsync("input[placeholder='localhost']", "192.168.1.100");
        await Page.FillAsync("input[type='number']", "8080");

        // Capture create response to get the new mapping ID.
        var createResponseTask = Page.WaitForResponseAsync(
            resp => resp.Url.EndsWith("/api/v1/mappings") && resp.Request.Method == "POST",
            new PageWaitForResponseOptions { Timeout = 60000 });

        await Page.ClickAsync("button:has-text('Create Mapping')");

        var createResponse = await createResponseTask;
        Assert.That(createResponse.Status, Is.EqualTo(201),
            $"Expected create mapping to return 201, got {createResponse.Status}. Body: {await createResponse.TextAsync()}");
        var createdJson = await createResponse.JsonAsync();
        var id = createdJson?.GetProperty("id").GetString();
        Assert.That(id, Is.Not.Null.And.Not.Empty, "Create mapping should return an id");

        // Navigate to edit page directly and assert GET /api/v1/mappings/{id} succeeds.
        var getResponseTask = Page.WaitForResponseAsync(resp =>
            Regex.IsMatch(resp.Url, $"/api/v1/mappings/{Regex.Escape(id!)}$") &&
            resp.Request.Method == "GET" &&
            resp.Status == 200);

        await Page.GotoAsync($"{AgentUrl}/mappings/{id}");
        await getResponseTask;

        // Wait for form to render populated values.
        await Expect(Page.Locator("text=Edit Mapping").First).ToBeVisibleAsync();
        await Expect(Page.Locator($"input[placeholder='app.example.com'][value='{domain}']")).ToBeVisibleAsync();

        // Change port and save.
        var updateResponseTask = Page.WaitForResponseAsync(resp =>
            Regex.IsMatch(resp.Url, $"/api/v1/mappings/{Regex.Escape(id!)}$") &&
            resp.Request.Method == "PUT" &&
            resp.Status == 200);

        await Page.FillAsync("input[type='number']", "8081");
        await Page.ClickAsync("button[type='submit']");

        await updateResponseTask;

        // Toast should appear.
        await Expect(Page.Locator("text=Mapping updated").First).ToBeVisibleAsync();
    }
}
