namespace WeatherBot.Infrastructure.Weather;

/// <summary>Корневой объект ответа API Open-Meteo.</summary>
internal sealed class OpenMeteoResponse
{
    /// <summary>Смещение локального времени от UTC, секунды.</summary>
    public int UtcOffsetSeconds { get; set; }

    /// <summary>Блок текущей погоды.</summary>
    public OpenMeteoCurrent? Current { get; set; }
}

/// <summary>Текущая погода от Open-Meteo.</summary>
internal sealed class OpenMeteoCurrent
{
    /// <summary>Время (локальное, без смещения).</summary>
    public string? Time { get; set; }

    /// <summary>Температура, °C.</summary>
    public double Temperature2m { get; set; }

    /// <summary>Ощущаемая температура, °C.</summary>
    public double ApparentTemperature { get; set; }

    /// <summary>Влажность, %.</summary>
    public double RelativeHumidity2m { get; set; }

    /// <summary>Осадки, мм/ч.</summary>
    public double Precipitation { get; set; }

    /// <summary>Дождь, мм/ч.</summary>
    public double Rain { get; set; }

    /// <summary>Снег, см.</summary>
    public double Snowfall { get; set; }

    /// <summary>Код погоды WMO.</summary>
    public int WeatherCode { get; set; }

    /// <summary>Облачность, %.</summary>
    public double CloudCover { get; set; }

    /// <summary>Давление на уровне моря, гПа.</summary>
    public double PressureMsl { get; set; }

    /// <summary>Скорость ветра, м/с (запрошено wind_speed_unit=ms).</summary>
    public double WindSpeed10m { get; set; }

    /// <summary>Направление ветра, градусы.</summary>
    public int WindDirection10m { get; set; }

    /// <summary>Порывы ветра, м/с.</summary>
    public double WindGusts10m { get; set; }
}
