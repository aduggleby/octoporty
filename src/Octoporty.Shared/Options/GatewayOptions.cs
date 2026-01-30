// GatewayOptions.cs
// Configuration options for the Octoporty Gateway service.
// Includes API key for Agent authentication and Caddy Admin API URL.
// Ports use the 17200-17299 range (Gateway: 17200, Caddy Admin: 17202).

namespace Octoporty.Shared.Options;

public class GatewayOptions
{
    public string ApiKey { get; set; } = "";
    public string CaddyAdminUrl { get; set; } = "http://localhost:17202";
    public int ListenPort { get; set; } = 17200;
    public bool DebugJson { get; set; }

    /// <summary>
    /// Path to write the update signal file. The host watcher monitors this file
    /// and triggers docker-compose pull/restart when it appears.
    /// </summary>
    public string UpdateSignalPath { get; set; } = "/data/update-signal";

    /// <summary>
    /// Whether to allow Agents to request Gateway self-updates.
    /// Disable this if you want to manage updates manually.
    /// </summary>
    public bool AllowRemoteUpdate { get; set; } = true;

    /// <summary>
    /// The Gateway's own FQDN for serving the landing page.
    /// When a request arrives for this domain at the root path, the landing page is served.
    /// Example: gateway.octoporty.com
    /// </summary>
    public string GatewayFqdn { get; set; } = "";
}
