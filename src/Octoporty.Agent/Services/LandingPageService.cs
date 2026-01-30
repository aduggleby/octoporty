// LandingPageService.cs
// Manages the Gateway landing page HTML stored in the Agent database.
// Provides get/set/reset operations and MD5 hash computation for sync detection.

using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Octoporty.Agent.Data;
using Octoporty.Shared.Entities;

namespace Octoporty.Agent.Services;

public class LandingPageService
{
    private const string LandingPageKey = "LandingPageHtml";
    private readonly IDbContextFactory<OctoportyDbContext> _dbContextFactory;

    public LandingPageService(IDbContextFactory<OctoportyDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>
    /// Gets the current landing page HTML and its MD5 hash.
    /// Returns the default landing page if no custom page is stored.
    /// </summary>
    public async Task<(string Html, string Hash)> GetLandingPageAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var setting = await db.Settings.FindAsync(LandingPageKey);
        var html = setting?.Value ?? GetDefaultHtml();
        var hash = ComputeHash(html);

        return (html, hash);
    }

    /// <summary>
    /// Saves custom landing page HTML to the database.
    /// </summary>
    public async Task<string> SetLandingPageAsync(string html)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var setting = await db.Settings.FindAsync(LandingPageKey);
        if (setting is null)
        {
            setting = new Settings { Key = LandingPageKey, Value = html, UpdatedAt = DateTime.UtcNow };
            db.Settings.Add(setting);
        }
        else
        {
            setting.Value = html;
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return ComputeHash(html);
    }

    /// <summary>
    /// Resets to the default landing page by removing the custom setting.
    /// </summary>
    public async Task<(string Html, string Hash)> ResetToDefaultAsync()
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var setting = await db.Settings.FindAsync(LandingPageKey);
        if (setting is not null)
        {
            db.Settings.Remove(setting);
            await db.SaveChangesAsync();
        }

        var html = GetDefaultHtml();
        return (html, ComputeHash(html));
    }

    /// <summary>
    /// Computes MD5 hash of HTML content for sync comparison.
    /// MD5 is used here for speed and simplicity - not for cryptographic security.
    /// </summary>
    public static string ComputeHash(string html)
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        var hashBytes = MD5.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Returns the default landing page HTML with Octoporty branding.
    /// Uses external URL for logo to keep the page size small.
    /// </summary>
    public static string GetDefaultHtml()
    {
        return """
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Octoporty Gateway</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            background: linear-gradient(135deg, #0a0a0a 0%, #1a1a2e 100%);
            color: #e5e5e5;
            display: flex;
            justify-content: center;
            align-items: center;
            min-height: 100vh;
        }
        .container {
            text-align: center;
            padding: 2rem;
            max-width: 500px;
        }
        .logo {
            width: 120px;
            height: 120px;
            margin: 0 auto 1.5rem;
            border-radius: 1rem;
            overflow: hidden;
            box-shadow: 0 4px 20px rgba(59, 130, 246, 0.3);
        }
        .logo img {
            width: 100%;
            height: 100%;
            object-fit: cover;
        }
        h1 {
            color: #3b82f6;
            font-size: 2.5rem;
            font-weight: 700;
            margin: 0 0 0.5rem;
            letter-spacing: -0.025em;
        }
        .tagline {
            color: #a1a1aa;
            font-size: 1.125rem;
            margin: 0 0 2rem;
        }
        .links {
            display: flex;
            justify-content: center;
            gap: 1.5rem;
        }
        .links a {
            color: #3b82f6;
            text-decoration: none;
            font-weight: 500;
            padding: 0.5rem 1rem;
            border-radius: 0.5rem;
            transition: all 0.2s ease;
        }
        .links a:hover {
            background: rgba(59, 130, 246, 0.1);
            transform: translateY(-1px);
        }
        .footer {
            margin-top: 3rem;
            color: #52525b;
            font-size: 0.875rem;
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="logo">
            <img src="https://octoporty.com/octoporty_logo.png" alt="Octoporty">
        </div>
        <h1>Octoporty Gateway</h1>
        <p class="tagline">Self-hosted reverse proxy tunneling solution</p>
        <div class="links">
            <a href="https://github.com/aduggleby/octoporty">GitHub</a>
            <a href="https://octoporty.com">Website</a>
            <a href="https://octoporty.com/docs">Docs</a>
        </div>
        <p class="footer">Powered by Octoporty</p>
    </div>
</body>
</html>
""";
    }
}
