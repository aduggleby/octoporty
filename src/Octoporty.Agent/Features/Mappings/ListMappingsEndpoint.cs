// ListMappingsEndpoint.cs
// Returns all port mappings ordered by ExternalDomain.
// Requires authentication via JWT bearer token.

using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Octoporty.Agent.Data;

namespace Octoporty.Agent.Features.Mappings;

public class ListMappingsEndpoint : EndpointWithoutRequest<List<MappingResponse>>
{
    private readonly OctoportyDbContext _db;

    public ListMappingsEndpoint(OctoportyDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/api/v1/mappings");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var mappings = await _db.PortMappings
            .OrderBy(m => m.ExternalDomain)
            .Select(m => new MappingResponse
            {
                Id = m.Id,
                Name = m.Description ?? m.ExternalDomain,  // Fallback to domain if no description
                ExternalDomain = m.ExternalDomain,
                InternalHost = m.InternalHost,
                InternalPort = m.InternalPort,
                InternalProtocol = m.InternalUseTls ? "Https" : "Http",
                AllowInvalidCertificates = m.AllowSelfSignedCerts,
                Enabled = m.IsEnabled,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt
            })
            .ToListAsync(ct);

        await Send.OkAsync(mappings, ct);
    }
}
