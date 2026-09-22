namespace WeatherBot.Infrastructure.Weather;

using System.Globalization;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WeatherBot.Application.Abstractions;
using WeatherBot.Domain;

/// <summary>Получение текущей погоды через REST API Open-Meteo.</summary>
public sealed class OpenMeteoWeatherProvider(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<WeatherSettings> weatherOptions,
    ILogger<OpenMeteoWeatherProvider> logger) : IWeatherProvider
{
    /// <summary>Таймаут обращения к API, секунды.</summary>
    public const int RequestTimeoutSeconds = 30;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<Result<WeatherInfo>> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var settings = weatherOptions.CurrentValue;

        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(RequestTimeoutSeconds);

        var url = BuildUrl(settings);

        try
        {
            using var response = await client.GetAsync(new Uri(url), cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Open-Meteo вернула код {StatusCode}", (int)response.StatusCode);

                return Result.Failure<WeatherInfo>(
                    $"Open-Meteo вернула код {(int)response.StatusCode}");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseResponse(body, settings);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Сбой обращения к Open-Meteo");

            return Result.Failure<WeatherInfo>(
                $"Не удалось связаться с Open-Meteo: {exception.Message}");
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Open-Meteo не ответила за {Timeout} с", RequestTimeoutSeconds);

            return Result.Failure<WeatherInfo>(
                $"Open-Meteo не ответила за {RequestTimeoutSeconds} с");
        }
        catch (JsonException exception)
        {
            logger.LogError(exception, "Не удалось разобрать ответ Open-Meteo");

            return Result.Failure<WeatherInfo>(
                $"Некорректный ответ Open-Meteo: {exception.Message}");
        }
    }

    private static string BuildUrl(WeatherSettings settings) =>
        $"https://api.open-meteo.com/v1/forecast" +
        $"?latitude={settings.Latitude.ToString(CultureInfo.InvariantCulture)}" +
        $"&longitude={settings.Longitude.ToString(CultureInfo.InvariantCulture)}" +
        $"&current=temperature_2m,relative_humidity_2m,apparent_temperature," +
        $"precipitation,rain,snowfall,weather_code,cloud_cover," +
        $"pressure_msl,wind_speed_10m,wind_direction_10m,wind_gusts_10m" +
        "&wind_speed_unit=ms" +
        "&timezone=auto";

    Result<WeatherInfo> ParseResponse(string body, WeatherSettings settings)
    {
        var parsed = JsonSerializer.Deserialize<OpenMeteoResponse>(body, SerializerOptions);

        if (parsed?.Current is null)
        {
            logger.LogError("В ответе Open-Meteo нет блока current");

            return Result.Failure<WeatherInfo>(
                "В ответе Open-Meteo нет данных о текущей погоде");
        }

        var c = parsed.Current;

        if (!OpenMeteoWeatherMapper.TryParseObservedAt(c.Time, parsed.UtcOffsetSeconds, out var observedAt))
        {
            logger.LogError("Не удалось разобрать время из Open-Meteo (time='{Time}', offset={Offset})", c.Time, parsed.UtcOffsetSeconds);

            return Result.Failure<WeatherInfo>("Не удалось разобрать время наблюдения из ответа Open-Meteo");
        }

        var weather = new WeatherInfo(
            TemperatureC: (int)Math.Round(c.Temperature2m),
            FeelsLikeC: (int)Math.Round(c.ApparentTemperature),
            HumidityPercent: (int)Math.Round(c.RelativeHumidity2m, MidpointRounding.AwayFromZero),
            PressureMmHg: OpenMeteoWeatherMapper.ToPressureMmHg(c.PressureMsl),
            WindSpeedMs: c.WindSpeed10m,
            WindDirection: OpenMeteoWeatherMapper.ToWindDirection(c.WindDirection10m),
            Cloudiness: OpenMeteoWeatherMapper.ToCloudiness(c.CloudCover),
            PrecipitationType: OpenMeteoWeatherMapper.ToPrecipitationType(
                c.WeatherCode, c.Precipitation, c.Rain, c.Snowfall),
            PrecipitationStrength: OpenMeteoWeatherMapper.ToPrecipitationStrength(c.Precipitation),
            ObservedAt: observedAt);

        logger.LogInformation(
            "Погода получена: {Temperature} °C, {Cloudiness}, точка {Latitude}/{Longitude}",
            weather.TemperatureC,
            weather.Cloudiness,
            settings.Latitude,
            settings.Longitude);

        return Result.Success(weather);
    }
}
