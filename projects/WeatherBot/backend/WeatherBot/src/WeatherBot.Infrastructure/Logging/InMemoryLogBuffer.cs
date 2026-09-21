namespace WeatherBot.Infrastructure.Logging;

using WeatherBot.Application.Abstractions;

/// <summary>
/// Кольцевой буфер последних записей журнала в памяти.
/// Регистрируется как Singleton и наполняется провайдером <see cref="InMemoryLoggerProvider"/>.
/// </summary>
public sealed class InMemoryLogBuffer : ILogBuffer
{
    /// <summary>Сколько записей хранится одновременно.</summary>
    public const int Capacity = 100;

    private readonly Lock _syncRoot = new();
    private readonly Queue<string> _entries = new(Capacity);

    /// <inheritdoc />
    public void Add(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        lock (_syncRoot)
        {
            if (_entries.Count >= Capacity)
            {
                _entries.Dequeue();
            }

            _entries.Enqueue(message);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetRecent(int count)
    {
        if (count <= 0)
        {
            return [];
        }

        lock (_syncRoot)
        {
            var skip = Math.Max(0, _entries.Count - count);

            return _entries.Skip(skip).ToArray();
        }
    }
}
