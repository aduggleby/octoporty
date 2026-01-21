// GetMappingEndpoint.cs
// Retrieves a single port mapping by ID.
// Returns 404 if mapping not found.

using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Octoporty.Agent.Data;

namespace Octoporty.Agent.Features.Mappings;

public class GetMappingRequest
{
    public Guid Id { get; set; }
}

public class GetMappingEndpoint : Endpoint<GetMappingRequest, MappingResponse>
{
    private readonly OctoportyDbContext _db;

    public GetMappingEndpoint(OctoportyDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/api/v1/mappings/{id}");
    }

    public override async Task HandleAsync(GetMappingRequest req, CancellationToken ct)
    {
        var mapping = await _db.PortMappings
            .Where(m => m.Id == req.Id)
            .Select(m => new MappingResponse
            {
                Id = m.Id,
                Name = m.Description ?? m.ExternalDomain,
                ExternalDomain = m.ExternalDomain,
                ExternalPort = m.ExternalPort,
                InternalHost = m.InternalHost,
                InternalPort = m.InternalPort,
                InternalProtocol = m.InternalUseTls ? "Https" : "Http",
                AllowInvalidCertificates = m.AllowSelfSignedCerts,
                Enabled = m.IsEnabled,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt
            })
            .FirstOrDefaultAsync(ct);

        if (mapping is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(mapping, ct);
    }
}
