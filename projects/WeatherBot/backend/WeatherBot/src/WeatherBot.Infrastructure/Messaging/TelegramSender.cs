namespace WeatherBot.Infrastructure.Messaging;

using System.Globalization;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using WeatherBot.Application.Abstractions;
using WeatherBot.Domain;
using WeatherBot.Infrastructure.Common;

/// <summary>
/// Отправка сообщений в канал через Telegram Bot API.
/// Ошибки не выбрасываются наружу: сбой отправки не должен останавливать приложение.
/// </summary>
/// <remarks>
/// Токен не попадает ни в журнал, ни в текст ошибки. Адрес Telegram Bot API содержит токен
/// (<c>https://api.telegram.org/bot&lt;token&gt;/sendMessage</c>), а сообщения сетевых исключений
/// могут включать URL запроса. Поэтому объект исключения в журнал не передаётся, а его текст
/// очищается через <see cref="SecretMasker"/>; журналирование вынесено из <c>catch</c>-блока.
/// </remarks>
public sealed class TelegramSender(
    IOptionsMonitor<TelegramSettings> telegramOptions,
    ILogger<TelegramSender> logger) : ITelegramSender
{
    /// <inheritdoc />
    public async Task<Result> SendAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var settings = telegramOptions.CurrentValue;

        if (string.IsNullOrWhiteSpace(settings.BotToken))
        {
            logger.LogError(
                "Токен бота не задан: {Section}:BotToken",
                TelegramSettings.SectionName);

            return Result.Failure("Токен бота не задан в конфигурации");
        }

        if (string.IsNullOrWhiteSpace(settings.ChannelId))
        {
            logger.LogError(
                "Идентификатор канала не задан: {Section}:ChannelId",
                TelegramSettings.SectionName);

            return Result.Failure("Идентификатор канала не задан в конфигурации");
        }

        var error = await TrySendAsync(text, settings, cancellationToken);
        if (error is not null)
        {
            logger.LogError("Отправка в Telegram не удалась: {Error}", error);

            return Result.Failure(error);
        }

        logger.LogInformation("Сообщение отправлено в канал {ChannelId}", settings.ChannelId);

        return Result.Success();
    }

    /// <summary>Пытается отправить сообщение и возвращает очищенное описание ошибки.</summary>
    /// <param name="text">Текст сообщения.</param>
    /// <param name="settings">Настройки Telegram.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns><c>null</c> при успехе; иначе текст ошибки без секретов.</returns>
    private static async Task<string?> TrySendAsync(
        string text,
        TelegramSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var botClient = new TelegramBotClient(settings.BotToken);
            var chatId = new ChatId(settings.ChannelId);

            await botClient.SendMessage(
                chatId,
                text,
                parseMode: ParseMode.Markdown,
                cancellationToken: cancellationToken);

            return null;
        }
        catch (ApiRequestException exception)
        {
            // Типичные причины: бот не администратор канала, неверный идентификатор канала.
            return string.Create(
                CultureInfo.InvariantCulture,
                $"Telegram отклонил запрос ({exception.ErrorCode}): {SecretMasker.Mask(exception.Message, settings.BotToken)}");
        }
        catch (RequestException exception)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"Не удалось связаться с Telegram ({exception.GetType().Name}): {SecretMasker.Mask(exception.Message, settings.BotToken)}");
        }
        catch (ArgumentException exception)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"Некорректные настройки Telegram: {SecretMasker.Mask(exception.Message, settings.BotToken)}");
        }
    }
}
