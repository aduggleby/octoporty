// AgentOptions.cs
// Configuration options for the Octoporty Agent service.
// Includes Gateway FQDN, API key, JWT secret, and auth credentials.
// JwtSecret and Password are validated at startup (minimum lengths enforced).

namespace Octoporty.Shared.Options;

public class AgentOptions
{
    /// <summary>
    /// Gateway FQDN (e.g., "gateway.example.com").
    /// This is the primary configuration - GatewayUrl is derived from this if not set.
    /// Also sent to Gateway for landing page routing.
    /// </summary>
    public string GatewayFqdn { get; set; } = "";

    /// <summary>
    /// WebSocket URL to connect to the Gateway.
    /// If not set, derived from GatewayFqdn as "wss://{GatewayFqdn}/tunnel".
    /// </summary>
    public string GatewayUrl { get; set; } = "";

    /// <summary>
    /// Gets the effective Gateway URL - either explicitly configured or derived from GatewayFqdn.
    /// </summary>
    public string EffectiveGatewayUrl =>
        !string.IsNullOrEmpty(GatewayUrl) ? GatewayUrl : $"wss://{GatewayFqdn}/tunnel";

    public string ApiKey { get; set; } = "";
    public string JwtSecret { get; set; } = "";
    public AuthOptions Auth { get; set; } = new();
}

public class AuthOptions
{
    /// <summary>
    /// SHA-512 crypt hash of the admin password.
    /// Generate with: openssl passwd -6 "YourPassword"
    /// Format: $6$salt$hash or $6$rounds=N$salt$hash
    /// </summary>
    public string PasswordHash { get; set; } = "";
}
