namespace WeatherBot.UnitTests;

using System.Globalization;
using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using WeatherBot.Domain;
using WeatherBot.Infrastructure.Weather;
using Xunit;

public class OpenMeteoWeatherProviderTests
{
    // ─── helpers ────────────────────────────────────────────────────────

    private static IOptionsMonitor<WeatherSettings> CreateWeatherOptions(
        double latitude = 55.7558, double longitude = 37.6173)
    {
        var settings = new WeatherSettings { Latitude = latitude, Longitude = longitude };
        var monitor = Substitute.For<IOptionsMonitor<WeatherSettings>>();
        monitor.CurrentValue.Returns(settings);
        return monitor;
    }

    private static async Task<T> WithClient<T>(
        HttpMessageHandler handler,
        Func<OpenMeteoWeatherProvider, Task<T>> action,
        IOptionsMonitor<WeatherSettings>? weatherOptions = null)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        using var client = new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(30);
        factory.CreateClient(Arg.Any<string>()).Returns(client);

        var provider = new OpenMeteoWeatherProvider(factory, weatherOptions ?? CreateWeatherOptions(), NullLogger<OpenMeteoWeatherProvider>.Instance);
        return await action(provider);
    }

    /// <summary>
    /// Формирует JSON-ответ Open-Meteo с именами свойств в snake_case (как отдаёт реальный API).
    /// </summary>
    private static string SuccessJson(
        double temperature = 15.3,
        double humidity = 72.4,
        double apparentTemp = 12.1,
        double precipitation = 0.3,
        double rain = 0.3,
        double snowfall = 0,
        int weatherCode = 3,
        double cloudCover = 65,
        double pressureMsl = 1013.25,
        double windSpeed = 4.2,
        int windDir = 135,
        double windGust = 7.8,
        string time = "2026-09-21T15:00",
        int utcOffsetSeconds = 10800)
    {
        var current = new
        {
            time,
            temperature_2m = temperature,
            relative_humidity_2m = humidity,
            apparent_temperature = apparentTemp,
            precipitation = precipitation,
            rain = rain,
            snowfall = snowfall,
            weather_code = weatherCode,
            cloud_cover = cloudCover,
            pressure_msl = pressureMsl,
            wind_speed_10m = windSpeed,
            wind_direction_10m = windDir,
            wind_gusts_10m = windGust
        };

        return JsonSerializer.Serialize(new { utc_offset_seconds = utcOffsetSeconds, current });
    }

    // ─── success ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCurrentAsync_success_returnsWeatherInfo()
    {
        using var handler = new SuccessHandler(SuccessJson());

        var result = await WithClient(handler, async p => await p.GetCurrentAsync(CancellationToken.None));

        result.IsSuccess.Should().BeTrue();
        var info = result.Value;
        info.TemperatureC.Should().Be(15);       // (int)Math.Round(15.3)
        info.FeelsLikeC.Should().Be(12);          // (int)Math.Round(12.1)
        info.HumidityPercent.Should().Be(72);     // (int)72.4
        info.PressureMmHg.Should().Be(760);       // 1013.25 * 0.750062 → 760
        info.WindSpeedMs.Should().Be(4.2);
        info.WindDirection.Should().Be(WindDirection.SouthEast);
        info.Cloudiness.Should().Be(Cloudiness.Cloudy);
        info.PrecipitationStrength.Should().Be(PrecipitationStrength.Weak);
        // ObservedAt: 15:00 + offset 10800 (3h) → UTC+3
        info.ObservedAt.Offset.Should().Be(TimeSpan.FromSeconds(10800));
    }

    [Fact]
    public async Task GetCurrentAsync_success_withHalfHourOffset_appliesOffsetMinutes()
    {
        using var handler = new SuccessHandler(SuccessJson(time: "2026-09-21T10:30", utcOffsetSeconds: 20700));

        var result = await WithClient(handler, async p => await p.GetCurrentAsync(CancellationToken.None));

        result.IsSuccess.Should().BeTrue();
        result.Value.ObservedAt.Offset.Should().Be(TimeSpan.FromSeconds(20700)); // +5:45
    }

    [Fact]
    public async Task GetCurrentAsync_success_withNegativeOffset_appliesCorrectOffset()
    {
        using var handler = new SuccessHandler(SuccessJson(time: "2026-09-21T10:00", utcOffsetSeconds: -18000));

        var result = await WithClient(handler, async p => await p.GetCurrentAsync(CancellationToken.None));

        result.IsSuccess.Should().BeTrue();
        result.Value.ObservedAt.Offset.Should().Be(TimeSpan.FromSeconds(-18000));
    }

    [Fact]
    public async Task GetCurrentAsync_success_requestContainsRequiredParams()
    {
        using var handler = new SuccessHandler(SuccessJson());

        await WithClient(handler, async p =>
        {
            _ = await p.GetCurrentAsync(CancellationToken.None);
            return true;
        });

        handler.RequestUrl.Should().NotBeNull();
        handler.RequestUrl.ToString().Should().Contain("wind_speed_unit=ms");
        handler.RequestUrl.ToString().Should().Contain("timezone=auto");
    }

    [Fact]
    public async Task GetCurrentAsync_requestCoordinates_areCultureInvariant()
    {
        // Интерполяция строки форматирует double в текущей культуре: при ru-RU
        // координаты ушли бы в API как 55,7558, и Open-Meteo отвечает 400.
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ru-RU");

            using var handler = new SuccessHandler(SuccessJson());

            await WithClient(handler, async p => await p.GetCurrentAsync(CancellationToken.None));

            var url = handler.RequestUrl!.ToString();
            url.Should().Contain("latitude=55.7558");
            url.Should().Contain("longitude=37.6173");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task GetCurrentAsync_success_withFractionalHumidity_roundsNotTruncates()
    {
        using var handler = new SuccessHandler(SuccessJson(humidity: 72.6));

        var result = await WithClient(handler, async p => await p.GetCurrentAsync(CancellationToken.None));

        result.IsSuccess.Should().BeTrue();
        result.Value.HumidityPercent.Should().Be(73); // Math.Round(72.6) → 73, not (int)72.6 → 72
    }

    [Fact]
    public async Task GetCurrentAsync_success_withFractionalHumidityBelowHalf_roundsDown()
    {
        using var handler = new SuccessHandler(SuccessJson(humidity: 72.4));

        var result = await WithClient(handler, async p => await p.GetCurrentAsync(CancellationToken.None));

        result.IsSuccess.Should().BeTrue();
        result.Value.HumidityPercent.Should().Be(72); // Math.Round(72.4) → 72
    }

    // ─── non-200 ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCurrentAsync_non200_returnsFailure()
    {
        using var handler = new ErrorHandler(HttpStatusCode.BadGateway);

        var result = await WithClient(handler, async p => await p.GetCurrentAsync(CancellationToken.None));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("502");
    }

    [Fact]
    public async Task GetCurrentAsync_404_returnsFailure()
    {
        using var handler = new ErrorHandler(HttpStatusCode.NotFound);

        var result = await WithClient(handler, async p => await p.GetCurrentAsync(CancellationToken.None));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("404");
    }

    // ─── timeout ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCurrentAsync_timeoutWithoutCancellation_returnsFailure()
    {
        using var handler = new TimeoutHandler();

        var result = await WithClient(handler, async p => await p.GetCurrentAsync(CancellationToken.None));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("не ответила");
    }

    // ─── bad JSON ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetCurrentAsync_badJson_returnsFailure()
    {
        using var handler = new SuccessHandler("not json at all {{{");

        var result = await WithClient(handler, async p => await p.GetCurrentAsync(CancellationToken.None));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Некорректный ответ");
    }

    // ─── missing current block ──────────────────────────────────────────

    [Fact]
    public async Task GetCurrentAsync_missingCurrentBlock_returnsFailure()
    {
        var json = JsonSerializer.Serialize(new { utc_offset_seconds = 10800 });
        using var handler = new SuccessHandler(json);

        var result = await WithClient(handler, async p => await p.GetCurrentAsync(CancellationToken.None));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("нет данных о текущей погоде");
    }

    // ─── unparseable time ───────────────────────────────────────────────

    [Fact]
    public async Task GetCurrentAsync_nullTime_returnsFailure()
    {
        using var handler = new SuccessHandler(SuccessJson(time: ""));

        var result = await WithClient(handler, async p => await p.GetCurrentAsync(CancellationToken.None));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Не удалось разобрать время");
    }

    [Fact]
    public async Task GetCurrentAsync_invalidTimeFormat_returnsFailure()
    {
        using var handler = new SuccessHandler(SuccessJson(time: "not-a-date"));

        var result = await WithClient(handler, async p => await p.GetCurrentAsync(CancellationToken.None));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Не удалось разобрать время");
    }

    // ─── no exception escapes (parameterised) ───────────────────────────

    [Theory]
    [InlineData("ServiceUnavailable", "503")]
    [InlineData("BadJson", "Некорректный")]
    [InlineData("MissingCurrent", "нет данных")]
    [InlineData("InvalidTime", "время наблюдения")]
    public async Task GetCurrentAsync_allPaths_returnResultNotException(string scenario, string expectedInError)
    {
        HttpMessageHandler handler = scenario switch
        {
            "ServiceUnavailable" => new ErrorHandler(HttpStatusCode.ServiceUnavailable),
            "BadJson" => new SuccessHandler("bad json"),
            "MissingCurrent" => new SuccessHandler(JsonSerializer.Serialize(new { utc_offset_seconds = 0 })),
            "InvalidTime" => new SuccessHandler(SuccessJson(time: "")),
            _ => throw new ArgumentException($"Unknown scenario: {scenario}", nameof(scenario)),
        };

        using var _ = handler;
        var result = await WithClient(handler, async p => await p.GetCurrentAsync(CancellationToken.None));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(expectedInError);
    }

    // ─── stub handlers ──────────────────────────────────────────────────

    private sealed class SuccessHandler : HttpMessageHandler
    {
        private readonly string _body;

        public SuccessHandler(string body) => _body = body;

        public Uri? RequestUrl { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUrl = request.RequestUri;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, System.Text.Encoding.UTF8, "application/json")
            };
            return response;
        }
    }

    private sealed class ErrorHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;

        public ErrorHandler(HttpStatusCode status) => _status = status;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_status);
            return Task.FromResult(response);
        }
    }

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Simulate a timeout by throwing TaskCanceledException directly —
            // OpenMeteoWeatherProvider catches it and returns Result.Failure.
            return Task.FromException<HttpResponseMessage>(new TaskCanceledException());
        }
    }
}
