// DeleteMappingEndpoint.cs
// Deletes a port mapping by ID.
// Returns 404 if mapping not found, 204 on success.

using FastEndpoints;
using Octoporty.Agent.Data;
using Octoporty.Agent.Services;

namespace Octoporty.Agent.Features.Mappings;

public class DeleteMappingRequest
{
    public Guid Id { get; set; }
}

public class DeleteMappingEndpoint : Endpoint<DeleteMappingRequest>
{
    private readonly OctoportyDbContext _db;
    private readonly TunnelClient _tunnelClient;
    private readonly ILogger<DeleteMappingEndpoint> _logger;

    public DeleteMappingEndpoint(
        OctoportyDbContext db,
        TunnelClient tunnelClient,
        ILogger<DeleteMappingEndpoint> logger)
    {
        _db = db;
        _tunnelClient = tunnelClient;
        _logger = logger;
    }

    public override void Configure()
    {
        Delete("/api/v1/mappings/{id}");
    }

    public override async Task HandleAsync(DeleteMappingRequest req, CancellationToken ct)
    {
        var mapping = await _db.PortMappings.FindAsync([req.Id], ct);

        if (mapping is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        _db.PortMappings.Remove(mapping);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted port mapping {Id} for {Domain}", mapping.Id, mapping.ExternalDomain);

        // Resync configuration with Gateway to remove the deleted mapping
        try
        {
            await _tunnelClient.ResyncConfigurationAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resync configuration after delete");
        }

        await Send.NoContentAsync(ct);
    }
}
