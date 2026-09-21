namespace WeatherBot.Domain;

/// <summary>Настройки Telegram-бота (секция <c>Telegram</c> в конфигурации).</summary>
public sealed class TelegramSettings
{
    /// <summary>Имя секции конфигурации.</summary>
    public const string SectionName = "Telegram";

    /// <summary>Токен бота, полученный у @BotFather.</summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>Идентификатор канала: <c>-1001234567890</c> либо <c>@username</c>.</summary>
    public string ChannelId { get; set; } = string.Empty;
}
