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
}
