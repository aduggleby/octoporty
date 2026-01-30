// UpdateLandingPageEndpoint.cs
// Updates the landing page HTML and triggers a resync with the Gateway.
// The new landing page will be synced on the next tunnel sync cycle.

using FastEndpoints;
using FluentValidation;
using Octoporty.Agent.Services;

namespace Octoporty.Agent.Features.Settings;

public class UpdateLandingPageValidator : Validator<UpdateLandingPageRequest>
{
    public UpdateLandingPageValidator()
    {
        RuleFor(x => x.Html)
            .NotEmpty()
            .WithMessage("HTML content is required")
            .MaximumLength(1_000_000)
            .WithMessage("HTML content is too large (max 1MB)");
    }
}

public class UpdateLandingPageEndpoint : Endpoint<UpdateLandingPageRequest, UpdateLandingPageResponse>
{
    private readonly LandingPageService _landingPageService;
    private readonly TunnelClient _tunnelClient;
    private readonly ILogger<UpdateLandingPageEndpoint> _logger;

    public UpdateLandingPageEndpoint(
        LandingPageService landingPageService,
        TunnelClient tunnelClient,
        ILogger<UpdateLandingPageEndpoint> logger)
    {
        _landingPageService = landingPageService;
        _tunnelClient = tunnelClient;
        _logger = logger;
    }

    public override void Configure()
    {
        Put("/api/v1/settings/landing-page");
    }

    public override async Task HandleAsync(UpdateLandingPageRequest req, CancellationToken ct)
    {
        var hash = await _landingPageService.SetLandingPageAsync(req.Html);

        _logger.LogInformation("Landing page updated, hash: {Hash}", hash);

        // Resync to push the new landing page to the Gateway
        try
        {
            await _tunnelClient.ResyncConfigurationAsync(ct);
            _logger.LogInformation("Configuration resynced after landing page update");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resync configuration after landing page update");
        }

        await Send.OkAsync(new UpdateLandingPageResponse { Hash = hash }, ct);
    }
}
