namespace QwenAnswers.Tests.Unit;

using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;
using QwenAnswers.Chat;
using QwenAnswers.Tests.Support;

/// <summary>
/// Сессия чата: что уходит модели, что попадает в историю и как обрабатываются сбои.
/// </summary>
public class ChatSessionTests
{
    private const string SystemPrompt = "System msg";

    private static ChatHistoryManager CreateHistory(int maxMessages = 50) => new(SystemPrompt, maxMessages);

    // ── Успешный ответ ──────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_Success_ReturnsAssistantText()
    {
        var session = new ChatSession(FakeChatClient.Replying("ответ модели").Object, CreateHistory());

        var result = await session.SendAsync("привет");

        result.Type.Should().Be(ChatResultType.Success);
        result.Text.Should().Be("ответ модели");
    }

    [Fact]
    public async Task SendAsync_Success_StoresExactlyOnePairInHistory()
    {
        // Регрессия: раньше вопрос добавлялся в историю дважды (AddUserMessage + AddPair).
        var history = CreateHistory();
        var session = new ChatSession(FakeChatClient.Replying("ответ модели").Object, history);

        await session.SendAsync("привет");

        history.UserMessageCount.Should().Be(1);
        history.BuildRequestMessages().Select(m => m.Text).Should().Equal(SystemPrompt, "привет", "ответ модели");
    }

    [Fact]
    public async Task SendAsync_Success_SendsSystemAndCurrentUserMessage()
    {
        var requests = new List<List<ChatMessage>>();
        var session = new ChatSession(FakeChatClient.Replying("ответ", requests).Object, CreateHistory());

        await session.SendAsync("привет");

        requests.Should().ContainSingle();
        requests[0].Select(m => m.Text).Should().Equal(SystemPrompt, "привет");
        requests[0].Select(m => m.Role).Should().Equal(ChatRole.System, ChatRole.User);
    }

    [Fact]
    public async Task SendAsync_SecondTurn_SendsWholePreviousDialogue()
    {
        var requests = new List<List<ChatMessage>>();
        var session = new ChatSession(FakeChatClient.Replying("ответ", requests).Object, CreateHistory());

        await session.SendAsync("первый");
        await session.SendAsync("второй");

        requests.Should().HaveCount(2);
        requests[0].Select(m => m.Text).Should().Equal(SystemPrompt, "первый");
        requests[1].Select(m => m.Text).Should().Equal(SystemPrompt, "первый", "ответ", "второй");
    }

    [Fact]
    public async Task SendAsync_Success_RespectsHistoryLimit()
    {
        var requests = new List<List<ChatMessage>>();
        var history = CreateHistory(maxMessages: 1);
        var session = new ChatSession(FakeChatClient.Replying("ответ", requests).Object, history);

        await session.SendAsync("первый");
        await session.SendAsync("второй");

        history.UserMessageCount.Should().Be(1);
        requests[1].Select(m => m.Text).Should().Equal(SystemPrompt, "первый", "ответ", "второй");
    }

    // ── Отмена и таймаут ────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_CancelledByUser_ReturnsCancelledAndLeavesHistoryEmpty()
    {
        var history = CreateHistory();
        var session = new ChatSession(FakeChatClient.NeverReplies().Object, history);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await session.SendAsync("привет", cancellation.Token);

        result.Type.Should().Be(ChatResultType.Cancelled);
        result.Text.Should().BeNull();
        history.BuildRequestMessages().Should().ContainSingle();
    }

    [Fact]
    public async Task SendAsync_Timeout_ReturnsTimeoutAndLeavesHistoryEmpty()
    {
        var history = CreateHistory();
        var session = new ChatSession(FakeChatClient.NeverReplies().Object, history, requestTimeoutSeconds: 1);

        var result = await session.SendAsync("привет");

        result.Type.Should().Be(ChatResultType.Timeout);
        result.Text.Should().BeNull();
        history.BuildRequestMessages().Should().ContainSingle();
    }

    [Fact]
    public async Task SendAsync_CancellationTakesPrecedenceOverTimeout()
    {
        var session = new ChatSession(FakeChatClient.NeverReplies().Object, CreateHistory(), requestTimeoutSeconds: 1);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await session.SendAsync("привет", cancellation.Token);

        result.Type.Should().Be(ChatResultType.Cancelled);
    }

    // ── Ошибки модели ───────────────────────────────────────────────────────

    [Fact]
    public async Task SendAsync_ClientThrows_ReturnsErrorWithMessageAndLeavesHistoryEmpty()
    {
        var history = CreateHistory();
        var session = new ChatSession(
            FakeChatClient.FailingWith(new HttpRequestException("Network error")).Object,
            history);

        var result = await session.SendAsync("привет");

        result.Type.Should().Be(ChatResultType.Error);
        result.Text.Should().Contain("Network error");
        history.BuildRequestMessages().Should().ContainSingle();
    }

    [Fact]
    public async Task SendAsync_AfterFailedTurn_NextTurnHasNoTracesOfIt()
    {
        var requests = new List<List<ChatMessage>>();
        var attempts = 0;

        var client = new Mock<IChatClient>();
        client
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns((IEnumerable<ChatMessage> messages, ChatOptions? options, CancellationToken cancellationToken) =>
            {
                attempts++;

                if (attempts == 1)
                    return Task.FromException<ChatResponse>(new HttpRequestException("Network error"));

                requests.Add(messages.ToList());
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ответ")));
            });

        var history = CreateHistory();
        var session = new ChatSession(client.Object, history);

        var failed = await session.SendAsync("неудачный вопрос");
        var succeeded = await session.SendAsync("удачный вопрос");

        failed.Type.Should().Be(ChatResultType.Error);
        succeeded.Type.Should().Be(ChatResultType.Success);
        requests.Should().ContainSingle();
        requests[0].Select(m => m.Text).Should().Equal(SystemPrompt, "удачный вопрос");
    }

    // ── Защита от некорректных аргументов ───────────────────────────────────

    [Fact]
    public void Constructor_NullChatClient_Throws()
    {
        Action act = () => new ChatSession(null!, CreateHistory());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullHistoryManager_Throws()
    {
        Action act = () => new ChatSession(FakeChatClient.Replying("ответ").Object, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveTimeout_Throws(int timeoutSeconds)
    {
        Action act = () => new ChatSession(FakeChatClient.Replying("ответ").Object, CreateHistory(), timeoutSeconds);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task SendAsync_NullMessage_Throws()
    {
        var session = new ChatSession(FakeChatClient.Replying("ответ").Object, CreateHistory());

        var act = async () => await session.SendAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
