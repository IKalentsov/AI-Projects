namespace QwenAnswers.Config;

/// <summary>
/// Загружает и парсит конфигурацию из .env файла.
/// </summary>
public interface IEnvConfigLoader
{
    /// <summary>
    /// Путь по умолчанию к .env файлу (в директории приложения).
    /// </summary>
    static abstract string DefaultEnvPath { get; }

    /// <summary>
    /// Загружает конфигурацию из .env файла.
    /// </summary>
    /// <param name="envPath">Путь к .env файлу. Если null — используется путь по умолчанию.</param>
    /// <returns>Результат загрузки конфигурации.</returns>
    EnvConfigResult Load(string? envPath = null);
}
