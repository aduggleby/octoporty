// CreateMappingEndpoint.cs
// Creates new port mapping configurations.
// Validates against SSRF attacks by blocking localhost, 127.x.x.x, 169.254.x.x (cloud metadata).
// ExternalDomain must be unique - enforced by database constraint.

using System.Net;
using FastEndpoints;
using FluentValidation;
using Octoporty.Agent.Data;
using Octoporty.Shared.Entities;

namespace Octoporty.Agent.Features.Mappings;

public class CreateMappingValidator : Validator<CreateMappingRequest>
{
    // CRITICAL-04/05: SSRF protection - blocked IP ranges
    private static readonly string[] BlockedHostPatterns =
    [
        "localhost",
        "127.",
        "0.0.0.0",
        "169.254.",      // Link-local / cloud metadata
        "metadata.",     // Cloud metadata services
        "metadata",
        "::1",           // IPv6 localhost
        "[::1]"
    ];

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
            .MaximumLength(255)
            .Must(BeValidInternalHost)
            .WithMessage("Invalid or blocked internal host. Cloud metadata endpoints and localhost are not allowed.");

        RuleFor(x => x.InternalPort)
            .InclusiveBetween(1, 65535);
    }

    private static bool BeValidInternalHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;

        var lowerHost = host.ToLowerInvariant().Trim();

        // Check against blocked patterns
        foreach (var pattern in BlockedHostPatterns)
        {
            if (lowerHost.StartsWith(pattern, StringComparison.OrdinalIgnoreCase) ||
                lowerHost.Equals(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Try to parse as IP and check for blocked ranges
        if (IPAddress.TryParse(host, out var ip))
        {
            // Block loopback
            if (IPAddress.IsLoopback(ip))
                return false;

            // Block link-local (169.254.x.x)
            var bytes = ip.GetAddressBytes();
            if (bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254)
                return false;

            // Block 0.0.0.0
            if (ip.Equals(IPAddress.Any))
                return false;
        }

        return true;
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
        Post("/api/v1/mappings");
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
            },
            cancellation: ct);
    }
}
