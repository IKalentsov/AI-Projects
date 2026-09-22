namespace WeatherBot.Application.Services;

using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using WeatherBot.Application.Abstractions;

/// <summary>
/// Оркестратор отправки сводки: получить погоду → отформатировать → отправить в канал.
/// Используется и фоновой задачей, и внеочередной отправкой из панели управления.
/// </summary>
/// <remarks>
/// Одновременные вызовы сериализуются: две сводки подряд в канал не уйдут.
/// </remarks>
public sealed class WeatherDigestService(
    IWeatherProvider weatherProvider,
    IWeatherFormatter weatherFormatter,
    ITelegramSender telegramSender,
    IBotStateManager botStateManager,
    ILogger<WeatherDigestService> logger) : IDisposable
{
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    /// <summary>Формирует и отправляет сводку о погоде.</summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Успех либо описание первой возникшей ошибки.</returns>
    public async Task<Result> SendDigestAsync(CancellationToken cancellationToken)
    {
        await _sendLock.WaitAsync(cancellationToken);

        try
        {
            logger.LogInformation("Запрос текущей погоды");

            var weather = await weatherProvider.GetCurrentAsync(cancellationToken);
            if (weather.IsFailure)
            {
                logger.LogError("Погода не получена: {Error}", weather.Error);
                return Result.Failure(weather.Error);
            }

            var text = weatherFormatter.Format(weather.Value);

            var sendResult = await telegramSender.SendAsync(text, cancellationToken);
            if (sendResult.IsFailure)
            {
                logger.LogError("Сводка не отправлена: {Error}", sendResult.Error);
                return Result.Failure(sendResult.Error);
            }

            botStateManager.MarkSent(DateTimeOffset.Now);
            logger.LogInformation("Сводка о погоде отправлена в канал");

            return Result.Success();
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _sendLock.Dispose();
}
