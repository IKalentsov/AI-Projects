namespace WeatherBot.Infrastructure.Weather;

using WeatherBot.Domain;

/// <summary>
/// Переводит строковые значения enum-ов API Яндекс.Погоды в типы предметной области.
/// Неизвестные значения дают <c>Unknown</c> — сообщение всё равно будет отправлено.
/// </summary>
internal static class YandexWeatherMapper
{
    /// <summary>Облачность.</summary>
    internal static Cloudiness ToCloudiness(string? value) => value?.ToUpperInvariant() switch
    {
        "CLEAR" => Cloudiness.Clear,
        "PARTLY_CLOUDY" => Cloudiness.PartlyCloudy,
        "CLOUDY" => Cloudiness.Cloudy,
        "OVERCAST" => Cloudiness.Overcast,
        _ => Cloudiness.Unknown
    };

    /// <summary>Тип осадков.</summary>
    internal static PrecipitationType ToPrecipitationType(string? value) => value?.ToUpperInvariant() switch
    {
        "NO_TYPE" => PrecipitationType.None,
        "RAIN" => PrecipitationType.Rain,
        "SNOW" => PrecipitationType.Snow,
        "HAIL" => PrecipitationType.Hail,
        "MIXED" => PrecipitationType.Mixed,
        _ => PrecipitationType.Unknown
    };

    /// <summary>Интенсивность осадков.</summary>
    internal static PrecipitationStrength ToPrecipitationStrength(string? value) => value?.ToUpperInvariant() switch
    {
        "ZERO" => PrecipitationStrength.None,
        "WEAK" => PrecipitationStrength.Weak,
        "MODERATE" => PrecipitationStrength.Moderate,
        "STRONG" => PrecipitationStrength.Heavy,
        "VERY_STRONG" => PrecipitationStrength.VeryHeavy,
        _ => PrecipitationStrength.Unknown
    };

    /// <summary>Направление ветра. Штиль (<c>CALM</c>) направлением не считается.</summary>
    internal static WindDirection ToWindDirection(string? value) => value?.ToUpperInvariant() switch
    {
        "NORTH" => WindDirection.North,
        "NORTH_EAST" => WindDirection.NorthEast,
        "EAST" => WindDirection.East,
        "SOUTH_EAST" => WindDirection.SouthEast,
        "SOUTH" => WindDirection.South,
        "SOUTH_WEST" => WindDirection.SouthWest,
        "WEST" => WindDirection.West,
        "NORTH_WEST" => WindDirection.NorthWest,
        _ => WindDirection.Unknown
    };
}
