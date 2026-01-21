// LoginEndpoint.cs
// Authenticates users and issues JWT access tokens with HttpOnly cookie storage.
// Uses constant-time comparison to prevent timing attacks on credentials.
// Rate limited: 5 failed attempts in 60s triggers 5-minute lockout.
// Returns both cookie and response body tokens for SPA compatibility.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FastEndpoints;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Octoporty.Agent.Services;
using Octoporty.Shared.Options;

namespace Octoporty.Agent.Features.Auth;

public class LoginEndpoint : Endpoint<LoginRequest, LoginResponse>
{
    private readonly AgentOptions _options;
    private readonly ILogger<LoginEndpoint> _logger;
    private readonly LoginRateLimiter _rateLimiter;
    private readonly RefreshTokenStore _tokenStore;

    // HIGH-03: Short-lived access token (15 min) + longer refresh token (7 days)
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    public LoginEndpoint(
        IOptions<AgentOptions> options,
        ILogger<LoginEndpoint> logger,
        LoginRateLimiter rateLimiter,
        RefreshTokenStore tokenStore)
    {
        _options = options.Value;
        _logger = logger;
        _rateLimiter = rateLimiter;
        _tokenStore = tokenStore;
    }

    public override void Configure()
    {
        Post("/api/v1/auth/login");
        AllowAnonymous();
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // MEDIUM-02: Check rate limiting before processing
        if (_rateLimiter.IsBlocked(clientIp))
        {
            var remaining = _rateLimiter.GetLockoutRemaining(clientIp);
            _logger.LogWarning("Rate limited login attempt from {RemoteIp}, locked for {Seconds}s",
                clientIp, remaining?.TotalSeconds);

            HttpContext.Response.Headers["Retry-After"] = ((int)(remaining?.TotalSeconds ?? 300)).ToString();
            HttpContext.Response.StatusCode = 429; // Too Many Requests
            await HttpContext.Response.WriteAsync("Too many login attempts. Please try again later.", ct);
            return;
        }

        // Validate credentials using constant-time comparison
        var expectedUsername = Encoding.UTF8.GetBytes(_options.Auth.Username);
        var providedUsername = Encoding.UTF8.GetBytes(req.Username);
        var usernameValid = CryptographicOperations.FixedTimeEquals(expectedUsername, providedUsername);

        var expectedPassword = Encoding.UTF8.GetBytes(_options.Auth.Password);
        var providedPassword = Encoding.UTF8.GetBytes(req.Password);
        var passwordValid = CryptographicOperations.FixedTimeEquals(expectedPassword, providedPassword);

        if (!usernameValid || !passwordValid)
        {
            // HIGH-04: Don't log username to prevent enumeration
            _logger.LogWarning("Failed login attempt from {RemoteIp}", clientIp);
            _rateLimiter.RecordFailedAttempt(clientIp);
            await Send.UnauthorizedAsync(ct);
            return;
        }

        // Success - clear rate limit tracking
        _rateLimiter.RecordSuccess(clientIp);

        // Generate access token (short-lived)
        var accessToken = GenerateAccessToken(req.Username);
        var accessExpiresAt = DateTime.UtcNow.Add(AccessTokenLifetime);

        // Generate refresh token (longer-lived)
        var refreshToken = GenerateRefreshToken();
        var refreshExpiresAt = DateTime.UtcNow.Add(RefreshTokenLifetime);

        // Store refresh token for validation
        _tokenStore.Store(refreshToken, req.Username, refreshExpiresAt);

        // HIGH-01/02: Set tokens in HttpOnly cookies for security
        SetAuthCookies(accessToken, accessExpiresAt, refreshToken, refreshExpiresAt);

        _logger.LogInformation("User logged in successfully from {RemoteIp}", clientIp);

        // Also return tokens in response body for SPA compatibility
        await Send.OkAsync(new LoginResponse
        {
            Token = accessToken,
            ExpiresAt = accessExpiresAt,
            RefreshToken = refreshToken,
            RefreshExpiresAt = refreshExpiresAt
        }, ct);
    }

    private string GenerateAccessToken(string username)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(JwtRegisteredClaimNames.Sub, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("token_type", "access")
        };

        var token = new JwtSecurityToken(
            issuer: "Octoporty.Agent",
            audience: "Octoporty.Agent.Web",
            claims: claims,
            expires: DateTime.UtcNow.Add(AccessTokenLifetime),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private void SetAuthCookies(string accessToken, DateTime accessExpires, string refreshToken, DateTime refreshExpires)
    {
        var secureCookie = !HttpContext.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);

        // HIGH-01: HttpOnly cookies prevent XSS token theft
        // HIGH-02: SameSite=Strict provides CSRF protection
        HttpContext.Response.Cookies.Append("octoporty_access", accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secureCookie,
            SameSite = SameSiteMode.Strict,
            Expires = accessExpires,
            Path = "/"
        });

        HttpContext.Response.Cookies.Append("octoporty_refresh", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = secureCookie,
            SameSite = SameSiteMode.Strict,
            Expires = refreshExpires,
            Path = "/api/v1/auth" // Only sent to auth endpoints
        });
    }
}
