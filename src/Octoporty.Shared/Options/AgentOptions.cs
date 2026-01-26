// AgentOptions.cs
// Configuration options for the Octoporty Agent service.
// Includes Gateway connection URL, API key, JWT secret, and auth credentials.
// JwtSecret and Password are validated at startup (minimum lengths enforced).

namespace Octoporty.Shared.Options;

public class AgentOptions
{
    public string GatewayUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string JwtSecret { get; set; } = "";
    public AuthOptions Auth { get; set; } = new();
}

public class AuthOptions
{
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "";
}
