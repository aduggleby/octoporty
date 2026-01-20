namespace Octoporty.Shared.Entities;

public class PortMapping
{
    public Guid Id { get; set; }
    public required string ExternalDomain { get; set; }
    public int ExternalPort { get; set; } = 443;
    public required string InternalHost { get; set; }
    public int InternalPort { get; set; }
    public bool InternalUseTls { get; set; }
    public bool AllowSelfSignedCerts { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
