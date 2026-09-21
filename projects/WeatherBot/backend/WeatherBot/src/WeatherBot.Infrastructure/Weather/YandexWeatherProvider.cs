namespace WeatherBot.Infrastructure.Weather;

using System.Text;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WeatherBot.Application.Abstractions;
using WeatherBot.Domain;
using WeatherBot.Infrastructure.Common;

/// <summary>Получение текущей погоды через GraphQL API Яндекс.Погоды.</summary>
public sealed class YandexWeatherProvider(
    HttpClient httpClient,
    IOptionsMonitor<WeatherSettings> weatherOptions,
    ILogger<YandexWeatherProvider> logger) : IWeatherProvider
{
    /// <summary>Таймаут обращения к API, секунды.</summary>
    public const int RequestTimeoutSeconds = 30;

    /// <summary>Заголовок авторизации API Яндекс.Погоды.</summary>
    internal const string ApiKeyHeaderName = "X-Yandex-Weather-Key";

    private const string Endpoint = "https://api.weather.yandex.ru/graphql/query";

    private const string QueryText = """
        query weatherByPoint($request: PointInput!) {
          weatherByPoint(request: $request) {
            now {
              temperature
              feelsLike
              humidity
              pressure
              windSpeed
              windDirection
              precType
              precStrength
              cloudiness
            }
          }
        }
        """;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<Result<WeatherInfo>> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var settings = weatherOptions.CurrentValue;

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            logger.LogError(
                "Ключ API Яндекс.Погоды не задан: {Section}:ApiKey",
                WeatherSettings.SectionName);

            return Result.Failure<WeatherInfo>("Ключ API Яндекс.Погоды не задан в конфигурации");
        }

        var payload = new YandexWeatherQueryRequest(
            QueryText,
            new YandexWeatherQueryVariables(new PointInput(settings.Latitude, settings.Longitude)));

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload, SerializerOptions),
                    Encoding.UTF8,
                    "application/json")
            };

            request.Headers.Add(ApiKeyHeaderName, settings.ApiKey);

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Яндекс.Погода вернула код {StatusCode}", (int)response.StatusCode);

                return Result.Failure<WeatherInfo>(
                    $"Яндекс.Погода вернула код {(int)response.StatusCode}");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            return ParseResponse(body, settings);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Сбой обращения к Яндекс.Погоде");

            return Result.Failure<WeatherInfo>(
                $"Не удалось связаться с Яндекс.Погодой: {SecretMasker.Mask(exception.Message, settings.ApiKey)}");
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Яндекс.Погода не ответила за {Timeout} с", RequestTimeoutSeconds);

            return Result.Failure<WeatherInfo>($"Яндекс.Погода не ответила за {RequestTimeoutSeconds} с");
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Не удалось разобрать ответ Яндекс.Погоды");

            return Result.Failure<WeatherInfo>(
                $"Некорректный ответ Яндекс.Погоды: {SecretMasker.Mask(exception.Message, settings.ApiKey)}");
        }
    }

    private Result<WeatherInfo> ParseResponse(string body, WeatherSettings settings)
    {
        var parsed = JsonSerializer.Deserialize<YandexWeatherQueryResponse>(body, SerializerOptions);

        if (parsed?.Errors is { Count: > 0 } errors)
        {
            var firstError = errors[0].Message ?? "без описания";
            logger.LogError("Яндекс.Погода вернула ошибку GraphQL: {Error}", firstError);

            return Result.Failure<WeatherInfo>($"Ошибка GraphQL Яндекс.Погоды: {firstError}");
        }

        var now = parsed?.WeatherByPoint?.Now;
        if (now is null)
        {
            logger.LogError("В ответе Яндекс.Погоды нет блока now");

            return Result.Failure<WeatherInfo>("В ответе Яндекс.Погоды нет данных о текущей погоде");
        }

        var weather = new WeatherInfo(
            TemperatureC: now.Temperature,
            FeelsLikeC: now.FeelsLike,
            HumidityPercent: now.Humidity,
            PressureMmHg: now.Pressure,
            WindSpeedMs: now.WindSpeed,
            WindDirection: YandexWeatherMapper.ToWindDirection(now.WindDirection),
            Cloudiness: YandexWeatherMapper.ToCloudiness(now.Cloudiness),
            PrecipitationType: YandexWeatherMapper.ToPrecipitationType(now.PrecType),
            PrecipitationStrength: YandexWeatherMapper.ToPrecipitationStrength(now.PrecStrength),
            ObservedAt: DateTimeOffset.Now);

        logger.LogInformation(
            "Погода получена: {Temperature} °C, {Cloudiness}, точка {Latitude}/{Longitude}",
            weather.TemperatureC,
            weather.Cloudiness,
            settings.Latitude,
            settings.Longitude);

        return Result.Success(weather);
    }
}
