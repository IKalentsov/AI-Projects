namespace QwenAnswers.Chat;

using QwenAnswers.ConsoleAbstraction;

/// <summary>
/// Цикл взаимодействия с пользователем через консоль.
/// </summary>
public class ChatLoop
{
    private const int EscapePollIntervalMs = 100;

    private readonly IChatSession _chatSession;
    private readonly IConsole _console;

    /// <summary>
    /// Признак того, что у консоли можно спрашивать про нажатые клавиши.
    /// При перенаправленном вводе <see cref="IConsole.KeyAvailable"/> бросает исключение —
    /// проверяем это один раз и больше не пытаемся.
    /// </summary>
    private bool _escapeTrackingAvailable = true;

    /// <summary>
    /// Создаёт цикл чата.
    /// </summary>
    /// <param name="chatSession">Сессия чата для отправки запросов.</param>
    /// <param name="console">Абстракция консоли для ввода/вывода.</param>
    public ChatLoop(IChatSession chatSession, IConsole console)
    {
        _chatSession = chatSession ?? throw new ArgumentNullException(nameof(chatSession));
        _console = console ?? throw new ArgumentNullException(nameof(console));
    }

    /// <summary>
    /// Запускает цикл чата. Выход — по команде exit/quit или по концу ввода.
    /// </summary>
    public async Task RunAsync()
    {
        _console.WriteLine("Чат с AI. Введите 'exit' для выхода.\n");

        while (true)
        {
            _console.Write("Вы: ");
            var userInput = _console.ReadLine();

            // null — ввод закончился (EOF, перенаправленный поток). Без этой проверки цикл завис бы навсегда.
            if (userInput is null)
                break;

            if (string.IsNullOrWhiteSpace(userInput))
                continue;

            if (IsExitCommand(userInput))
                break;

            // Свой токен на каждый запрос: иначе после первого Esc он остаётся отменённым
            // и все последующие запросы падают мгновенно.
            using var cancellation = new CancellationTokenSource();

            await ProcessUserMessageAsync(userInput, cancellation);
        }
    }

    private async Task ProcessUserMessageAsync(string userInput, CancellationTokenSource cancellation)
    {
        _console.Write("⏳ Думаю...");

        try
        {
            var answerTask = _chatSession.SendAsync(userInput, cancellation.Token);

            // Ждём ответ и параллельно следим за нажатием Esc.
            while (!answerTask.IsCompleted)
            {
                if (TryReadEscape())
                {
                    cancellation.Cancel();
                    break;
                }

                await Task.Delay(EscapePollIntervalMs);
            }

            var result = await answerTask;

            switch (result.Type)
            {
                case ChatResultType.Success:
                    _console.WriteLine($"\rAI: {result.Text}\n");
                    break;

                case ChatResultType.Cancelled:
                    _console.WriteLine("\r[Запрос отменён пользователем]\n");
                    break;

                case ChatResultType.Timeout:
                    _console.WriteLine("\r[Превышено время ожидания ответа]\n");
                    break;

                case ChatResultType.Error:
                    _console.WriteLine($"\r[Ошибка: {result.Text}]\n");
                    break;
            }
        }
        catch (Exception ex)
        {
            _console.WriteLine($"\r[Критическая ошибка: {ex.Message}]\n");
        }
    }

    private bool TryReadEscape()
    {
        if (!_escapeTrackingAvailable)
            return false;

        try
        {
            if (!_console.KeyAvailable)
                return false;

            return _console.ReadKey(intercept: true).Key == ConsoleKey.Escape;
        }
        catch (InvalidOperationException)
        {
            // Ввод перенаправлен — клавиши недоступны, отслеживание Esc отключаем.
            _escapeTrackingAvailable = false;
            return false;
        }
    }

    private static bool IsExitCommand(string input)
    {
        var trimmed = input.Trim();

        return trimmed.Equals("exit", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("quit", StringComparison.OrdinalIgnoreCase);
    }
}
