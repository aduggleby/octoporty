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
