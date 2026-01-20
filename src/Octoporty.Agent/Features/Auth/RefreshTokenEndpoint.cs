using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FastEndpoints;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Octoporty.Shared.Options;

namespace Octoporty.Agent.Features.Auth;

/// <summary>
/// HIGH-03: Token refresh endpoint for extending sessions without re-authentication.
/// Validates refresh token and issues new short-lived access token.
/// </summary>
public class RefreshTokenEndpoint : Endpoint<RefreshTokenRequest, RefreshTokenResponse>
{
    private readonly AgentOptions _options;
    private readonly ILogger<RefreshTokenEndpoint> _logger;
    private readonly RefreshTokenStore _tokenStore;

    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    public RefreshTokenEndpoint(
        IOptions<AgentOptions> options,
        ILogger<RefreshTokenEndpoint> logger,
        RefreshTokenStore tokenStore)
    {
        _options = options.Value;
        _logger = logger;
        _tokenStore = tokenStore;
    }

    public override void Configure()
    {
        Post("/api/v1/auth/refresh");
        AllowAnonymous(); // Uses refresh token instead of access token
    }

    public override async Task HandleAsync(RefreshTokenRequest req, CancellationToken ct)
    {
        // Try cookie first, then request body
        var refreshToken = HttpContext.Request.Cookies["octoporty_refresh"] ?? req.RefreshToken;

        if (string.IsNullOrEmpty(refreshToken))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        // Validate refresh token
        var username = _tokenStore.ValidateAndConsume(refreshToken);
        if (username == null)
        {
            _logger.LogWarning("Invalid or expired refresh token from {RemoteIp}",
                HttpContext.Connection.RemoteIpAddress);
            await Send.UnauthorizedAsync(ct);
            return;
        }

        // Generate new access token
        var accessToken = GenerateAccessToken(username);
        var accessExpiresAt = DateTime.UtcNow.Add(AccessTokenLifetime);

        // Generate new refresh token (rotation for security)
        var newRefreshToken = GenerateRefreshToken();
        var refreshExpiresAt = DateTime.UtcNow.AddDays(7);
        _tokenStore.Store(newRefreshToken, username, refreshExpiresAt);

        // Set cookies
        SetAuthCookies(accessToken, accessExpiresAt, newRefreshToken, refreshExpiresAt);

        await Send.OkAsync(new RefreshTokenResponse
        {
            Token = accessToken,
            ExpiresAt = accessExpiresAt
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
            Path = "/api/v1/auth"
        });
    }
}

/// <summary>
/// In-memory refresh token store. In production, consider using a distributed cache.
/// </summary>
public class RefreshTokenStore
{
    private readonly ConcurrentDictionary<string, (string Username, DateTime ExpiresAt)> _tokens = new();

    public void Store(string token, string username, DateTime expiresAt)
    {
        _tokens[token] = (username, expiresAt);
        CleanupExpired();
    }

    public string? ValidateAndConsume(string token)
    {
        if (_tokens.TryRemove(token, out var entry))
        {
            if (entry.ExpiresAt > DateTime.UtcNow)
            {
                return entry.Username;
            }
        }
        return null;
    }

    private void CleanupExpired()
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _tokens)
        {
            if (kvp.Value.ExpiresAt <= now)
            {
                _tokens.TryRemove(kvp.Key, out _);
            }
        }
    }
}
