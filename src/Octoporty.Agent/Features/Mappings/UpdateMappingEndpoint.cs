// UpdateMappingEndpoint.cs
// Updates an existing port mapping configuration.
// Applies same SSRF validation as CreateMappingEndpoint.
// Updates the UpdatedAt timestamp.

using System.Net;
using FastEndpoints;
using FluentValidation;
using Octoporty.Agent.Data;
using Octoporty.Agent.Services;

namespace Octoporty.Agent.Features.Mappings;

public class UpdateMappingFullRequest
{
    public Guid Id { get; set; }
    public required string ExternalDomain { get; set; }
    public required string InternalHost { get; set; }
    public int InternalPort { get; set; }
    public bool InternalUseTls { get; set; }
    public bool AllowSelfSignedCerts { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? Description { get; set; }
}

public class UpdateMappingValidator : Validator<UpdateMappingFullRequest>
{
    // CRITICAL-04/05: SSRF protection - blocked IP ranges (same as Create)
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

    public UpdateMappingValidator()
    {
        RuleFor(x => x.ExternalDomain)
            .NotEmpty()
            .MaximumLength(255)
            .Matches(@"^[a-zA-Z0-9]([a-zA-Z0-9\-\.]*[a-zA-Z0-9])?$")
            .WithMessage("Invalid domain format");

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

public class UpdateMappingEndpoint : Endpoint<UpdateMappingFullRequest, MappingResponse>
{
    private readonly OctoportyDbContext _db;
    private readonly TunnelClient _tunnelClient;
    private readonly ILogger<UpdateMappingEndpoint> _logger;

    public UpdateMappingEndpoint(
        OctoportyDbContext db,
        TunnelClient tunnelClient,
        ILogger<UpdateMappingEndpoint> logger)
    {
        _db = db;
        _tunnelClient = tunnelClient;
        _logger = logger;
    }

    public override void Configure()
    {
        Put("/api/v1/mappings/{id}");
    }

    public override async Task HandleAsync(UpdateMappingFullRequest req, CancellationToken ct)
    {
        var mapping = await _db.PortMappings.FindAsync([req.Id], ct);

        if (mapping is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        mapping.ExternalDomain = req.ExternalDomain;
        mapping.InternalHost = req.InternalHost;
        mapping.InternalPort = req.InternalPort;
        mapping.InternalUseTls = req.InternalUseTls;
        mapping.AllowSelfSignedCerts = req.AllowSelfSignedCerts;
        mapping.IsEnabled = req.IsEnabled;
        mapping.Description = req.Description;
        mapping.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Updated port mapping {Id} for {Domain}", mapping.Id, mapping.ExternalDomain);

        // Resync configuration with Gateway to apply the changes immediately
        try
        {
            await _tunnelClient.ResyncConfigurationAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resync configuration after update");
        }

        await Send.OkAsync(new MappingResponse
        {
            Id = mapping.Id,
            Name = mapping.Description ?? mapping.ExternalDomain,
            ExternalDomain = mapping.ExternalDomain,
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
