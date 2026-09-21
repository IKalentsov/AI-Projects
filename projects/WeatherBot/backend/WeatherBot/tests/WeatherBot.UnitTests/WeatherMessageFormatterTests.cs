namespace WeatherBot.UnitTests;

using System.Globalization;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using WeatherBot.Application.Abstractions;
using WeatherBot.Domain;
using WeatherBot.Infrastructure.Formatting;
using Xunit;

public class WeatherMessageFormatterTests
{
    private static IOptionsMonitor<WeatherSettings> CreateOptions(string city)
    {
        var settings = new WeatherSettings { City = city };
        var monitor = NSubstitute.Substitute.For<IOptionsMonitor<WeatherSettings>>();
        monitor.CurrentValue.Returns(settings);
        return monitor;
    }

    private static WeatherInfo CreateWeather(
        int temperatureC = 15,
        int feelsLikeC = 12,
        int humidityPercent = 70,
        int pressureMmHg = 750,
        double windSpeedMs = 3.5,
        WindDirection windDirection = WindDirection.SouthEast,
        Cloudiness cloudiness = Cloudiness.PartlyCloudy,
        PrecipitationType precipitationType = PrecipitationType.Rain,
        PrecipitationStrength precipitationStrength = PrecipitationStrength.Weak,
        DateTimeOffset? observedAt = null)
    {
        return new WeatherInfo(
            temperatureC, feelsLikeC, humidityPercent, pressureMmHg,
            windSpeedMs, windDirection, cloudiness,
            precipitationType, precipitationStrength,
            observedAt ?? new DateTimeOffset(2026, 9, 21, 15, 0, 0, TimeSpan.FromSeconds(10800)));
    }

    // ─── City from settings ────────────────────────────────────────────

    [Fact]
    public void Format_cityFromSettings_includesCityInHeader()
    {
        var formatter = new WeatherMessageFormatter(CreateOptions("Москва"));
        var result = formatter.Format(CreateWeather());
        result.Should().Contain("*Погода в Москва*");
    }

    [Fact]
    public void Format_differentCity_includesCorrectCityInHeader()
    {
        var formatter = new WeatherMessageFormatter(CreateOptions("Санкт-Петербург"));
        var result = formatter.Format(CreateWeather());
        result.Should().Contain("*Погода в Санкт-Петербург*");
    }

    // ─── No "МСК" literal ──────────────────────────────────────────────

    [Fact]
    public void Format_noLiteralMSK_in_output()
    {
        var formatter = new WeatherMessageFormatter(CreateOptions("Москва"));
        var result = formatter.Format(CreateWeather());
        result.Should().NotContain("МСК");
    }

    // ─── Offset display ────────────────────────────────────────────────

    [Fact]
    public void Format_positiveOffset_displaysUTCPlus()
    {
        var weather = CreateWeather(observedAt: new DateTimeOffset(2026, 9, 21, 15, 0, 0, TimeSpan.FromSeconds(7200)));
        var formatter = new WeatherMessageFormatter(CreateOptions("TestCity"));
        var result = formatter.Format(weather);
        result.Should().Contain("(UTC+2)");
    }

    [Fact]
    public void Format_negativeOffset_displaysUTCMinus()
    {
        var weather = CreateWeather(observedAt: new DateTimeOffset(2026, 9, 21, 15, 0, 0, TimeSpan.FromSeconds(-14400)));
        var formatter = new WeatherMessageFormatter(CreateOptions("TestCity"));
        var result = formatter.Format(weather);
        result.Should().Contain("(UTC-4)");
    }

    // ─── Unknown values ────────────────────────────────────────────────

    [Fact]
    public void Format_unknownCloudiness_displaysНетДанных()
    {
        var weather = CreateWeather(cloudiness: Cloudiness.Unknown);
        var formatter = new WeatherMessageFormatter(CreateOptions("TestCity"));
        var result = formatter.Format(weather);
        result.Should().Contain("Нет данных");
    }

    [Fact]
    public void Format_unknownWindDirection_displaysDash()
    {
        var weather = CreateWeather(windDirection: WindDirection.Unknown);
        var formatter = new WeatherMessageFormatter(CreateOptions("TestCity"));
        var result = formatter.Format(weather);
        result.Should().Contain("—");
    }

    // ─── Precipitation None and Unknown ────────────────────────────────

    [Fact]
    public void Format_precipitationNone_noStrengthParenthesis()
    {
        var weather = CreateWeather(precipitationType: PrecipitationType.None, precipitationStrength: PrecipitationStrength.None);
        var formatter = new WeatherMessageFormatter(CreateOptions("TestCity"));
        var result = formatter.Format(weather);
        result.Should().Contain("Без осадков");
        // Should NOT have a strength parenthesis after "Без осадков"
        var precipLine = result.Split('\n').FirstOrDefault(l => l.Contains("Осадки:", StringComparison.OrdinalIgnoreCase));
        precipLine!.Should().NotContain("(");
    }

    [Fact]
    public void Format_precipitationUnknown_noStrengthParenthesis()
    {
        var weather = CreateWeather(precipitationType: PrecipitationType.Unknown, precipitationStrength: PrecipitationStrength.Unknown);
        var formatter = new WeatherMessageFormatter(CreateOptions("TestCity"));
        var result = formatter.Format(weather);
        var precipLine = result.Split('\n').FirstOrDefault(l => l.Contains("Осадки:", StringComparison.OrdinalIgnoreCase));
        precipLine!.Should().Contain("Нет данных");
        precipLine.Should().NotContain("(");
    }

    [Fact]
    public void Format_precipitationRain_withStrengthParenthesis()
    {
        var weather = CreateWeather(precipitationType: PrecipitationType.Rain, precipitationStrength: PrecipitationStrength.Moderate);
        var formatter = new WeatherMessageFormatter(CreateOptions("TestCity"));
        var result = formatter.Format(weather);
        var precipLine = result.Split('\n').FirstOrDefault(l => l.Contains("Осадки:", StringComparison.OrdinalIgnoreCase));
        precipLine!.Should().Contain("Дождь (умеренные)");
    }

    [Fact]
    public void Format_precipitationHeavy_withStrengthParenthesis()
    {
        var weather = CreateWeather(precipitationType: PrecipitationType.Snow, precipitationStrength: PrecipitationStrength.Heavy);
        var formatter = new WeatherMessageFormatter(CreateOptions("TestCity"));
        var result = formatter.Format(weather);
        var precipLine = result.Split('\n').FirstOrDefault(l => l.Contains("Осадки:", StringComparison.OrdinalIgnoreCase));
        precipLine!.Should().Contain("Снег (сильные)");
    }
}
