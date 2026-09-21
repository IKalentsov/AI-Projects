namespace WeatherBot.Contracts.Bot;

/// <summary>Результат управляющего действия над ботом.</summary>
/// <param name="Success">Признак успешного выполнения действия.</param>
/// <param name="IsRunning">Состояние бота после выполнения действия.</param>
/// <param name="Message">Пояснение для пользователя.</param>
public sealed record BotActionResponse(
    bool Success,
    bool IsRunning,
    string Message);
