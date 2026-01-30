// GatewayState.cs
// Singleton service tracking Gateway runtime state.
// Provides uptime calculation for heartbeat responses to connected Agents.
// Stores the landing page HTML and hash for serving on the Gateway's FQDN.

using System.Security.Cryptography;
using System.Text;

namespace Octoporty.Gateway.Services;

public class GatewayState
{
    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;
    private readonly object _landingPageLock = new();
    private string _landingPageHtml;
    private string _landingPageHash;
    private string? _gatewayFqdn;

    public GatewayState()
    {
        // Initialize with default landing page
        _landingPageHtml = GetDefaultHtml();
        _landingPageHash = ComputeHash(_landingPageHtml);
    }

    /// <summary>
    /// Gateway FQDN received from Agent during config sync.
    /// Used for landing page routing when not manually configured.
    /// </summary>
    public string? GatewayFqdn
    {
        get { lock (_landingPageLock) return _gatewayFqdn; }
        set { lock (_landingPageLock) _gatewayFqdn = value; }
    }

    /// <summary>
    /// Returns the number of seconds the Gateway has been running.
    /// </summary>
    public long UptimeSeconds => (long)(DateTimeOffset.UtcNow - _startTime).TotalSeconds;

    /// <summary>
    /// Gets the current landing page HTML.
    /// </summary>
    public string LandingPageHtml
    {
        get { lock (_landingPageLock) return _landingPageHtml; }
    }

    /// <summary>
    /// Gets the MD5 hash of the current landing page HTML.
    /// </summary>
    public string LandingPageHash
    {
        get { lock (_landingPageLock) return _landingPageHash; }
    }

    /// <summary>
    /// Updates the landing page HTML and hash.
    /// Called when Agent syncs a new landing page.
    /// </summary>
    public void UpdateLandingPage(string html, string hash)
    {
        lock (_landingPageLock)
        {
            _landingPageHtml = html;
            _landingPageHash = hash;
        }
    }

    /// <summary>
    /// Computes MD5 hash of HTML content.
    /// MD5 is used for speed and simplicity - not for cryptographic security.
    /// </summary>
    public static string ComputeHash(string html)
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        var hashBytes = MD5.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Returns the default landing page HTML with Octoporty branding.
    /// This matches the Agent's default landing page.
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
