namespace WeatherBot.Domain;

/// <summary>Настройки источника погоды (секция <c>YandexWeather</c> в конфигурации).</summary>
public sealed class WeatherSettings
{
    /// <summary>Имя секции конфигурации.</summary>
    public const string SectionName = "YandexWeather";

    /// <summary>Ключ API Яндекс.Погоды (заголовок <c>X-Yandex-Weather-Key</c>).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Широта точки наблюдения.</summary>
    public double Latitude { get; set; } = 55.7558;

    /// <summary>Долгота точки наблюдения.</summary>
    public double Longitude { get; set; } = 37.6173;
}
