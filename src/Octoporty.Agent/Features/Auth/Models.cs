namespace Octoporty.Agent.Features.Auth;

public record LoginRequest
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}

public record LoginResponse
{
    public required string Token { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required string RefreshToken { get; init; }
    public required DateTime RefreshExpiresAt { get; init; }
}

public record RefreshTokenRequest
{
    public required string RefreshToken { get; init; }
}

public record RefreshTokenResponse
{
    public required string Token { get; init; }
    public required DateTime ExpiresAt { get; init; }
}
