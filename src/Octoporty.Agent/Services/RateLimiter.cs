using System.Collections.Concurrent;

namespace Octoporty.Agent.Services;

/// <summary>
/// MEDIUM-02: Simple in-memory rate limiter for login attempts.
/// Uses sliding window approach - tracks attempts per IP with lockout.
/// </summary>
public class LoginRateLimiter
{
    private readonly ConcurrentDictionary<string, RateLimitEntry> _attempts = new();
    private readonly int _maxAttempts;
    private readonly TimeSpan _windowDuration;
    private readonly TimeSpan _lockoutDuration;

    public LoginRateLimiter(int maxAttempts = 5, int windowSeconds = 60, int lockoutMinutes = 5)
    {
        _maxAttempts = maxAttempts;
        _windowDuration = TimeSpan.FromSeconds(windowSeconds);
        _lockoutDuration = TimeSpan.FromMinutes(lockoutMinutes);
    }

    public bool IsBlocked(string identifier)
    {
        if (_attempts.TryGetValue(identifier, out var entry))
        {
            // Check if locked out
            if (entry.LockedUntil.HasValue && entry.LockedUntil > DateTime.UtcNow)
            {
                return true;
            }

            // Clear expired lockout
            if (entry.LockedUntil.HasValue && entry.LockedUntil <= DateTime.UtcNow)
            {
                _attempts.TryRemove(identifier, out _);
            }
        }

        return false;
    }

    public TimeSpan? GetLockoutRemaining(string identifier)
    {
        if (_attempts.TryGetValue(identifier, out var entry) && entry.LockedUntil.HasValue)
        {
            var remaining = entry.LockedUntil.Value - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : null;
        }
        return null;
    }

    public void RecordFailedAttempt(string identifier)
    {
        var entry = _attempts.GetOrAdd(identifier, _ => new RateLimitEntry());

        lock (entry)
        {
            // Clean old attempts outside the window
            var cutoff = DateTime.UtcNow - _windowDuration;
            entry.Attempts.RemoveAll(t => t < cutoff);

            // Add new attempt
            entry.Attempts.Add(DateTime.UtcNow);

            // Check if exceeded limit
            if (entry.Attempts.Count >= _maxAttempts)
            {
                entry.LockedUntil = DateTime.UtcNow + _lockoutDuration;
            }
        }
    }

    public void RecordSuccess(string identifier)
    {
        // Clear attempts on successful login
        _attempts.TryRemove(identifier, out _);
    }

    private class RateLimitEntry
    {
        public List<DateTime> Attempts { get; } = [];
        public DateTime? LockedUntil { get; set; }
    }
}
