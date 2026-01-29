// Models.cs
// Request/response DTOs for port mapping CRUD endpoints.
// Defines ExternalDomain→InternalHost:Port tunnel configuration.

namespace Octoporty.Agent.Features.Mappings;

public record CreateMappingRequest
{
    public required string ExternalDomain { get; init; }
    public required string InternalHost { get; init; }
    public int InternalPort { get; init; }
    public bool InternalUseTls { get; init; }
    public bool AllowSelfSignedCerts { get; init; }
    public bool IsEnabled { get; init; } = true;
    public string? Description { get; init; }
}

public record UpdateMappingRequest
{
    public required string ExternalDomain { get; init; }
    public required string InternalHost { get; init; }
    public int InternalPort { get; init; }
    public bool InternalUseTls { get; init; }
    public bool AllowSelfSignedCerts { get; init; }
    public bool IsEnabled { get; init; } = true;
    public string? Description { get; init; }
}

public record MappingResponse
{
    public Guid Id { get; init; }
    public required string Name { get; init; }  // Maps to Description in DB, required for UI
    public required string ExternalDomain { get; init; }
    public required string InternalHost { get; init; }
    public int InternalPort { get; init; }
    public required string InternalProtocol { get; init; }  // "Http" or "Https"
    public bool AllowInvalidCertificates { get; init; }  // Maps to AllowSelfSignedCerts
    public bool Enabled { get; init; }  // Maps to IsEnabled
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public record ToggleMappingRequest
{
    public bool Enabled { get; init; }
}
