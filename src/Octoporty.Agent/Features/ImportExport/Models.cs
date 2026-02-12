// Models.cs
// Import/Export DTOs for backing up and restoring Agent definitions.
// Currently supports exporting/importing port mappings and optional landing page HTML.

namespace Octoporty.Agent.Features.ImportExport;

public sealed class AgentDefinitionsExportV1
{
    public string SchemaVersion { get; init; } = "1";
    public DateTime ExportedAtUtc { get; init; } = DateTime.UtcNow;
    public required PortMappingDefinitionV1[] Mappings { get; init; }
    public string? LandingPageHtml { get; init; }
    public bool LandingPageIsDefault { get; init; }
}

public sealed class PortMappingDefinitionV1
{
    public string? Id { get; init; }
    public required string ExternalDomain { get; init; }
    public required string InternalHost { get; init; }
    public int InternalPort { get; init; }
    public bool InternalUseTls { get; init; }
    public bool AllowSelfSignedCerts { get; init; }
    public bool IsEnabled { get; init; } = true;
    public string? Description { get; init; }
}

public sealed class AgentDefinitionsImportV1
{
    public string SchemaVersion { get; init; } = "1";
    public required PortMappingDefinitionV1[] Mappings { get; init; }
    public string? LandingPageHtml { get; init; }
    public bool? LandingPageIsDefault { get; init; }
}

public sealed class AgentDefinitionsImportResponse
{
    public required bool Success { get; init; }
    public string? Error { get; init; }
    public int Created { get; init; }
    public int Updated { get; init; }
    public int Skipped { get; init; }
    public required string[] Errors { get; init; }
}

