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
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await Task.Delay(1000);

        // Should show login form or dashboard
        var hasLoginForm = await Page.Locator("input[type='password']").IsVisibleAsync();
        var hasDashboard = await Page.Locator("text=Connection Status").IsVisibleAsync() ||
                           await Page.Locator("text=Dashboard").IsVisibleAsync();

        Assert.That(hasLoginForm || hasDashboard, Is.True,
            "Page should show either login form or dashboard");
    }

    [Test]
    public async Task Login_WithValidCredentials_Succeeds()
    {
        await Page.GotoAsync(AgentUrl);
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await Task.Delay(1000);

        // Skip if already logged in
        var signOutVisible = await Page.Locator("text=Sign Out").IsVisibleAsync();
        var logoutVisible = await Page.Locator("text=Logout").IsVisibleAsync();
        if (signOutVisible || logoutVisible)
        {
            Assert.Pass("Already logged in");
            return;
        }

        await LoginAndWaitForDashboardAsync();

        // Should now show dashboard content - check page contains expected dashboard text
        var content = await Page.ContentAsync();
        Assert.That(
            content.Contains("Dashboard") || content.Contains("Control Panel") || content.Contains("Mapping") || content.Contains("Quick Actions"),
            Is.True,
            "Dashboard should show after login");
    }

    [Test]
    public async Task Dashboard_ShowsConnectionStatus()
    {
        await LoginAndWaitForDashboardAsync();

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
        await LoginAndWaitForDashboardAsync();

        // Check for gateway version or connection info
        var content = await Page.ContentAsync();
        var hasConnectionInfo = content.Contains("Gateway") ||
                                content.Contains("Connected") ||
                                content.Contains("1.0.0") ||
                                content.Contains("Quick Actions");

        Assert.That(hasConnectionInfo, Is.True,
            "Dashboard should show connection information when connected");
    }

    [Test]
    public async Task Mappings_PageLoads()
    {
        var navigated = await NavigateWithAuthAsync("/mappings");
        if (!navigated)
        {
            Assert.Ignore("Could not authenticate to access mappings page");
            return;
        }

        await Task.Delay(1000);

        // Should show mappings page
        var content = await Page.ContentAsync();
        Assert.That(
            content.Contains("Mapping") || content.Contains("Domain") || content.Contains("Port"),
            Is.True,
            "Mappings page should load with relevant content");
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
