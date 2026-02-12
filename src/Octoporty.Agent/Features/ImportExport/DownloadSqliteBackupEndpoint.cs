// DownloadSqliteBackupEndpoint.cs
// Streams a consistent SQLite backup of the Agent database as a downloadable file.
// Uses the SQLite backup API to avoid copying a live DB file directly.

using FastEndpoints;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace Octoporty.Agent.Features.ImportExport;

public sealed class DownloadSqliteBackupEndpoint : EndpointWithoutRequest
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DownloadSqliteBackupEndpoint> _logger;

    public DownloadSqliteBackupEndpoint(IConfiguration configuration, ILogger<DownloadSqliteBackupEndpoint> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public override void Configure()
    {
        Get("/api/v1/import-export/sqlite");
        Description(d => d
            .WithSummary("Download SQLite Backup")
            .WithDescription("Downloads a consistent backup of the Agent SQLite database."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = "Data Source=/app/data/octoporty.db";
        }

        var backupPath = Path.Combine(
            Path.GetTempPath(),
            $"octoporty-agent-backup-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.db");

        try
        {
            // Create a consistent backup file.
            await using var source = new SqliteConnection(connectionString);
            await source.OpenAsync(ct);

            var destCs = new SqliteConnectionStringBuilder { DataSource = backupPath }.ToString();
            await using var dest = new SqliteConnection(destCs);
            await dest.OpenAsync(ct);

            source.BackupDatabase(dest);
            await dest.CloseAsync();
            await source.CloseAsync();

            var fileName = Path.GetFileName(backupPath);
            HttpContext.Response.ContentType = "application/x-sqlite3";
            HttpContext.Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";

            await using var fs = File.OpenRead(backupPath);
            await fs.CopyToAsync(HttpContext.Response.Body, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create/download SQLite backup");
            HttpContext.Response.StatusCode = 500;
            await HttpContext.Response.WriteAsync("Failed to generate SQLite backup", ct);
        }
        finally
        {
            try
            {
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }
}

