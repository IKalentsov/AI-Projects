namespace QwenAnswers.Tests.Integration;

using FluentAssertions;
using QwenAnswers.Config;
using QwenAnswers.Tests.Support;

/// <summary>
/// Готовность окружения: эти тесты отвечают на вопрос
/// «пользователь склонировал проект, заполнил .env — всё ли настроено верно».
/// Без заполненного .env они падают осознанно: это и есть проверка настройки.
/// </summary>
[Trait("Category", "Readiness")]
public class EnvironmentReadinessTests
{
    private const string FillHint =
        "Скопируйте .env-public в .env и заполните AI_API_KEY, AI_ENDPOINT, AI_MODEL_NAME своими данными.";

    [Fact]
    public void EnvFile_ExistsInRepositoryRoot()
    {
        File.Exists(TestPaths.EnvFile).Should().BeTrue(FillHint);
    }

    [Fact]
    public void EnvFile_IsLoadedWithoutErrors()
    {
        var result = new EnvConfigLoader().Load(TestPaths.EnvFile);

        result.Found.Should().BeTrue(FillHint);
        result.Error.Should().BeNull();
        result.Config.Should().NotBeNull();
    }

    [Fact]
    public void EnvFile_PassesValidation()
    {
        var config = LoadUserConfig();

        var problems = AppConfigValidator.Validate(config);

        problems.Should().BeEmpty($"{FillHint} Найдено: {string.Join(" | ", problems)}");
    }

    [Fact]
    public void EnvFile_EndpointIsReadyForOpenAiClient()
    {
        var config = LoadUserConfig();

        Uri.TryCreate(config.Endpoint, UriKind.Absolute, out var uri).Should().BeTrue(FillHint);
        uri!.Scheme.Should().BeOneOf("http", "https");
    }

    [Fact]
    public void EnvFile_NumericSettingsArePositive()
    {
        var config = LoadUserConfig();

        config.MaxHistoryMessages.Should().BePositive();
        config.RequestTimeoutSeconds.Should().BePositive();
    }

    [Fact]
    public void UneditedTemplateCopy_IsDetectedAsUnfilled()
    {
        // Копия шаблона «как есть» — самая частая ситуация: файл есть, но он пустой.
        using var temp = new TempDirectory();
        var envPath = temp.GetPath(".env");
        File.Copy(TestPaths.EnvPublicFile, envPath);

        var result = new EnvConfigLoader().Load(envPath);

        result.Found.Should().BeTrue();
        result.Config.Should().NotBeNull();

        AppConfigValidator.IsUnfilledTemplate(result.Config!).Should().BeTrue();
        AppConfigValidator.Validate(result.Config!).Should().HaveCount(EnvKeys.Required.Count);
    }

    [Fact]
    public void UneditedTemplateCopy_IsNotUsableForStartingTheApp()
    {
        using var temp = new TempDirectory();
        var envPath = temp.GetPath(".env");
        File.Copy(TestPaths.EnvPublicFile, envPath);

        var config = new EnvConfigLoader().Load(envPath).Config!;

        config.ApiKey.Should().BeEmpty();
        config.Endpoint.Should().BeEmpty();
        config.ModelName.Should().BeEmpty();
    }

    private static AppConfig LoadUserConfig()
    {
        File.Exists(TestPaths.EnvFile).Should().BeTrue(FillHint);

        var result = new EnvConfigLoader().Load(TestPaths.EnvFile);

        result.Found.Should().BeTrue(FillHint);
        result.Config.Should().NotBeNull();

        return result.Config!;
    }
}
