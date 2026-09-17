namespace QwenAnswers.Tests.Support;

using QwenAnswers.Config;

/// <summary>
/// Включатель живых тестов (реальные запросы к запущенной модели).
/// Флаг читается из переменной окружения, а если её нет — из .env в корне репозитория,
/// чтобы включение не зависело от того, как именно запускаются тесты.
/// </summary>
public static class LiveTestGate
{
    /// <summary>Имя флага.</summary>
    public const string FlagName = "AI_RUN_LIVE_TESTS";

    public static bool Enabled { get; } = ResolveEnabled();

    public static string SkipReason { get; } =
        $"Живые тесты выключены. Запустите модель, поставьте {FlagName}=1 " +
        $"(в окружении или в .env) и повторите запуск.";

    private static bool ResolveEnabled()
    {
        if (IsTruthy(Environment.GetEnvironmentVariable(FlagName)))
            return true;

        return IsTruthy(ReadFlagFromEnvFile());
    }

    internal static bool IsTruthy(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();

        return normalized is "1" or "true" or "yes" or "on";
    }

    private static string? ReadFlagFromEnvFile()
    {
        if (!File.Exists(TestPaths.EnvFile))
            return null;

        try
        {
            foreach (var line in File.ReadAllLines(TestPaths.EnvFile))
            {
                var trimmed = line.Trim();

                if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                    continue;

                var separatorIndex = trimmed.IndexOf('=');

                if (separatorIndex <= 0)
                    continue;

                if (trimmed[..separatorIndex].Trim() == FlagName)
                    return trimmed[(separatorIndex + 1)..].Trim();
            }
        }
        catch (IOException)
        {
            // Не смогли прочитать .env — считаем, что флаг не выставлен.
        }

        return null;
    }
}

/// <summary>
/// <see cref="FactAttribute"/>, который выполняется только при включённом флаге
/// <see cref="LiveTestGate.FlagName"/>. Иначе тест помечается как пропущенный с понятной причиной.
/// </summary>
public sealed class LiveFactAttribute : FactAttribute
{
    public LiveFactAttribute()
    {
        if (!LiveTestGate.Enabled)
            Skip = LiveTestGate.SkipReason;
    }
}

/// <summary>
/// Загружает и проверяет реальный .env. Используется только живыми тестами.
/// </summary>
internal static class LiveTestSupport
{
    public static AppConfig RequireConfig()
    {
        if (!File.Exists(TestPaths.EnvFile))
        {
            throw new InvalidOperationException(
                $"Живым тестам нужен заполненный .env. Скопируйте '{TestPaths.EnvPublicFile}' в '{TestPaths.EnvFile}' " +
                "и заполните AI_API_KEY, AI_ENDPOINT, AI_MODEL_NAME.");
        }

        var loadResult = new EnvConfigLoader().Load(TestPaths.EnvFile);

        if (!loadResult.Found)
            throw new InvalidOperationException(loadResult.Error);

        var config = loadResult.Config!;
        var problems = AppConfigValidator.Validate(config);

        if (problems.Count > 0)
            throw new InvalidOperationException($"Конфигурация в .env некорректна: {string.Join(" | ", problems)}");

        return config;
    }
}
