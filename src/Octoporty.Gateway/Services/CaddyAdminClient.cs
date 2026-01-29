// CaddyAdminClient.cs
// HTTP client for Caddy's Admin API to dynamically configure reverse proxy routes.
// Creates routes that forward external requests to the Gateway, adding X-Octoporty-Mapping-Id header.
// Maintains local cache of known routes to avoid redundant API calls.

using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Octoporty.Shared.Contracts;
using Octoporty.Shared.Options;

namespace Octoporty.Gateway.Services;

public sealed class CaddyAdminClient : ICaddyAdminClient
{
    private readonly HttpClient _http;
    private readonly ILogger<CaddyAdminClient> _logger;
    private readonly GatewayOptions _options;
    private readonly ConcurrentDictionary<Guid, bool> _knownRoutes = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public CaddyAdminClient(
        HttpClient http,
        IOptions<GatewayOptions> options,
        ILogger<CaddyAdminClient> logger)
    {
        _http = http;
        _http.BaseAddress = new Uri(options.Value.CaddyAdminUrl);
        _logger = logger;
        _options = options.Value;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync("/config/", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Caddy health check failed");
            return false;
        }
    }

    public async Task EnsureRouteExistsAsync(PortMappingDto mapping, CancellationToken ct)
    {
        var routeId = GetRouteId(mapping.Id);

        // Check if we already know about this route
        if (_knownRoutes.ContainsKey(mapping.Id))
        {
            // Verify it still exists in Caddy
            var exists = await RouteExistsAsync(routeId, ct);
            if (exists)
            {
                _logger.LogDebug("Route {RouteId} already exists", routeId);
                return;
            }
        }

        // Create or update the route
        var route = new CaddyRoute
        {
            Id = routeId,
            Match = [new CaddyMatch { Host = [mapping.ExternalDomain] }],
            Handle =
            [
                new CaddyHandler
                {
                    Handler = "reverse_proxy",
                    // Use "gateway" as the Docker service hostname.
                    // "localhost" doesn't work because Caddy runs in a separate container.
                    Upstreams = [new CaddyUpstream { Dial = $"gateway:{_options.ListenPort}" }],
                    Headers = new CaddyHeaders
                    {
                        Request = new CaddyHeaderOps
                        {
                            Set = new Dictionary<string, string[]>
                            {
                                ["X-Octoporty-Mapping-Id"] = [mapping.Id.ToString()]
                            }
                        }
                    }
                }
            ]
        };

        try
        {
            // First try to update existing route
            var updateResponse = await _http.PatchAsync(
                $"/id/{routeId}",
                JsonContent.Create(route, options: JsonOptions),
                ct);

            if (!updateResponse.IsSuccessStatusCode)
            {
                // Route doesn't exist, add it
                var addResponse = await _http.PostAsync(
                    "/config/apps/http/servers/srv0/routes",
                    JsonContent.Create(route, options: JsonOptions),
                    ct);

                if (!addResponse.IsSuccessStatusCode)
                {
                    var error = await addResponse.Content.ReadAsStringAsync(ct);
                    _logger.LogError("Failed to add Caddy route {RouteId}: {Error}", routeId, error);
                    throw new InvalidOperationException($"Failed to add Caddy route: {error}");
                }

                _logger.LogInformation("Added Caddy route {RouteId} for {Domain}", routeId, mapping.ExternalDomain);
            }
            else
            {
                _logger.LogInformation("Updated Caddy route {RouteId} for {Domain}", routeId, mapping.ExternalDomain);
            }

            _knownRoutes[mapping.Id] = true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to configure Caddy route {RouteId}", routeId);
            throw;
        }
    }

    public async Task RemoveRouteAsync(Guid mappingId, CancellationToken ct)
    {
        var routeId = GetRouteId(mappingId);

        try
        {
            var response = await _http.DeleteAsync($"/id/{routeId}", ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Removed Caddy route {RouteId}", routeId);
                _knownRoutes.TryRemove(mappingId, out _);
            }
            else if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Failed to remove Caddy route {RouteId}: {Error}", routeId, error);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to remove Caddy route {RouteId}", routeId);
        }
    }

    public async Task RemoveStaleRoutesAsync(HashSet<Guid> activeIds, CancellationToken ct)
    {
        var staleIds = _knownRoutes.Keys.Where(id => !activeIds.Contains(id)).ToList();

        foreach (var staleId in staleIds)
        {
            await RemoveRouteAsync(staleId, ct);
        }
    }

    private async Task<bool> RouteExistsAsync(string routeId, CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync($"/id/{routeId}", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string GetRouteId(Guid mappingId) => $"octoporty-{mappingId:N}";
}

// Caddy JSON config models
public class CaddyRoute
{
    [JsonPropertyName("@id")]
    public string? Id { get; set; }

    [JsonPropertyName("match")]
    public CaddyMatch[]? Match { get; set; }

    [JsonPropertyName("handle")]
    public CaddyHandler[]? Handle { get; set; }
}

public class CaddyMatch
{
    [JsonPropertyName("host")]
    public string[]? Host { get; set; }
}

public class CaddyHandler
{
    [JsonPropertyName("handler")]
    public string? Handler { get; set; }

    [JsonPropertyName("upstreams")]
    public CaddyUpstream[]? Upstreams { get; set; }

    [JsonPropertyName("headers")]
    public CaddyHeaders? Headers { get; set; }
}

public class CaddyUpstream
{
    [JsonPropertyName("dial")]
    public string? Dial { get; set; }
}

public class CaddyHeaders
{
    [JsonPropertyName("request")]
    public CaddyHeaderOps? Request { get; set; }
}

public class CaddyHeaderOps
{
    [JsonPropertyName("set")]
    public Dictionary<string, string[]>? Set { get; set; }
}
