namespace Octoporty.Agent.Features.Mappings;

public record CreateMappingRequest
{
    public required string ExternalDomain { get; init; }
    public int ExternalPort { get; init; } = 443;
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
    public int ExternalPort { get; init; } = 443;
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
    public required string ExternalDomain { get; init; }
    public int ExternalPort { get; init; }
    public required string InternalHost { get; init; }
    public int InternalPort { get; init; }
    public bool InternalUseTls { get; init; }
    public bool AllowSelfSignedCerts { get; init; }
    public bool IsEnabled { get; init; }
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
