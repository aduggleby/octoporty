// ToggleMappingEndpoint.cs
// Toggles the enabled state of a port mapping.
// Returns the updated mapping or 404 if not found.

using FastEndpoints;
using Octoporty.Agent.Data;
using Octoporty.Agent.Services;

namespace Octoporty.Agent.Features.Mappings;

public class ToggleMappingFullRequest
{
    public Guid Id { get; set; }
    public bool Enabled { get; set; }
}

public class ToggleMappingEndpoint : Endpoint<ToggleMappingFullRequest, MappingResponse>
{
    private readonly OctoportyDbContext _db;
    private readonly TunnelClient _tunnelClient;
    private readonly ILogger<ToggleMappingEndpoint> _logger;

    public ToggleMappingEndpoint(
        OctoportyDbContext db,
        TunnelClient tunnelClient,
        ILogger<ToggleMappingEndpoint> logger)
    {
        _db = db;
        _tunnelClient = tunnelClient;
        _logger = logger;
    }

    public override void Configure()
    {
        Patch("/api/v1/mappings/{id}/toggle");
    }

    public override async Task HandleAsync(ToggleMappingFullRequest req, CancellationToken ct)
    {
        var mapping = await _db.PortMappings.FindAsync([req.Id], ct);

        if (mapping is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        mapping.IsEnabled = req.Enabled;
        mapping.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Toggled port mapping {Id} to {State}", mapping.Id, req.Enabled ? "enabled" : "disabled");

        // Resync configuration with Gateway
        try
        {
            await _tunnelClient.ResyncConfigurationAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resync configuration after toggle");
        }

        await Send.OkAsync(new MappingResponse
        {
            Id = mapping.Id,
            Name = mapping.Description ?? mapping.ExternalDomain,
            ExternalDomain = mapping.ExternalDomain,
            ExternalPort = mapping.ExternalPort,
            InternalHost = mapping.InternalHost,
            InternalPort = mapping.InternalPort,
            InternalProtocol = mapping.InternalUseTls ? "Https" : "Http",
            AllowInvalidCertificates = mapping.AllowSelfSignedCerts,
            Enabled = mapping.IsEnabled,
            CreatedAt = mapping.CreatedAt,
            UpdatedAt = mapping.UpdatedAt
        }, ct);
    }
}
