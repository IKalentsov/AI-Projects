namespace WeatherBot.Infrastructure.BackgroundServices;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WeatherBot.Application.Abstractions;
using WeatherBot.Application.Services;
using WeatherBot.Domain;

/// <summary>
/// Периодическая отправка сводки о погоде.
/// Первый тик происходит через полный интервал — сразу после старта приложения сообщение не уходит.
/// </summary>
/// <remarks>
/// Интервал читается один раз при запуске. Изменение <c>WeatherBot:IntervalMinutes</c>
/// на лету не подхватывается: настройки пока доступны только для чтения, и применяются
/// после перезапуска приложения.
/// </remarks>
public sealed class WeatherBotBackgroundService(
    WeatherDigestService weatherDigestService,
    IBotStateManager botStateManager,
    IOptionsMonitor<WeatherBotSettings> botOptions,
    ILogger<WeatherBotBackgroundService> logger) : BackgroundService
{
    /// <summary>Интервал по умолчанию, если в конфигурации задано некорректное значение.</summary>
    private const int FallbackIntervalMinutes = 30;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = GetIntervalMinutes();
        logger.LogInformation(
            "Фоновая отправка запущена. Интервал — {IntervalMinutes} мин, состояние бота: {State}",
            intervalMinutes,
            botStateManager.IsRunning ? "работает" : "остановлен");

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                botStateManager.MarkNextTick(DateTimeOffset.Now.AddMinutes(intervalMinutes));

                if (!await WaitForTickAsync(timer, stoppingToken))
                {
                    break;
                }

                if (!botStateManager.IsRunning)
                {
                    logger.LogDebug("Бот остановлен — отправка пропущена");
                    continue;
                }

                await RunIterationAsync(stoppingToken);
            }
        }
        finally
        {
            botStateManager.MarkNextTick(null);
            logger.LogInformation("Фоновая отправка остановлена");
        }
    }

    private static async Task<bool> WaitForTickAsync(
        PeriodicTimer timer,
        CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task RunIterationAsync(CancellationToken stoppingToken)
    {
        try
        {
            var result = await weatherDigestService.SendDigestAsync(stoppingToken);
            if (result.IsFailure)
            {
                logger.LogWarning("Итерация завершилась ошибкой: {Error}", result.Error);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Штатная остановка приложения.
        }
        catch (Exception exception)
        {
            // Ни одна итерация не должна останавливать фоновую задачу.
            logger.LogError(exception, "Непредвиденная ошибка при отправке сводки");
        }
    }

    private int GetIntervalMinutes()
    {
        var minutes = botOptions.CurrentValue.IntervalMinutes;

        return minutes > 0 ? minutes : FallbackIntervalMinutes;
    }
}
