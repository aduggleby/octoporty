// ConnectionLog.cs
// Entity for tracking tunnel connection sessions.
// Records connection duration, bytes transferred, and disconnect reasons for audit.

namespace Octoporty.Shared.Entities;

public class ConnectionLog
{
    public Guid Id { get; set; }
    public Guid PortMappingId { get; set; }
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DisconnectedAt { get; set; }
    public string? ClientIp { get; set; }
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }
    public string? DisconnectReason { get; set; }

    public PortMapping? PortMapping { get; set; }
}
