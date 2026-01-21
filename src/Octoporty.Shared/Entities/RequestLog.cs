// RequestLog.cs
// Entity for logging individual HTTP requests through the tunnel.
// Tracks method, path, status, timing, and sizes for analytics and debugging.

namespace Octoporty.Shared.Entities;

public class RequestLog
{
    public Guid Id { get; set; }
    public Guid PortMappingId { get; set; }
    public Guid? ConnectionLogId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public required string Method { get; set; }
    public required string Path { get; set; }
    public string? QueryString { get; set; }
    public int StatusCode { get; set; }
    public long RequestSize { get; set; }
    public long ResponseSize { get; set; }
    public int DurationMs { get; set; }
    public string? ClientIp { get; set; }
    public string? UserAgent { get; set; }
    public string? ErrorMessage { get; set; }

    public PortMapping? PortMapping { get; set; }
    public ConnectionLog? ConnectionLog { get; set; }
}
