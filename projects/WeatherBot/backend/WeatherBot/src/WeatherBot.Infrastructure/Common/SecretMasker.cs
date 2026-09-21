namespace WeatherBot.Infrastructure.Common;

/// <summary>
/// Скрывает секреты в текстах, которые попадают в журнал или в ответ API.
/// </summary>
/// <remarks>
/// Нужен потому, что сообщения исключений сетевых клиентов могут содержать URL запроса,
/// а адрес Telegram Bot API включает токен бота: <c>https://api.telegram.org/bot&lt;token&gt;/...</c>.
/// Без маскировки токен утёк бы в журнал и в ответ <c>/api/bot/*</c>.
/// </remarks>
internal static class SecretMasker
{
    private const string Placeholder = "***";

    /// <summary>Заменяет секрет в тексте на заглушку.</summary>
    /// <param name="text">Текст, который может содержать секрет.</param>
    /// <param name="secret">Секрет (токен, ключ API).</param>
    /// <returns>Текст без секрета.</returns>
    internal static string Mask(string? text, string? secret)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        if (string.IsNullOrEmpty(secret))
        {
            return text;
        }

        return text.Replace(secret, Placeholder, StringComparison.Ordinal);
    }
}
