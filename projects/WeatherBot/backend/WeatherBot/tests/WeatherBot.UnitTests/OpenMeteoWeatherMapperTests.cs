namespace WeatherBot.UnitTests;

using System.Globalization;
using AwesomeAssertions;
using WeatherBot.Domain;
using WeatherBot.Infrastructure.Weather;
using Xunit;

public class OpenMeteoWeatherMapperTests
{
    // ─── ToPressureMmHg ────────────────────────────────────────────────

    [Fact]
    public void ToPressureMmHg_normalValue_returnsCorrectRounded()
    {
        // 1013.25 hPa × 0.750062 = 759.98… → 760
        var result = OpenMeteoWeatherMapper.ToPressureMmHg(1013.25);
        result.Should().Be(760);
    }

    [Fact]
    public void ToPressureMmHg_integerHpa_returnsExactConversion()
    {
        // 1000 hPa × 0.750062 = 750.062 → 750
        var result = OpenMeteoWeatherMapper.ToPressureMmHg(1000);
        result.Should().Be(750);
    }

    [Fact]
    public void ToPressureMmHg_lowPressure_returnsLowValue()
    {
        var result = OpenMeteoWeatherMapper.ToPressureMmHg(980);
        result.Should().Be(735); // 980 * 0.750062 = 735.06 → 735
    }

    [Fact]
    public void ToPressureMmHg_highPressure_returnsHighValue()
    {
        var result = OpenMeteoWeatherMapper.ToPressureMmHg(1040);
        result.Should().Be(780); // 1040 * 0.750062 = 780.06 → 780
    }

    // ─── ToWindDirection ────────────────────────────────────────────────

    [Theory]
    [InlineData(0, WindDirection.North)]              // Север (0°)
    [InlineData(45, WindDirection.NorthEast)]         // СВ (45°)
    [InlineData(90, WindDirection.East)]              // Восток (90°)
    [InlineData(135, WindDirection.SouthEast)]        // ЮВ (135°)
    [InlineData(180, WindDirection.South)]            // Юг (180°)
    [InlineData(225, WindDirection.SouthWest)]        // ЮЗ (225°)
    [InlineData(270, WindDirection.West)]             // Запад (270°)
    [InlineData(315, WindDirection.NorthWest)]        // СЗ (315°)
    [InlineData(23, WindDirection.NorthEast)]         // граница сектора: 22.5° — уже СВ
    [InlineData(337, WindDirection.NorthWest)]        // граница сектора: до 337.5° — ещё СЗ
    [InlineData(-1, WindDirection.Unknown)]           // вне диапазона
    [InlineData(360, WindDirection.Unknown)]
    [InlineData(361, WindDirection.Unknown)]
    [InlineData(720, WindDirection.Unknown)]
    public void ToWindDirection_returnsExpectedDirection(int degrees, WindDirection expected)
    {
        var result = OpenMeteoWeatherMapper.ToWindDirection(degrees);
        result.Should().Be(expected);
    }

    // ─── ToCloudiness ───────────────────────────────────────────────────

    [Theory]
    [InlineData(0, Cloudiness.Clear)]
    [InlineData(19.9, Cloudiness.Clear)]
    [InlineData(20, Cloudiness.PartlyCloudy)]
    [InlineData(30, Cloudiness.PartlyCloudy)]
    [InlineData(59.9, Cloudiness.PartlyCloudy)]
    [InlineData(60, Cloudiness.Cloudy)]
    [InlineData(70, Cloudiness.Cloudy)]
    [InlineData(84.9, Cloudiness.Cloudy)]
    [InlineData(85, Cloudiness.Overcast)]
    [InlineData(90, Cloudiness.Overcast)]
    [InlineData(100, Cloudiness.Overcast)]
    public void ToCloudiness_thresholds_returnsCorrectCloudiness(double percent, Cloudiness expected)
    {
        var result = OpenMeteoWeatherMapper.ToCloudiness(percent);
        result.Should().Be(expected);
    }

    // ─── ToPrecipitationType ────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0, 0, 0, PrecipitationType.None)]        // Ясно
    [InlineData(3, 0, 0, 0, PrecipitationType.None)]        // Облачно
    [InlineData(45, 0, 0, 0, PrecipitationType.None)]       // Туман
    [InlineData(61, 0.2, 0.2, 0, PrecipitationType.Rain)]   // Дождь слабый
    [InlineData(71, 0.5, 0, 0.5, PrecipitationType.Snow)]   // Снег слабый
    [InlineData(96, 0.1, 0, 0, PrecipitationType.Hail)]     // Град (гроза)
    [InlineData(99, 0.1, 0, 0, PrecipitationType.Hail)]     // Град (гроза)
    [InlineData(61, 0, 0, 0, PrecipitationType.None)]       // код дождя, но осадков нет
    [InlineData(71, 0, 0, 0, PrecipitationType.None)]       // код снега, но осадков нет
    [InlineData(55, 0, 0, 0, PrecipitationType.None)]       // код ливня, но осадков нет
    public void ToPrecipitationType_returnsExpectedType(int code, double precip, double rain, double snow, PrecipitationType expected)
    {
        var result = OpenMeteoWeatherMapper.ToPrecipitationType(code, precip, rain, snow);
        result.Should().Be(expected);
    }

    [Fact]
    public void ToPrecipitationType_rainAndSnowBothPositive_returnsMixed()
    {
        var result = OpenMeteoWeatherMapper.ToPrecipitationType(61, 0.5, 0.3, 0.2);
        result.Should().Be(PrecipitationType.Mixed);
    }

    [Fact]
    public void ToPrecipitationType_unknownCode_returnsUnknown()
    {
        var result = OpenMeteoWeatherMapper.ToPrecipitationType(999, 1.0, 1.0, 0);
        result.Should().Be(PrecipitationType.Unknown);
    }

    // ─── ToPrecipitationStrength ────────────────────────────────────────

    [Theory]
    [InlineData(0, PrecipitationStrength.None)]
    [InlineData(0.1, PrecipitationStrength.Weak)]
    [InlineData(0.5, PrecipitationStrength.Weak)]
    [InlineData(0.6, PrecipitationStrength.Moderate)]
    [InlineData(1.0, PrecipitationStrength.Moderate)]
    [InlineData(2.5, PrecipitationStrength.Moderate)]
    [InlineData(2.6, PrecipitationStrength.Heavy)]
    [InlineData(5.0, PrecipitationStrength.Heavy)]
    [InlineData(7.5, PrecipitationStrength.Heavy)]
    [InlineData(7.6, PrecipitationStrength.VeryHeavy)]
    [InlineData(10.0, PrecipitationStrength.VeryHeavy)]
    public void ToPrecipitationStrength_boundaries_returnsCorrectStrength(double mmH, PrecipitationStrength expected)
    {
        var result = OpenMeteoWeatherMapper.ToPrecipitationStrength(mmH);
        result.Should().Be(expected);
    }

    // ─── TryParseObservedAt ─────────────────────────────────────────────

    [Fact]
    public void TryParseObservedAt_validTimeWithOffset_returnsTrueAndCorrectOffset()
    {
        var result = OpenMeteoWeatherMapper.TryParseObservedAt("2026-09-21T15:00", 10800, out var observedAt);
        result.Should().BeTrue();
        observedAt.Offset.Should().Be(TimeSpan.FromSeconds(10800)); // +3 часа (Москва)
        observedAt.Hour.Should().Be(15);
    }

    [Fact]
    public void TryParseObservedAt_nullTime_returnsFalse()
    {
        var result = OpenMeteoWeatherMapper.TryParseObservedAt(null!, 0, out var observedAt);
        result.Should().BeFalse();
        observedAt.Should().Be(default);
    }

    [Fact]
    public void TryParseObservedAt_emptyString_returnsFalse()
    {
        var result = OpenMeteoWeatherMapper.TryParseObservedAt("", 0, out var observedAt);
        result.Should().BeFalse();
        observedAt.Should().Be(default);
    }

    [Fact]
    public void TryParseObservedAt_whitespace_returnsFalse()
    {
        var result = OpenMeteoWeatherMapper.TryParseObservedAt("   ", 0, out var observedAt);
        result.Should().BeFalse();
        observedAt.Should().Be(default);
    }

    [Fact]
    public void TryParseObservedAt_negativeOffset_appliesCorrectOffset()
    {
        var result = OpenMeteoWeatherMapper.TryParseObservedAt("2026-09-21T15:00", -18000, out var observedAt);
        result.Should().BeTrue();
        observedAt.Offset.Should().Be(TimeSpan.FromSeconds(-18000)); // -5 часов
    }
}
