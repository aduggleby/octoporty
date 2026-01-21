// LoggingOptions.cs
// Configuration options for Serilog logging.
// Supports console, file (with rotation), and Seq centralized logging.

namespace Octoporty.Shared.Logging;

public class LoggingOptions
{
    public string LogLevel { get; set; } = "Debug";
    public string? SeqUrl { get; set; }
    public string? SeqApiKey { get; set; }
    public string? FilePath { get; set; }
    public int RetainedFileCountLimit { get; set; } = 30;
    public long FileSizeLimitBytes { get; set; } = 100 * 1024 * 1024; // 100MB
}
