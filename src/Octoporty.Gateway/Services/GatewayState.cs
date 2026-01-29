// GatewayState.cs
// Singleton service tracking Gateway runtime state.
// Provides uptime calculation for heartbeat responses to connected Agents.

namespace Octoporty.Gateway.Services;

public class GatewayState
{
    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;

    /// <summary>
    /// Returns the number of seconds the Gateway has been running.
    /// </summary>
    public long UptimeSeconds => (long)(DateTimeOffset.UtcNow - _startTime).TotalSeconds;
}
