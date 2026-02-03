// ICaddyAdminClient.cs
// Interface for Caddy Admin API client.
// Manages dynamic route creation/removal for tunnel mappings.

using Octoporty.Shared.Contracts;

namespace Octoporty.Gateway.Services;

public interface ICaddyAdminClient
{
    Task<bool> IsHealthyAsync(CancellationToken ct);
    Task EnsureRouteExistsAsync(PortMappingDto mapping, CancellationToken ct);
    Task RemoveRouteAsync(Guid mappingId, CancellationToken ct);
    Task RemoveStaleRoutesAsync(HashSet<Guid> activeIds, CancellationToken ct);

    /// <summary>
    /// Gets the current Caddy configuration as JSON.
    /// </summary>
    Task<string> GetConfigJsonAsync(CancellationToken ct);
}
