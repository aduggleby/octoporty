// ExportDefinitionsEndpoint.cs
// Exports Agent definitions (mappings + optional landing page HTML) as JSON for backup/migration.

using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Octoporty.Agent.Data;
using Octoporty.Agent.Services;

namespace Octoporty.Agent.Features.ImportExport;

public sealed class ExportDefinitionsEndpoint : EndpointWithoutRequest<AgentDefinitionsExportV1>
{
    private readonly OctoportyDbContext _db;
    private readonly LandingPageService _landingPageService;

    public ExportDefinitionsEndpoint(OctoportyDbContext db, LandingPageService landingPageService)
    {
        _db = db;
        _landingPageService = landingPageService;
    }

    public override void Configure()
    {
        Get("/api/v1/import-export/export");
        Description(d => d
            .WithSummary("Export Agent Definitions")
            .WithDescription("Exports port mappings and landing page HTML for backup/import."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var mappings = await _db.PortMappings
            .AsNoTracking()
            .OrderBy(m => m.ExternalDomain)
            .Select(m => new PortMappingDefinitionV1
            {
                Id = m.Id.ToString(),
                ExternalDomain = m.ExternalDomain,
                InternalHost = m.InternalHost,
                InternalPort = m.InternalPort,
                InternalUseTls = m.InternalUseTls,
                AllowSelfSignedCerts = m.AllowSelfSignedCerts,
                IsEnabled = m.IsEnabled,
                Description = m.Description
            })
            .ToArrayAsync(ct);

        var hasCustomLandingPage = await _db.Settings.AsNoTracking()
            .AnyAsync(s => s.Key == "LandingPageHtml", ct);

        string? landingPageHtml = null;
        if (hasCustomLandingPage)
        {
            // Only include HTML if it's custom; default can be regenerated from code.
            var (html, _) = await _landingPageService.GetLandingPageAsync();
            landingPageHtml = html;
        }

        await Send.OkAsync(new AgentDefinitionsExportV1
        {
            SchemaVersion = "1",
            ExportedAtUtc = DateTime.UtcNow,
            Mappings = mappings,
            LandingPageHtml = landingPageHtml,
            LandingPageIsDefault = !hasCustomLandingPage
        }, ct);
    }
}

