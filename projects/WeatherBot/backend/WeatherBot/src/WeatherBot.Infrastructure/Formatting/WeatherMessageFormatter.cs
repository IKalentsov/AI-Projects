namespace WeatherBot.Infrastructure.Formatting;

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using WeatherBot.Application.Abstractions;
using WeatherBot.Domain;

/// <summary>Формирует текст сообщения о погоде в разметке Markdown.</summary>
public sealed class WeatherMessageFormatter(
    IOptionsMonitor<WeatherSettings> weatherOptions) : IWeatherFormatter
{
    /// <inheritdoc />
    public string Format(WeatherInfo weather)
    {
        ArgumentNullException.ThrowIfNull(weather);

        var city = weatherOptions.CurrentValue.City;

        var builder = new StringBuilder();

        builder.AppendLine(CultureInfo.InvariantCulture, $"🌤 *Погода в {city}*");
        builder.AppendLine();

        builder
            .Append("🌡 *Температура:* ")
            .Append(Number(weather.TemperatureC))
            .Append("°C (ощущается как ")
            .Append(Number(weather.FeelsLikeC))
            .Append("°C)")
            .AppendLine();

        builder
            .Append("☁️ *Облачность:* ")
            .Append(CloudinessText(weather.Cloudiness))
            .AppendLine();

        builder
            .Append("💧 *Влажность:* ")
            .Append(Number(weather.HumidityPercent))
            .Append('%')
            .AppendLine();

        builder
            .Append("📊 *Давление:* ")
            .Append(Number(weather.PressureMmHg))
            .Append(" мм рт.ст.")
            .AppendLine();

        builder
            .Append("💨 *Ветер:* ")
            .Append(weather.WindSpeedMs.ToString("0.#", CultureInfo.InvariantCulture))
            .Append(" м/с, ")
            .Append(WindDirectionText(weather.WindDirection))
            .AppendLine();

        builder
            .Append("🌧 *Осадки:* ")
            .Append(PrecipitationText(weather.PrecipitationType, weather.PrecipitationStrength))
            .AppendLine();

        builder.AppendLine();

        var offset = weather.ObservedAt.Offset;
        var sign = offset.TotalHours >= 0 ? "+" : "-";
        var absMinutes = Math.Abs(offset.Minutes);
        var offsetStr = absMinutes == 0
            ? $"UTC{sign}{Math.Abs(offset.Hours)}"
            : $"UTC{sign}{Math.Abs(offset.Hours)}:{absMinutes:D2}";

        builder
            .Append("_Обновлено: ")
            .Append(weather.ObservedAt.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture))
            .Append(CultureInfo.InvariantCulture, $" ({offsetStr})_");

        return builder.ToString();
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string CloudinessText(Cloudiness cloudiness) => cloudiness switch
    {
        Cloudiness.Clear => "Ясно",
        Cloudiness.PartlyCloudy => "Малооблачно",
        Cloudiness.Cloudy => "Облачно",
        Cloudiness.Overcast => "Пасмурно",
        _ => "Нет данных"
    };

    private static string WindDirectionText(WindDirection direction) => direction switch
    {
        WindDirection.North => "С",
        WindDirection.NorthEast => "СВ",
        WindDirection.East => "В",
        WindDirection.SouthEast => "ЮВ",
        WindDirection.South => "Ю",
        WindDirection.SouthWest => "ЮЗ",
        WindDirection.West => "З",
        WindDirection.NorthWest => "СЗ",
        _ => "—"
    };

    private static string PrecipitationText(
        PrecipitationType type,
        PrecipitationStrength strength)
    {
        var typeText = type switch
        {
            PrecipitationType.None => "Без осадков",
            PrecipitationType.Rain => "Дождь",
            PrecipitationType.Snow => "Снег",
            PrecipitationType.Hail => "Град",
            PrecipitationType.Mixed => "Смешанные",
            _ => "Нет данных"
        };

        if (type is PrecipitationType.None or PrecipitationType.Unknown)
        {
            return typeText;
        }

        var strengthText = strength switch
        {
            PrecipitationStrength.Weak => "слабые",
            PrecipitationStrength.Moderate => "умеренные",
            PrecipitationStrength.Heavy => "сильные",
            PrecipitationStrength.VeryHeavy => "очень сильные",
            _ => null
        };

        return strengthText is null ? typeText : $"{typeText} ({strengthText})";
    }
}
