using FastEndpoints;
using FluentValidation;
using Octoporty.Agent.Data;
using Octoporty.Shared.Entities;

namespace Octoporty.Agent.Features.Mappings;

public class CreateMappingValidator : Validator<CreateMappingRequest>
{
    public CreateMappingValidator()
    {
        RuleFor(x => x.ExternalDomain)
            .NotEmpty()
            .MaximumLength(255)
            .Matches(@"^[a-zA-Z0-9]([a-zA-Z0-9\-\.]*[a-zA-Z0-9])?$")
            .WithMessage("Invalid domain format");

        RuleFor(x => x.ExternalPort)
            .InclusiveBetween(1, 65535);

        RuleFor(x => x.InternalHost)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(x => x.InternalPort)
            .InclusiveBetween(1, 65535);
    }
}

public class CreateMappingEndpoint : Endpoint<CreateMappingRequest, MappingResponse>
{
    private readonly OctoportyDbContext _db;
    private readonly ILogger<CreateMappingEndpoint> _logger;

    public CreateMappingEndpoint(OctoportyDbContext db, ILogger<CreateMappingEndpoint> logger)
    {
        _db = db;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/api/mappings");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreateMappingRequest req, CancellationToken ct)
    {
        var mapping = new PortMapping
        {
            Id = Guid.NewGuid(),
            ExternalDomain = req.ExternalDomain,
            ExternalPort = req.ExternalPort,
            InternalHost = req.InternalHost,
            InternalPort = req.InternalPort,
            InternalUseTls = req.InternalUseTls,
            AllowSelfSignedCerts = req.AllowSelfSignedCerts,
            IsEnabled = req.IsEnabled,
            Description = req.Description
        };

        _db.PortMappings.Add(mapping);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created port mapping {Id} for {Domain}", mapping.Id, mapping.ExternalDomain);

        await Send.CreatedAtAsync<GetMappingEndpoint>(
            new { id = mapping.Id },
            new MappingResponse
            {
                Id = mapping.Id,
                ExternalDomain = mapping.ExternalDomain,
                ExternalPort = mapping.ExternalPort,
                InternalHost = mapping.InternalHost,
                InternalPort = mapping.InternalPort,
                InternalUseTls = mapping.InternalUseTls,
                AllowSelfSignedCerts = mapping.AllowSelfSignedCerts,
                IsEnabled = mapping.IsEnabled,
                Description = mapping.Description,
                CreatedAt = mapping.CreatedAt,
                UpdatedAt = mapping.UpdatedAt
            },
            cancellation: ct);
    }
}
