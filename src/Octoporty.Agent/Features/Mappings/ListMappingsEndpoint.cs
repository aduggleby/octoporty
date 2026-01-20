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
        Get("/api/mappings");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var mappings = await _db.PortMappings
            .OrderBy(m => m.ExternalDomain)
            .Select(m => new MappingResponse
            {
                Id = m.Id,
                ExternalDomain = m.ExternalDomain,
                ExternalPort = m.ExternalPort,
                InternalHost = m.InternalHost,
                InternalPort = m.InternalPort,
                InternalUseTls = m.InternalUseTls,
                AllowSelfSignedCerts = m.AllowSelfSignedCerts,
                IsEnabled = m.IsEnabled,
                Description = m.Description,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt
            })
            .ToListAsync(ct);

        await Send.OkAsync(mappings, ct);
    }
}
