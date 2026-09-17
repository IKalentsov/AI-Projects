namespace QwenAnswers.Config;

/// <summary>
/// Конфигурация приложения, загруженная из .env файла.
/// </summary>
/// <param name="ApiKey">API-ключ (<see cref="EnvKeys.ApiKey"/>).</param>
/// <param name="Endpoint">URL AI-сервера с путём к /v1 (<see cref="EnvKeys.Endpoint"/>).</param>
/// <param name="ModelName">Имя модели на сервере (<see cref="EnvKeys.ModelName"/>).</param>
/// <param name="MaxHistoryMessages">
/// Максимальное количество реплик диалога (пар «вопрос + ответ») в контексте
/// (<see cref="EnvKeys.MaxHistoryMessages"/>). System-сообщение в лимит не входит.
/// </param>
/// <param name="RequestTimeoutSeconds">
/// Таймаут одного запроса к модели в секундах (<see cref="EnvKeys.RequestTimeoutSeconds"/>).
/// </param>
public record AppConfig(
    string ApiKey,
    string Endpoint,
    string ModelName,
    int MaxHistoryMessages,
    int RequestTimeoutSeconds);
