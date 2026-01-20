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
        Get("/api/mappings/{id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetMappingRequest req, CancellationToken ct)
    {
        var mapping = await _db.PortMappings
            .Where(m => m.Id == req.Id)
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
            .FirstOrDefaultAsync(ct);

        if (mapping is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(mapping, ct);
    }
}
