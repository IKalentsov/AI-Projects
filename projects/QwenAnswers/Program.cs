using QwenAnswers.Chat;
using QwenAnswers.Config;
using QwenAnswers.ConsoleAbstraction;

// ── Загрузка настроек из .env (файл рядом с исполняемым файлом) ──────────────
const string CopyTemplateHint = "Скопируйте .env-public в .env и заполните своими данными.";

var envPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, EnvConfigLoader.DefaultEnvPath);

var loadResult = new EnvConfigLoader().Load(envPath);

if (!loadResult.Found)
{
    Console.WriteLine(loadResult.Error);
    Console.WriteLine(CopyTemplateHint);
    return;
}

var config = loadResult.Config!;

// ── Валидация: обязательные поля заполнены, endpoint корректен ───────────────
var problems = AppConfigValidator.Validate(config);

if (problems.Count > 0)
{
    foreach (var problem in problems)
        Console.WriteLine(problem);

    // Отдельная подсказка для случая «скопировал шаблон и не заполнил».
    if (AppConfigValidator.IsUnfilledTemplate(config))
        Console.WriteLine(CopyTemplateHint);

    return;
}

try
{
    // ── Создание компонентов ────────────────────────────────────────────────
    using var chatClient = ChatSessionFactory.CreateChatClient(config);
    var chatSession = ChatSessionFactory.CreateSession(chatClient, config);

    var chatLoop = new ChatLoop(chatSession, new SystemConsole());

    // ── Запуск цикла ────────────────────────────────────────────────────────
    await chatLoop.RunAsync();
}
catch (Exception ex)
{
    Console.WriteLine($"Критическая ошибка: {ex.Message}");
    Console.WriteLine("Нажмите любую клавишу для выхода...");

    // При перенаправленном вводе Console.ReadKey() бросает исключение.
    if (!Console.IsInputRedirected)
        Console.ReadKey();
}
