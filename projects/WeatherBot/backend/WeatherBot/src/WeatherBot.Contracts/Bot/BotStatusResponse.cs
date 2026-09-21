namespace WeatherBot.Contracts.Bot;

/// <summary>Статус бота для панели управления.</summary>
/// <param name="IsRunning">Признак того, что периодическая отправка включена.</param>
/// <param name="LastSentAt">Момент последней успешной отправки сводки.</param>
/// <param name="NextTickAt">Момент следующей плановой отправки.</param>
public sealed record BotStatusResponse(
    bool IsRunning,
    DateTimeOffset? LastSentAt,
    DateTimeOffset? NextTickAt);
