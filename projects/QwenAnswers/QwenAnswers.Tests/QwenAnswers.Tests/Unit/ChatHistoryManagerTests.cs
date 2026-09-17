namespace QwenAnswers.Tests.Unit;

using FluentAssertions;
using Microsoft.Extensions.AI;
using QwenAnswers.Chat;

/// <summary>
/// История диалога: system-сообщение, снимок запроса и лимит реплик.
/// </summary>
public class ChatHistoryManagerTests
{
    private const string SystemPrompt = "Ты полезный ассистент.";

    private static ChatMessage User(string text) => new(ChatRole.User, text);

    private static ChatMessage Assistant(string text) => new(ChatRole.Assistant, text);

    private static ChatHistoryManager Create(int maxMessages = 50) => new(SystemPrompt, maxMessages);

    private static void AddPairs(ChatHistoryManager manager, int count)
    {
        for (var i = 0; i < count; i++)
            manager.AddPair(User($"U{i}"), Assistant($"A{i}"));
    }

    // ── Конструктор ─────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullSystemMessage_Throws()
    {
        Action act = () => new ChatHistoryManager(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveLimit_Throws(int maxMessages)
    {
        Action act = () => new ChatHistoryManager(SystemPrompt, maxMessages);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ── Формирование запроса ────────────────────────────────────────────────

    [Fact]
    public void BuildRequestMessages_EmptyHistory_ReturnsOnlySystemMessage()
    {
        var messages = Create().BuildRequestMessages();

        messages.Should().ContainSingle();
        messages[0].Role.Should().Be(ChatRole.System);
        messages[0].Text.Should().Be(SystemPrompt);
    }

    [Fact]
    public void BuildRequestMessages_WithPendingMessage_AppendsItAsUser()
    {
        var messages = Create().BuildRequestMessages("текущий вопрос");

        messages.Select(m => m.Text).Should().Equal(SystemPrompt, "текущий вопрос");
        messages[1].Role.Should().Be(ChatRole.User);
    }

    [Fact]
    public void BuildRequestMessages_WithPendingMessage_KeepsItAfterHistory()
    {
        var manager = Create();
        manager.AddPair(User("старый вопрос"), Assistant("старый ответ"));

        var messages = manager.BuildRequestMessages("новый вопрос");

        messages.Select(m => m.Text).Should().Equal(SystemPrompt, "старый вопрос", "старый ответ", "новый вопрос");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void BuildRequestMessages_WithoutPendingMessage_DoesNotAddUserMessage(string? pending)
    {
        var messages = Create().BuildRequestMessages(pending);

        messages.Should().ContainSingle();
    }

    [Fact]
    public void BuildRequestMessages_ReturnsSnapshot_UnaffectedByLaterChanges()
    {
        var manager = Create();

        var snapshot = manager.BuildRequestMessages("вопрос");
        manager.AddPair(User("вопрос"), Assistant("ответ"));

        snapshot.Should().HaveCount(2, "снимок запроса не должен меняться после изменения истории");
    }

    // ── Добавление реплик ───────────────────────────────────────────────────

    [Fact]
    public void AddPair_AddsUserThenAssistant()
    {
        var manager = Create();

        manager.AddPair(User("вопрос"), Assistant("ответ"));

        manager.BuildRequestMessages().Select(m => m.Text).Should().Equal(SystemPrompt, "вопрос", "ответ");
        manager.BuildRequestMessages()[1].Role.Should().Be(ChatRole.User);
        manager.BuildRequestMessages()[2].Role.Should().Be(ChatRole.Assistant);
    }

    [Fact]
    public void AddPair_NullUserMessage_Throws()
    {
        var manager = Create();

        Action act = () => manager.AddPair(null!, Assistant("ответ"));

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddPair_NullAssistantMessage_Throws()
    {
        var manager = Create();

        Action act = () => manager.AddPair(User("вопрос"), null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── Подсчёт реплик ──────────────────────────────────────────────────────

    [Fact]
    public void UserMessageCount_CountsOnlyUserMessages()
    {
        var manager = Create();

        manager.UserMessageCount.Should().Be(0);

        manager.AddPair(User("вопрос 1"), Assistant("ответ 1"));
        manager.UserMessageCount.Should().Be(1);

        manager.AddPair(User("вопрос 2"), Assistant("ответ 2"));
        manager.UserMessageCount.Should().Be(2);
    }

    // ── Лимит истории ───────────────────────────────────────────────────────

    [Fact]
    public void AddPair_UpToLimit_KeepsEverything()
    {
        var manager = Create(maxMessages: 3);

        AddPairs(manager, 3);

        manager.BuildRequestMessages().Should().HaveCount(7); // System + 3 реплики
        manager.UserMessageCount.Should().Be(3);
    }

    [Fact]
    public void AddPair_BeyondLimit_DropsOldestPairsCompletely()
    {
        var manager = Create(maxMessages: 2);

        AddPairs(manager, 4);

        manager.BuildRequestMessages().Select(m => m.Text).Should().Equal(SystemPrompt, "U2", "A2", "U3", "A3");
    }

    [Fact]
    public void AddPair_BeyondLimit_NeverLeavesUserWithoutAssistant()
    {
        var manager = Create(maxMessages: 2);

        AddPairs(manager, 5);

        var roles = manager.BuildRequestMessages().Select(m => m.Role).ToArray();

        roles.Should().Equal(ChatRole.System, ChatRole.User, ChatRole.Assistant, ChatRole.User, ChatRole.Assistant);
    }

    [Fact]
    public void AddPair_LimitOne_KeepsOnlyLastPair()
    {
        var manager = Create(maxMessages: 1);

        AddPairs(manager, 3);

        manager.BuildRequestMessages().Select(m => m.Text).Should().Equal(SystemPrompt, "U2", "A2");
    }

    [Fact]
    public void AddPair_SystemMessage_IsNotCountedTowardsLimit()
    {
        var manager = Create(maxMessages: 2);

        AddPairs(manager, 2);

        manager.BuildRequestMessages().Should().HaveCount(5); // System + 2 реплики, лимит не превышен
    }
}
