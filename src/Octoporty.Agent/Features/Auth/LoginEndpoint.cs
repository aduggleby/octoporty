using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FastEndpoints;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Octoporty.Shared.Options;

namespace Octoporty.Agent.Features.Auth;

public class LoginEndpoint : Endpoint<LoginRequest, LoginResponse>
{
    private readonly AgentOptions _options;
    private readonly ILogger<LoginEndpoint> _logger;

    public LoginEndpoint(IOptions<AgentOptions> options, ILogger<LoginEndpoint> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/api/v1/auth/login");
        AllowAnonymous();
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        // Validate credentials using constant-time comparison
        var expectedUsername = Encoding.UTF8.GetBytes(_options.Auth.Username);
        var providedUsername = Encoding.UTF8.GetBytes(req.Username);
        var usernameValid = CryptographicOperations.FixedTimeEquals(expectedUsername, providedUsername);

        var expectedPassword = Encoding.UTF8.GetBytes(_options.Auth.Password);
        var providedPassword = Encoding.UTF8.GetBytes(req.Password);
        var passwordValid = CryptographicOperations.FixedTimeEquals(expectedPassword, providedPassword);

        if (!usernameValid || !passwordValid)
        {
            _logger.LogWarning("Failed login attempt for user: {Username}", req.Username);
            await Send.UnauthorizedAsync(ct);
            return;
        }

        // Generate JWT token
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddHours(24);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, req.Username),
            new Claim(JwtRegisteredClaimNames.Sub, req.Username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: "Octoporty.Agent",
            audience: "Octoporty.Agent.Web",
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        _logger.LogInformation("User {Username} logged in successfully", req.Username);

        await Send.OkAsync(new LoginResponse
        {
            Token = tokenString,
            ExpiresAt = expiresAt
        }, ct);
    }
}
