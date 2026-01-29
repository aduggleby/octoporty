// PortMapping.cs
// Entity representing a tunnel port mapping configuration.
// Maps external domain/port to internal host/port with TLS options.
// ExternalDomain must be unique - enforced by database constraint.

namespace Octoporty.Shared.Entities;

public class PortMapping
{
    public Guid Id { get; set; }
    public required string ExternalDomain { get; set; }
    public required string InternalHost { get; set; }
    public int InternalPort { get; set; }
    public bool InternalUseTls { get; set; }
    public bool AllowSelfSignedCerts { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
