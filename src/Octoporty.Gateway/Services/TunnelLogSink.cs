// TunnelLogSink.cs
// Custom Serilog sink that forwards log messages to the connected Agent.
// Allows the Agent's web UI to display real-time Gateway logs.
// Uses fire-and-forget pattern to avoid blocking log processing.

using Octoporty.Shared.Contracts;
using Serilog.Core;
using Serilog.Events;

namespace Octoporty.Gateway.Services;

/// <summary>
/// Serilog sink that forwards log messages to the connected Agent via WebSocket tunnel.
/// </summary>
public class TunnelLogSink : ILogEventSink
{
    private readonly Func<TunnelConnectionManager?> _connectionManagerFactory;

    public TunnelLogSink(Func<TunnelConnectionManager?> connectionManagerFactory)
    {
        _connectionManagerFactory = connectionManagerFactory;
    }

    public void Emit(LogEvent logEvent)
    {
        var connectionManager = _connectionManagerFactory();
        var connection = connectionManager?.ActiveConnection;

        if (connection == null)
            return;

        var level = logEvent.Level switch
        {
            LogEventLevel.Verbose or LogEventLevel.Debug => GatewayLogLevel.Debug,
            LogEventLevel.Information => GatewayLogLevel.Info,
            LogEventLevel.Warning => GatewayLogLevel.Warning,
            LogEventLevel.Error or LogEventLevel.Fatal => GatewayLogLevel.Error,
            _ => GatewayLogLevel.Info
        };

        var message = new GatewayLogMessage
        {
            Timestamp = logEvent.Timestamp.UtcDateTime,
            Level = level,
            Message = logEvent.RenderMessage()
        };

        // Fire-and-forget - don't block log processing
        // Use Task.Run to avoid deadlocks with sync logging
        _ = Task.Run(async () =>
        {
            try
            {
                await connection.SendAsync(message, CancellationToken.None);
            }
            catch
            {
                // Ignore errors - connection may have closed
            }
        });
    }
}
