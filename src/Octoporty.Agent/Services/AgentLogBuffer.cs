// AgentLogBuffer.cs
// In-memory buffer of recent Agent logs for the web UI.
// Filled by AgentLogTailService (tails the Serilog rolling file output).

using System.Collections.Concurrent;

namespace Octoporty.Agent.Services;

public enum AgentLogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public class AgentLogBuffer
{
    private readonly ConcurrentQueue<AgentLogEntry> _logs = new();
    private readonly int _maxEntries;
    private long _totalCount;

    public AgentLogBuffer(int maxEntries = 10_000)
    {
        _maxEntries = maxEntries;
    }

    public void Add(DateTime timestamp, AgentLogLevel level, string message)
    {
        var entry = new AgentLogEntry(Interlocked.Increment(ref _totalCount), timestamp, level, message);
        _logs.Enqueue(entry);

        while (_logs.Count > _maxEntries && _logs.TryDequeue(out _))
        {
        }
    }

    public (IReadOnlyList<AgentLogEntry> Logs, bool HasMore) GetLogs(long? beforeId, int count)
    {
        var allLogs = _logs.ToArray();

        IEnumerable<AgentLogEntry> filtered = allLogs;
        if (beforeId.HasValue && beforeId.Value > 0)
        {
            filtered = filtered.Where(l => l.Id < beforeId.Value);
        }

        var ordered = filtered
            .OrderByDescending(l => l.Id)
            .Take(count + 1)
            .ToArray();

        var hasMore = ordered.Length > count;
        var result = ordered.Take(count).ToArray();

        return (result, hasMore);
    }
}

public record AgentLogEntry(
    long Id,
    DateTime Timestamp,
    AgentLogLevel Level,
    string Message);

