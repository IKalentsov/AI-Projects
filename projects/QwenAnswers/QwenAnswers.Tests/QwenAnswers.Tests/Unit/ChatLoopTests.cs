namespace QwenAnswers.Tests.Unit;

using FluentAssertions;
using Moq;
using QwenAnswers.Chat;
using QwenAnswers.ConsoleAbstraction;
using QwenAnswers.Tests.Support;

/// <summary>
/// Консольный цикл: вывод, команды выхода, отмена по Esc и устойчивость к сбоям.
/// </summary>
public class ChatLoopTests
{
    private const string WelcomeMessage = "Чат с AI. Введите 'exit' для выхода.\n";
    private const string Prompt = "Вы: ";
    private const string Thinking = "⏳ Думаю...";

    private static Mock<IChatSession> SessionReplying(string reply = "ответ")
    {
        var session = new Mock<IChatSession>();
        session
            .Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResult(ChatResultType.Success, reply));

        return session;
    }

    private static Mock<IChatSession> SessionReturning(ChatResult result)
    {
        var session = new Mock<IChatSession>();
        session
            .Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        return session;
    }

    private static Task RunAsync(IChatSession session, FakeConsole console) =>
        new ChatLoop(session, console).RunAsync();

    // ── Вывод ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_PrintsWelcomeMessage()
    {
        var console = new FakeConsole("exit");

        await RunAsync(SessionReplying().Object, console);

        console.WrittenLines.Should().ContainSingle().Which.Should().Be(WelcomeMessage);
    }

    [Fact]
    public async Task RunAsync_PrintsPromptBeforeEachInput()
    {
        var console = new FakeConsole("привет", "exit");

        await RunAsync(SessionReplying().Object, console);

        console.Written.Count(w => w == Prompt).Should().Be(2);
    }

    [Fact]
    public async Task RunAsync_PrintsThinkingIndicator()
    {
        var console = new FakeConsole("привет", "exit");

        await RunAsync(SessionReplying().Object, console);

        console.Written.Should().Contain(Thinking);
    }

    [Fact]
    public async Task RunAsync_Success_PrintsAssistantAnswer()
    {
        var console = new FakeConsole("привет", "exit");

        await RunAsync(SessionReturning(new ChatResult(ChatResultType.Success, "ответ модели")).Object, console);

        console.WrittenLines.Should().Contain("\rAI: ответ модели\n");
    }

    [Fact]
    public async Task RunAsync_Cancelled_PrintsCancellationNotice()
    {
        var console = new FakeConsole("привет", "exit");

        await RunAsync(SessionReturning(new ChatResult(ChatResultType.Cancelled, null)).Object, console);

        console.WrittenLines.Should().Contain("\r[Запрос отменён пользователем]\n");
    }

    [Fact]
    public async Task RunAsync_Timeout_PrintsTimeoutNotice()
    {
        var console = new FakeConsole("привет", "exit");

        await RunAsync(SessionReturning(new ChatResult(ChatResultType.Timeout, null)).Object, console);

        console.WrittenLines.Should().Contain("\r[Превышено время ожидания ответа]\n");
    }

    [Fact]
    public async Task RunAsync_Error_PrintsErrorMessage()
    {
        var console = new FakeConsole("привет", "exit");

        await RunAsync(SessionReturning(new ChatResult(ChatResultType.Error, "Network error")).Object, console);

        console.WrittenLines.Should().Contain("\r[Ошибка: Network error]\n");
    }

    // ── Ввод ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_BlankInput_IsSkippedWithoutCallingModel()
    {
        var session = new Mock<IChatSession>();
        var console = new FakeConsole("", "   ", "exit");

        await RunAsync(session.Object, console);

        session.Verify(s => s.SendAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        console.Written.Count(w => w == Prompt).Should().Be(3);
    }

    [Theory]
    [InlineData("exit")]
    [InlineData("quit")]
    [InlineData("EXIT")]
    [InlineData("Quit")]
    [InlineData(" exit ")]
    public async Task RunAsync_ExitCommand_StopsTheLoop(string command)
    {
        var session = SessionReplying();
        var console = new FakeConsole("привет", command);

        await RunAsync(session.Object, console);

        session.Verify(s => s.SendAsync("привет", It.IsAny<CancellationToken>()), Times.Once);
        console.Written.Count(w => w == Prompt).Should().Be(2, "после команды выхода ввод больше не читается");
    }

    [Fact]
    public async Task RunAsync_EndOfInput_StopsTheLoopInsteadOfSpinningForever()
    {
        // Раньше ReadLine() == null приводил к бесконечному циклу (например, при перенаправленном вводе).
        var session = SessionReplying();
        var console = new FakeConsole("привет");

        await RunAsync(session.Object, console);

        session.Verify(s => s.SendAsync("привет", It.IsAny<CancellationToken>()), Times.Once);
        console.Written.Count(w => w == Prompt).Should().Be(2);
    }

    [Fact]
    public async Task RunAsync_NonSuccessResult_DoesNotStopTheLoop()
    {
        var session = SessionReturning(new ChatResult(ChatResultType.Error, "сбой"));
        var console = new FakeConsole("первый", "второй", "exit");

        await RunAsync(session.Object, console);

        session.Verify(s => s.SendAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ── Отмена по Esc ───────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_EscapeDuringRequest_CancelsItAndNextRequestStillWorks()
    {
        var tokens = new List<CancellationToken>();
        var session = new Mock<IChatSession>();

        session
            .Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string message, CancellationToken cancellationToken) =>
            {
                tokens.Add(cancellationToken);

                return tokens.Count == 1
                    ? CancelledWhenTokenFires(cancellationToken)
                    : Task.FromResult(new ChatResult(ChatResultType.Success, "ответ"));
            });

        var console = new FakeConsole("первый", "второй", "exit")
        {
            KeyAvailableProvider = () => true,
            KeyReader = () => new ConsoleKeyInfoResult(ConsoleKey.Escape, '\0', false),
        };

        await RunAsync(session.Object, console);

        tokens.Should().HaveCount(2, "после Esc чат должен продолжать работать");
        tokens[0].IsCancellationRequested.Should().BeTrue();
        tokens[1].IsCancellationRequested.Should().BeFalse("новому запросу нужен свежий токен отмены");
        console.WrittenLines.Should().Contain("\r[Запрос отменён пользователем]\n");
        console.WrittenLines.Should().Contain("\rAI: ответ\n");
    }

    [Fact]
    public async Task RunAsync_KeyAvailableThrows_LoopKeepsWorking()
    {
        // При перенаправленном вводе Console.KeyAvailable бросает InvalidOperationException.
        var session = new Mock<IChatSession>();
        session
            .Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string message, CancellationToken cancellationToken) =>
            {
                await Task.Delay(250, CancellationToken.None);
                return new ChatResult(ChatResultType.Success, "ответ");
            });

        var console = new FakeConsole("привет", "exit")
        {
            KeyAvailableProvider = () => throw new InvalidOperationException("Cannot see if a key has been pressed."),
        };

        await RunAsync(session.Object, console);

        console.WrittenLines.Should().Contain("\rAI: ответ\n");
        console.WrittenLines.Should().NotContain(line => line.Contains("Критическая ошибка"));
    }

    // ── Сбои ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SessionThrows_PrintsCriticalErrorAndContinues()
    {
        var session = new Mock<IChatSession>();
        session
            .Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("модель недоступна"));

        var console = new FakeConsole("первый", "второй", "exit");

        await RunAsync(session.Object, console);

        session.Verify(s => s.SendAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        console.WrittenLines.Should().Contain("\r[Критическая ошибка: модель недоступна]\n");
    }

    private static async Task<ChatResult> CancelledWhenTokenFires(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new ChatResult(ChatResultType.Cancelled, null);
        }

        return new ChatResult(ChatResultType.Success, "недостижимо");
    }

    /// <summary>Поддельная консоль: заранее заданный ввод и запись всего вывода.</summary>
    private sealed class FakeConsole : IConsole
    {
        private readonly Queue<string?> _inputs;

        public FakeConsole(params string?[] inputs)
        {
            _inputs = new Queue<string?>(inputs);
        }

        public List<string> Written { get; } = [];

        public List<string> WrittenLines { get; } = [];

        public Func<bool> KeyAvailableProvider { get; init; } = () => false;

        public Func<ConsoleKeyInfoResult> KeyReader { get; init; } = () => new(ConsoleKey.X, 'x', false);

        public void Write(string value) => Written.Add(value);

        public void WriteLine(string value) => WrittenLines.Add(value);

        // null означает конец ввода (EOF).
        public string? ReadLine() => _inputs.Count > 0 ? _inputs.Dequeue() : null;

        public bool KeyAvailable => KeyAvailableProvider();

        public ConsoleKeyInfoResult ReadKey(bool intercept) => KeyReader();
    }
}
