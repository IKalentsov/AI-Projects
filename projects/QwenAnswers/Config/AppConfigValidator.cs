namespace QwenAnswers.Config;

/// <summary>
/// Проверяет, что конфигурация из .env заполнена корректно.
/// Раньше эта логика жила инлайном в Program.cs и не покрывалась тестами.
/// </summary>
public static class AppConfigValidator
{
    /// <summary>Схемы, которые поддерживает OpenAI-совместимый API.</summary>
    private static readonly string[] SupportedSchemes = ["http", "https"];

    /// <summary>
    /// Проверяет обязательные поля и корректность endpoint.
    /// </summary>
    /// <returns>
    /// Список человекочитаемых проблем в порядке проверки.
    /// Пустой список означает, что конфигурация готова к запуску.
    /// </returns>
    public static IReadOnlyList<string> Validate(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(config.ApiKey))
            problems.Add(MissingFieldMessage(EnvKeys.ApiKey));

        if (string.IsNullOrWhiteSpace(config.Endpoint))
            problems.Add(MissingFieldMessage(EnvKeys.Endpoint));
        else if (!IsValidEndpoint(config.Endpoint, out var endpointError))
            problems.Add(endpointError);

        if (string.IsNullOrWhiteSpace(config.ModelName))
            problems.Add(MissingFieldMessage(EnvKeys.ModelName));

        return problems;
    }

    /// <summary>
    /// Определяет, что .env — это неотредактированная копия .env-public:
    /// ни одно из обязательных полей не заполнено.
    /// </summary>
    public static bool IsUnfilledTemplate(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return string.IsNullOrWhiteSpace(config.ApiKey)
            && string.IsNullOrWhiteSpace(config.Endpoint)
            && string.IsNullOrWhiteSpace(config.ModelName);
    }

    private static string MissingFieldMessage(string key)
        => $"Ошибка: не заполнено поле {key} в .env.";

    private static bool IsValidEndpoint(string endpoint, out string error)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            error = $"Ошибка: {EnvKeys.Endpoint} не является валидным URI: '{endpoint}'";
            return false;
        }

        // Без проверки схемы опечатка вида "localhost:8080/v1" проходит как absolute URI
        // со схемой "localhost" и падает уже внутри клиента с невнятной ошибкой.
        if (!SupportedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase))
        {
            error = $"Ошибка: {EnvKeys.Endpoint} должен начинаться с http:// или https:// (указано: '{endpoint}')";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
