namespace WeatherBot.Web.Controllers;

using Microsoft.AspNetCore.Mvc;
using WeatherBot.Application.Abstractions;
using WeatherBot.Application.Services;
using WeatherBot.Contracts.Bot;

/// <summary>Управление ботом: пуск, остановка, внеочередная отправка, статус.</summary>
[ApiController]
[Route("api/bot")]
public sealed class BotController(
    IBotStateManager botStateManager,
    WeatherDigestService weatherDigestService,
    ITelegramSender telegramSender,
    ILogger<BotController> logger) : ControllerBase
{
    /// <summary>Текст диагностического сообщения.</summary>
    private const string TestMessage = """
        🤖 *WeatherBot* — проверка связи.

        Если вы видите это сообщение, отправка в канал настроена верно.
        """;

    /// <summary>Запускает периодическую отправку.</summary>
    /// <returns>Состояние бота; 409, если бот уже запущен.</returns>
    [HttpPost("start")]
    public ActionResult<BotActionResponse> Start()
    {
        if (botStateManager.IsRunning)
        {
            logger.LogWarning("Повторный запуск бота отклонён — бот уже работает");

            return Conflict(new BotActionResponse(false, true, "Бот уже запущен"));
        }

        botStateManager.Start();
        logger.LogInformation("Бот запущен через API");

        return Ok(new BotActionResponse(true, true, "Бот запущен"));
    }

    /// <summary>Останавливает периодическую отправку.</summary>
    /// <returns>Состояние бота; 409, если бот уже остановлен.</returns>
    [HttpPost("stop")]
    public ActionResult<BotActionResponse> Stop()
    {
        if (!botStateManager.IsRunning)
        {
            logger.LogWarning("Повторная остановка бота отклонена — бот уже остановлен");

            return Conflict(new BotActionResponse(false, false, "Бот уже остановлен"));
        }

        botStateManager.Stop();
        logger.LogInformation("Бот остановлен через API");

        return Ok(new BotActionResponse(true, false, "Бот остановлен"));
    }

    /// <summary>Отправляет сводку вне очереди, независимо от состояния бота.</summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат отправки; 502, если погода не получена или сообщение не отправлено.</returns>
    [HttpPost("send-now")]
    public async Task<ActionResult<BotActionResponse>> SendNowAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Запрошена внеочередная отправка сводки");

        var result = await weatherDigestService.SendDigestAsync(cancellationToken);

        if (result.IsFailure)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new BotActionResponse(false, botStateManager.IsRunning, result.Error));
        }

        return Ok(new BotActionResponse(true, botStateManager.IsRunning, "Сводка отправлена"));
    }

    /// <summary>
    /// Отправляет в канал тестовое сообщение, не обращаясь к API погоды.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат отправки; 502, если Telegram отклонил запрос.</returns>
    [HttpPost("send-test")]
    public async Task<ActionResult<BotActionResponse>> SendTestAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Запрошена диагностическая отправка в Telegram");

        var result = await telegramSender.SendAsync(TestMessage, cancellationToken);

        if (result.IsFailure)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new BotActionResponse(false, botStateManager.IsRunning, result.Error));
        }

        return Ok(new BotActionResponse(true, botStateManager.IsRunning, "Тестовое сообщение отправлено"));
    }

    /// <summary>Возвращает текущее состояние бота.</summary>
    /// <returns>Флаг работы, время последней и следующей отправки.</returns>
    [HttpGet("status")]
    public ActionResult<BotStatusResponse> GetStatus()
    {
        return Ok(new BotStatusResponse(
            botStateManager.IsRunning,
            botStateManager.LastSentAt,
            botStateManager.NextTickAt));
    }
}
