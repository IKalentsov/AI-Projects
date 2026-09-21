namespace WeatherBot.Domain;

/// <summary>Настройки периодической отправки (секция <c>WeatherBot</c> в конфигурации).</summary>
public sealed class WeatherBotSettings
{
    /// <summary>Имя секции конфигурации.</summary>
    public const string SectionName = "WeatherBot";

    /// <summary>Интервал отправки сводки, минуты.</summary>
    public int IntervalMinutes { get; set; } = 30;
}
