namespace WeatherBot.Infrastructure.Weather;

/// <summary>Тело GraphQL-запроса к API Яндекс.Погоды.</summary>
/// <param name="Query">Текст запроса.</param>
/// <param name="Variables">Переменные запроса.</param>
internal sealed record YandexWeatherQueryRequest(string Query, YandexWeatherQueryVariables Variables);

/// <summary>Переменные GraphQL-запроса.</summary>
/// <param name="Request">Точка, для которой запрашивается погода.</param>
internal sealed record YandexWeatherQueryVariables(PointInput Request);

/// <summary>Географическая точка.</summary>
/// <param name="Lat">Широта.</param>
/// <param name="Lon">Долгота.</param>
internal sealed record PointInput(double Lat, double Lon);

/// <summary>Ответ GraphQL: либо данные, либо список ошибок.</summary>
/// <param name="WeatherByPoint">Запрошенные данные.</param>
/// <param name="Errors">Ошибки GraphQL.</param>
internal sealed record YandexWeatherQueryResponse(
    WeatherByPointPayload? WeatherByPoint,
    IReadOnlyList<YandexWeatherError>? Errors);

/// <summary>Ошибка GraphQL.</summary>
/// <param name="Message">Текст ошибки.</param>
internal sealed record YandexWeatherError(string? Message);

/// <summary>Полезная нагрузка ответа <c>weatherByPoint</c>.</summary>
/// <param name="Now">Текущая погода.</param>
internal sealed record WeatherByPointPayload(WeatherNowPayload? Now);

/// <summary>Текущая погода (<c>now</c>).</summary>
/// <param name="Temperature">Температура, °C.</param>
/// <param name="FeelsLike">Ощущаемая температура, °C.</param>
/// <param name="Humidity">Влажность, %.</param>
/// <param name="Pressure">Давление, мм рт. ст.</param>
/// <param name="WindSpeed">Скорость ветра, м/с.</param>
/// <param name="WindDirection">Направление ветра (enum API, строкой).</param>
/// <param name="PrecType">Тип осадков (enum API, строкой).</param>
/// <param name="PrecStrength">Интенсивность осадков (enum API, строкой).</param>
/// <param name="Cloudiness">Облачность (enum API, строкой).</param>
internal sealed record WeatherNowPayload(
    int Temperature,
    int FeelsLike,
    int Humidity,
    int Pressure,
    double WindSpeed,
    string? WindDirection,
    string? PrecType,
    string? PrecStrength,
    string? Cloudiness);
