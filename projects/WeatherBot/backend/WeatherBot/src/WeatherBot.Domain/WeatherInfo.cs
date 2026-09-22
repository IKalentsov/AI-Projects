namespace WeatherBot.Domain;

/// <summary>
/// Текущее состояние погоды в точке наблюдения.
/// Значения приходят из внешнего API уже приведёнными к типам домена.
/// </summary>
/// <param name="TemperatureC">Температура воздуха, °C.</param>
/// <param name="FeelsLikeC">Ощущаемая температура, °C.</param>
/// <param name="HumidityPercent">Относительная влажность, %.</param>
/// <param name="PressureMmHg">Атмосферное давление, мм рт. ст.</param>
/// <param name="WindSpeedMs">Скорость ветра, м/с.</param>
/// <param name="WindDirection">Направление ветра.</param>
/// <param name="Cloudiness">Облачность.</param>
/// <param name="PrecipitationType">Тип осадков.</param>
/// <param name="PrecipitationStrength">Интенсивность осадков.</param>
/// <param name="ObservedAt">Момент получения данных.</param>
public sealed record WeatherInfo(
    int TemperatureC,
    int FeelsLikeC,
    int HumidityPercent,
    int PressureMmHg,
    double WindSpeedMs,
    WindDirection WindDirection,
    Cloudiness Cloudiness,
    PrecipitationType PrecipitationType,
    PrecipitationStrength PrecipitationStrength,
    DateTimeOffset ObservedAt);
