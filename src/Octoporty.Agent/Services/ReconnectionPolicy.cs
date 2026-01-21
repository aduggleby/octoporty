// ReconnectionPolicy.cs
// Implements exponential backoff with jitter for tunnel reconnection attempts.
// Base delay doubles each attempt (1s, 2s, 4s, 8s...) up to max of 60s.
// Random 0-1s jitter prevents thundering herd when multiple agents reconnect.

namespace Octoporty.Agent.Services;

public class ReconnectionPolicy
{
    private readonly Random _random = new();
    private int _attempt;
    private readonly double _baseDelaySeconds;
    private readonly double _maxDelaySeconds;

    public ReconnectionPolicy(double baseDelaySeconds = 1, double maxDelaySeconds = 60)
    {
        _baseDelaySeconds = baseDelaySeconds;
        _maxDelaySeconds = maxDelaySeconds;
    }

    public TimeSpan GetNextDelay()
    {
        _attempt++;
        var baseDelay = Math.Min(Math.Pow(2, _attempt) * _baseDelaySeconds, _maxDelaySeconds);
        var jitter = _random.NextDouble(); // 0-1 second jitter
        return TimeSpan.FromSeconds(baseDelay + jitter);
    }

    public void Reset() => _attempt = 0;

    public int CurrentAttempt => _attempt;
}
