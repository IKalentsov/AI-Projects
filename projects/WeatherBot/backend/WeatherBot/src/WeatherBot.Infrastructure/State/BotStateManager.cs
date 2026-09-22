namespace WeatherBot.Infrastructure.State;

using WeatherBot.Application.Abstractions;

/// <summary>Потокобезопасное состояние бота. Регистрируется как Singleton.</summary>
public sealed class BotStateManager : IBotStateManager
{
    private readonly Lock _syncRoot = new();

    private bool _isRunning;
    private DateTimeOffset? _lastSentAt;
    private DateTimeOffset? _nextTickAt;

    /// <inheritdoc />
    public bool IsRunning
    {
        get
        {
            lock (_syncRoot)
            {
                return _isRunning;
            }
        }
    }

    /// <inheritdoc />
    public DateTimeOffset? LastSentAt
    {
        get
        {
            lock (_syncRoot)
            {
                return _lastSentAt;
            }
        }
    }

    /// <inheritdoc />
    public DateTimeOffset? NextTickAt
    {
        get
        {
            lock (_syncRoot)
            {
                return _nextTickAt;
            }
        }
    }

    /// <inheritdoc />
    public void Start()
    {
        lock (_syncRoot)
        {
            _isRunning = true;
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        lock (_syncRoot)
        {
            _isRunning = false;
            _nextTickAt = null;
        }
    }

    /// <inheritdoc />
    public void MarkSent(DateTimeOffset sentAt)
    {
        lock (_syncRoot)
        {
            _lastSentAt = sentAt;
        }
    }

    /// <inheritdoc />
    public void MarkNextTick(DateTimeOffset? nextTickAt)
    {
        lock (_syncRoot)
        {
            _nextTickAt = nextTickAt;
        }
    }
}
