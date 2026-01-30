// Models.cs
// DTOs for Settings endpoints including landing page operations.

namespace Octoporty.Agent.Features.Settings;

public class LandingPageResponse
{
    public required string Html { get; init; }
    public required string Hash { get; init; }
    public required bool IsDefault { get; init; }
}

public class UpdateLandingPageRequest
{
    public required string Html { get; init; }
}

public class UpdateLandingPageResponse
{
    public required string Hash { get; init; }
}
