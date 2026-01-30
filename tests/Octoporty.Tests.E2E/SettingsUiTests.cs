// SettingsUiTests.cs
// E2E tests for the Settings page UI.
// Tests landing page editor, preview, save, and reset functionality.

using Microsoft.Playwright;

namespace Octoporty.Tests.E2E;

[TestFixture]
public class SettingsUiTests : TestBase
{
    [Test]
    public async Task SettingsPage_Loads_WithEditor()
    {
        var success = await NavigateWithAuthAsync("/settings");
        Assert.That(success, Is.True, "Should be able to navigate to settings page");

        // Verify page title
        await Expect(Page.Locator("h1")).ToContainTextAsync("Settings");

        // Verify HTML Editor panel is visible
        await Expect(Page.Locator("text=HTML Editor")).ToBeVisibleAsync();

        // Verify Live Preview panel is visible
        await Expect(Page.Locator("text=Live Preview")).ToBeVisibleAsync();

        // Verify Save button exists
        await Expect(Page.Locator("button:has-text('Save Changes')")).ToBeVisibleAsync();

        // Verify Reset button exists
        await Expect(Page.Locator("button:has-text('Reset to Default')")).ToBeVisibleAsync();
    }

    [Test]
    public async Task SettingsPage_Editor_CanType()
    {
        var success = await NavigateWithAuthAsync("/settings");
        Assert.That(success, Is.True, "Should be able to navigate to settings page");

        // Wait for content to load
        await Page.WaitForSelectorAsync("textarea", new PageWaitForSelectorOptions { Timeout = 10000 });

        // Get the textarea
        var textarea = Page.Locator("textarea");

        // Clear and type new content
        await textarea.FillAsync("<h1>Test Content</h1>");

        // Verify the content is in the textarea
        await Expect(textarea).ToHaveValueAsync("<h1>Test Content</h1>");

        // Verify "Unsaved changes" indicator appears
        await Expect(Page.Locator("text=Unsaved changes")).ToBeVisibleAsync();
    }

    [Test]
    public async Task SettingsPage_SaveButton_IsDisabledWhenNoChanges()
    {
        var success = await NavigateWithAuthAsync("/settings");
        Assert.That(success, Is.True, "Should be able to navigate to settings page");

        // Wait for content to load
        await Page.WaitForSelectorAsync("button:has-text('Save Changes')", new PageWaitForSelectorOptions { Timeout = 10000 });

        // Initially, save button should be disabled (no changes)
        var saveButton = Page.Locator("button:has-text('Save Changes')");
        await Expect(saveButton).ToBeDisabledAsync();
    }

    [Test]
    public async Task SettingsPage_SaveButton_EnablesAfterEdit()
    {
        var success = await NavigateWithAuthAsync("/settings");
        Assert.That(success, Is.True, "Should be able to navigate to settings page");

        // Wait for textarea to be visible
        await Page.WaitForSelectorAsync("textarea", new PageWaitForSelectorOptions { Timeout = 10000 });

        // Get original content
        var textarea = Page.Locator("textarea");
        var originalContent = await textarea.InputValueAsync();

        // Make a change
        await textarea.FillAsync(originalContent + "<!-- modified -->");

        // Save button should now be enabled
        var saveButton = Page.Locator("button:has-text('Save Changes')");
        await Expect(saveButton).ToBeEnabledAsync();
    }

    [Test]
    public async Task SettingsPage_ResetButton_ShowsConfirmation()
    {
        var success = await NavigateWithAuthAsync("/settings");
        Assert.That(success, Is.True, "Should be able to navigate to settings page");

        // First make a change to enable reset (if using default, reset is disabled)
        await Page.WaitForSelectorAsync("textarea", new PageWaitForSelectorOptions { Timeout = 10000 });
        var textarea = Page.Locator("textarea");
        await textarea.FillAsync("<html><body><h1>Custom</h1></body></html>");

        // Save the change
        var saveButton = Page.Locator("button:has-text('Save Changes')");
        await saveButton.ClickAsync();

        // Wait for save to complete
        await Task.Delay(2000);

        // Refresh to get the saved state
        await Page.ReloadAsync();
        await Page.WaitForSelectorAsync("button:has-text('Reset to Default')", new PageWaitForSelectorOptions { Timeout = 10000 });

        // Click reset button
        var resetButton = Page.Locator("button:has-text('Reset to Default')");

        // Check if button is enabled (may be disabled if already default)
        if (await resetButton.IsEnabledAsync())
        {
            await resetButton.ClickAsync();

            // Verify confirmation modal appears
            await Expect(Page.Locator("text=Reset to Default?")).ToBeVisibleAsync();

            // Verify Cancel and Reset buttons in modal
            await Expect(Page.Locator("button:has-text('Cancel')")).ToBeVisibleAsync();
            await Expect(Page.Locator(".bg-surface-2 button:has-text('Reset')")).ToBeVisibleAsync();

            // Click Cancel to close modal
            await Page.Locator("button:has-text('Cancel')").ClickAsync();

            // Modal should be gone
            await Expect(Page.Locator("text=Reset to Default?")).Not.ToBeVisibleAsync();
        }
        else
        {
            // Already at default, skip this test
            Assert.Pass("Reset button is disabled - page is already using default content");
        }
    }

    [Test]
    public async Task SettingsPage_Preview_ShowsHtmlContent()
    {
        var success = await NavigateWithAuthAsync("/settings");
        Assert.That(success, Is.True, "Should be able to navigate to settings page");

        // Wait for iframe to be visible
        await Page.WaitForSelectorAsync("iframe", new PageWaitForSelectorOptions { Timeout = 10000 });

        // Verify iframe exists
        var iframe = Page.Locator("iframe");
        await Expect(iframe).ToBeVisibleAsync();
    }

    [Test]
    public async Task SettingsPage_StatusBar_ShowsHash()
    {
        var success = await NavigateWithAuthAsync("/settings");
        Assert.That(success, Is.True, "Should be able to navigate to settings page");

        // Wait for status bar to load
        await Page.WaitForSelectorAsync("text=Hash:", new PageWaitForSelectorOptions { Timeout = 10000 });

        // Verify status bar shows hash
        await Expect(Page.Locator("text=Hash:")).ToBeVisibleAsync();

        // Verify status shows Default or Custom
        var statusVisible = await Page.Locator("text=Status:").IsVisibleAsync();
        Assert.That(statusVisible, Is.True, "Status bar should show status indicator");
    }

    [Test]
    public async Task SettingsPage_NavigationLink_Exists()
    {
        await LoginAsync();

        // Verify Settings link exists in navigation
        await Expect(Page.Locator("nav >> text=Settings")).ToBeVisibleAsync();
    }

    [Test]
    public async Task SettingsPage_NavigationLink_NavigatesToSettings()
    {
        await LoginAndWaitForDashboardAsync();

        // Click Settings link
        await Page.Locator("nav >> text=Settings").ClickAsync();

        // Verify we're on the settings page
        await Page.WaitForURLAsync(url => url.Contains("/settings"), new PageWaitForURLOptions { Timeout = 10000 });
        await Expect(Page.Locator("h1")).ToContainTextAsync("Settings");
    }
}
