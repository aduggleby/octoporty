// TunnelConnectionManager.cs
// Singleton manager for the active Agent tunnel connection.
// Currently supports single-Agent mode (one active connection at a time).
// Routes incoming requests to the Agent by matching Host header to port mappings.

using Octoporty.Shared.Contracts;

namespace Octoporty.Gateway.Services;

public interface ITunnelConnectionManager
{
    ITunnelConnection? ActiveConnection { get; }
    bool HasActiveConnection { get; }
    PortMappingDto? GetMappingByHost(string host);
    PortMappingDto? GetMappingById(Guid mappingId);
    Task<ResponseMessage?> ForwardRequestAsync(RequestMessage request, TimeSpan timeout, CancellationToken ct);
    IAsyncEnumerable<StreamingResponse> ForwardStreamingRequestAsync(RequestMessage request, TimeSpan timeout, CancellationToken ct);
}

public sealed class TunnelConnectionManager : ITunnelConnectionManager
{
    private readonly ILogger<TunnelConnectionManager> _logger;
    private TunnelConnection? _activeConnection;
    private readonly object _lock = new();

    public TunnelConnectionManager(ILogger<TunnelConnectionManager> logger)
    {
        _logger = logger;
    }

    public ITunnelConnection? ActiveConnection
    {
        get
        {
            lock (_lock)
            {
                return _activeConnection?.IsConnected == true ? _activeConnection : null;
            }
        }
    }

    public bool HasActiveConnection => ActiveConnection != null;

    public void SetActiveConnection(TunnelConnection connection)
    {
        lock (_lock)
        {
            _activeConnection = connection;
            _logger.LogInformation("Active connection set: {ConnectionId}", connection.ConnectionId);
        }
    }

    public async Task RemoveConnectionAsync(string connectionId)
    {
        TunnelConnection? toDispose = null;

        lock (_lock)
        {
            if (_activeConnection?.ConnectionId == connectionId)
            {
                toDispose = _activeConnection;
                _activeConnection = null;
                _logger.LogInformation("Active connection removed: {ConnectionId}", connectionId);
            }
        }

        if (toDispose != null)
        {
            await toDispose.DisposeAsync();
        }
    }

    public PortMappingDto? GetMappingByHost(string host)
    {
        var connection = ActiveConnection;
        if (connection == null)
            return null;

        // Extract domain from host (strip port if present)
        var domain = host.Contains(':') ? host.Split(':')[0] : host;

        return connection.Mappings.Values
            .FirstOrDefault(m => m.IsEnabled &&
                                 m.ExternalDomain.Equals(domain, StringComparison.OrdinalIgnoreCase));
    }

    public PortMappingDto? GetMappingById(Guid mappingId)
    {
        var connection = ActiveConnection;
        if (connection == null)
            return null;

        return connection.Mappings.TryGetValue(mappingId, out var mapping) ? mapping : null;
    }

    public async Task<ResponseMessage?> ForwardRequestAsync(RequestMessage request, TimeSpan timeout, CancellationToken ct)
    {
        var connection = ActiveConnection;
        if (connection == null)
        {
            _logger.LogWarning("No active connection to forward request {RequestId}", request.RequestId);
            return null;
        }

        return await connection.SendRequestAsync(request, timeout, ct);
    }

    public async IAsyncEnumerable<StreamingResponse> ForwardStreamingRequestAsync(
        RequestMessage request,
        TimeSpan timeout,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var connection = ActiveConnection;
        if (connection == null)
        {
            _logger.LogWarning("No active connection to forward streaming request {RequestId}", request.RequestId);
            yield break;
        }

        await foreach (var response in connection.SendStreamingRequestAsync(request, timeout, ct))
        {
            yield return response;
        }
    }
}
