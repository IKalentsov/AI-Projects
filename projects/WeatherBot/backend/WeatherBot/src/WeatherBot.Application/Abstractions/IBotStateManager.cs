namespace WeatherBot.Application.Abstractions;

/// <summary>
/// Состояние бота: работает он или остановлен, а также сведения для панели управления.
/// Реализация обязана быть потокобезопасной и регистрируется как Singleton.
/// </summary>
public interface IBotStateManager
{
    /// <summary>Признак того, что периодическая отправка включена.</summary>
    bool IsRunning { get; }

    /// <summary>Момент последней успешной отправки сводки, если она была.</summary>
    DateTimeOffset? LastSentAt { get; }

    /// <summary>Момент следующей плановой отправки, если бот запущен.</summary>
    DateTimeOffset? NextTickAt { get; }

    /// <summary>Включает периодическую отправку.</summary>
    void Start();

    /// <summary>Выключает периодическую отправку. Уже начатая отправка не прерывается.</summary>
    void Stop();

    /// <summary>Фиксирует успешную отправку сводки.</summary>
    /// <param name="sentAt">Момент отправки.</param>
    void MarkSent(DateTimeOffset sentAt);

    /// <summary>Фиксирует момент следующей плановой отправки.</summary>
    /// <param name="nextTickAt">Момент отправки либо <c>null</c>, если она не запланирована.</param>
    void MarkNextTick(DateTimeOffset? nextTickAt);
}
