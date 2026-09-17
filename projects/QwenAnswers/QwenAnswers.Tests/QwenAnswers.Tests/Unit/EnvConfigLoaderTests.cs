namespace QwenAnswers.Tests.Unit;

using System.Text;
using FluentAssertions;
using QwenAnswers.Config;
using QwenAnswers.Tests.Support;

/// <summary>
/// Разбор .env: сценарий «пользователь склонировал проект и заполняет файл настроек руками».
/// </summary>
public class EnvConfigLoaderTests : IDisposable
{
    private static readonly string[] MinimalEnvFile =
    [
        "AI_API_KEY=test-key",
        "AI_ENDPOINT=http://localhost:8080/v1",
        "AI_MODEL_NAME=test-model",
    ];

    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private string WriteEnv(params string[] lines) => _temp.WriteFile(".env", lines);

    private static EnvConfigResult LoadFrom(string path) => new EnvConfigLoader().Load(path);

    // ── Успешная загрузка ───────────────────────────────────────────────────

    [Fact]
    public void Load_FilledFile_ReturnsConfigWithAllValues()
    {
        var result = LoadFrom(WriteEnv(MinimalEnvFile));

        result.Found.Should().BeTrue();
        result.Error.Should().BeNull();
        result.Config.Should().NotBeNull();
        result.Config!.ApiKey.Should().Be("test-key");
        result.Config.Endpoint.Should().Be("http://localhost:8080/v1");
        result.Config.ModelName.Should().Be("test-model");
    }

    [Fact]
    public void Load_FilledFile_AppliesDefaultNumericSettings()
    {
        var result = LoadFrom(WriteEnv(MinimalEnvFile));

        result.Config!.MaxHistoryMessages.Should().Be(50);
        result.Config.RequestTimeoutSeconds.Should().Be(300);
    }

    // ── Отсутствующий файл ──────────────────────────────────────────────────

    [Fact]
    public void Load_MissingFile_ReturnsNotFoundWithPathInError()
    {
        var missingPath = _temp.GetPath("no-such-file.env");

        var result = LoadFrom(missingPath);

        result.Found.Should().BeFalse();
        result.Config.Should().BeNull();
        result.Error.Should().Contain("no-such-file.env");
        result.Error.Should().Contain("не найден");
    }

    [Fact]
    public void Load_FileLockedByAnotherProcess_ReturnsReadErrorInsteadOfThrowing()
    {
        var path = WriteEnv(MinimalEnvFile);

        using var lockedFile = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        var result = LoadFrom(path);

        result.Found.Should().BeFalse();
        result.Config.Should().BeNull();
        result.Error.Should().Contain("Не удалось прочитать файл");
    }

    // ── Синтаксис файла ─────────────────────────────────────────────────────

    [Fact]
    public void Load_BlankAndCommentLines_AreIgnored()
    {
        var result = LoadFrom(WriteEnv(
            "",
            "# комментарий",
            "   ",
            "AI_API_KEY=test-key",
            "   # ещё комментарий",
            "AI_ENDPOINT=http://localhost:8080/v1",
            "AI_MODEL_NAME=test-model"));

        result.Found.Should().BeTrue();
        result.Config!.ApiKey.Should().Be("test-key");
        result.Config.Endpoint.Should().Be("http://localhost:8080/v1");
        result.Config.ModelName.Should().Be("test-model");
    }

    [Fact]
    public void Load_WhitespaceAroundKeyAndValue_IsTrimmed()
    {
        var result = LoadFrom(WriteEnv(
            "  AI_API_KEY   =   test-key   ",
            "  AI_ENDPOINT  =  http://localhost:8080/v1  ",
            "  AI_MODEL_NAME  =   my model   "));

        result.Config!.ApiKey.Should().Be("test-key");
        result.Config.Endpoint.Should().Be("http://localhost:8080/v1");
        result.Config.ModelName.Should().Be("my model");
    }

    [Fact]
    public void Load_SurroundingQuotes_AreRemoved()
    {
        var result = LoadFrom(WriteEnv(
            "AI_API_KEY=\"test-key\"",
            "AI_ENDPOINT='http://localhost:8080/v1'",
            "AI_MODEL_NAME=test-model"));

        result.Config!.ApiKey.Should().Be("test-key");
        result.Config.Endpoint.Should().Be("http://localhost:8080/v1");
    }

    [Fact]
    public void Load_LineWithoutEquals_IsIgnored()
    {
        var result = LoadFrom(WriteEnv("ЭТО_НЕ_ПАРА_КЛЮЧ_ЗНАЧЕНИЕ", "AI_API_KEY=test-key"));

        result.Found.Should().BeTrue();
        result.Config!.ApiKey.Should().Be("test-key");
    }

    [Fact]
    public void Load_LineStartingWithEquals_IsIgnored()
    {
        var result = LoadFrom(WriteEnv("=начение-без-ключа", "AI_API_KEY=test-key"));

        result.Found.Should().BeTrue();
        result.Config!.ApiKey.Should().Be("test-key");
    }

    [Fact]
    public void Load_ValueWithEqualsInside_IsKeptEntirely()
    {
        var result = LoadFrom(WriteEnv("AI_ENDPOINT=http://localhost:8080/v1?token=a=b&x=y"));

        result.Config!.Endpoint.Should().Be("http://localhost:8080/v1?token=a=b&x=y");
    }

    [Fact]
    public void Load_DuplicateKeys_LastValueWins()
    {
        var result = LoadFrom(WriteEnv(
            "AI_API_KEY=first",
            "AI_ENDPOINT=http://localhost:8080/v1",
            "AI_API_KEY=second"));

        result.Config!.ApiKey.Should().Be("second");
    }

    [Fact]
    public void Load_UnknownKeys_AreIgnored()
    {
        var result = LoadFrom(WriteEnv(
            "SOME_OTHER_SETTING=1",
            "PATH=/usr/bin",
            "AI_API_KEY=test-key"));

        result.Found.Should().BeTrue();
        result.Config!.ApiKey.Should().Be("test-key");
        result.Config.Endpoint.Should().BeEmpty();
    }

    [Fact]
    public void Load_EmptyValues_ReturnEmptyStrings()
    {
        var result = LoadFrom(WriteEnv("AI_API_KEY=", "AI_ENDPOINT=", "AI_MODEL_NAME="));

        result.Found.Should().BeTrue();
        result.Config!.ApiKey.Should().BeEmpty();
        result.Config.Endpoint.Should().BeEmpty();
        result.Config.ModelName.Should().BeEmpty();
    }

    [Fact]
    public void Load_FileSavedWithUtf8Bom_StillReadsKeys()
    {
        // Блокнот по умолчанию сохраняет «UTF-8 с BOM»: без учёта BOM первый ключ перестал бы читаться.
        var path = _temp.GetPath(".env");
        File.WriteAllText(path, string.Join(Environment.NewLine, MinimalEnvFile), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var result = LoadFrom(path);

        result.Found.Should().BeTrue();
        result.Config!.ApiKey.Should().Be("test-key");
        result.Config.ModelName.Should().Be("test-model");
    }

    [Fact]
    public void Load_FileWithCrlfLineEndings_IsParsed()
    {
        var path = _temp.GetPath(".env");
        File.WriteAllText(path, "AI_API_KEY=test-key\r\nAI_ENDPOINT=http://localhost:8080/v1\r\nAI_MODEL_NAME=test-model\r\n");

        var result = LoadFrom(path);

        result.Found.Should().BeTrue();
        result.Config!.ApiKey.Should().Be("test-key");
        result.Config.Endpoint.Should().Be("http://localhost:8080/v1");
        result.Config.ModelName.Should().Be("test-model");
    }

    // ── Числовые настройки ──────────────────────────────────────────────────

    [Theory]
    [InlineData(100)]
    [InlineData(1)]
    public void Load_ValidMaxHistoryMessages_IsApplied(int value)
    {
        var result = LoadFrom(WriteEnv([.. MinimalEnvFile, $"{EnvKeys.MaxHistoryMessages}={value}"]));

        result.Config!.MaxHistoryMessages.Should().Be(value);
    }

    [Fact]
    public void Load_ValidRequestTimeoutSeconds_IsApplied()
    {
        var result = LoadFrom(WriteEnv([.. MinimalEnvFile, $"{EnvKeys.RequestTimeoutSeconds}=45"]));

        result.Config!.RequestTimeoutSeconds.Should().Be(45);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("1.5")]
    public void Load_InvalidMaxHistoryMessages_FallsBackToDefault(string rawValue)
    {
        var result = LoadFrom(WriteEnv([.. MinimalEnvFile, $"{EnvKeys.MaxHistoryMessages}={rawValue}"]));

        result.Config!.MaxHistoryMessages.Should().Be(50);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-100")]
    public void Load_InvalidRequestTimeoutSeconds_FallsBackToDefault(string rawValue)
    {
        var result = LoadFrom(WriteEnv([.. MinimalEnvFile, $"{EnvKeys.RequestTimeoutSeconds}={rawValue}"]));

        result.Config!.RequestTimeoutSeconds.Should().Be(300);
    }

    // ── Путь по умолчанию ───────────────────────────────────────────────────

    [Fact]
    public void DefaultEnvPath_IsDotEnvInCurrentDirectory()
    {
        EnvConfigLoader.DefaultEnvPath.Should().Be(".env");
    }
}
