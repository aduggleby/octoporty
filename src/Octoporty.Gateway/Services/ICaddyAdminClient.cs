using Octoporty.Shared.Contracts;

namespace Octoporty.Gateway.Services;

public interface ICaddyAdminClient
{
    Task<bool> IsHealthyAsync(CancellationToken ct);
    Task EnsureRouteExistsAsync(PortMappingDto mapping, CancellationToken ct);
    Task RemoveRouteAsync(Guid mappingId, CancellationToken ct);
    Task RemoveStaleRoutesAsync(HashSet<Guid> activeIds, CancellationToken ct);
}
