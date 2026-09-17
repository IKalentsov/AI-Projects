namespace QwenAnswers.Chat;

/// <summary>
/// Результат отправки запроса к модели.
/// </summary>
public enum ChatResultType
{
    /// <summary>Успешный ответ от модели.</summary>
    Success,
    /// <summary>Запрос отменён пользователем (Esc).</summary>
    Cancelled,
    /// <summary>Превышено время ожидания ответа.</summary>
    Timeout,
    /// <summary>Ошибка при вызове модели.</summary>
    Error,
}

/// <summary>
/// Результат отправки запроса к модели.
/// </summary>
public record ChatResult(ChatResultType Type, string? Text);

/// <summary>
/// Сессия чата: отправка запросов к модели и ведение истории диалога.
/// </summary>
public interface IChatSession
{
    /// <summary>
    /// Отправляет запрос к модели и возвращает результат.
    /// Вопрос попадает в историю только при успешном ответе.
    /// </summary>
    /// <param name="userMessage">Сообщение пользователя.</param>
    /// <param name="cancellationToken">Токен отмены для отслеживания нажатия Esc.</param>
    /// <returns>Результат запроса.</returns>
    Task<ChatResult> SendAsync(string userMessage, CancellationToken cancellationToken = default);
}
