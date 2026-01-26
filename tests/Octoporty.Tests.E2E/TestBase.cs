// TestBase.cs
// Base class for Playwright E2E tests.
// Manages Agent and Gateway processes for testing.

using System.Diagnostics;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Octoporty.Tests.E2E;

public class TestBase : PageTest
{
    protected const string AgentUrl = "http://localhost:17201";
    protected const string GatewayUrl = "http://46.224.221.66:17200";
    protected const string TestUsername = "admin";
    protected const string TestPassword = "octoporty-test-password-123";

    protected Process? AgentProcess;

    public override BrowserNewContextOptions ContextOptions()
    {
        return new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true
        };
    }

    [SetUp]
    public async Task SetUpTest()
    {
        // Start Agent if not running
        await EnsureAgentRunning();
    }

    [TearDown]
    public async Task TearDownTest()
    {
        // Dispose process reference (process may still run for next test)
        AgentProcess?.Dispose();
        AgentProcess = null;
    }

    protected async Task EnsureAgentRunning()
    {
        try
        {
            using var checkClient = new HttpClient();
            var response = await checkClient.GetAsync($"{AgentUrl}/health");
            if (response.IsSuccessStatusCode)
                return; // Already running
        }
        catch
        {
            // Not running, start it
        }

        var projectPath = Path.GetFullPath(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "src", "Octoporty.Agent"));

        AgentProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --no-build",
                WorkingDirectory = projectPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Environment =
                {
                    ["ASPNETCORE_ENVIRONMENT"] = "Development"
                }
            }
        };

        AgentProcess.Start();

        // Wait for Agent to be ready
        using var client = new HttpClient();
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(1000);
            try
            {
                var response = await client.GetAsync($"{AgentUrl}/health");
                if (response.IsSuccessStatusCode)
                    return;
            }
            catch
            {
                // Not ready yet
            }
        }

        throw new Exception("Agent failed to start within 30 seconds");
    }

    protected async Task LoginAsync()
    {
        await Page.GotoAsync(AgentUrl);
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await Task.Delay(500);

        // Check if already logged in (look for sidebar nav or Sign Out)
        var isLoggedIn = await Page.Locator("text=Sign Out").IsVisibleAsync() ||
                         await Page.Locator("text=Quick Actions").IsVisibleAsync();
        if (isLoggedIn)
            return;

        // Fill login form with exact placeholder selectors
        await Page.FillAsync("input[placeholder='Enter username']", TestUsername);
        await Page.FillAsync("input[placeholder='Enter password']", TestPassword);
        await Page.ClickAsync("button[type='submit']");

        // Wait for redirect away from login page
        try
        {
            await Page.WaitForURLAsync(url => !url.Contains("/login"), new PageWaitForURLOptions { Timeout = 10000 });
        }
        catch
        {
            // Fallback: just wait for content to change
            await Task.Delay(2000);
        }

        // Ensure auth cookies are properly set
        await Task.Delay(500);
    }

    /// <summary>
    /// Login and wait for the dashboard to fully load (including async data).
    /// Use this when testing dashboard elements.
    /// </summary>
    protected async Task LoginAndWaitForDashboardAsync()
    {
        await LoginAsync();

        // Wait for dashboard data to load (Quick Actions panel appears after loading)
        try
        {
            await Page.WaitForSelectorAsync("text=Quick Actions", new PageWaitForSelectorOptions { Timeout = 15000 });
        }
        catch
        {
            // Dashboard might be slow, give it more time
            await Task.Delay(3000);
        }
    }

    /// <summary>
    /// Navigate to a page with session retry logic.
    /// If redirected to login, re-authenticates and tries again.
    /// </summary>
    protected async Task<bool> NavigateWithAuthAsync(string path)
    {
        await LoginAsync();
        await Page.GotoAsync($"{AgentUrl}{path}");
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await Task.Delay(1000);

        // Check if we got redirected to login
        if (Page.Url.Contains("/login"))
        {
            // Session expired - re-login and try again
            await Page.FillAsync("input[placeholder='Enter username']", TestUsername);
            await Page.FillAsync("input[placeholder='Enter password']", TestPassword);
            await Page.ClickAsync("button[type='submit']");

            try
            {
                await Page.WaitForURLAsync(url => !url.Contains("/login"), new PageWaitForURLOptions { Timeout = 10000 });
            }
            catch
            {
                return false; // Login failed
            }

            // Navigate again after re-login
            await Page.GotoAsync($"{AgentUrl}{path}");
            await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
            await Task.Delay(1000);

            // Final check
            if (Page.Url.Contains("/login"))
                return false;
        }

        return true;
    }
}
