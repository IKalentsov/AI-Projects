namespace QwenAnswers.Config;

/// <summary>
/// Результат загрузки конфигурации из .env файла.
/// </summary>
public record EnvConfigResult(
    bool Found,
    AppConfig? Config,
    string? Error);
