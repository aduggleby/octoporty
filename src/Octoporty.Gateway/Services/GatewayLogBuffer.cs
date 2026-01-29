// GatewayLogBuffer.cs
// Thread-safe ring buffer for storing recent Gateway log messages.
// Allows historical log retrieval for the Agent web UI.
// Maintains a configurable maximum number of entries (default 10,000).

using System.Collections.Concurrent;
using Octoporty.Shared.Contracts;

namespace Octoporty.Gateway.Services;

/// <summary>
/// Thread-safe ring buffer for storing Gateway log messages.
/// Provides historical log retrieval with pagination support.
/// </summary>
public class GatewayLogBuffer
{
    private readonly ConcurrentQueue<GatewayLogEntry> _logs = new();
    private readonly int _maxEntries;
    private long _totalCount;

    public GatewayLogBuffer(int maxEntries = 10_000)
    {
        _maxEntries = maxEntries;
    }

    /// <summary>
    /// Adds a log entry to the buffer, removing oldest if at capacity.
    /// </summary>
    public void Add(DateTime timestamp, GatewayLogLevel level, string message)
    {
        var entry = new GatewayLogEntry(Interlocked.Increment(ref _totalCount), timestamp, level, message);
        _logs.Enqueue(entry);

        // Remove oldest entries if over capacity
        while (_logs.Count > _maxEntries && _logs.TryDequeue(out _))
        {
        }
    }

    /// <summary>
    /// Gets logs with pagination support.
    /// Returns logs in reverse chronological order (newest first).
    /// </summary>
    /// <param name="beforeId">Return logs with ID less than this value. Use 0 or null for latest.</param>
    /// <param name="count">Maximum number of logs to return.</param>
    /// <returns>Logs and whether there are more older logs available.</returns>
    public (IReadOnlyList<GatewayLogEntry> Logs, bool HasMore) GetLogs(long? beforeId, int count)
    {
        var allLogs = _logs.ToArray();

        IEnumerable<GatewayLogEntry> filtered = allLogs;

        if (beforeId.HasValue && beforeId.Value > 0)
        {
            filtered = allLogs.Where(l => l.Id < beforeId.Value);
        }

        // Order by ID descending (newest first), then take requested count
        var result = filtered
            .OrderByDescending(l => l.Id)
            .Take(count)
            .ToList();

        // Check if there are more logs before the oldest one we're returning
        var hasMore = result.Count > 0 && allLogs.Any(l => l.Id < result[^1].Id);

        return (result, hasMore);
    }

    /// <summary>
    /// Gets the total number of logs currently in the buffer.
    /// </summary>
    public int Count => _logs.Count;
}

/// <summary>
/// A log entry with a unique sequential ID for pagination.
/// </summary>
public record GatewayLogEntry(
    long Id,
    DateTime Timestamp,
    GatewayLogLevel Level,
    string Message);
