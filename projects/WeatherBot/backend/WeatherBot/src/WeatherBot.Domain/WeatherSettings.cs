namespace WeatherBot.Domain;

/// <summary>Настройки источника погоды Open-Meteo (секция <c>OpenMeteo</c> в конфигурации).</summary>
public sealed class WeatherSettings
{
    /// <summary>Имя секции конфигурации.</summary>
    public const string SectionName = "OpenMeteo";

    /// <summary>Широта точки наблюдения.</summary>
    public double Latitude { get; set; } = 55.7558;

    /// <summary>Долгота точки наблюдения.</summary>
    public double Longitude { get; set; } = 37.6173;

    /// <summary>Город для подписи в заголовке сообщения.</summary>
    public string City { get; set; } = "Москва";
}
