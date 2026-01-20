using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace Octoporty.Shared.Logging;

public static class SerilogExtensions
{
    public static IHostBuilder UseOctoportySerilog(this IHostBuilder hostBuilder, string appName)
    {
        return hostBuilder.UseSerilog((context, services, configuration) =>
        {
            var options = context.Configuration
                .GetSection("Logging")
                .Get<LoggingOptions>() ?? new LoggingOptions();

            var minLevel = ParseLogLevel(options.LogLevel);

            configuration
                .MinimumLevel.Is(minLevel)
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", appName)
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

            if (!string.IsNullOrEmpty(options.SeqUrl))
            {
                configuration.WriteTo.Seq(
                    serverUrl: options.SeqUrl,
                    apiKey: options.SeqApiKey);
            }

            if (!string.IsNullOrEmpty(options.FilePath))
            {
                configuration.WriteTo.File(
                    path: options.FilePath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: options.RetainedFileCountLimit,
                    fileSizeLimitBytes: options.FileSizeLimitBytes,
                    rollOnFileSizeLimit: true,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
            }
        });
    }

    private static LogEventLevel ParseLogLevel(string level)
    {
        return level.ToLowerInvariant() switch
        {
            "verbose" or "trace" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "information" or "info" => LogEventLevel.Information,
            "warning" or "warn" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" or "critical" => LogEventLevel.Fatal,
            _ => LogEventLevel.Debug
        };
    }
}
