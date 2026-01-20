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
