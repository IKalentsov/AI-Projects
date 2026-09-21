namespace WeatherBot.Application.Abstractions;

using CSharpFunctionalExtensions;

/// <summary>Отправка сообщений в Telegram-канал.</summary>
public interface ITelegramSender
{
    /// <summary>Отправляет текстовое сообщение в настроенный канал.</summary>
    /// <param name="text">Текст сообщения в разметке Markdown.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>
    /// <see cref="Result.Success()"/> — сообщение принято Telegram;
    /// <see cref="Result.Failure(string)"/> — отправка не удалась (ошибка описана в тексте).
    /// </returns>
    Task<Result> SendAsync(string text, CancellationToken cancellationToken);
}
