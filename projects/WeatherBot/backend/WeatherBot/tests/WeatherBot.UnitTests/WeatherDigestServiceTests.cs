namespace WeatherBot.UnitTests;

using AwesomeAssertions;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WeatherBot.Application.Abstractions;
using WeatherBot.Application.Services;
using WeatherBot.Domain;
using Xunit;

public class WeatherDigestServiceTests
{
    private static CancellationToken TestCt => TestContext.Current.CancellationToken;

    // ─── helpers ────────────────────────────────────────────────────────

    private static WeatherInfo CreateWeather() => new(
        TemperatureC: 15, FeelsLikeC: 12, HumidityPercent: 70,
        PressureMmHg: 750, WindSpeedMs: 4.2,
        WindDirection: WindDirection.SouthEast, Cloudiness: Cloudiness.PartlyCloudy,
        PrecipitationType: PrecipitationType.None, PrecipitationStrength: PrecipitationStrength.None,
        ObservedAt: new DateTimeOffset(2026, 9, 21, 15, 0, 0, TimeSpan.FromSeconds(10800)));

    private static IWeatherProvider CreateSuccessProvider(WeatherInfo weather)
    {
        var provider = Substitute.For<IWeatherProvider>();
        provider.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns(Result.Success<WeatherInfo>(weather));
        return provider;
    }

    private static IWeatherFormatter CreateFormatter(string text)
    {
        var formatter = Substitute.For<IWeatherFormatter>();
        formatter.Format(Arg.Any<WeatherInfo>()).Returns(text);
        return formatter;
    }

    private static ITelegramSender CreateSuccessSender()
    {
        var sender = Substitute.For<ITelegramSender>();
        sender.SendAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        return sender;
    }

    // ─── success path ───────────────────────────────────────────────────

    [Fact]
    public async Task SendDigestAsync_success_callsAllStepsAndMarkSent()
    {
        var weather = CreateWeather();
        var expectedText = "*Погода*";

        var provider = CreateSuccessProvider(weather);
        var formatter = CreateFormatter(expectedText);
        var sender = CreateSuccessSender();
        var stateManager = Substitute.For<IBotStateManager>();

        using var service = new WeatherDigestService(provider, formatter, sender, stateManager, NullLogger<WeatherDigestService>.Instance);

        var result = await service.SendDigestAsync(TestCt);

        result.IsSuccess.Should().BeTrue();

        await provider.Received(1).GetCurrentAsync(Arg.Any<CancellationToken>());
        formatter.Received(1).Format(weather);
        await sender.Received(1).SendAsync(expectedText, Arg.Any<CancellationToken>());
        stateManager.Received(1).MarkSent(Arg.Any<DateTimeOffset>());
    }

    // ─── provider failure ───────────────────────────────────────────────

    [Fact]
    public async Task SendDigestAsync_providerFailure_returnsProviderErrorNoSend()
    {
        var providerError = "Open-Meteo вернула код 502";

        var provider = Substitute.For<IWeatherProvider>();
        provider.GetCurrentAsync(Arg.Any<CancellationToken>()).Returns(Result.Failure<WeatherInfo>(providerError));

        var formatter = Substitute.For<IWeatherFormatter>();
        var sender = Substitute.For<ITelegramSender>();
        var stateManager = Substitute.For<IBotStateManager>();

        using var service = new WeatherDigestService(provider, formatter, sender, stateManager, NullLogger<WeatherDigestService>.Instance);

        var result = await service.SendDigestAsync(TestCt);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(providerError);

        await provider.Received(1).GetCurrentAsync(Arg.Any<CancellationToken>());
        formatter.DidNotReceive().Format(Arg.Any<WeatherInfo>());
        await sender.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        stateManager.DidNotReceive().MarkSent(Arg.Any<DateTimeOffset>());
    }

    // ─── telegram rejection ─────────────────────────────────────────────

    [Fact]
    public async Task SendDigestAsync_telegramRejects_returnsFailureNoMarkSent()
    {
        var weather = CreateWeather();
        var expectedText = "*Погода*";
        var sendError = "Telegram отклонил сообщение";

        var sender = Substitute.For<ITelegramSender>();
        sender.SendAsync(expectedText, Arg.Any<CancellationToken>()).Returns(Result.Failure(sendError));

        var stateManager = Substitute.For<IBotStateManager>();

        using var service = new WeatherDigestService(
            CreateSuccessProvider(weather),
            CreateFormatter(expectedText),
            sender,
            stateManager,
            NullLogger<WeatherDigestService>.Instance);

        var result = await service.SendDigestAsync(TestCt);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(sendError);

        await sender.Received(1).SendAsync(expectedText, Arg.Any<CancellationToken>());
        stateManager.DidNotReceive().MarkSent(Arg.Any<DateTimeOffset>());
    }

    // ─── concurrent calls ───────────────────────────────────────────────

    [Fact]
    public async Task SendDigestAsync_concurrentCalls_bothDigestsSendExactlyOnce()
    {
        var weather = CreateWeather();
        var expectedText = "*Погода*";
        var sendCount = 0;

        var sender = Substitute.For<ITelegramSender>();
        sender.SendAsync(expectedText, Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                Interlocked.Increment(ref sendCount);
                await Task.Delay(50, CancellationToken.None);
                return Result.Success();
            });

        var stateManager = Substitute.For<IBotStateManager>();

        using var service = new WeatherDigestService(
            CreateSuccessProvider(weather),
            CreateFormatter(expectedText),
            sender,
            stateManager,
            NullLogger<WeatherDigestService>.Instance);

        var t1 = service.SendDigestAsync(TestCt);
        var t2 = service.SendDigestAsync(TestCt);

        await Task.WhenAll(t1, t2);

        // Both should succeed
        (await t1).IsSuccess.Should().BeTrue();
        (await t2).IsSuccess.Should().BeTrue();

        // Send should happen exactly twice (once per digest)
        sendCount.Should().Be(2);

        // Both digests must have been marked as sent
        stateManager.Received(2).MarkSent(Arg.Any<DateTimeOffset>());
    }

    [Fact]
    public async Task SendDigestAsync_concurrentCalls_secondWaitsForFirst()
    {
        var weather = CreateWeather();
        var expectedText = "*Погода*";
        var sendOrder = new List<string>();

        var sender = Substitute.For<ITelegramSender>();
        sender.SendAsync(expectedText, Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                sendOrder.Add("send");
                await Task.Delay(100, CancellationToken.None);
                sendOrder.Add("send-done");
                return Result.Success();
            });

        using var service = new WeatherDigestService(
            CreateSuccessProvider(weather),
            CreateFormatter(expectedText),
            sender,
            Substitute.For<IBotStateManager>(),
            NullLogger<WeatherDigestService>.Instance);

        var t1 = service.SendDigestAsync(TestCt);

        // Wait for first send to start but not finish
        while (sendOrder.Count != 1)
        {
            await Task.Delay(10, CancellationToken.None);
        }

        // Start second call — it should wait on the semaphore
        var t2 = service.SendDigestAsync(TestCt);

        await Task.WhenAll(t1, t2);

        // Order: send, send-done, send, send-done (no overlap)
        sendOrder.Should().Equal("send", "send-done", "send", "send-done");
    }
}
