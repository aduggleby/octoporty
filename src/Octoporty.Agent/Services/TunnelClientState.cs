// TunnelClientState.cs
// Enum representing the tunnel client's connection state machine.
// Used by StatusHub to broadcast real-time state changes to the web UI.

namespace Octoporty.Agent.Services;

public enum TunnelClientState
{
    Disconnected,
    Connecting,
    Authenticating,
    Syncing,
    Connected,
    Reconnecting
}
