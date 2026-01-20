using FastEndpoints;

namespace Octoporty.Agent.Features.Auth;

/// <summary>
/// Logout endpoint - clears auth cookies.
/// </summary>
public class LogoutEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/api/v1/auth/logout");
        AllowAnonymous(); // Allow logout even if token expired
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        // Clear auth cookies
        HttpContext.Response.Cookies.Delete("octoporty_access", new CookieOptions
        {
            Path = "/"
        });

        HttpContext.Response.Cookies.Delete("octoporty_refresh", new CookieOptions
        {
            Path = "/api/v1/auth"
        });

        return Send.NoContentAsync(ct);
    }
}
