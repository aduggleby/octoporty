// LogoutEndpoint.cs
// Clears authentication cookies to log out the user.
// AllowAnonymous to allow logout even if access token expired.

using FastEndpoints;

namespace Octoporty.Agent.Features.Auth;
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
