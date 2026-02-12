// ImportDefinitionsEndpoint.cs
// Imports Agent definitions (currently: port mappings + optional landing page HTML) from JSON.
// Import behavior is merge-only: upserts by ExternalDomain and does not delete missing mappings.

using System.Net;
using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Octoporty.Agent.Data;
using Octoporty.Agent.Services;
using Octoporty.Shared.Entities;
using System.Text.Json;

namespace Octoporty.Agent.Features.ImportExport;

public sealed class ImportDefinitionsValidator : Validator<AgentDefinitionsImportV1>
{
    public ImportDefinitionsValidator()
    {
        RuleFor(x => x.SchemaVersion)
            .NotEmpty()
            .Must(v => v == "1")
            .WithMessage("Unsupported schema version");

        RuleFor(x => x.Mappings)
            .NotNull();
    }
}

public sealed class ImportDefinitionsEndpoint : Endpoint<AgentDefinitionsImportV1, AgentDefinitionsImportResponse>
{
    private readonly OctoportyDbContext _db;
    private readonly TunnelClient _tunnelClient;
    private readonly LandingPageService _landingPageService;
    private readonly ILogger<ImportDefinitionsEndpoint> _logger;

    // SSRF protection: align with Create/Update mapping validators.
    private static readonly string[] BlockedHostPatterns =
    [
        "localhost",
        "127.",
        "0.0.0.0",
        "169.254.",
        "metadata.",
        "metadata",
        "::1",
        "[::1]"
    ];

    public ImportDefinitionsEndpoint(
        OctoportyDbContext db,
        TunnelClient tunnelClient,
        LandingPageService landingPageService,
        ILogger<ImportDefinitionsEndpoint> logger)
    {
        _db = db;
        _tunnelClient = tunnelClient;
        _landingPageService = landingPageService;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/api/v1/import-export/import");
        Description(d => d
            .WithSummary("Import Agent Definitions")
            .WithDescription("Imports port mappings and landing page HTML from JSON. Upserts by ExternalDomain; does not delete."));
    }

    public override async Task HandleAsync(AgentDefinitionsImportV1 req, CancellationToken ct)
    {
        if (req.SchemaVersion != "1")
        {
            HttpContext.Response.StatusCode = 400;
            HttpContext.Response.ContentType = "application/json";
            await HttpContext.Response.WriteAsync(JsonSerializer.Serialize(new AgentDefinitionsImportResponse
            {
                Success = false,
                Error = $"Unsupported schema version '{req.SchemaVersion}'",
                Created = 0,
                Updated = 0,
                Skipped = 0,
                Errors = []
            }), ct);
            return;
        }

        var errors = new List<string>();
        var created = 0;
        var updated = 0;
        var skipped = 0;

        // De-dupe by ExternalDomain (last one wins).
        var byDomain = new Dictionary<string, PortMappingDefinitionV1>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in req.Mappings ?? [])
        {
            if (string.IsNullOrWhiteSpace(m.ExternalDomain))
                continue;
            byDomain[m.ExternalDomain.Trim()] = m;
        }

        foreach (var kv in byDomain)
        {
            var m = kv.Value;
            var domain = m.ExternalDomain.Trim();

            if (!IsValidDomain(domain))
            {
                skipped++;
                errors.Add($"Skipping mapping '{domain}': invalid ExternalDomain format");
                continue;
            }

            if (!IsValidInternalHost(m.InternalHost))
            {
                skipped++;
                errors.Add($"Skipping mapping '{domain}': invalid or blocked InternalHost");
                continue;
            }

            if (m.InternalPort is < 1 or > 65535)
            {
                skipped++;
                errors.Add($"Skipping mapping '{domain}': InternalPort must be between 1 and 65535");
                continue;
            }

            var existing = await _db.PortMappings
                .FirstOrDefaultAsync(x => x.ExternalDomain == domain, ct);

            if (existing is null)
            {
                var entity = new PortMapping
                {
                    Id = Guid.NewGuid(),
                    ExternalDomain = domain,
                    InternalHost = m.InternalHost.Trim(),
                    InternalPort = m.InternalPort,
                    InternalUseTls = m.InternalUseTls,
                    AllowSelfSignedCerts = m.AllowSelfSignedCerts,
                    IsEnabled = m.IsEnabled,
                    Description = string.IsNullOrWhiteSpace(m.Description) ? null : m.Description.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.PortMappings.Add(entity);
                created++;
            }
            else
            {
                existing.InternalHost = m.InternalHost.Trim();
                existing.InternalPort = m.InternalPort;
                existing.InternalUseTls = m.InternalUseTls;
                existing.AllowSelfSignedCerts = m.AllowSelfSignedCerts;
                existing.IsEnabled = m.IsEnabled;
                existing.Description = string.IsNullOrWhiteSpace(m.Description) ? null : m.Description.Trim();
                existing.UpdatedAt = DateTime.UtcNow;
                updated++;
            }
        }

        // Landing page import
        if (req.LandingPageIsDefault is true)
        {
            try
            {
                await _landingPageService.ResetToDefaultAsync();
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to reset landing page: {ex.Message}");
            }
        }
        else if (!string.IsNullOrWhiteSpace(req.LandingPageHtml))
        {
            try
            {
                await _landingPageService.SetLandingPageAsync(req.LandingPageHtml);
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to import landing page HTML: {ex.Message}");
            }
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Failed to save imported definitions");
            HttpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            HttpContext.Response.ContentType = "application/json";
            await HttpContext.Response.WriteAsync(JsonSerializer.Serialize(new AgentDefinitionsImportResponse
            {
                Success = false,
                Error = "Failed to save definitions to database",
                Created = created,
                Updated = updated,
                Skipped = skipped,
                Errors = errors.Append(ex.Message).ToArray()
            }), ct);
            return;
        }

        // Resync after import so changes apply immediately.
        try
        {
            await _tunnelClient.ResyncConfigurationAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resync configuration after import");
            errors.Add($"Failed to resync configuration: {ex.Message}");
        }

        await Send.OkAsync(new AgentDefinitionsImportResponse
        {
            Success = true,
            Created = created,
            Updated = updated,
            Skipped = skipped,
            Errors = errors.ToArray()
        }, ct);
    }

    private static bool IsValidDomain(string domain)
    {
        // Keep consistent with mapping validators (basic DNS-ish validation, not full RFC).
        // Must start/end with alnum, allow internal hyphens/dots.
        if (domain.Length is < 1 or > 255)
            return false;

        return System.Text.RegularExpressions.Regex.IsMatch(
            domain,
            @"^[a-zA-Z0-9]([a-zA-Z0-9\-\.]*[a-zA-Z0-9])?$");
    }

    private static bool IsValidInternalHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        var lowerHost = host.ToLowerInvariant().Trim();
        foreach (var pattern in BlockedHostPatterns)
        {
            if (lowerHost.StartsWith(pattern, StringComparison.OrdinalIgnoreCase) ||
                lowerHost.Equals(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (IPAddress.TryParse(host, out var ip))
        {
            if (IPAddress.IsLoopback(ip))
                return false;

            var bytes = ip.GetAddressBytes();
            if (bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254)
                return false;

            if (ip.Equals(IPAddress.Any))
                return false;
        }

        return true;
    }
}
