// AgentLogTailService.cs
// Background service that tails the Agent's Serilog rolling file log and feeds AgentLogBuffer + SignalR.
// This avoids needing a custom Serilog sink and works in production where logs are written to /var/log.

using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Octoporty.Shared.Logging;

namespace Octoporty.Agent.Services;

public sealed class AgentLogTailService : BackgroundService
{
    private readonly IOptions<LoggingOptions> _loggingOptions;
    private readonly AgentLogBuffer _buffer;
    private readonly StatusNotifier _notifier;
    private readonly ILogger<AgentLogTailService> _logger;

    // Example: 2026-02-12 08:44:10.123 +00:00 [INF] message...
    private static readonly Regex LogLineRegex = new(
        @"^(?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+\-]\d{2}:\d{2}) \[(?<lvl>[A-Z]{3})\] (?<msg>.*)$",
        RegexOptions.Compiled);

    public AgentLogTailService(
        IOptions<LoggingOptions> loggingOptions,
        AgentLogBuffer buffer,
        StatusNotifier notifier,
        ILogger<AgentLogTailService> logger)
    {
        _loggingOptions = loggingOptions;
        _buffer = buffer;
        _notifier = notifier;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var configured = _loggingOptions.Value.FilePath;
        if (string.IsNullOrWhiteSpace(configured))
        {
            _logger.LogInformation("Agent log tailing disabled (Logging:FilePath not set)");
            return;
        }

        // Preload last lines so the UI has some history immediately after startup.
        try
        {
            var file = ResolveCurrentLogFile(configured);
            if (file != null && File.Exists(file))
            {
                foreach (var line in ReadLastLines(file, 400))
                {
                    await HandleLineAsync(line, stoppingToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to preload agent log history");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var filePath = ResolveCurrentLogFile(configured);
            if (filePath == null)
            {
                await Task.Delay(2000, stoppingToken);
                continue;
            }

            try
            {
                await TailFileAsync(configured, filePath, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Agent log tailer error (will retry)");
                await Task.Delay(2000, stoppingToken);
            }
        }
    }

    private async Task TailFileAsync(string configuredTemplatePath, string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath))
        {
            await Task.Delay(2000, ct);
            return;
        }

        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs);

        // Start at end; history is loaded separately on service start.
        fs.Seek(0, SeekOrigin.End);

        while (!ct.IsCancellationRequested)
        {
            // Follow daily rolling file rotation. When Serilog switches files, reopen.
            var current = ResolveCurrentLogFile(configuredTemplatePath);
            if (current != null && !string.Equals(current, filePath, StringComparison.Ordinal))
            {
                return;
            }

            var line = await reader.ReadLineAsync(ct);
            if (line == null)
            {
                await Task.Delay(250, ct);
                continue;
            }

            await HandleLineAsync(line, ct);
        }
    }

    private async Task HandleLineAsync(string line, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        var parsed = TryParse(line);
        if (parsed == null)
            return;

        _buffer.Add(parsed.Value.Timestamp.UtcDateTime, parsed.Value.Level, parsed.Value.Message);
        await _notifier.NotifyAgentLogAsync(parsed.Value.Timestamp.UtcDateTime, parsed.Value.Level.ToString(), parsed.Value.Message);
    }

    private static (DateTimeOffset Timestamp, AgentLogLevel Level, string Message)? TryParse(string line)
    {
        var match = LogLineRegex.Match(line);
        if (!match.Success)
            return null;

        if (!DateTimeOffset.TryParseExact(
                match.Groups["ts"].Value,
                "yyyy-MM-dd HH:mm:ss.fff zzz",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var ts))
        {
            return null;
        }

        var level = match.Groups["lvl"].Value switch
        {
            "DBG" or "VRB" => AgentLogLevel.Debug,
            "INF" => AgentLogLevel.Info,
            "WRN" => AgentLogLevel.Warning,
            "ERR" or "FTL" => AgentLogLevel.Error,
            _ => AgentLogLevel.Info
        };

        return (ts, level, match.Groups["msg"].Value);
    }

    private static string? ResolveCurrentLogFile(string configuredPath)
    {
        // Serilog rolling file with "agent-.log" produces "agent-YYYYMMDD.log".
        // We pick the most recently written file matching the prefix/suffix.
        var directory = Path.GetDirectoryName(configuredPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return null;

        var fileName = Path.GetFileName(configuredPath);
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var dashIndex = fileName.LastIndexOf('-');
        var dotIndex = fileName.LastIndexOf(".log", StringComparison.OrdinalIgnoreCase);
        if (dashIndex < 0 || dotIndex < 0 || dotIndex <= dashIndex)
        {
            // If the filename isn't a rolling template, just tail the configured file.
            return Path.Combine(directory, fileName);
        }

        var prefix = fileName[..(dashIndex + 1)];
        var pattern = $"{prefix}*.log";

        return Directory.EnumerateFiles(directory, pattern)
            .Select(p => new FileInfo(p))
            .OrderByDescending(fi => fi.LastWriteTimeUtc)
            .FirstOrDefault()
            ?.FullName;
    }

    private static IEnumerable<string> ReadLastLines(string filePath, int maxLines)
    {
        // Simple implementation: read all lines and take the tail.
        // This is acceptable because it's only used on service start.
        var lines = File.ReadAllLines(filePath);
        return lines.Length <= maxLines ? lines : lines.Skip(lines.Length - maxLines);
    }
}
