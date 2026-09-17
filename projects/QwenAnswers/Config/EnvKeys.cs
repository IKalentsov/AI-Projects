namespace QwenAnswers.Config;

/// <summary>
/// Имена ключей, которые приложение читает из .env файла.
/// Единый источник правды: используется загрузчиком, шаблоном .env-public, README и тестами,
/// чтобы документация не расходилась с кодом.
/// </summary>
public static class EnvKeys
{
    /// <summary>API-ключ для доступа к AI-серверу. Обязательный.</summary>
    public const string ApiKey = "AI_API_KEY";

    /// <summary>URL AI-сервера вместе с путём к /v1. Обязательный.</summary>
    public const string Endpoint = "AI_ENDPOINT";

    /// <summary>Имя модели, запущенной на сервере. Обязательный.</summary>
    public const string ModelName = "AI_MODEL_NAME";

    /// <summary>
    /// Сколько последних реплик диалога (пар «вопрос + ответ») держать в контексте.
    /// Опциональный: при отсутствии используется значение по умолчанию.
    /// </summary>
    public const string MaxHistoryMessages = "AI_MAX_HISTORY_MESSAGES";

    /// <summary>
    /// Таймаут одного запроса к модели в секундах.
    /// Опциональный: при отсутствии используется значение по умолчанию.
    /// </summary>
    public const string RequestTimeoutSeconds = "AI_REQUEST_TIMEOUT_SECONDS";

    /// <summary>Обязательные ключи — без них приложение не запустится.</summary>
    public static IReadOnlyList<string> Required { get; } = [ApiKey, Endpoint, ModelName];

    /// <summary>Опциональные ключи — при отсутствии подставляются значения по умолчанию.</summary>
    public static IReadOnlyList<string> Optional { get; } = [MaxHistoryMessages, RequestTimeoutSeconds];

    /// <summary>Все ключи, которые читает приложение.</summary>
    public static IReadOnlyList<string> All { get; } = [.. Required, .. Optional];
}
