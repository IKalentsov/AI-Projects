namespace QwenAnswers.Tests.Integration;

using FluentAssertions;
using QwenAnswers.Config;
using QwenAnswers.Tests.Support;

/// <summary>
/// Защита от коммита секретов: ключ из локального .env не должен встречаться в файлах проекта.
/// </summary>
[Trait("Category", "Readiness")]
public class SecretsNotCommittedTests
{
    private static readonly string[] InspectedExtensions =
    [
        ".cs", ".csproj", ".slnx", ".props", ".targets", ".json", ".md", ".yml", ".yaml", ".gitignore", ".editorconfig",
    ];

    [Fact]
    public void ApiKeyFromEnvFile_DoesNotAppearInProjectFiles()
    {
        if (!File.Exists(TestPaths.EnvFile))
            return; // без .env проверять нечего — за наличие файла отвечает EnvironmentReadinessTests

        var apiKey = new EnvConfigLoader().Load(TestPaths.EnvFile).Config?.ApiKey?.Trim() ?? string.Empty;

        // Короткие значения (например, "ollama") специально встречаются в документации как пример.
        if (apiKey.Length < 12)
            return;

        var offenders = EnumerateProjectFiles()
            .Where(file => File.ReadAllText(file).Contains(apiKey, StringComparison.Ordinal))
            .ToList();

        offenders.Should().BeEmpty(
            $"значение {EnvKeys.ApiKey} из .env не должно попадать в файлы проекта: {string.Join(", ", offenders)}");
    }

    private static IEnumerable<string> EnumerateProjectFiles()
    {
        var separator = Path.DirectorySeparatorChar;

        return Directory
            .EnumerateFiles(TestPaths.RepoRoot, "*", SearchOption.AllDirectories)
            .Where(file => InspectedExtensions.Contains(Path.GetExtension(file)))
            .Where(file => !file.Contains($"{separator}bin{separator}"))
            .Where(file => !file.Contains($"{separator}obj{separator}"))
            .Where(file => !file.Contains($"{separator}.vs{separator}"))
            .Where(file => !file.Contains($"{separator}.git{separator}"));
    }
}
