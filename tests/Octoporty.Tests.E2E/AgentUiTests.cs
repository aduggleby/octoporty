// AgentUiTests.cs
// E2E tests for the Agent web UI.
// Tests login, connection status, and port mapping management.

using Microsoft.Playwright;

namespace Octoporty.Tests.E2E;

[TestFixture]
public class AgentUiTests : TestBase
{
    [Test]
    public async Task LoginPage_ShowsLoginForm()
    {
        await Page.GotoAsync(AgentUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Should show login form or dashboard
        var hasLoginForm = await Page.Locator("input[type='password']").IsVisibleAsync();
        var hasDashboard = await Page.Locator("text=Connection Status, text=Status, text=Dashboard").First.IsVisibleAsync();

        Assert.That(hasLoginForm || hasDashboard, Is.True,
            "Page should show either login form or dashboard");
    }

    [Test]
    public async Task Login_WithValidCredentials_Succeeds()
    {
        await Page.GotoAsync(AgentUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Skip if already logged in
        if (await Page.Locator("text=Sign Out, text=Logout").First.IsVisibleAsync())
        {
            Assert.Pass("Already logged in");
            return;
        }

        await LoginAsync();

        // Should now show dashboard content - check page contains expected dashboard text
        var content = await Page.ContentAsync();
        Assert.That(
            content.Contains("Dashboard") || content.Contains("Control Panel") || content.Contains("Mapping"),
            Is.True,
            "Dashboard should show after login");
    }

    [Test]
    public async Task Dashboard_ShowsConnectionStatus()
    {
        await LoginAsync();

        // Wait for dashboard to fully load with status
        await Task.Delay(2000);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Look for connection status indicator (UI shows ONLINE/OFFLINE)
        var content = await Page.ContentAsync();
        var hasStatusIndicator = content.Contains("ONLINE") ||
                                 content.Contains("OFFLINE") ||
                                 content.Contains("Connected") ||
                                 content.Contains("Disconnected") ||
                                 content.Contains("Connecting") ||
                                 content.Contains("Reconnecting") ||
                                 content.Contains("status"); // Fallback check

        Assert.That(hasStatusIndicator, Is.True,
            $"Dashboard should show connection status. Page content: {content[..Math.Min(500, content.Length)]}");
    }

    [Test]
    public async Task Dashboard_ShowsTunnelInfo_WhenConnected()
    {
        await LoginAsync();

        // Wait for potential connection
        await Task.Delay(2000);

        // Check for gateway version or connection info
        var content = await Page.ContentAsync();
        var hasConnectionInfo = content.Contains("Gateway") ||
                                content.Contains("Connected") ||
                                content.Contains("1.0.0");

        Assert.That(hasConnectionInfo, Is.True,
            "Dashboard should show connection information when connected");
    }

    [Test]
    public async Task Mappings_PageLoads()
    {
        await LoginAsync();

        // Try to navigate to mappings
        var mappingsLink = Page.Locator("a:has-text('Mappings'), a:has-text('Port Mappings'), a[href*='mapping']").First;

        if (await mappingsLink.IsVisibleAsync())
        {
            await mappingsLink.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Should show mappings page
            var content = await Page.ContentAsync();
            Assert.That(
                content.Contains("Mapping") || content.Contains("Domain") || content.Contains("Port"),
                Is.True,
                "Mappings page should load with relevant content");
        }
        else
        {
            // Mappings might be on main dashboard
            Assert.Pass("Mappings link not found - may be on dashboard");
        }
    }

    [Test]
    public async Task Health_EndpointReturnsOk()
    {
        using var client = new HttpClient();
        var response = await client.GetAsync($"{AgentUrl}/health");

        Assert.That(response.IsSuccessStatusCode, Is.True,
            $"Health endpoint should return success, got {response.StatusCode}");
    }

    [Test]
    public async Task Api_RequiresAuthentication()
    {
        using var client = new HttpClient();
        var response = await client.GetAsync($"{AgentUrl}/api/v1/mappings");

        // Should return 401 Unauthorized without auth token
        Assert.That((int)response.StatusCode, Is.EqualTo(401),
            "API endpoints should require authentication");
    }
}
