namespace QwenAnswers.Tests.Unit;

using FluentAssertions;
using Microsoft.Extensions.AI;
using QwenAnswers.Chat;
using QwenAnswers.Config;
using QwenAnswers.Tests.Support;

/// <summary>
/// Сборка компонентов чата из конфигурации: тесты проверяют тот же путь, что и Program.cs.
/// </summary>
public class ChatSessionFactoryTests
{
    private static AppConfig Config(int maxHistoryMessages = 50, int requestTimeoutSeconds = 300) =>
        new("test-key", "http://127.0.0.1:9/v1", "test-model", maxHistoryMessages, requestTimeoutSeconds);

    [Fact]
    public void CreateHistory_UsesProductionSystemPrompt()
    {
        var history = ChatSessionFactory.CreateHistory(Config());

        history.BuildRequestMessages()[0].Text.Should().Be(ChatSessionFactory.SystemPrompt);
    }

    [Fact]
    public void CreateHistory_AppliesConfiguredLimit()
    {
        var history = ChatSessionFactory.CreateHistory(Config(maxHistoryMessages: 2));

        for (var i = 0; i < 4; i++)
        {
            history.AddPair(
                new ChatMessage(ChatRole.User, $"U{i}"),
                new ChatMessage(ChatRole.Assistant, $"A{i}"));
        }

        history.UserMessageCount.Should().Be(2);
        history.BuildRequestMessages().Select(m => m.Text)
            .Should().Equal(ChatSessionFactory.SystemPrompt, "U2", "A2", "U3", "A3");
    }

    [Fact]
    public async Task CreateSession_SendsProductionSystemPrompt()
    {
        var requests = new List<List<ChatMessage>>();
        var session = ChatSessionFactory.CreateSession(FakeChatClient.Replying("ответ", requests).Object, Config());

        var result = await session.SendAsync("привет");

        result.Type.Should().Be(ChatResultType.Success);
        requests.Should().ContainSingle();
        requests[0][0].Text.Should().Be(ChatSessionFactory.SystemPrompt);
        requests[0][1].Text.Should().Be("привет");
    }

    [Fact]
    public void CreateChatClient_ValidConfig_ReturnsClient()
    {
        // Конструктор клиента не должен ходить в сеть — иначе тест стал бы сетевым.
        using var client = ChatSessionFactory.CreateChatClient(Config());

        client.Should().NotBeNull();
    }

    [Fact]
    public void CreateChatClient_NullConfig_Throws()
    {
        Action act = () => ChatSessionFactory.CreateChatClient(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateSession_NullChatClient_Throws()
    {
        Action act = () => ChatSessionFactory.CreateSession(null!, Config());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateSession_NullConfig_Throws()
    {
        Action act = () => ChatSessionFactory.CreateSession(FakeChatClient.Replying("ответ").Object, null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
