// ResetLandingPageEndpoint.cs
// Resets the landing page to the default Octoporty branding.
// Triggers a resync to update the Gateway.

using FastEndpoints;
using Octoporty.Agent.Services;

namespace Octoporty.Agent.Features.Settings;

public class ResetLandingPageEndpoint : EndpointWithoutRequest<LandingPageResponse>
{
    private readonly LandingPageService _landingPageService;
    private readonly TunnelClient _tunnelClient;
    private readonly ILogger<ResetLandingPageEndpoint> _logger;

    public ResetLandingPageEndpoint(
        LandingPageService landingPageService,
        TunnelClient tunnelClient,
        ILogger<ResetLandingPageEndpoint> logger)
    {
        _landingPageService = landingPageService;
        _tunnelClient = tunnelClient;
        _logger = logger;
    }

    public override void Configure()
    {
        Delete("/api/v1/settings/landing-page");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var (html, hash) = await _landingPageService.ResetToDefaultAsync();

        _logger.LogInformation("Landing page reset to default, hash: {Hash}", hash);

        // Resync to push the default landing page to the Gateway
        try
        {
            await _tunnelClient.ResyncConfigurationAsync(ct);
            _logger.LogInformation("Configuration resynced after landing page reset");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resync configuration after landing page reset");
        }

        await Send.OkAsync(new LandingPageResponse
        {
            Html = html,
            Hash = hash,
            IsDefault = true
        }, ct);
    }
}
