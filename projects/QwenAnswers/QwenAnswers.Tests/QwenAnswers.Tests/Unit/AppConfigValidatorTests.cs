namespace QwenAnswers.Tests.Unit;

using FluentAssertions;
using QwenAnswers.Config;

/// <summary>
/// Валидация настроек: именно эти проверки сообщают пользователю, что он заполнил .env неверно.
/// </summary>
public class AppConfigValidatorTests
{
    private static AppConfig ValidConfig() =>
        new("test-key", "http://localhost:8080/v1", "test-model", 50, 300);

    // ── Корректная конфигурация ─────────────────────────────────────────────

    [Fact]
    public void Validate_FullyFilledConfig_ReturnsNoProblems()
    {
        var problems = AppConfigValidator.Validate(ValidConfig());

        problems.Should().BeEmpty();
    }

    [Theory]
    [InlineData("https://api.example.com/v1")]
    [InlineData("http://localhost:8080/v1")]
    [InlineData("http://127.0.0.1:11434/v1")]
    [InlineData("HTTP://LOCALHOST:8080/v1")]
    public void Validate_SupportedEndpointSchemes_Pass(string endpoint)
    {
        var config = ValidConfig() with { Endpoint = endpoint };

        AppConfigValidator.Validate(config).Should().BeEmpty();
    }

    // ── Пустые обязательные поля ────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankApiKey_ReportsApiKeyProblem(string blank)
    {
        var config = ValidConfig() with { ApiKey = blank };

        var problems = AppConfigValidator.Validate(config);

        problems.Should().ContainSingle().Which.Should().Contain(EnvKeys.ApiKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankEndpoint_ReportsEndpointProblem(string blank)
    {
        var config = ValidConfig() with { Endpoint = blank };

        var problems = AppConfigValidator.Validate(config);

        problems.Should().ContainSingle().Which.Should().Contain(EnvKeys.Endpoint);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankModelName_ReportsModelNameProblem(string blank)
    {
        var config = ValidConfig() with { ModelName = blank };

        var problems = AppConfigValidator.Validate(config);

        problems.Should().ContainSingle().Which.Should().Contain(EnvKeys.ModelName);
    }

    [Fact]
    public void Validate_UntouchedTemplate_ReportsAllThreeRequiredFields()
    {
        var config = ValidConfig() with { ApiKey = "", Endpoint = "", ModelName = "" };

        var problems = AppConfigValidator.Validate(config);

        problems.Should().HaveCount(3);
        problems[0].Should().Contain(EnvKeys.ApiKey);
        problems[1].Should().Contain(EnvKeys.Endpoint);
        problems[2].Should().Contain(EnvKeys.ModelName);
    }

    [Fact]
    public void Validate_UntouchedTemplate_DoesNotComplainAboutEndpointFormat()
    {
        // Пустой endpoint должен давать «поле не заполнено», а не «невалидный URI».
        var config = ValidConfig() with { ApiKey = "", Endpoint = "", ModelName = "" };

        var problems = AppConfigValidator.Validate(config);

        problems.Should().NotContain(problem => problem.Contains("URI"));
    }

    // ── Некорректный endpoint ───────────────────────────────────────────────

    [Fact]
    public void Validate_EndpointWithoutHttpScheme_IsRejectedWithHint()
    {
        // Опечатка «забыл http://»: строка разбирается как absolute URI со схемой localhost.
        var config = ValidConfig() with { Endpoint = "localhost:8080/v1" };

        var problems = AppConfigValidator.Validate(config);

        problems.Should().ContainSingle();
        problems[0].Should().Contain("http://");
        problems[0].Should().Contain("localhost:8080/v1");
    }

    [Theory]
    [InlineData("ftp://localhost:21/v1")]
    [InlineData("file:///c:/temp/model")]
    [InlineData("ws://localhost:8080/v1")]
    public void Validate_EndpointWithUnsupportedScheme_IsRejected(string endpoint)
    {
        var config = ValidConfig() with { Endpoint = endpoint };

        var problems = AppConfigValidator.Validate(config);

        problems.Should().ContainSingle();
        problems[0].Should().Contain("http://");
    }

    [Theory]
    [InlineData("не-url")]
    [InlineData("/v1")]
    [InlineData("http//localhost:8080/v1")]
    public void Validate_EndpointThatIsNotAnAbsoluteUri_IsRejected(string endpoint)
    {
        var config = ValidConfig() with { Endpoint = endpoint };

        var problems = AppConfigValidator.Validate(config);

        problems.Should().ContainSingle();
        problems[0].Should().Contain("не является валидным URI");
    }

    [Fact]
    public void Validate_SingleBadEndpoint_ReportsExactlyOneProblem()
    {
        var config = ValidConfig() with { Endpoint = "localhost:8080/v1" };

        AppConfigValidator.Validate(config).Should().ContainSingle();
    }

    // ── Неотредактированный шаблон ──────────────────────────────────────────

    [Fact]
    public void IsUnfilledTemplate_AllRequiredFieldsBlank_IsTrue()
    {
        var config = ValidConfig() with { ApiKey = "", Endpoint = "   ", ModelName = "" };

        AppConfigValidator.IsUnfilledTemplate(config).Should().BeTrue();
    }

    [Theory]
    [InlineData("key", "", "")]
    [InlineData("", "http://localhost:8080/v1", "")]
    [InlineData("", "", "model")]
    public void IsUnfilledTemplate_AtLeastOneFieldFilled_IsFalse(string apiKey, string endpoint, string modelName)
    {
        var config = ValidConfig() with { ApiKey = apiKey, Endpoint = endpoint, ModelName = modelName };

        AppConfigValidator.IsUnfilledTemplate(config).Should().BeFalse();
    }

    // ── Защита от null ──────────────────────────────────────────────────────

    [Fact]
    public void Validate_NullConfig_Throws()
    {
        Action act = () => AppConfigValidator.Validate(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IsUnfilledTemplate_NullConfig_Throws()
    {
        Action act = () => AppConfigValidator.IsUnfilledTemplate(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
