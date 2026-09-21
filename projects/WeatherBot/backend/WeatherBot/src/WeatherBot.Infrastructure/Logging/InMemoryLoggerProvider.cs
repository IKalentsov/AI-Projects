namespace WeatherBot.Infrastructure.Logging;

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using WeatherBot.Application.Abstractions;

/// <summary>
/// Провайдер журналирования, который складывает записи в <see cref="ILogBuffer"/>
/// (в дополнение к консоли, куда пишет стандартный провайдер).
/// </summary>
public sealed class InMemoryLoggerProvider(ILogBuffer logBuffer) : ILoggerProvider
{
    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new InMemoryLogger(logBuffer, categoryName);

    /// <inheritdoc />
    public void Dispose()
    {
        // Освобождать нечего: буфер — Singleton, живёт до остановки приложения.
    }
}

/// <summary>Запись журнала в кольцевой буфер.</summary>
internal sealed class InMemoryLogger(ILogBuffer logBuffer, string categoryName) : ILogger
{
    /// <summary>Ниже этого уровня записи в буфер не попадают.</summary>
    private const LogLevel MinimumLevel = LogLevel.Information;

    private readonly string _category = Shorten(categoryName);

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) =>
        logLevel >= MinimumLevel && logLevel != LogLevel.None;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel) || formatter is null)
        {
            return;
        }

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception is null)
        {
            return;
        }

        var builder = new StringBuilder()
            .Append(DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture))
            .Append(" [")
            .Append(LevelLabel(logLevel))
            .Append("] ")
            .Append(_category)
            .Append(" — ");

        if (!string.IsNullOrEmpty(message))
        {
            builder.Append(message);
        }

        if (exception is not null)
        {
            builder.Append(" | ").Append(exception.Message);
        }

        logBuffer.Add(builder.ToString());
    }

    /// <summary>Оставляет от полного имени категории только последний сегмент.</summary>
    private static string Shorten(string categoryName)
    {
        var separator = categoryName.LastIndexOf('.');

        return separator >= 0 ? categoryName[(separator + 1)..] : categoryName;
    }

    private static string LevelLabel(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRCE",
        LogLevel.Debug => "DBUG",
        LogLevel.Information => "INFO",
        LogLevel.Warning => "WARN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRIT",
        _ => "NONE"
    };
}
