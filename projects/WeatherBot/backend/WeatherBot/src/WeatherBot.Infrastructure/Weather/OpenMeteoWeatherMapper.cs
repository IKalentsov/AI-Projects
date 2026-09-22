namespace WeatherBot.Infrastructure.Weather;

using System.Globalization;
using WeatherBot.Domain;

/// <summary>Переводит значения API Open-Meteo в типы предметной области.</summary>
internal static class OpenMeteoWeatherMapper
{
    /// <summary>Коэффициент перевода гектопаскалей в мм рт.ст.</summary>
    private const double HpaToMmHg = 0.750062;

    /// <summary>Преобразует давление из гПа в мм рт.ст.</summary>
    internal static int ToPressureMmHg(double hpa) =>
        (int)Math.Round(hpa * HpaToMmHg, MidpointRounding.AwayFromZero);

    /// <summary>Преобразует градусы в 8 румбов ветра.</summary>
    internal static WindDirection ToWindDirection(int degrees)
    {
        if (degrees < 0 || degrees >= 360)
            return WindDirection.Unknown;

        // Нормализуем на случай граничных значений.
        var d = degrees % 360;
        if (d < 0) d += 360;

        // 8 секторов по 45°, начало каждого — 22.5° + k*45°.
        var index = (int)((d + 22.5) / 45);
        return index switch
        {
            0 => WindDirection.North,
            1 => WindDirection.NorthEast,
            2 => WindDirection.East,
            3 => WindDirection.SouthEast,
            4 => WindDirection.South,
            5 => WindDirection.SouthWest,
            6 => WindDirection.West,
            7 => WindDirection.NorthWest,
            _ => WindDirection.Unknown
        };
    }

    /// <summary>Преобразует облачность из % в enum домена.</summary>
    internal static Cloudiness ToCloudiness(double percent) => percent switch
    {
        < 20 => Cloudiness.Clear,
        < 60 => Cloudiness.PartlyCloudy,
        < 85 => Cloudiness.Cloudy,
        _ => Cloudiness.Overcast
    };

    /// <summary>Преобразует код WMO и данные осадков в тип домена.</summary>
    internal static PrecipitationType ToPrecipitationType(
        int weatherCode,
        double precipitation,
        double rain,
        double snowfall)
    {
        // Приоритет уточнения: одновременно дождь и снег → смешанные.
        if (rain > 0 && snowfall > 0)
            return PrecipitationType.Mixed;

        // Нет осадков ни по одному измерению → без осадков, независимо от weather_code.
        if (precipitation == 0 && rain == 0 && snowfall == 0)
            return PrecipitationType.None;

        return weatherCode switch
        {
            // Без осадков: ясно, малооблачно, облачно, пасмурно, туман, замёрзший туман.
            0 or 1 or 2 or 3 or 45 or 48 => PrecipitationType.None,
            // Дождь: слабая, умеренная, сильная интенсивность; морось; ливни.
            51 or 53 or 55 or 56 or 57 or 61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => PrecipitationType.Rain,
            // Снег: слабое, умеренное, сильное; дождливый снег; мокрый снег.
            71 or 73 or 75 or 77 or 85 or 86 => PrecipitationType.Snow,
            // Град (гроза).
            96 or 99 => PrecipitationType.Hail,
            _ => PrecipitationType.Unknown
        };
    }

    /// <summary>Преобразует интенсивность осадков (мм/ч) в enum домена.</summary>
    internal static PrecipitationStrength ToPrecipitationStrength(double precipitationMmH) =>
        precipitationMmH switch
        {
            <= 0 => PrecipitationStrength.None,
            > 0 and <= 0.5 => PrecipitationStrength.Weak,
            > 0.5 and <= 2.5 => PrecipitationStrength.Moderate,
            > 2.5 and <= 7.5 => PrecipitationStrength.Heavy,
            _ => PrecipitationStrength.VeryHeavy
        };

    /// <summary>Собирает DateTimeOffset из локального времени API и смещения. Возвращает false при ошибке разбора.</summary>
    internal static bool TryParseObservedAt(string? localTime, int utcOffsetSeconds, out DateTimeOffset observedAt)
    {
        if (string.IsNullOrWhiteSpace(localTime) || !DateTime.TryParse(localTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            observedAt = default;
            return false;
        }

        observedAt = new DateTimeOffset(dt, TimeSpan.FromSeconds(utcOffsetSeconds));
        return true;
    }
}
