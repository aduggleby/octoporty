// GetLandingPageEndpoint.cs
// Retrieves the current landing page HTML and its MD5 hash.
// Returns IsDefault=true if using the default landing page.

using FastEndpoints;
using Octoporty.Agent.Data;
using Octoporty.Agent.Services;

namespace Octoporty.Agent.Features.Settings;

public class GetLandingPageEndpoint : EndpointWithoutRequest<LandingPageResponse>
{
    private readonly LandingPageService _landingPageService;
    private readonly OctoportyDbContext _db;

    public GetLandingPageEndpoint(LandingPageService landingPageService, OctoportyDbContext db)
    {
        _landingPageService = landingPageService;
        _db = db;
    }

    public override void Configure()
    {
        Get("/api/v1/settings/landing-page");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var (html, hash) = await _landingPageService.GetLandingPageAsync();

        // Check if a custom landing page exists in the database
        var hasCustomPage = await _db.Settings.FindAsync(["LandingPageHtml"], ct) is not null;

        await Send.OkAsync(new LandingPageResponse
        {
            Html = html,
            Hash = hash,
            IsDefault = !hasCustomPage
        }, ct);
    }
}
