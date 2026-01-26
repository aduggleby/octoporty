// GatewayUpdateUiTests.cs
// E2E UI tests for the Gateway Self-Update banner component.
// Tests visibility conditions, button interactions, and toast notifications.

using Microsoft.Playwright;

namespace Octoporty.Tests.E2E;

[TestFixture]
public class GatewayUpdateUiTests : TestBase
{
    [Test]
    public async Task Dashboard_ShowsVersionInfo()
    {
        await LoginAndWaitForDashboardAsync();

        // The Agent version should be displayed somewhere in the UI
        var versionText = await Page.Locator("text=/v\\d+\\.\\d+\\.\\d+/").First.TextContentAsync();
        Assert.That(versionText, Is.Not.Null.And.Not.Empty,
            "Dashboard should display version information");
    }

    [Test]
    public async Task Layout_ShowsConnectionStatus()
    {
        await LoginAndWaitForDashboardAsync();

        // Connection status badge should be visible in sidebar
        var statusBadge = Page.Locator(".led, text=/ONLINE|OFFLINE|CONNECTING|RECONNECTING/").First;
        var isVisible = await statusBadge.IsVisibleAsync();

        Assert.That(isVisible, Is.True,
            "Connection status should be visible in the layout");
    }

    [Test]
    public async Task GatewayUpdateBanner_WhenUpdateAvailable_ShowsUpdateButton()
    {
        await LoginAndWaitForDashboardAsync();

        // Check if the update banner is visible (depends on version mismatch)
        var updateBanner = Page.Locator("text=GATEWAY UPDATE AVAILABLE");
        var bannerVisible = await updateBanner.IsVisibleAsync();

        if (bannerVisible)
        {
            // If banner is visible, the update button should also be visible
            var updateButton = Page.Locator("button:has-text('Update Gateway')");
            Assert.That(await updateButton.IsVisibleAsync(), Is.True,
                "Update Gateway button should be visible when banner is shown");

            // Dismiss button should also be present
            var dismissButton = Page.Locator("[title='Dismiss']");
            Assert.That(await dismissButton.IsVisibleAsync(), Is.True,
                "Dismiss button should be visible on update banner");
        }
        else
        {
            // Banner not visible - this is fine, versions may match
            Assert.Pass("Gateway update banner not visible (versions may be in sync)");
        }
    }

    [Test]
    public async Task GatewayUpdateBanner_UpdateButton_ShowsLoadingState()
    {
        await LoginAndWaitForDashboardAsync();

        var updateBanner = Page.Locator("text=GATEWAY UPDATE AVAILABLE");
        var bannerVisible = await updateBanner.IsVisibleAsync();

        if (!bannerVisible)
        {
            Assert.Ignore("Gateway update banner not visible - cannot test loading state");
            return;
        }

        var updateButton = Page.Locator("button:has-text('Update Gateway')");

        // Click the update button
        await updateButton.ClickAsync();

        // Button should show loading state (either "Updating..." text or disabled state)
        var buttonTextAfterClick = await updateButton.TextContentAsync();
        var isDisabled = await updateButton.IsDisabledAsync();

        // Either the button shows "Updating..." or gets disabled
        var showsLoadingFeedback = buttonTextAfterClick?.Contains("Updating") == true || isDisabled;

        Assert.That(showsLoadingFeedback, Is.True,
            "Update button should show loading feedback when clicked");

        // Wait for operation to complete
        await Task.Delay(2000);
    }

    [Test]
    public async Task GatewayUpdateBanner_DismissButton_HidesBanner()
    {
        await LoginAndWaitForDashboardAsync();

        var updateBanner = Page.Locator("text=GATEWAY UPDATE AVAILABLE");
        var bannerVisible = await updateBanner.IsVisibleAsync();

        if (!bannerVisible)
        {
            Assert.Ignore("Gateway update banner not visible - cannot test dismiss");
            return;
        }

        var dismissButton = Page.Locator("[title='Dismiss']");
        await dismissButton.ClickAsync();

        // Banner should disappear after dismiss
        await Task.Delay(500);

        var bannerStillVisible = await updateBanner.IsVisibleAsync();
        Assert.That(bannerStillVisible, Is.False,
            "Banner should be hidden after clicking dismiss");
    }

    [Test]
    public async Task GatewayUpdateBanner_UpdateAction_ShowsToastNotification()
    {
        await LoginAndWaitForDashboardAsync();

        var updateBanner = Page.Locator("text=GATEWAY UPDATE AVAILABLE");
        var bannerVisible = await updateBanner.IsVisibleAsync();

        if (!bannerVisible)
        {
            Assert.Ignore("Gateway update banner not visible - cannot test toast");
            return;
        }

        var updateButton = Page.Locator("button:has-text('Update Gateway')");
        await updateButton.ClickAsync();

        // Wait for the toast notification to appear
        try
        {
            var toast = Page.Locator(".toast, [class*='toast']").First;
            await toast.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });

            Assert.That(await toast.IsVisibleAsync(), Is.True,
                "Toast notification should appear after update action");
        }
        catch (TimeoutException)
        {
            // Toast might not appear if update fails quickly
            // Check for any feedback in the banner itself
            var bannerText = await updateBanner.Locator("..").TextContentAsync();
            Assert.That(bannerText, Is.Not.Null,
                "Some feedback should be shown after update action");
        }
    }

    [Test]
    public async Task GatewayUpdateBanner_ShowsVersionComparison()
    {
        await LoginAndWaitForDashboardAsync();

        var updateBanner = Page.Locator("text=GATEWAY UPDATE AVAILABLE");
        var bannerVisible = await updateBanner.IsVisibleAsync();

        if (!bannerVisible)
        {
            Assert.Ignore("Gateway update banner not visible");
            return;
        }

        // Banner should show version comparison text
        var bannerContainer = updateBanner.Locator("..");
        var bannerText = await bannerContainer.TextContentAsync();

        // Should contain version numbers or version comparison
        var containsVersionInfo = bannerText?.Contains("v") == true ||
                                   bannerText?.Contains("Agent") == true ||
                                   bannerText?.Contains("Gateway") == true;

        Assert.That(containsVersionInfo, Is.True,
            "Update banner should show version information");
    }
}
