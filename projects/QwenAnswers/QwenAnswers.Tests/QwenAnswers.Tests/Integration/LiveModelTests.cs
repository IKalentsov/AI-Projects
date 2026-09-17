namespace QwenAnswers.Tests.Integration;

using System.Net.Http.Headers;
using FluentAssertions;
using QwenAnswers.Chat;
using QwenAnswers.Tests.Support;

/// <summary>
/// Живые тесты: реальные запросы к запущенной модели.
/// Включаются флагом AI_RUN_LIVE_TESTS=1 (в окружении или в .env), иначе помечаются как пропущенные.
/// </summary>
[Trait("Category", "Live")]
public class LiveModelTests
{
    [LiveFact]
    public async Task ChatSession_RealModel_ReturnsNonEmptyAnswer()
    {
        var config = LiveTestSupport.RequireConfig();

        using var client = ChatSessionFactory.CreateChatClient(config);
        var session = ChatSessionFactory.CreateSession(client, config);

        var result = await session.SendAsync("Ответь одним словом: привет");

        result.Type.Should().Be(
            ChatResultType.Success,
            $"модель '{config.ModelName}' по адресу {config.Endpoint} должна отвечать");
        result.Text.Should().NotBeNullOrWhiteSpace();
    }

    [LiveFact]
    public async Task ChatSession_TwoTurns_KeepsDialogueInHistory()
    {
        var config = LiveTestSupport.RequireConfig();

        using var client = ChatSessionFactory.CreateChatClient(config);
        var history = ChatSessionFactory.CreateHistory(config);
        var session = new ChatSession(client, history, config.RequestTimeoutSeconds);

        var first = await session.SendAsync("Запомни слово: банан");
        var second = await session.SendAsync("Повтори слово, которое я просил запомнить");

        first.Type.Should().Be(ChatResultType.Success);
        second.Type.Should().Be(ChatResultType.Success);
        second.Text.Should().NotBeNullOrWhiteSpace();

        history.UserMessageCount.Should().Be(2);
        history.BuildRequestMessages().Should().HaveCount(5); // System + две реплики
    }

    [LiveFact]
    public async Task Endpoint_ReportsConfiguredModel()
    {
        var config = LiveTestSupport.RequireConfig();

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

        var modelsUri = new Uri(config.Endpoint.TrimEnd('/') + "/models");
        var response = await http.GetAsync(modelsUri);

        response.IsSuccessStatusCode.Should().BeTrue(
            $"GET {modelsUri} должен отвечать успехом — проверьте, что сервер запущен и доступен");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(
            config.ModelName,
            $"в списке моделей сервера должна быть '{config.ModelName}' — иначе поправьте AI_MODEL_NAME");
    }
}
