namespace WeatherBot.Infrastructure.Weather;

using System.Text.Json.Serialization;

/// <summary>Корневой объект ответа API Open-Meteo.</summary>
internal sealed class OpenMeteoResponse
{
    /// <summary>Смещение локального времени от UTC, секунды.</summary>
    [JsonPropertyName("utc_offset_seconds")]
    public int UtcOffsetSeconds { get; set; }

    /// <summary>Блок текущей погоды.</summary>
    [JsonPropertyName("current")]
    public OpenMeteoCurrent? Current { get; set; }
}

/// <summary>Текущая погода от Open-Meteo.</summary>
internal sealed class OpenMeteoCurrent
{
    /// <summary>Время (локальное, без смещения).</summary>
    [JsonPropertyName("time")]
    public string? Time { get; set; }

    /// <summary>Температура, °C.</summary>
    [JsonPropertyName("temperature_2m")]
    public double Temperature2m { get; set; }

    /// <summary>Ощущаемая температура, °C.</summary>
    [JsonPropertyName("apparent_temperature")]
    public double ApparentTemperature { get; set; }

    /// <summary>Влажность, %.</summary>
    [JsonPropertyName("relative_humidity_2m")]
    public double RelativeHumidity2m { get; set; }

    /// <summary>Осадки, мм/ч.</summary>
    [JsonPropertyName("precipitation")]
    public double Precipitation { get; set; }

    /// <summary>Дождь, мм/ч.</summary>
    [JsonPropertyName("rain")]
    public double Rain { get; set; }

    /// <summary>Снег, см.</summary>
    [JsonPropertyName("snowfall")]
    public double Snowfall { get; set; }

    /// <summary>Код погоды WMO.</summary>
    [JsonPropertyName("weather_code")]
    public int WeatherCode { get; set; }

    /// <summary>Облачность, %.</summary>
    [JsonPropertyName("cloud_cover")]
    public double CloudCover { get; set; }

    /// <summary>Давление на уровне моря, гПа.</summary>
    [JsonPropertyName("pressure_msl")]
    public double PressureMsl { get; set; }

    /// <summary>Скорость ветра, м/с (запрошено wind_speed_unit=ms).</summary>
    [JsonPropertyName("wind_speed_10m")]
    public double WindSpeed10m { get; set; }

    /// <summary>Направление ветра, градусы.</summary>
    [JsonPropertyName("wind_direction_10m")]
    public int WindDirection10m { get; set; }

    /// <summary>Порывы ветра, м/с.</summary>
    [JsonPropertyName("wind_gusts_10m")]
    public double WindGusts10m { get; set; }
}
