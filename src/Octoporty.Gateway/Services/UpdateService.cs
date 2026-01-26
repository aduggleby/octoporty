// UpdateService.cs
// Handles update requests from Agents by writing a signal file for the host watcher.
// The host watcher (systemd timer) monitors this file and triggers docker-compose pull/restart.
// Thread-safe using semaphore to prevent race conditions when multiple Agents request updates.

using System.Text.Json;
using Microsoft.Extensions.Options;
using Octoporty.Shared.Contracts;
using Octoporty.Shared.Options;

namespace Octoporty.Gateway.Services;

public class UpdateService
{
    private readonly GatewayOptions _options;
    private readonly ILogger<UpdateService> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Track if an update is already queued to prevent duplicate signal files
    private bool _updateQueued;
    private DateTime? _queuedAt;

    public UpdateService(
        IOptions<GatewayOptions> options,
        ILogger<UpdateService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Processes an update request from an Agent.
    /// Writes a signal file for the host watcher if updates are allowed and not already queued.
    /// </summary>
    public async Task<UpdateResponseMessage> RequestUpdateAsync(
        UpdateRequestMessage request,
        string currentVersion,
        CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            // Check if remote updates are disabled
            if (!_options.AllowRemoteUpdate)
            {
                _logger.LogWarning("Update request rejected: remote updates are disabled");
                return new UpdateResponseMessage
                {
                    Accepted = false,
                    Error = "Remote updates are disabled on this Gateway",
                    CurrentVersion = currentVersion,
                    Status = UpdateStatus.Rejected
                };
            }

            // Check if target version is the same or older
            // Simple string comparison - assumes semantic versioning
            if (string.Compare(request.TargetVersion, currentVersion, StringComparison.OrdinalIgnoreCase) <= 0)
            {
                _logger.LogInformation(
                    "Update request rejected: requested version {RequestedVersion} is not newer than current {CurrentVersion}",
                    request.TargetVersion, currentVersion);
                return new UpdateResponseMessage
                {
                    Accepted = false,
                    Error = $"Requested version {request.TargetVersion} is not newer than current version {currentVersion}",
                    CurrentVersion = currentVersion,
                    Status = UpdateStatus.Rejected
                };
            }

            // Check if update is already queued
            if (_updateQueued)
            {
                _logger.LogInformation(
                    "Update already queued at {QueuedAt}, rejecting duplicate request",
                    _queuedAt);
                return new UpdateResponseMessage
                {
                    Accepted = true,
                    CurrentVersion = currentVersion,
                    Status = UpdateStatus.AlreadyQueued
                };
            }

            // Write the signal file
            try
            {
                await WriteSignalFileAsync(request, currentVersion, ct);
                _updateQueued = true;
                _queuedAt = DateTime.UtcNow;

                _logger.LogInformation(
                    "Update queued: {CurrentVersion} -> {TargetVersion} (requested by {RequestedBy})",
                    currentVersion, request.TargetVersion, request.RequestedBy);

                return new UpdateResponseMessage
                {
                    Accepted = true,
                    CurrentVersion = currentVersion,
                    Status = UpdateStatus.Queued
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write update signal file");
                return new UpdateResponseMessage
                {
                    Accepted = false,
                    Error = $"Failed to queue update: {ex.Message}",
                    CurrentVersion = currentVersion,
                    Status = UpdateStatus.Rejected
                };
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task WriteSignalFileAsync(
        UpdateRequestMessage request,
        string currentVersion,
        CancellationToken ct)
    {
        var signalData = new
        {
            targetVersion = request.TargetVersion,
            currentVersion,
            requestedBy = request.RequestedBy,
            requestedAt = DateTime.UtcNow.ToString("O")
        };

        var json = JsonSerializer.Serialize(signalData, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        // Ensure directory exists
        var directory = Path.GetDirectoryName(_options.UpdateSignalPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(_options.UpdateSignalPath, json, ct);

        _logger.LogDebug("Signal file written to {Path}", _options.UpdateSignalPath);
    }

    /// <summary>
    /// Checks if an update is currently queued.
    /// </summary>
    public bool IsUpdateQueued => _updateQueued;

    /// <summary>
    /// Resets the queued state. Called after the Gateway restarts (implicitly via new instance).
    /// Can also be called if the signal file is manually deleted.
    /// </summary>
    public void ResetQueuedState()
    {
        _lock.Wait();
        try
        {
            _updateQueued = false;
            _queuedAt = null;
        }
        finally
        {
            _lock.Release();
        }
    }
}
