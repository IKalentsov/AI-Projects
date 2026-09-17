namespace QwenAnswers.Config;

/// <summary>
/// Загружает и парсит конфигурацию из .env файла.
/// </summary>
public class EnvConfigLoader : IEnvConfigLoader
{
    private const int DefaultMaxHistoryMessages = 50;
    private const int DefaultRequestTimeoutSeconds = 300;

    /// <inheritdoc />
    public static string DefaultEnvPath => ".env";

    /// <inheritdoc />
    public EnvConfigResult Load(string? envPath = null)
    {
        var path = envPath ?? DefaultEnvPath;

        if (!File.Exists(path))
            return new EnvConfigResult(Found: false, Config: null, Error: $"Файл '{path}' не найден.");

        Dictionary<string, string> raw;
        try
        {
            raw = ParseEnvFile(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Файл существует, но недоступен (занят другим процессом, нет прав и т.п.).
            // Возвращаем понятную ошибку вместо необработанного исключения.
            return new EnvConfigResult(Found: false, Config: null, Error: $"Не удалось прочитать файл '{path}': {ex.Message}");
        }

        var apiKey = GetValue(raw, EnvKeys.ApiKey);
        var endpoint = GetValue(raw, EnvKeys.Endpoint);
        var modelName = GetValue(raw, EnvKeys.ModelName);

        var maxHistory = ParseIntParam(raw, EnvKeys.MaxHistoryMessages, DefaultMaxHistoryMessages);
        var timeout = ParseIntParam(raw, EnvKeys.RequestTimeoutSeconds, DefaultRequestTimeoutSeconds);

        return new EnvConfigResult(
            Found: true,
            Config: new AppConfig(apiKey, endpoint, modelName, maxHistory, timeout),
            Error: null);
    }

    private static string GetValue(Dictionary<string, string> config, string key)
        => config.TryGetValue(key, out var value) ? value : string.Empty;

    private static Dictionary<string, string> ParseEnvFile(string path)
    {
        var result = new Dictionary<string, string>();

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();

            // Игнорируем пустые строки и комментарии
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;

            var separatorIndex = trimmed.IndexOf('=');

            // Игнорируем строки без = или с = в позиции 0
            if (separatorIndex <= 0)
                continue;

            var key = trimmed[..separatorIndex].Trim();
            var value = trimmed[(separatorIndex + 1)..].Trim().Trim('"').Trim('\'');

            // При дублировании ключа — переопределяем значение
            result[key] = value;
        }

        return result;
    }

    private static int ParseIntParam(Dictionary<string, string> config, string key, int defaultValue)
    {
        if (!config.TryGetValue(key, out var raw))
            return defaultValue;

        if (int.TryParse(raw, out var value) && value > 0)
            return value;

        return defaultValue;
    }
}
