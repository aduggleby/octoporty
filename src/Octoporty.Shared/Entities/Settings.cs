// Settings.cs
// Key-value settings entity for storing Agent configuration.
// Used for landing page HTML and other global settings.

namespace Octoporty.Shared.Entities;

public class Settings
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
